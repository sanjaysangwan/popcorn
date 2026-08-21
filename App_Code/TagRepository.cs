using System;
using System.Collections.Generic;
using System.Data;

/// <summary>
/// The searchable categories a movie can be put in.
///
/// Tags live in their own table rather than as a comma-separated column, so
/// "Christmas" typed by one member is the same category as "christmas" typed
/// by another, films can be browsed by tag, and each tag can be counted.
/// </summary>
public static class TagRepository
{
    /// <summary>
    /// The categories every new site starts with. Anyone can add more while
    /// tagging a film; these are just a sensible starting shelf.
    /// </summary>
    public static readonly string[] DefaultTags = new string[]
    {
        "Comedy", "Sad", "Romantic", "Family", "Christmas",
        "SciFi", "Historical", "Real Life Story"
    };

    /// <summary>
    /// What OMDb calls a genre, mapped onto the family's own categories, so a
    /// newly added film arrives already sorted where the names line up. Only
    /// ever applies tags that exist - it never invents new ones.
    /// </summary>
    private static readonly string[][] GenreAliases = new string[][]
    {
        new string[] { "sci-fi", "scifi" },
        new string[] { "science fiction", "scifi" },
        new string[] { "romance", "romantic" },
        new string[] { "history", "historical" },
        new string[] { "biography", "real life story" },
        new string[] { "documentary", "real life story" },
        new string[] { "holiday", "christmas" }
    };

    private const int MaxNameLength = 50;

    // ----- reading ---------------------------------------------------------

    /// <summary>Every tag, alphabetically, each with the number of films carrying it.</summary>
    public static List<Tag> All()
    {
        DataTable tagRows = Db.Query("SELECT TagId, TagName, TagKey FROM Tags");
        DataTable linkRows = Db.Query("SELECT MovieId, TagId FROM MovieTags");

        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (DataRow row in linkRows.Rows)
        {
            int tagId = Db.Int(row, "TagId");
            counts[tagId] = (counts.ContainsKey(tagId) ? counts[tagId] : 0) + 1;
        }

        List<Tag> tags = new List<Tag>();
        foreach (DataRow row in tagRows.Rows)
        {
            Tag tag = Tag.FromRow(row);
            if (counts.ContainsKey(tag.TagId)) tag.MovieCount = counts[tag.TagId];
            tags.Add(tag);
        }

        tags.Sort(delegate(Tag a, Tag b)
        {
            return String.Compare(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase);
        });

        return tags;
    }

    public static Tag GetByKey(string tagKey)
    {
        tagKey = Tag.ToKey(tagKey);
        if (tagKey.Length == 0) return null;

        DataTable t = Db.Query("SELECT TagId, TagName, TagKey FROM Tags WHERE TagKey = ?", tagKey);
        return t.Rows.Count == 0 ? null : Tag.FromRow(t.Rows[0]);
    }

    /// <summary>The tags on one film.</summary>
    public static List<Tag> ForMovie(int movieId)
    {
        DataTable t = Db.Query(
            "SELECT t.TagId, t.TagName, t.TagKey FROM Tags AS t INNER JOIN MovieTags AS mt " +
            "ON t.TagId = mt.TagId WHERE mt.MovieId = ?", movieId);

        List<Tag> tags = new List<Tag>();
        foreach (DataRow row in t.Rows) tags.Add(Tag.FromRow(row));
        tags.Sort(delegate(Tag a, Tag b)
        {
            return String.Compare(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase);
        });
        return tags;
    }

    /// <summary>Every movie-to-tag link, for hanging tags on a whole list at once.</summary>
    public static DataTable AllLinks()
    {
        return Db.Query("SELECT MovieId, TagId FROM MovieTags");
    }

    public static DataTable AllTagRows()
    {
        return Db.Query("SELECT TagId, TagName, TagKey FROM Tags");
    }

    // ----- writing ---------------------------------------------------------

    /// <summary>
    /// Finds a tag by name, creating it if the family has not used it before.
    /// Returns 0 for a name that is blank or nothing but punctuation.
    /// </summary>
    public static int EnsureTag(string name, int createdByUserId)
    {
        string clean = Clean(name);
        if (clean.Length == 0) return 0;

        string key = Tag.ToKey(clean);

        object existing = Db.Scalar("SELECT TagId FROM Tags WHERE TagKey = ?", key);
        if (existing != null) return Convert.ToInt32(existing);

        return Db.Insert(
            "INSERT INTO Tags (TagName, TagKey, CreatedByUserId, CreatedUtc) VALUES (?, ?, ?, ?)",
            clean, key, createdByUserId, DateTime.UtcNow);
    }

