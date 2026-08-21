using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

/// <summary>
/// Looks film details up on OMDb (https://www.omdbapi.com), a free API that
/// wraps IMDb data. A free key allows 1,000 requests a day.
///
/// Everything here degrades gracefully: if the key is missing, the host blocks
/// outbound calls, or OMDb is down, the site still works - members just type
/// the title in by hand and get no poster or synopsis.
/// </summary>
public static class OmdbClient
{
    private const string Host = "www.omdbapi.com";
    private const int TimeoutMs = 12000;

    public static string ApiKey
    {
        get { return (ConfigurationManager.AppSettings["OmdbApiKey"] ?? "").Trim(); }
    }

    public static bool IsConfigured
    {
        get { return ApiKey.Length > 0; }
    }

    /// <summary>Title search. Returns an empty list and sets <paramref name="error"/> on failure.</summary>
    public static List<MovieSearchResult> Search(string term, out string error)
    {
        List<MovieSearchResult> results = new List<MovieSearchResult>();
        error = null;

        if (!IsConfigured)
        {
            error = "No OMDb API key is configured, so automatic lookups are turned off. " +
                    "You can still add a movie by typing its details in yourself.";
            return results;
        }
        if (String.IsNullOrEmpty(term) || term.Trim().Length < 2)
        {
            error = "Type at least two characters to search.";
            return results;
        }

        object json = Get("s=" + HttpUtility.UrlEncode(term.Trim()) + "&type=movie", out error);
        if (json == null) return results;

        return ReadSearchResults(json, out error);
    }

    /// <summary>
    /// Turns a decoded OMDb search response into results. Separated from the
    /// HTTP call so the parsing is covered by tests - this is where a silent
    /// "no results" came from once already.
    /// </summary>
    public static List<MovieSearchResult> ReadSearchResults(object json, out string error)
    {
        List<MovieSearchResult> results = new List<MovieSearchResult>();
        error = null;

        if (json == null)
        {
            error = "The movie service returned nothing.";
            return results;
        }

        if (!IsSuccess(json))
        {
            error = Text(json, "Error");
            if (String.IsNullOrEmpty(error)) error = "No movies matched that search.";
            return results;
        }

        System.Collections.IDictionary map = json as System.Collections.IDictionary;
        object searchNode = (map != null && map.Contains("Search")) ? map["Search"] : null;

        foreach (object item in Items(searchNode))
        {
            string imdbId = Text(item, "imdbID");
            string title = Text(item, "Title");
            if (title.Length == 0) continue;

            results.Add(new MovieSearchResult
            {
                ImdbId = imdbId,
                Title = title,
                Year = Text(item, "Year"),
                PosterUrl = Clean(Text(item, "Poster")),
                Type = Text(item, "Type")
            });
        }

        // OMDb said it succeeded but nothing could be read out of it. Say so
        // rather than showing an empty page as though there were no matches.
        if (results.Count == 0)
            error = "The movie service replied, but its list of matches could not be read.";

        return results;
    }

    /// <summary>Full details for one IMDb id.</summary>
    public static Movie GetByImdbId(string imdbId, out string error)
    {
        error = null;
        if (!IsConfigured) { error = "No OMDb API key is configured."; return null; }
        if (String.IsNullOrEmpty(imdbId)) { error = "No movie id supplied."; return null; }

        object json = Get("i=" + HttpUtility.UrlEncode(imdbId) + "&plot=full", out error);
        return ToMovie(json, ref error);
    }

    /// <summary>Full details for an exact title (and optionally a year).</summary>
    public static Movie GetByTitle(string title, string year, out string error)
    {
        error = null;
        if (!IsConfigured) { error = "No OMDb API key is configured."; return null; }
        if (String.IsNullOrEmpty(title)) { error = "No title supplied."; return null; }

        string query = "t=" + HttpUtility.UrlEncode(title.Trim()) + "&plot=full";
        if (!String.IsNullOrEmpty(year))
            query += "&y=" + HttpUtility.UrlEncode(year.Trim());

        object json = Get(query, out error);
        return ToMovie(json, ref error);
    }

    public static Movie ToMovie(object json, ref string error)
    {
        if (json == null) return null;
        if (!IsSuccess(json))
        {
            string apiError = Text(json, "Error");
            error = String.IsNullOrEmpty(apiError) ? "That movie could not be found." : apiError;
            return null;
        }

        return new Movie
        {
            Title = Text(json, "Title"),
            ReleaseYear = Text(json, "Year"),
            ImdbId = Text(json, "imdbID"),
            PosterUrl = Clean(Text(json, "Poster")),
            Plot = Clean(Text(json, "Plot")),
            Actors = Clean(Text(json, "Actors")),
            Director = Clean(Text(json, "Director")),
            Genre = Clean(Text(json, "Genre")),
            Runtime = Clean(Text(json, "Runtime")),
            MpaaRating = Clean(Text(json, "Rated")),
            ImdbScore = Clean(Text(json, "imdbRating"))
        };
    }

    // ----- plumbing --------------------------------------------------------

    private static object Get(string query, out string error)
    {
        error = null;
        string body = Download("https://" + Host + "/?apikey=" + HttpUtility.UrlEncode(ApiKey) +
                               "&r=json&" + query, out error);

        // Some shared hosts only allow plain HTTP outbound, so try that once.
        if (body == null)
        {
            string ignored;
            body = Download("http://" + Host + "/?apikey=" + HttpUtility.UrlEncode(ApiKey) +
                            "&r=json&" + query, out ignored);
            if (body != null) error = null;
        }
        if (body == null) return null;

        try
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 4 * 1024 * 1024;
            return serializer.DeserializeObject(body);
        }
        catch (Exception ex)
        {
            error = "The movie service returned something unexpected: " + ex.Message;
            return null;
        }
    }

    private static string Download(string url, out string error)
    {
        error = null;
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;
            request.UserAgent = "Popcorn-FamilyMovies/1.0";
            request.Accept = "application/json";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
        catch (WebException ex)
        {
            error = "Could not reach the movie service (" + ex.Message + "). " +
                    "Check the API key in web.config and that outbound web requests are allowed.";
            return null;
        }
        catch (Exception ex)
        {
            error = "Movie lookup failed: " + ex.Message;
            return null;
        }
    }

    private static bool IsSuccess(object json)
    {
        return String.Equals(Text(json, "Response"), "True", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads one field out of a decoded JSON object.
    ///
    /// Takes "object" rather than a dictionary type on purpose:
    /// JavaScriptSerializer decodes a nested JSON object as
    /// Dictionary&lt;string, object&gt; but a nested JSON array as ArrayList,
    /// and being strict about either of those is how the search silently
    /// returned nothing at all.
    /// </summary>
    private static string Text(object node, string key)
    {
        System.Collections.IDictionary map = node as System.Collections.IDictionary;
        if (map == null || !map.Contains(key)) return "";

        object value = map[key];
        return value == null ? "" : Convert.ToString(value);
    }

    /// <summary>Every element of a decoded JSON array, whatever list type it arrived as.</summary>
    private static List<object> Items(object node)
    {
        List<object> items = new List<object>();

        System.Collections.IEnumerable list = node as System.Collections.IEnumerable;
        if (list == null || node is string) return items;

        foreach (object item in list) items.Add(item);
        return items;
    }

    /// <summary>OMDb writes "N/A" where it has no data; treat that as empty.</summary>
    private static string Clean(string value)
    {
        if (String.IsNullOrEmpty(value)) return "";
        return String.Equals(value.Trim(), "N/A", StringComparison.OrdinalIgnoreCase) ? "" : value.Trim();
    }
}
