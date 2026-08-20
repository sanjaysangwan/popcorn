using System;
using System.Web;

/// <summary>
/// The "save my rating" form appears on several pages, so the handling for it
/// lives here rather than being copied into each code-behind.
/// </summary>
public static class RatingActions
{
    /// <summary>
    /// Handles a posted rating form. Returns true when the request was a rating
    /// post (whether or not it succeeded) so the caller knows to redirect.
    /// </summary>
    public static bool TryHandle(HttpRequest request, FamilyUser user, out string message,
                                 out bool succeeded)
    {
        message = null;
        succeeded = false;

        if (!String.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!String.Equals(request.Form["action"], "rate", StringComparison.OrdinalIgnoreCase))
            return false;

        if (user == null)
        {
            message = "Please sign in again - your session expired.";
            return true;
        }
        if (!Csrf.IsValid(request))
        {
            message = "That form expired. Please try again.";
            return true;
        }

        int movieId, stars;
        Int32.TryParse(request.Form["movieId"] ?? "", out movieId);
        Int32.TryParse(request.Form["stars"] ?? "", out stars);

        if (movieId <= 0)
        {
            message = "Could not work out which movie that was.";
            return true;
        }
        if (stars < RatingRepository.MinStars || stars > RatingRepository.MaxStars)
        {
            message = "Pick between 1 and 10 stars.";
            return true;
        }

        Movie movie = MovieRepository.GetById(movieId, user.UserId);
        if (movie == null)
        {
            message = "That movie is no longer on the list.";
            return true;
        }

        // Null when the posted form had no review box at all (the movie cards),
        // which tells the repository to leave any existing note alone.
        string review = request.Form["review"];
        if (review != null) review = review.Trim();

        RatingRepository.Save(movieId, user.UserId, stars, review);

        succeeded = true;
        message = "Saved your " + stars + "-star rating for " + movie.Title + ".";
        return true;
    }

    /// <summary>Where to send the browser after a rating post.</summary>
    public static string ReturnUrl(HttpRequest request, string fallback)
    {
        string target = (request.Form["return"] ?? "").Trim();

        // Only ever redirect within this site.
        if (String.IsNullOrEmpty(target) || target.Contains("//") || target.StartsWith("/") ||
            target.Contains(":") || target.Contains("\\"))
            return fallback;

        return target;
    }
}
