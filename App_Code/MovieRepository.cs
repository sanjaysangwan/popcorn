using System;
using System.Collections.Generic;
using System.Data;

/// <summary>
/// Reading and writing the movie list.
///
/// Every query here is a plain SELECT over one table: no correlated
/// sub-selects, no aggregates in the field list, no ordering by a computed
/// alias. Access accepts some of those and rejects others depending on
/// whether Jet or ACE is behind it, and a rejection surfaces as a generic
/// error a long way from the cause. The family average, the vote count and
/// the viewer's own score are assembled in memory instead, by
/// <see cref="Assemble"/>, which is ordinary code that can be tested.
///
/// This is a family movie list - hundreds of rows, not millions - so reading
/// the ratings table and joining it up here costs nothing worth measuring.
/// </summary>
public static class MovieRepository
{
    private const string SelectMovies =
        "SELECT MovieId, Title, ReleaseYear, ImdbId, PosterUrl, Plot, Actors, Director, " +
        "Genre, Runtime, MpaaRating, ImdbScore, AddedByUserId, AddedUtc, IsWatched, WatchedUtc " +
        "FROM Movies";

    // ----- loading ---------------------------------------------------------

    /// <summary>Every movie, with the family verdict and the viewer's own score attached.</summary>
    private static List<Movie> LoadAll(int viewerUserId)
    {
        List<Movie> movies = Assemble(Db.Query(SelectMovies),
                                      Db.Query("SELECT MovieId, UserId, Stars FROM Ratings"),
                                      Db.Query("SELECT UserId, DisplayName FROM Users"),
                                      viewerUserId);

        return AttachTags(movies, TagRepository.AllLinks(), TagRepository.AllTagRows());
    }

    /// <summary>
    /// Hangs the categories on an already-assembled list. Kept apart from
    /// <see cref="Assemble"/> so each does one job and both can be tested.
    /// </summary>
    public static List<Movie> AttachTags(List<Movie> movies, DataTable linkRows, DataTable tagRows)
    {
        if (movies == null) return new List<Movie>();
        if (linkRows == null || tagRows == null) return movies;

        Dictionary<int, Tag> tags = new Dictionary<int, Tag>();
        foreach (DataRow row in tagRows.Rows)
        {
            Tag tag = Tag.FromRow(row);
            tags[tag.TagId] = tag;
        }

        Dictionary<int, List<Tag>> byMovie = new Dictionary<int, List<Tag>>();
        foreach (DataRow row in linkRows.Rows)
        {
            int movieId = Db.Int(row, "MovieId");
            int tagId = Db.Int(row, "TagId");
            if (!tags.ContainsKey(tagId)) continue;

            if (!byMovie.ContainsKey(movieId)) byMovie[movieId] = new List<Tag>();
            byMovie[movieId].Add(tags[tagId]);
        }

        foreach (Movie movie in movies)
        {
            if (!byMovie.ContainsKey(movie.MovieId)) continue;

            List<Tag> theirs = byMovie[movie.MovieId];
            theirs.Sort(delegate(Tag a, Tag b)
            {
                return String.Compare(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase);
            });
            movie.Tags = theirs;
        }

        return movies;
    }

    /// <summary>Keeps only the films in a given category.</summary>
    public static List<Movie> WithTag(List<Movie> movies, string tagKey)
    {
        tagKey = Tag.ToKey(tagKey);
        if (tagKey.Length == 0) return movies;

        List<Movie> kept = new List<Movie>();
        foreach (Movie movie in movies)
            if (movie.HasTag(tagKey)) kept.Add(movie);

        return kept;
    }

