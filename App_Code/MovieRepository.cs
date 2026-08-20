using System;
using System.Collections.Generic;
using System.Data;

public static class MovieRepository
{
    /// <summary>
    /// Every movie query starts here. The correlated sub-selects give us the
    /// family average, how many members have voted, and the viewer's own stars
    /// in one round trip - Access cannot GROUP BY a MEMO column such as Plot,
    /// so sub-selects are used instead of a join plus aggregate.
    ///
    /// The first "?" is always the id of the signed-in member.
    /// </summary>
    private const string BaseSelect =
        "SELECT m.MovieId, m.Title, m.ReleaseYear, m.ImdbId, m.PosterUrl, m.Plot, m.Actors, " +
        "       m.Director, m.Genre, m.Runtime, m.MpaaRating, m.ImdbScore, " +
        "       m.AddedByUserId, m.AddedUtc, " +
        "       (SELECT u.DisplayName FROM Users AS u WHERE u.UserId = m.AddedByUserId) AS AddedByName, " +
        "       (SELECT AVG(ra.Stars) FROM Ratings AS ra WHERE ra.MovieId = m.MovieId) AS AvgStars, " +
        "       (SELECT COUNT(*) FROM Ratings AS rc WHERE rc.MovieId = m.MovieId) AS RatingCount, " +
        "       (SELECT MAX(rm.Stars) FROM Ratings AS rm WHERE rm.MovieId = m.MovieId " +
        "               AND rm.UserId = ?) AS MyStars " +
        "FROM Movies AS m ";

    private static List<Movie> Read(DataTable table)
    {
        List<Movie> list = new List<Movie>();
        foreach (DataRow row in table.Rows) list.Add(Movie.FromRow(row));
        return list;
    }

    public static Movie GetById(int movieId, int viewerUserId)
    {
        DataTable t = Db.Query(BaseSelect + "WHERE m.MovieId = ?", viewerUserId, movieId);
        return t.Rows.Count == 0 ? null : Movie.FromRow(t.Rows[0]);
    }

    /// <summary>
    /// The login landing list: films somebody else put up that the viewer has
    /// not rated yet. Newest first.
    /// </summary>
    public static List<Movie> AwaitingMyRating(int viewerUserId, int max)
    {
        string top = max > 0 ? "TOP " + max + " " : "";
        string sql = BaseSelect.Replace("SELECT m.MovieId", "SELECT " + top + "m.MovieId") +
                     "WHERE m.AddedByUserId <> ? " +
                     "  AND m.MovieId NOT IN (SELECT r.MovieId FROM Ratings AS r WHERE r.UserId = ?) " +
                     "ORDER BY m.AddedUtc DESC";
        return Read(Db.Query(sql, viewerUserId, viewerUserId, viewerUserId));
    }

    /// <summary>Newest additions from anyone, including the viewer.</summary>
    public static List<Movie> RecentlyAdded(int viewerUserId, int max)
    {
        string top = max > 0 ? "TOP " + max + " " : "";
        string sql = BaseSelect.Replace("SELECT m.MovieId", "SELECT " + top + "m.MovieId") +
                     "ORDER BY m.AddedUtc DESC";
        return Read(Db.Query(sql, viewerUserId));
    }

    /// <summary>
    /// The full library, optionally filtered by a search term.
    /// sort: "recent" (default), "rating", "title".
    /// </summary>
    public static List<Movie> Search(int viewerUserId, string term, string sort)
    {
        List<object> args = new List<object>();
        args.Add(viewerUserId);

        string sql = BaseSelect;
        if (!String.IsNullOrEmpty(term))
        {
            string like = "%" + term.Trim() + "%";
            sql += "WHERE (m.Title LIKE ? OR m.Actors LIKE ? OR m.Director LIKE ? OR m.Genre LIKE ?) ";
            args.Add(like); args.Add(like); args.Add(like); args.Add(like);
        }

        switch (sort)
        {
            case "rating":
                // Unrated films fall to the bottom because Access sorts NULL last
                // in a descending sort.
                sql += "ORDER BY AvgStars DESC, m.Title";
                break;
            case "title":
                sql += "ORDER BY m.Title";
                break;
            default:
                sql += "ORDER BY m.AddedUtc DESC";
                break;
        }

        return Read(Db.Query(sql, args.ToArray()));
    }

    /// <summary>Everything the viewer has rated, best first.</summary>
    public static List<Movie> RatedBy(int viewerUserId)
    {
        string sql = BaseSelect +
                     "WHERE m.MovieId IN (SELECT r.MovieId FROM Ratings AS r WHERE r.UserId = ?) " +
                     "ORDER BY MyStars DESC, m.Title";
        return Read(Db.Query(sql, viewerUserId, viewerUserId));
    }

    /// <summary>Films added by a particular member.</summary>
    public static List<Movie> AddedBy(int viewerUserId, int authorUserId)
    {
        return Read(Db.Query(BaseSelect + "WHERE m.AddedByUserId = ? ORDER BY m.AddedUtc DESC",
                             viewerUserId, authorUserId));
    }

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

    /// <summary>Removes a film and every rating attached to it.</summary>
    public static void Delete(int movieId)
    {
        Db.Execute("DELETE FROM Ratings WHERE MovieId = ?", movieId);
        Db.Execute("DELETE FROM Movies WHERE MovieId = ?", movieId);
    }
}
