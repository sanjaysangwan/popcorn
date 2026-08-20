using System;
using System.Text;
using System.Web;

/// <summary>
/// Small HTML builders shared by the pages. Keeping the movie card and the
/// star widget in one place means the dashboard, the library and "my ratings"
/// all look and behave the same.
/// </summary>
public static class Ui
{
    private static string E(string value) { return HttpUtility.HtmlEncode(value ?? ""); }
    private static string A(string value) { return HttpUtility.HtmlAttributeEncode(value ?? ""); }

    /// <summary>
    /// Read-only star display, e.g. the family average. Ten glyphs, filled up
    /// to the rounded score, followed by the number itself.
    /// </summary>
    public static string StarDisplay(double value, int voteCount)
    {
        StringBuilder sb = new StringBuilder();
        int filled = (int)Math.Round(value, MidpointRounding.AwayFromZero);

        sb.Append("<span class=\"stars-read\" title=\"");
        sb.Append(voteCount == 0 ? "Not rated yet"
                                 : A(value.ToString("0.0") + " out of 10 from " + voteCount +
                                     (voteCount == 1 ? " family member" : " family members")));
        sb.Append("\" aria-label=\"");
        sb.Append(voteCount == 0 ? "Not rated yet" : A(value.ToString("0.0") + " out of 10"));
        sb.Append("\">");

        for (int i = 1; i <= 10; i++)
            sb.Append(i <= filled ? "<span class=\"on\">&#9733;</span>"
                                  : "<span class=\"off\">&#9733;</span>");

        sb.Append("</span>");
        return sb.ToString();
    }