    /// <summary>
    /// Turns three flat result sets into movies carrying their ratings.
    /// Separated out from the database so it can be tested directly.
    /// </summary>
    public static List<Movie> Assemble(DataTable movieRows, DataTable ratingRows,
                                       DataTable userRows, int viewerUserId)
    {
        Dictionary<int, string> names = new Dictionary<int, string>();
        if (userRows != null)
            foreach (DataRow row in userRows.Rows)
                names[Db.Int(row, "UserId")] = Db.Str(row, "DisplayName");

        // MovieId -> running total and count, plus this viewer's own score.
        Dictionary<int, int> totals = new Dictionary<int, int>();
        Dictionary<int, int> counts = new Dictionary<int, int>();
        Dictionary<int, int> mine = new Dictionary<int, int>();

        if (ratingRows != null)
        {
            foreach (DataRow row in ratingRows.Rows)
            {
                int movieId = Db.Int(row, "MovieId");
                int stars = Db.Int(row, "Stars");

                totals[movieId] = (totals.ContainsKey(movieId) ? totals[movieId] : 0) + stars;
                counts[movieId] = (counts.ContainsKey(movieId) ? counts[movieId] : 0) + 1;

                if (Db.Int(row, "UserId") == viewerUserId) mine[movieId] = stars;
            }
        }

        List<Movie> movies = new List<Movie>();
        if (movieRows == null) return movies;

        foreach (DataRow row in movieRows.Rows)
        {
            Movie movie = Movie.FromRow(row);

            if (names.ContainsKey(movie.AddedByUserId))
                movie.AddedByName = names[movie.AddedByUserId];

            if (counts.ContainsKey(movie.MovieId) && counts[movie.MovieId] > 0)
            {
                movie.RatingCount = counts[movie.MovieId];
                // The film's headline score: the average of every family
                // member's rating, never stored, always recomputed.
                movie.FamilyAverage = (double)totals[movie.MovieId] / counts[movie.MovieId];
            }

            if (mine.ContainsKey(movie.MovieId)) movie.MyStars = mine[movie.MovieId];

            movies.Add(movie);
        }

        return movies;
    }

    // ----- the views the pages ask for -------------------------------------

    public static Movie GetById(int movieId, int viewerUserId)
    {
        if (movieId <= 0) return null;

        List<Movie> found = Assemble(
            Db.Query(SelectMovies + " WHERE MovieId = ?", movieId),
            Db.Query("SELECT MovieId, UserId, Stars FROM Ratings WHERE MovieId = ?", movieId),
            Db.Query("SELECT UserId, DisplayName FROM Users"),
            viewerUserId);

        if (found.Count == 0) return null;

        // Assemble does not know about categories - LoadAll attaches them for
        // the list views, and a single movie needs the same thing here.
        found[0].Tags = TagRepository.ForMovie(movieId);
        return found[0];
    }

    /// <summary>
    /// The login landing list: films somebody else put up that the viewer has
    /// not rated yet, newest first.
    /// </summary>
    public static List<Movie> AwaitingMyRating(int viewerUserId, int max)
    {
        List<Movie> awaiting = new List<Movie>();

        foreach (Movie movie in SortByNewest(LoadAll(viewerUserId)))
        {
            if (movie.AddedByUserId == viewerUserId) continue;
            if (movie.RatedByMe) continue;

            awaiting.Add(movie);
            if (max > 0 && awaiting.Count == max) break;
        }

        return awaiting;
    }

    /// <summary>
    /// The full library, optionally filtered by a search term.
    /// sort: "recent" (default), "rating", "title".
    /// </summary>
    public static List<Movie> Search(int viewerUserId, string term, string sort)
    {
        return Search(viewerUserId, term, sort, ShowToWatch);
    }

    /// <summary>
    /// The library, filtered by search term and by whether the family has
    /// watched the film yet. <paramref name="show"/> is one of
    /// <see cref="ShowToWatch"/>, <see cref="ShowWatched"/> or <see cref="ShowAll"/>.
    /// </summary>
    public static List<Movie> Search(int viewerUserId, string term, string sort, string show)
    {
        return Search(viewerUserId, term, sort, show, null);
    }

    /// <summary>The library, additionally narrowed to one category.</summary>
    public static List<Movie> Search(int viewerUserId, string term, string sort, string show,
                                     string tagKey)
    {
        List<Movie> movies = OnlyWatched(LoadAll(viewerUserId), show);
        movies = WithTag(movies, tagKey);
        return SortBy(Match(movies, term), sort);
    }

