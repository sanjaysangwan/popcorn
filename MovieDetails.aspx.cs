using System;
using System.Collections.Generic;
using System.Web;

public partial class MovieDetailsPage : PageBase
{
    public Movie Film { get; private set; }
    public List<MovieRating> Ratings = new List<MovieRating>();
    public string MyReview { get; private set; }
    public string Message { get; private set; }
    public string MessageKind { get; private set; }

    /// <summary>"12 ratings from the family", or the empty-state wording.</summary>
    public string AverageCaption
    {
        get
        {
            if (Film == null || Film.RatingCount == 0) return "no family ratings yet";
            return "family average from " + Film.RatingCount +
                   (Film.RatingCount == 1 ? " rating" : " ratings");
        }
    }

    public string PlotMarkup
    {
        get
        {
            if (Film == null || String.IsNullOrEmpty(Film.Plot)) return "";
            return "<p>" + HttpUtility.HtmlEncode(Film.Plot) + "</p>";
        }
    }

    /// <summary>The cast / director / genre list, skipping anything OMDb had no value for.</summary>
    public string FactsMarkup
    {
        get
        {
            if (Film == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            Fact(sb, "Starring", Film.Actors);
            Fact(sb, "Director", Film.Director);
            Fact(sb, "Genre", Film.Genre);
            Fact(sb, "Runtime", Film.Runtime);
            Fact(sb, "Certificate", Film.MpaaRating);
            if (!String.IsNullOrEmpty(Film.ImdbScore))
                Fact(sb, "IMDb", Film.ImdbScore + "/10");
            return sb.ToString();
        }
    }

    private static void Fact(System.Text.StringBuilder sb, string label, string value)
    {
        if (String.IsNullOrEmpty(value)) return;
        sb.Append("<li><strong>").Append(HttpUtility.HtmlEncode(label)).Append("</strong> ")
          .Append(HttpUtility.HtmlEncode(value)).Append("</li>");
    }

    /// <summary>Whoever added the film, and any administrator, may remove it.</summary>
    public bool CanDelete
    {
        get
        {
            return Film != null && CurrentUser != null &&
                   (CurrentUser.IsAdmin || Film.AddedByUserId == CurrentUser.UserId);
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";
        MyReview = "";

        int movieId = QueryInt("id");

        if (String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            if (FormInt("movieId") > 0) movieId = FormInt("movieId");
            if (HandlePost(movieId)) return;
        }

        LoadMovie(movieId);
        phMessage.Visible = !String.IsNullOrEmpty(Message);
    }

    /// <summary>Returns true when the request has been redirected.</summary>
    private bool HandlePost(int movieId)
    {
        if (!Csrf.IsValid(Request))
        {
            Message = "That form expired. Please try again.";
            return false;
        }

        // A second submit button posts its own "action" value, and ASP.NET puts
        // both in the same field, so take whichever one is not "rate".
        string action = PickAction();

        if (action == "watched" || action == "unwatched")
        {
            string watchedMessage;
            bool watchedOk;
            MovieActions.TryHandleWatched(Request, CurrentUser, out watchedMessage, out watchedOk);
            SetFlash(watchedMessage, watchedOk ? "success" : "error");
            Go("~/MovieDetails.aspx?id=" + movieId);
            return true;
        }

        switch (action)
        {
            case "rate":
            {
                string message;
                bool ok;
                RatingActions.TryHandle(Request, CurrentUser, out message, out ok);
                SetFlash(message, ok ? "success" : "error");
                Go("~/MovieDetails.aspx?id=" + movieId);
                return true;
            }

            case "unrate":
                RatingRepository.Remove(movieId, CurrentUser.UserId);
                SetFlash("Your rating has been removed.", "success");
                Go("~/MovieDetails.aspx?id=" + movieId);
                return true;

            case "refresh":
                RefreshFromOmdb(movieId);
                return false;

            case "delete":
            {
                Movie film = MovieRepository.GetById(movieId, CurrentUser.UserId);
                if (film == null) return false;
                if (!CurrentUser.IsAdmin && film.AddedByUserId != CurrentUser.UserId)
                {
                    Message = "Only the person who added a movie, or an administrator, can remove it.";
                    return false;
                }
                MovieRepository.Delete(movieId);
                SetFlash("Removed " + film.Title + " and every rating for it.", "success");
                Go("~/Movies.aspx");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The rating form has a submit button named "action" as well as a hidden
    /// field of the same name, so the posted value can be "rate,unrate".
    /// </summary>
    private string PickAction()
    {
        string raw = Request.Form["action"] ?? "";
        string[] parts = raw.Split(',');
        foreach (string part in parts)
        {
            string value = part.Trim();
            if (value.Length > 0 && value != "rate") return value;
        }
        return parts.Length > 0 ? parts[0].Trim() : "";
    }

    private void RefreshFromOmdb(int movieId)
    {
        Movie film = MovieRepository.GetById(movieId, CurrentUser.UserId);
        if (film == null) return;

        string error;
        Movie fresh = String.IsNullOrEmpty(film.ImdbId)
            ? OmdbClient.GetByTitle(film.Title, film.ReleaseYear, out error)
            : OmdbClient.GetByImdbId(film.ImdbId, out error);

        if (fresh == null)
        {
            Message = error ?? "Could not reach the movie service.";
            return;
        }

        fresh.MovieId = film.MovieId;
        MovieRepository.UpdateDetails(fresh);
        Message = "Details refreshed from OMDb.";
        MessageKind = "success";
    }

    private void LoadMovie(int movieId)
    {
        Film = movieId <= 0 ? null : MovieRepository.GetById(movieId, CurrentUser.UserId);

        if (Film == null)
        {
            phNotFound.Visible = true;
            return;
        }

        phFilm.Visible = true;
        Page.Title = Film.Title;

        Ratings = RatingRepository.ForMovie(movieId);
        if (Ratings.Count > 0)
        {
            phRatings.Visible = true;
            rptRatings.DataSource = Ratings;
            rptRatings.DataBind();
        }
        else
        {
            phNoRatings.Visible = true;
        }

        MovieRating mine = RatingRepository.Get(movieId, CurrentUser.UserId);
        if (mine != null) MyReview = mine.Review;

        phUnrate.Visible = Film.RatedByMe;
        phDelete.Visible = CanDelete;
        phWatched.Visible = MovieActions.CanManage(Film, CurrentUser);
    }

    protected string ReviewMarkup(MovieRating rating)
    {
        if (String.IsNullOrEmpty(rating.Review)) return "";
        return "<span class=\"rating-note\">&ldquo;" +
               HttpUtility.HtmlEncode(rating.Review) + "&rdquo;</span>";
    }
}
