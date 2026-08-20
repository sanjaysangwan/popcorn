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

        Dictionary<string, object> json = Get("s=" + HttpUtility.UrlEncode(term.Trim()) + "&type=movie",
                                              out error);
        if (json == null) return results;

        if (!IsSuccess(json))
        {
            error = Text(json, "Error");
            if (String.IsNullOrEmpty(error)) error = "No movies matched that search.";
            return results;
        }

        object searchNode;
        if (!json.TryGetValue("Search", out searchNode)) return results;

        object[] items = searchNode as object[];
        if (items == null) return results;

        foreach (object item in items)
        {
            Dictionary<string, object> row = item as Dictionary<string, object>;
            if (row == null) continue;

            results.Add(new MovieSearchResult
            {
                ImdbId = Text(row, "imdbID"),
                Title = Text(row, "Title"),
                Year = Text(row, "Year"),
                PosterUrl = Clean(Text(row, "Poster")),
                Type = Text(row, "Type")
            });
        }
        return results;
    }

    /// <summary>Full details for one IMDb id.</summary>
    public static Movie GetByImdbId(string imdbId, out string error)
    {
        error = null;
        if (!IsConfigured) { error = "No OMDb API key is configured."; return null; }
        if (String.IsNullOrEmpty(imdbId)) { error = "No movie id supplied."; return null; }

        Dictionary<string, object> json =
            Get("i=" + HttpUtility.UrlEncode(imdbId) + "&plot=full", out error);
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

        Dictionary<string, object> json = Get(query, out error);
        return ToMovie(json, ref error);
    }

    private static Movie ToMovie(Dictionary<string, object> json, ref string error)
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

    private static Dictionary<string, object> Get(string query, out string error)
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
            return serializer.Deserialize<Dictionary<string, object>>(body);
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

    private static bool IsSuccess(Dictionary<string, object> json)
    {
        return String.Equals(Text(json, "Response"), "True", StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(Dictionary<string, object> json, string key)
    {
        object value;
        if (json == null || !json.TryGetValue(key, out value) || value == null) return "";
        return Convert.ToString(value);
    }

    /// <summary>OMDb writes "N/A" where it has no data; treat that as empty.</summary>
    private static string Clean(string value)
    {
        if (String.IsNullOrEmpty(value)) return "";
        return String.Equals(value.Trim(), "N/A", StringComparison.OrdinalIgnoreCase) ? "" : value.Trim();
    }
}