    /// <summary>Everything the viewer has rated, their own favourites first.</summary>
    public static List<Movie> RatedBy(int viewerUserId)
    {
        List<Movie> rated = new List<Movie>();
        foreach (Movie movie in LoadAll(viewerUserId))
            if (movie.RatedByMe) rated.Add(movie);

        return SortBy(rated, "mine");
    }

    /// <summary>Films added by a particular member.</summary>
    public static List<Movie> AddedBy(int viewerUserId, int authorUserId)
    {
        List<Movie> theirs = new List<Movie>();
        foreach (Movie movie in LoadAll(viewerUserId))
            if (movie.AddedByUserId == authorUserId) theirs.Add(movie);

        return SortByNewest(theirs);
    }

    // ----- filtering and ordering ------------------------------------------

    public const string ShowToWatch = "towatch";
    public const string ShowWatched = "watched";
    public const string ShowAll = "all";

    /// <summary>Normalises the value that arrives on the query string.</summary>
    public static string CleanShow(string show)
    {
        show = (show ?? "").Trim().ToLowerInvariant();
        return (show == ShowWatched || show == ShowAll) ? show : ShowToWatch;
    }

    /// <summary>Keeps the films matching the watched filter.</summary>
    public static List<Movie> OnlyWatched(List<Movie> movies, string show)
    {
        show = CleanShow(show);
        if (show == ShowAll) return movies;

        bool wantWatched = (show == ShowWatched);

        List<Movie> kept = new List<Movie>();
        foreach (Movie movie in movies)
            if (movie.IsWatched == wantWatched) kept.Add(movie);

        return kept;
    }

    /// <summary>Free-text match over title, cast, director and genre.</summary>
    public static List<Movie> Match(List<Movie> movies, string term)
    {
        if (String.IsNullOrEmpty(term) || term.Trim().Length == 0) return movies;

        string needle = term.Trim();
        List<Movie> hits = new List<Movie>();

        foreach (Movie movie in movies)
        {
            if (Contains(movie.Title, needle) || Contains(movie.Actors, needle) ||
                Contains(movie.Director, needle) || Contains(movie.Genre, needle) ||
                Contains(movie.ReleaseYear, needle) || HasMatchingTag(movie, needle))
                hits.Add(movie);
        }

        return hits;
    }

    /// <summary>So typing "christmas" in the search box finds the Christmas shelf.</summary>
    private static bool HasMatchingTag(Movie movie, string needle)
    {
        foreach (Tag tag in movie.Tags)
            if (Contains(tag.TagName, needle)) return true;
        return false;
    }

