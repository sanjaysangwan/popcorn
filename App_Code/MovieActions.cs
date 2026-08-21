using System;
using System.Web;

/// <summary>
/// Moving a film into or out of the watched category. The control appears on
/// both the library and the details page, so the handling lives here rather
/// than in two code-behinds.
/// </summary>
public static class MovieActions
{
    /// <summary>
    /// Who may retire a film from the list: an administrator, or whoever put
    /// it up in the first place. The same rule as deleting one.
    /// </summary>
    public static bool CanManage(Movie movie, FamilyUser user)
    {
        return movie != null && user != null &&
               (user.IsAdmin || movie.AddedByUserId == user.UserId);
    }

    /// <summary>
    /// Handles a posted watched/unwatched form. Returns true when the request
    /// was one of those, so the caller knows to redirect.
    /// </summary>
    public static bool TryHandleWatched(HttpRequest request, FamilyUser user,
                                        out string message, out bool succeeded)
    {
        message = null;
        succeeded = false;

        if (!String.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        string action = (request.Form["action"] ?? "").Trim();
        bool watched = String.Equals(action, "watched", StringComparison.OrdinalIgnoreCase);
        bool unwatched = String.Equals(action, "unwatched", StringComparison.OrdinalIgnoreCase);
        if (!watched && !unwatched) return false;

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

        int movieId;
        Int32.TryParse(request.Form["movieId"] ?? "", out movieId);

        Movie movie = movieId <= 0 ? null : MovieRepository.GetById(movieId, user.UserId);
        if (movie == null)
        {
            message = "That movie is no longer on the list.";
            return true;
        }

        if (!CanManage(movie, user))
        {
            message = "Only the person who added a movie, or an administrator, can move it " +
                      "to the watched list.";
            return true;
        }

        MovieRepository.SetWatched(movieId, watched);
        succeeded = true;
        message = watched
            ? movie.Title + " has been marked as watched, so it drops out of the main list. " +
              "You can still rate it."
            : movie.Title + " is back on the list to watch.";

        return true;
    }
}
