using System;
using System.Collections.Generic;

/// <summary>
/// Finds a film on OMDb and stores the details in the Access database, so the
/// poster, synopsis and cast are only fetched once rather than on every view.
/// </summary>
public partial class AddMoviePage : PageBase
{
    public string Message { get; private set; }
    public string MessageKind { get; private set; }
    public string Term { get; private set; }
    public List<MovieSearchResult> Results = new List<MovieSearchResult>();

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";
        Term = "";
        phNoKey.Visible = !OmdbClient.IsConfigured;

        if (String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Csrf.IsValid(Request))
            {
                Message = "That form expired. Please try again.";
            }
            else
            {
                switch (Posted("action"))
                {
                    case "search": DoSearch(); break;
                    case "add": AddFromOmdb(Posted("imdbId")); break;
                    case "manual": AddByHand(); break;
                }
            }
        }

        phMessage.Visible = !String.IsNullOrEmpty(Message);
    }

    private void DoSearch()
    {
        Term = Posted("q");

        string error;
        Results = OmdbClient.Search(Term, out error);
        if (!String.IsNullOrEmpty(error)) Message = error;

        if (Results.Count > 0)
        {
            phResults.Visible = true;
            rptResults.DataSource = Results;
            rptResults.DataBind();
        }
        else
        {
            phNoResults.Visible = true;
        }
    }

    private void AddFromOmdb(string imdbId)
    {
        if (String.IsNullOrEmpty(imdbId))
        {
            Message = "That result did not come with an id - try adding it by hand.";
            return;
        }

        string error;
        Movie movie = OmdbClient.GetByImdbId(imdbId, out error);
        if (movie == null)
        {
            Message = error ?? "Could not fetch that movie's details.";
            return;
        }

        Store(movie);
    }

    private void AddByHand()
    {
        string title = Posted("title");
        string year = Posted("year");

        if (title.Length < 1)
        {
            Message = "Please give the movie a title.";
            return;
        }

        // Try a lookup anyway, so a hand-typed title still gets a poster.
        string error;
        Movie movie = OmdbClient.GetByTitle(title, year, out error);
        if (movie == null)
            movie = new Movie { Title = title, ReleaseYear = year };

        Store(movie);
    }

    /// <summary>Saves the film, or jumps to the existing entry if it is already there.</summary>
    private void Store(Movie movie)
    {
        try
        {
            int existingId = MovieRepository.FindDuplicate(movie.ImdbId, movie.Title, movie.ReleaseYear);
            if (existingId > 0)
            {
                SetFlash(movie.Title + " is already on the family list - here it is. " +
                         "Add your own rating below.", "info");
                Go("~/MovieDetails.aspx?id=" + existingId);
                return;
            }

            int movieId = MovieRepository.Add(movie, CurrentUser.UserId);

            // Put it straight onto any shelf whose name matches an OMDb genre,
            // so a new film is not completely uncategorised.
            try { TagRepository.ApplyGenreTags(movieId, movie.Genre); }
            catch { /* categories are a convenience, never a reason to fail an add */ }

            SetFlash("Added " + movie.Title + ". Now give it your own star rating, " +
                     "and check its categories.", "success");
            Go("~/MovieDetails.aspx?id=" + movieId);
        }
        catch (Exception ex)
        {
            Message = "The movie could not be saved: " + ex.Message;
        }
    }

    protected string ResultPoster(MovieSearchResult result)
    {
        if (result.HasPoster)
            return "<img src=\"" + Attr(result.PosterUrl) + "\" alt=\"" +
                   Attr(result.Title + " poster") + "\" loading=\"lazy\" />";
        return "<span class=\"poster-missing\" aria-hidden=\"true\">&#127916;</span>";
    }
}
