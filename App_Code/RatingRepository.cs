using System;
using System.Collections.Generic;
using System.Data;

public static class RatingRepository
{
    public const int MinStars = 1;
    public const int MaxStars = 10;

    /// <summary>Every family member's verdict on one movie, highest first.</summary>
    public static List<MovieRating> ForMovie(int movieId)
    {
        DataTable t = Db.Query(
            "SELECT r.RatingId, r.MovieId, r.UserId, r.Stars, r.Review, r.UpdatedUtc, u.DisplayName " +
            "FROM Ratings AS r INNER JOIN Users AS u ON r.UserId = u.UserId " +
            "WHERE r.MovieId = ? ORDER BY r.Stars DESC, u.DisplayName",
            movieId);

        List<MovieRating> list = new List<MovieRating>();
        foreach (DataRow row in t.Rows) list.Add(MovieRating.FromRow(row));
        return list;
    }

    public static MovieRating Get(int movieId, int userId)
    {
        DataTable t = Db.Query(
            "SELECT r.RatingId, r.MovieId, r.UserId, r.Stars, r.Review, r.UpdatedUtc, u.DisplayName " +
            "FROM Ratings AS r INNER JOIN Users AS u ON r.UserId = u.UserId " +
            "WHERE r.MovieId = ? AND r.UserId = ?",
            movieId, userId);
        return t.Rows.Count == 0 ? null : MovieRating.FromRow(t.Rows[0]);
    }

    /// <summary>
    /// Records the member's own stars for a movie, replacing their previous
    /// score if they had already rated it. The family average is never stored -
    /// it is always recomputed from the individual ratings.
    ///
    /// A null <paramref name="review"/> means "the form had no review box on
    /// it", as on the movie cards, and leaves any existing note alone. An
    /// empty string means the member cleared it deliberately.
    /// </summary>
    public static void Save(int movieId, int userId, int stars, string review)
    {
        if (stars < MinStars) stars = MinStars;
        if (stars > MaxStars) stars = MaxStars;

        object existing = Db.Scalar(
            "SELECT RatingId FROM Ratings WHERE MovieId = ? AND UserId = ?", movieId, userId);

        if (existing == null)
        {
            Db.Execute(
                "INSERT INTO Ratings (MovieId, UserId, Stars, Review, CreatedUtc, UpdatedUtc) " +
                "VALUES (?, ?, ?, ?, ?, ?)",
                movieId, userId, stars, Db.Text(review, 2000), DateTime.UtcNow, DateTime.UtcNow);
        }
        else if (review == null)
        {
            Db.Execute("UPDATE Ratings SET Stars = ?, UpdatedUtc = ? WHERE RatingId = ?",
                       stars, DateTime.UtcNow, Convert.ToInt32(existing));
        }
        else
        {
            Db.Execute(
                "UPDATE Ratings SET Stars = ?, Review = ?, UpdatedUtc = ? WHERE RatingId = ?",
                stars, Db.Text(review, 2000), DateTime.UtcNow, Convert.ToInt32(existing));
        }
    }

    public static void Remove(int movieId, int userId)
    {
        Db.Execute("DELETE FROM Ratings WHERE MovieId = ? AND UserId = ?", movieId, userId);
    }

    public static int CountByUser(int userId)
    {
        return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Ratings WHERE UserId = ?", userId) ?? 0);
    }

    public static int TotalCount()
    {
        return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Ratings") ?? 0);
    }
}