    private static bool Contains(string haystack, string needle)
    {
        return !String.IsNullOrEmpty(haystack) &&
               haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Orders a list in place and returns it.
    /// "recent" (the default), "rating", "title", or "mine" for the viewer's
    /// own score. Ties fall back to the title so the order never wobbles
    /// between page loads.
    /// </summary>
    public static List<Movie> SortBy(List<Movie> movies, string sort)
    {
        switch (sort)
        {
            case "rating":
                movies.Sort(delegate(Movie a, Movie b)
                {
                    // Unrated films have an average of 0, so they fall to the bottom.
                    int byScore = b.FamilyAverage.CompareTo(a.FamilyAverage);
                    return byScore != 0 ? byScore : ByTitle(a, b);
                });
                return movies;

            case "mine":
                movies.Sort(delegate(Movie a, Movie b)
                {
                    int byMine = b.MyStars.CompareTo(a.MyStars);
                    return byMine != 0 ? byMine : ByTitle(a, b);
                });
                return movies;

            case "title":
                movies.Sort(ByTitle);
                return movies;

            default:
                return SortByNewest(movies);
        }
    }

    private static List<Movie> SortByNewest(List<Movie> movies)
    {
        movies.Sort(delegate(Movie a, Movie b)
        {
            int byDate = b.AddedUtc.CompareTo(a.AddedUtc);
            return byDate != 0 ? byDate : b.MovieId.CompareTo(a.MovieId);
        });
        return movies;
    }

    private static int ByTitle(Movie a, Movie b)
    {
        return String.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
    }

    // ----- writing ---------------------------------------------------------

    public static int Count()
    {
        return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Movies") ?? 0);
    }

    /// <summary>Finds an existing entry so the same film is not added twice.</summary>
    public static int FindDuplicate(string imdbId, string title, string year)
    {
        if (!String.IsNullOrEmpty(imdbId))
        {
            object byImdb = Db.Scalar("SELECT MIN(MovieId) FROM Movies WHERE ImdbId = ?", imdbId);
            if (byImdb != null) return Convert.ToInt32(byImdb);
        }

        // ReleaseYear is stored as NULL when unknown, and "= NULL" never matches
        // in SQL, so an unknown year needs the IS NULL form instead.
        string cleanYear = (year ?? "").Trim();
        object byTitle = cleanYear.Length == 0
            ? Db.Scalar("SELECT MIN(MovieId) FROM Movies WHERE Title = ? AND ReleaseYear IS NULL",
                        Db.Trim((title ?? "").Trim(), 200))
            : Db.Scalar("SELECT MIN(MovieId) FROM Movies WHERE Title = ? AND ReleaseYear = ?",
                        Db.Trim((title ?? "").Trim(), 200), Db.Trim(cleanYear, 12));

        return byTitle == null ? 0 : Convert.ToInt32(byTitle);
    }

    public static int Add(Movie movie, int addedByUserId)
    {
        return Db.Insert(
            "INSERT INTO Movies (Title, ReleaseYear, ImdbId, PosterUrl, Plot, Actors, Director, " +
            "Genre, Runtime, MpaaRating, ImdbScore, AddedByUserId, AddedUtc) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Db.Trim(movie.Title, 200),
            Db.Text(movie.ReleaseYear, 12),
            Db.Text(movie.ImdbId, 20),
            Db.Text(movie.PosterUrl, 255),
            Db.Text(movie.Plot, 60000),
            Db.Text(movie.Actors, 255),
            Db.Text(movie.Director, 255),
            Db.Text(movie.Genre, 150),
            Db.Text(movie.Runtime, 40),
            Db.Text(movie.MpaaRating, 20),
            Db.Text(movie.ImdbScore, 10),
            addedByUserId,
            DateTime.UtcNow);
    }

    /// <summary>Refreshes the cached details from OMDb without touching ratings.</summary>
    public static void UpdateDetails(Movie movie)
    {
        Db.Execute(
            "UPDATE Movies SET Title = ?, ReleaseYear = ?, ImdbId = ?, PosterUrl = ?, Plot = ?, " +
            "Actors = ?, Director = ?, Genre = ?, Runtime = ?, MpaaRating = ?, ImdbScore = ? " +
            "WHERE MovieId = ?",
            Db.Trim(movie.Title, 200),
            Db.Text(movie.ReleaseYear, 12),
            Db.Text(movie.ImdbId, 20),
            Db.Text(movie.PosterUrl, 255),
            Db.Text(movie.Plot, 60000),
            Db.Text(movie.Actors, 255),
            Db.Text(movie.Director, 255),
            Db.Text(movie.Genre, 150),
            Db.Text(movie.Runtime, 40),
            Db.Text(movie.MpaaRating, 20),
            Db.Text(movie.ImdbScore, 10),
            movie.MovieId);
    }

    /// <summary>
    /// Moves a film into or out of the watched category. Ratings are left
    /// alone - this only decides whether it shows up in the default library
    /// view.
    /// </summary>
    public static void SetWatched(int movieId, bool watched)
    {
        if (watched)
            Db.Execute("UPDATE Movies SET IsWatched = ?, WatchedUtc = ? WHERE MovieId = ?",
                       true, DateTime.UtcNow, movieId);
        else
            Db.Execute("UPDATE Movies SET IsWatched = ? WHERE MovieId = ?", false, movieId);
    }

    /// <summary>Removes a film and every rating attached to it.</summary>
    public static void Delete(int movieId)
    {
        Db.Execute("DELETE FROM Ratings WHERE MovieId = ?", movieId);
        Db.Execute("DELETE FROM Movies WHERE MovieId = ?", movieId);
    }
}
