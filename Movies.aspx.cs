using System;
using System.Collections.Generic;

public partial class MoviesPage : PageBase
{
    public string Term { get; private set; }
    public string Sort { get; private set; }
    public int Count { get; private set; }

    /// <summary>Where a saved rating should send the browser back to.</summary>
    public string ReturnUrl { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        Term = (Request.QueryString["q"] ?? "").Trim();
        Sort = (Request.QueryString["sort"] ?? "recent").Trim().ToLowerInvariant();
        if (Sort != "rating" && Sort != "title") Sort = "recent";

        ReturnUrl = "Movies.aspx?q=" + Url(Term) + "&sort=" + Url(Sort);

        string message;
        bool ok;
        if (RatingActions.TryHandle(Request, CurrentUser, out message, out ok))
        {
            SetFlash(message, ok ? "success" : "error");
            Go(RatingActions.ReturnUrl(Request, "Movies.aspx"));
            return;
        }

        List<Movie> movies = MovieRepository.Search(CurrentUser.UserId, Term, Sort);
        Count = movies.Count;

        rptMovies.DataSource = movies;
        rptMovies.DataBind();
        phEmpty.Visible = movies.Count == 0;
    }
}
