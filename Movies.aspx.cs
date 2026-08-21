using System;
using System.Collections.Generic;

public partial class MoviesPage : PageBase
{
    public string Term { get; private set; }
    public string Sort { get; private set; }
    public string Show { get; private set; }

    /// <summary>The category the library is narrowed to, or "" for all of them.</summary>
    public string TagKey { get; private set; }

    /// <summary>The row of category chips above the list, current one highlighted.</summary>
    public string TagCloud
    {
        get
        {
            List<Tag> tags = TagRepository.All();
            if (tags.Count == 0) return "";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append("<a class=\"tag").Append(TagKey.Length == 0 ? " is-current" : "")
              .Append("\" href=\"").Append(Attr(BaseUrl(""))).Append("\">All categories</a>");

            foreach (Tag tag in tags)
            {
                if (tag.MovieCount == 0 && !String.Equals(tag.TagKey, TagKey)) continue;

                sb.Append("<a class=\"tag")
                  .Append(String.Equals(tag.TagKey, TagKey, StringComparison.OrdinalIgnoreCase)
                              ? " is-current" : "")
                  .Append("\" href=\"").Append(Attr(BaseUrl(tag.TagKey))).Append("\">")
                  .Append(H(tag.TagName))
                  .Append("<span class=\"tag-count\">").Append(tag.MovieCount).Append("</span></a>");
            }

            return sb.ToString();
        }
    }

    /// <summary>This page's URL with the same search and ordering, for a given category.</summary>
    private string BaseUrl(string tagKey)
    {
        return "Movies.aspx?q=" + Url(Term) + "&sort=" + Url(Sort) +
               "&show=" + Url(Show) + "&tag=" + Url(tagKey);
    }
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
            if (TagKey.Length > 0)
                return "No movies in that category yet.";
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
        TagKey = Tag.ToKey(Request.QueryString["tag"]);

        ReturnUrl = BaseUrl(TagKey);

        string message;
        bool ok;
        if (MovieActions.TryHandleWatched(Request, CurrentUser, out message, out ok) ||
            RatingActions.TryHandle(Request, CurrentUser, out message, out ok))
        {
            SetFlash(message, ok ? "success" : "error");
            Go(RatingActions.ReturnUrl(Request, "Movies.aspx"));
            return;
        }

        List<Movie> movies = MovieRepository.Search(CurrentUser.UserId, Term, Sort, Show, TagKey);
        Count = movies.Count;

        WatchedCount = MovieRepository.Search(
            CurrentUser.UserId, null, "recent", MovieRepository.ShowWatched).Count;

        rptMovies.DataSource = movies;
        rptMovies.DataBind();
        phEmpty.Visible = movies.Count == 0;
    }
}