    /// <summary>
    /// The 1-10 star picker. Pure CSS radio buttons, so it works with
    /// JavaScript switched off and posts back as an ordinary form field.
    /// </summary>
    public static string StarInput(string groupId, int selected)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<span class=\"star-input\">");

        // Rendered 10 down to 1 and flipped with CSS row-reverse, which is what
        // lets "input:checked ~ label" light up every star to its left.
        for (int i = 10; i >= 1; i--)
        {
            string id = "s" + A(groupId) + "-" + i;
            sb.Append("<input type=\"radio\" name=\"stars\" id=\"").Append(id)
              .Append("\" value=\"").Append(i).Append("\"");
            if (selected == i) sb.Append(" checked=\"checked\"");
            sb.Append(" />");
            sb.Append("<label for=\"").Append(id).Append("\" title=\"")
              .Append(i).Append(" star").Append(i == 1 ? "" : "s")
              .Append("\"><span class=\"sr-only\">").Append(i).Append(" stars</span>&#9733;</label>");
        }

        sb.Append("</span>");
        return sb.ToString();
    }

    public static string PosterTag(Movie movie, string cssClass)
    {
        if (movie.HasPoster)
            return "<img class=\"" + A(cssClass) + "\" src=\"" + A(movie.PosterUrl) +
                   "\" alt=\"" + A(movie.Title + " poster") + "\" loading=\"lazy\" />";

        return "<span class=\"" + A(cssClass) + " poster-missing\" aria-hidden=\"true\">&#127916;</span>";
    }

    /// <summary>
    /// One movie tile. When <paramref name="showRatingForm"/> is true the tile
    /// carries its own little form so a member can score the film without
    /// leaving the page.
    /// </summary>
    public static string MovieCard(Movie movie, bool showRatingForm, string returnUrl)
    {
        StringBuilder sb = new StringBuilder();
        string detailsUrl = "MovieDetails.aspx?id=" + movie.MovieId;

        sb.Append("<article class=\"movie-card\">");

        sb.Append("<a class=\"movie-poster\" href=\"").Append(A(detailsUrl)).Append("\">");
        sb.Append(PosterTag(movie, "poster"));
        sb.Append("</a>");

        sb.Append("<div class=\"movie-body\">");

        sb.Append("<h3 class=\"movie-title\"><a href=\"").Append(A(detailsUrl)).Append("\">")
          .Append(E(movie.Title)).Append("</a> <span class=\"movie-year\">")
          .Append(E(movie.YearText)).Append("</span></h3>");

        sb.Append("<p class=\"movie-meta\">");
        if (!String.IsNullOrEmpty(movie.Genre)) sb.Append(E(movie.Genre));
        if (!String.IsNullOrEmpty(movie.Runtime))
            sb.Append(String.IsNullOrEmpty(movie.Genre) ? "" : " &middot; ").Append(E(movie.Runtime));
        if (!String.IsNullOrEmpty(movie.MpaaRating))
            sb.Append(" &middot; ").Append(E(movie.MpaaRating));
        sb.Append("</p>");

        if (!String.IsNullOrEmpty(movie.Actors))
            sb.Append("<p class=\"movie-cast\"><strong>Starring</strong> ")
              .Append(E(movie.Actors)).Append("</p>");

        if (!String.IsNullOrEmpty(movie.Plot))
            sb.Append("<p class=\"movie-plot\">").Append(E(Shorten(movie.Plot, 260))).Append("</p>");

        sb.Append("<p class=\"movie-added\">Added by <strong>")
          .Append(E(String.IsNullOrEmpty(movie.AddedByName) ? "a family member" : movie.AddedByName))
          .Append("</strong></p>");

        // The family verdict: the average of everyone's stars.
        sb.Append("<div class=\"family-score\">");
        sb.Append("<span class=\"score-number\">").Append(E(movie.FamilyAverageText)).Append("</span>");
        sb.Append("<span class=\"score-detail\">");
        sb.Append(StarDisplay(movie.FamilyAverage, movie.RatingCount));
        sb.Append("<span class=\"score-votes\">");
        sb.Append(movie.RatingCount == 0
                    ? "No family ratings yet"
                    : "family average of " + movie.RatingCount +
                      (movie.RatingCount == 1 ? " rating" : " ratings"));
        sb.Append("</span></span></div>");

        if (showRatingForm)
        {
            sb.Append("<form class=\"rate-form\" method=\"post\">");
            sb.Append(Csrf.Field);
            sb.Append("<input type=\"hidden\" name=\"action\" value=\"rate\" />");
            sb.Append("<input type=\"hidden\" name=\"movieId\" value=\"").Append(movie.MovieId).Append("\" />");
            if (!String.IsNullOrEmpty(returnUrl))
                sb.Append("<input type=\"hidden\" name=\"return\" value=\"").Append(A(returnUrl)).Append("\" />");

            sb.Append("<label class=\"rate-label\">")
              .Append(movie.RatedByMe ? "Your rating" : "Your rating (1-10 stars)")
              .Append("</label>");
            sb.Append(StarInput(movie.MovieId.ToString(), movie.MyStars));
            sb.Append("<button type=\"submit\" class=\"btn btn-primary btn-rate\">")
              .Append(movie.RatedByMe ? "Update my rating" : "Save my rating")
              .Append("</button>");
            sb.Append("</form>");
        }
        else if (movie.RatedByMe)
        {
            sb.Append("<p class=\"my-score\">You rated this <strong>")
              .Append(movie.MyStars).Append("/10</strong></p>");
        }

        sb.Append("</div></article>");
        return sb.ToString();
    }

    public static string Shorten(string text, int max)
    {
        if (String.IsNullOrEmpty(text) || text.Length <= max) return text;
        int cut = text.LastIndexOf(' ', Math.Min(max, text.Length - 1));
        if (cut < max / 2) cut = max;
        return text.Substring(0, cut).TrimEnd(',', '.', ';', ' ') + "...";
    }

    public static string When(DateTime utc)
    {
        TimeSpan ago = DateTime.UtcNow - utc;
        if (ago.TotalMinutes < 2) return "just now";
        if (ago.TotalMinutes < 60) return (int)ago.TotalMinutes + " minutes ago";
        if (ago.TotalHours < 24) return (int)ago.TotalHours + (ago.TotalHours < 2 ? " hour ago" : " hours ago");
        if (ago.TotalDays < 30) return (int)ago.TotalDays + (ago.TotalDays < 2 ? " day ago" : " days ago");
        return utc.ToString("d MMM yyyy");
    }
}