    /// <summary>
    /// Replaces a film's categories with exactly the names given, creating any
    /// the family has not used before.
    /// </summary>
    public static void SetMovieTags(int movieId, List<string> names, int userId)
    {
        List<int> wanted = new List<int>();
        foreach (string name in names)
        {
            int tagId = EnsureTag(name, userId);
            if (tagId > 0 && !wanted.Contains(tagId)) wanted.Add(tagId);
        }

        List<int> current = new List<int>();
        foreach (DataRow row in Db.Query("SELECT TagId FROM MovieTags WHERE MovieId = ?", movieId).Rows)
            current.Add(Db.Int(row, "TagId"));

        foreach (int tagId in wanted)
            if (!current.Contains(tagId)) Link(movieId, tagId);

        foreach (int tagId in current)
            if (!wanted.Contains(tagId))
                Db.Execute("DELETE FROM MovieTags WHERE MovieId = ? AND TagId = ?", movieId, tagId);
    }

    private static void Link(int movieId, int tagId)
    {
        Db.Execute("INSERT INTO MovieTags (MovieId, TagId, AddedUtc) VALUES (?, ?, ?)",
                   movieId, tagId, DateTime.UtcNow);
    }

    /// <summary>
    /// Puts a newly added film into any category whose name matches one of the
    /// genres OMDb reported. Existing tags only - it never creates new ones,
    /// so the family's own shelf stays the family's.
    /// </summary>
    public static void ApplyGenreTags(int movieId, string omdbGenre)
    {
        if (String.IsNullOrEmpty(omdbGenre)) return;

        List<int> alreadyLinked = new List<int>();
        foreach (DataRow row in Db.Query("SELECT TagId FROM MovieTags WHERE MovieId = ?", movieId).Rows)
            alreadyLinked.Add(Db.Int(row, "TagId"));

        foreach (string piece in omdbGenre.Split(','))
        {
            string key = Tag.ToKey(piece);
            if (key.Length == 0) continue;

            foreach (string[] alias in GenreAliases)
                if (key == alias[0]) { key = alias[1]; break; }

            Tag tag = GetByKey(key);
            if (tag == null || alreadyLinked.Contains(tag.TagId)) continue;

            Link(movieId, tag.TagId);
            alreadyLinked.Add(tag.TagId);
        }
    }

    /// <summary>Removes a category entirely, and takes it off every film.</summary>
    public static void Delete(int tagId)
    {
        Db.Execute("DELETE FROM MovieTags WHERE TagId = ?", tagId);
        Db.Execute("DELETE FROM Tags WHERE TagId = ?", tagId);
    }

    public static void Rename(int tagId, string name)
    {
        string clean = Clean(name);
        if (clean.Length == 0) return;

        string key = Tag.ToKey(clean);

        // Refuse to collide with a different tag that already owns the name.
        object clash = Db.Scalar("SELECT TagId FROM Tags WHERE TagKey = ?", key);
        if (clash != null && Convert.ToInt32(clash) != tagId) return;

        Db.Execute("UPDATE Tags SET TagName = ?, TagKey = ? WHERE TagId = ?", clean, key, tagId);
    }

    /// <summary>Adds the starting categories, once, if the table is empty.</summary>
    public static void SeedDefaults()
    {
        object count;
        try { count = Db.Scalar("SELECT COUNT(*) FROM Tags"); }
        catch { return; }        // the table is not there yet

        if (Convert.ToInt32(count ?? 0) > 0) return;

        foreach (string name in DefaultTags) EnsureTag(name, 0);
    }

    // ----- names -----------------------------------------------------------

    /// <summary>Tidies a typed-in category name: collapses spaces, caps the length.</summary>
    public static string Clean(string name)
    {
        if (name == null) return "";

        string trimmed = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
        trimmed = trimmed.Trim(',', ';', '|');
        if (trimmed.Length > MaxNameLength) trimmed = trimmed.Substring(0, MaxNameLength).Trim();
        return trimmed;
    }

    /// <summary>
    /// Splits what somebody typed into a "new categories" box. Commas are the
    /// separator, but people use semicolons too.
    /// </summary>
    public static List<string> SplitNames(string typed)
    {
        List<string> names = new List<string>();
        if (String.IsNullOrEmpty(typed)) return names;

        foreach (string piece in typed.Split(',', ';', '\n', '\r'))
        {
            string clean = Clean(piece);
            if (clean.Length == 0) continue;

            bool seen = false;
            foreach (string existing in names)
                if (String.Equals(existing, clean, StringComparison.OrdinalIgnoreCase)) seen = true;

            if (!seen) names.Add(clean);
        }

        return names;
    }
}
