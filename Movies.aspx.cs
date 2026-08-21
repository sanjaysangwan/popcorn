using System;
using System.Collections.Generic;

public partial class MoviesPage : PageBase
{
    public string Term { get; private set; }
    public string Sort { get; private set; }
    public string Show { get; private set; }
    public int Count { get; private set; }

    /// <summary>"still to watch" / "already watched", for the count line.</summary>
    public string ShowCaption
    {
        get
        {
            if (Show == MovieRepository.ShowWatched) return " already watched";
            if (Show == MovieRepository.ShowAll) return " in the library";
            return " still to watch";
        }
    }

    public string EmptyMessage
    {
        get
        {
            if (Show == MovieRepository.ShowWatched)
                return "Nothing has been marked as watched yet.";
            if (!String.IsNullOrEmpty(Term))
                return "Nothing here matches that search.";
            return "Nothing here yet.";
        }
    }

    /// <summary>Explains where the watched films went, but only once there are some.</summary>
    public string WatchedHint
    {
        get
        {
            if (Show != MovieRepository.ShowToWatch || WatchedCount == 0) return "";
            return "<a href=\"" + ResolveUrl("~/Movies.aspx") + "?show=watched\">" +
                   WatchedCount + (WatchedCount == 1 ? " watched movie is" : " watched movies are") +
                   " tucked away.</a>";
        }
    }

    public int WatchedCount { get; private set; }

    /// <summary>Whether this member may retire the film from the list.</summary>
    public bool CanManage(Movie movie)
    {
        return MovieActions.CanManage(movie, CurrentUser);
    }

    /// <summary>Where a saved rating should send the browser back to.</summary>
    public string ReturnUrl { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        Term = (Request.QueryString["q"] ?? "").Trim();
        Sort = (Request.QueryString["sort"] ?? "recent").Trim().ToLowerInvariant();
        if (Sort != "rating" && Sort != "title") Sort = "recent";
        Show = MovieRepository.CleanShow(Request.QueryString["show"]);

        ReturnUrl = "Movies.aspx?q=" + Url(Term) + "&sort=" + Url(Sort) + "&show=" + Url(Show);

        string message;
        bool ok;
        if (MovieActions.TryHandleWatched(Request, CurrentUser, out message, out ok) ||
            RatingActions.TryHandle(Request, CurrentUser, out message, out ok))
        {
            SetFlash(message, ok ? "success" : "error");
            Go(RatingActions.ReturnUrl(Request, "Movies.aspx"));
            return;
        }

        List<Movie> movies = MovieRepository.Search(CurrentUser.UserId, Term, Sort, Show);
        Count = movies.Count;

        WatchedCount = MovieRepository.Search(
            CurrentUser.UserId, null, "recent", MovieRepository.ShowWatched).Count;

        rptMovies.DataSource = movies;
        rptMovies.DataBind();
        phEmpty.Visible = movies.Count == 0;
    }
}
