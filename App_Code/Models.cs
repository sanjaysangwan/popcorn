using System;
using System.Data;

public class FamilyUser
{
    public int UserId;
    public string Email = "";
    public string DisplayName = "";
    public bool IsApproved;
    public bool IsAdmin;
    public bool IsDisabled;
    public DateTime CreatedUtc;
    public DateTime? ApprovedUtc;
    public DateTime? LastLoginUtc;

    public bool CanSignIn { get { return IsApproved && !IsDisabled; } }

    public static FamilyUser FromRow(DataRow row)
    {
        return new FamilyUser
        {
            UserId = Db.Int(row, "UserId"),
            Email = Db.Str(row, "Email"),
            DisplayName = Db.Str(row, "DisplayName"),
            IsApproved = Db.Bool(row, "IsApproved"),
            IsAdmin = Db.Bool(row, "IsAdmin"),
            IsDisabled = Db.Bool(row, "IsDisabled"),
            CreatedUtc = Db.Date(row, "CreatedUtc") ?? DateTime.UtcNow,
            ApprovedUtc = Db.Date(row, "ApprovedUtc"),
            LastLoginUtc = Db.Date(row, "LastLoginUtc")
        };
    }
}

/// <summary>A movie plus the aggregated family verdict and the viewer's own rating.</summary>
public class Movie
{
    public int MovieId;
    public string Title = "";
    public string ReleaseYear = "";
    public string ImdbId = "";
    public string PosterUrl = "";
    public string Plot = "";
    public string Actors = "";
    public string Director = "";
    public string Genre = "";
    public string Runtime = "";
    public string MpaaRating = "";
    public string ImdbScore = "";
    public int AddedByUserId;
    public string AddedByName = "";
    public DateTime AddedUtc;

    /// <summary>Average of every family member's stars. 0 when nobody has rated it.</summary>
    public double FamilyAverage;
    public int RatingCount;

    /// <summary>The signed-in member's own stars, or 0 when they have not rated it.</summary>
    public int MyStars;

    public bool RatedByMe { get { return MyStars > 0; } }

    public string FamilyAverageText
    {
        get { return RatingCount == 0 ? "-" : FamilyAverage.ToString("0.0"); }
    }

    public string YearText
    {
        get { return String.IsNullOrEmpty(ReleaseYear) ? "" : "(" + ReleaseYear + ")"; }
    }

    public bool HasPoster
    {
        get
        {
            return !String.IsNullOrEmpty(PosterUrl) &&
                   PosterUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static Movie FromRow(DataRow row)
    {
        Movie m = new Movie
        {
            MovieId = Db.Int(row, "MovieId"),
            Title = Db.Str(row, "Title"),
            ReleaseYear = Db.Str(row, "ReleaseYear"),
            ImdbId = Db.Str(row, "ImdbId"),
            PosterUrl = Db.Str(row, "PosterUrl"),
            Plot = Db.Str(row, "Plot"),
            Actors = Db.Str(row, "Actors"),
            Director = Db.Str(row, "Director"),
            Genre = Db.Str(row, "Genre"),
            Runtime = Db.Str(row, "Runtime"),
            MpaaRating = Db.Str(row, "MpaaRating"),
            ImdbScore = Db.Str(row, "ImdbScore"),
            AddedByUserId = Db.Int(row, "AddedByUserId"),
            AddedByName = Db.Str(row, "AddedByName"),
            AddedUtc = Db.Date(row, "AddedUtc") ?? DateTime.UtcNow,
            FamilyAverage = Db.Dbl(row, "AvgStars"),
            RatingCount = Db.Int(row, "RatingCount"),
            MyStars = Db.Int(row, "MyStars")
        };
        return m;
    }
}

/// <summary>One family member's verdict on one movie.</summary>
public class MovieRating
{
    public int RatingId;
    public int MovieId;
    public int UserId;
    public string DisplayName = "";
    public int Stars;
    public string Review = "";
    public DateTime UpdatedUtc;

    public static MovieRating FromRow(DataRow row)
    {
        return new MovieRating
        {
            RatingId = Db.Int(row, "RatingId"),
            MovieId = Db.Int(row, "MovieId"),
            UserId = Db.Int(row, "UserId"),
            DisplayName = Db.Str(row, "DisplayName"),
            Stars = Db.Int(row, "Stars"),
            Review = Db.Str(row, "Review"),
            UpdatedUtc = Db.Date(row, "UpdatedUtc") ?? DateTime.UtcNow
        };
    }
}

/// <summary>A single hit from the OMDb search endpoint.</summary>
public class MovieSearchResult
{
    public string ImdbId = "";
    public string Title = "";
    public string Year = "";
    public string PosterUrl = "";
    public string Type = "";

    public bool HasPoster
    {
        get
        {
            return !String.IsNullOrEmpty(PosterUrl) &&
                   PosterUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>One row of the Family page: a member and how active they have been.</summary>
public class MemberSummary
{
    public string Name = "";
    public bool IsAdmin;
    public int MoviesAdded;
    public int RatingsGiven;
    public string AverageText = "-";
    public string Since = "";
}
