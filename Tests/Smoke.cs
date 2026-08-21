using System;
using System.Collections.Generic;

public static class Smoke
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    public static int Main()
    {
        // --- password hashing -------------------------------------------------
        string hash, salt;
        PasswordHasher.CreateHash("correct horse battery", out hash, out salt);
        Check(PasswordHasher.Verify("correct horse battery", hash, salt), "right password verifies");
        Check(!PasswordHasher.Verify("wrong", hash, salt), "wrong password rejected");
        Check(!PasswordHasher.Verify("correct horse battery", hash, "bWFuZ2xlZA=="), "wrong salt rejected");
        Check(!PasswordHasher.Verify("x", "not base64!!", salt), "malformed hash rejected, no throw");

        string hash2, salt2;
        PasswordHasher.CreateHash("correct horse battery", out hash2, out salt2);
        Check(hash != hash2 && salt != salt2, "same password hashes differently (salted)");

        // --- star display -----------------------------------------------------
        string stars = Ui.StarDisplay(7.4, 5);
        Check(CountOccurrences(stars, "class=\"on\"") == 7, "7.4 lights 7 stars");
        Check(CountOccurrences(stars, "class=\"off\"") == 3, "7.4 leaves 3 dark");
        Check(Ui.StarDisplay(0, 0).Contains("Not rated yet"), "unrated says so");
        Check(CountOccurrences(Ui.StarDisplay(9.5, 2), "class=\"on\"") == 10, "9.5 rounds up to 10");

        // --- star picker ------------------------------------------------------
        string input = Ui.StarInput("42", 6);
        Check(CountOccurrences(input, "<input type=\"radio\"") == 10, "picker has 10 scores");
        Check(input.Contains("id=\"s42-6\" value=\"6\" checked=\"checked\""), "current score preselected");
        Check(CountOccurrences(input, "checked=\"checked\"") == 1, "exactly one preselected");
        Check(Ui.StarInput("42", 0).IndexOf("checked") < 0, "unrated picker has nothing selected");

        // Every score is a pressable label showing its number, in reading order,
        // each tied to its own hidden radio so it still posts without JavaScript.
        Check(CountOccurrences(input, "<label for=\"s42-") == 10, "every score has its own label");
        Check(input.Contains(">1</label>") && input.Contains(">10</label>"), "labels show the number");
        Check(input.IndexOf("value=\"1\"") < input.IndexOf("value=\"10\""), "rendered 1 to 10 in reading order");
        Check(input.IndexOf("type=\"radio\"") < input.IndexOf("<label"), "each radio precedes its label, for input:checked + label");
        Check(input.Contains("name=\"stars\""), "still posts as the stars field");

        // --- averages ---------------------------------------------------------
        Movie m = new Movie();
        m.RatingCount = 0;
        Check(m.FamilyAverageText == "-", "no ratings shows a dash");
        m.RatingCount = 3; m.FamilyAverage = (8 + 9 + 4) / 3.0;
        Check(m.FamilyAverageText == "7.0", "8, 9 and 4 average to 7.0");
        m.MyStars = 0;
        Check(!m.RatedByMe, "zero stars means not rated by me");
        m.MyStars = 1;
        Check(m.RatedByMe, "one star counts as rated");

        // --- posters ----------------------------------------------------------
        m.PosterUrl = "";
        Check(!m.HasPoster, "empty poster url");
        m.PosterUrl = "N/A";
        Check(!m.HasPoster, "OMDb 'N/A' is not a poster");
        m.PosterUrl = "https://example.com/p.jpg";
        Check(m.HasPoster, "real poster url");

        // --- text helpers -----------------------------------------------------
        Check(Ui.Shorten("short", 260) == "short", "short text untouched");
        string longText = new String('a', 40) + " " + new String('b', 40);
        Check(Ui.Shorten(longText, 50).EndsWith("..."), "long text is cut with an ellipsis");
        Check(Ui.Shorten(longText, 50).Length <= 54, "cut respects the limit");

        // --- Access parameter types -------------------------------------------
        // Inferred types are what produced "Data type mismatch in criteria
        // expression" against YESNO and DATETIME columns, so pin them down.
        Check(Type("b", true) == System.Data.OleDb.OleDbType.Boolean, "bool binds as Boolean, for YESNO");
        Check(Type("b", false) == System.Data.OleDb.OleDbType.Boolean, "false binds as Boolean too");
        Check(Type("d", DateTime.UtcNow) == System.Data.OleDb.OleDbType.Date, "DateTime binds as Date, not DBTimeStamp");
        Check(Type("i", 42) == System.Data.OleDb.OleDbType.Integer, "int binds as Integer, for LONG");
        Check(Type("s", "hello") == System.Data.OleDb.OleDbType.VarWChar, "short string binds as VarWChar, for TEXT");
        Check(Type("s", new String('x', 300)) == System.Data.OleDb.OleDbType.LongVarWChar, "long string binds as LongVarWChar, for MEMO");
        Check(Type("n", null) == System.Data.OleDb.OleDbType.Variant, "null binds untyped");
        Check(Type("v", DBNull.Value) == System.Data.OleDb.OleDbType.Variant, "DBNull binds untyped");
        Check(Type("f", 7.5) == System.Data.OleDb.OleDbType.Double, "double binds as Double");

        // --- optional text becomes NULL, never "" -----------------------------
        Check(Db.Text("", 50) == DBNull.Value, "empty optional text is NULL");
        Check(Db.Text("   ", 50) == DBNull.Value, "whitespace-only optional text is NULL");
        Check(Db.Text(null, 50) == DBNull.Value, "missing optional text is NULL");
        Check(Convert.ToString(Db.Text(" Casablanca ", 50)) == "Casablanca", "optional text is trimmed");

        // --- assembling movies from flat rows ---------------------------------
        // The family average is never stored: it is recomputed from the
        // individual ratings every time, right here.
        System.Data.DataTable movies = MovieTable();
        AddMovie(movies, 1, "Casablanca", "1942", 2, "Bogart, Bergman", "Curtiz", "Drama",
                 new DateTime(2026, 1, 1));
        AddMovie(movies, 2, "The Princess Bride", "1987", 3, "Elwes, Wright", "Reiner", "Comedy",
                 new DateTime(2026, 3, 1));
        AddMovie(movies, 3, "Arrival", "2016", 2, "Adams, Renner", "Villeneuve", "Sci-Fi",
                 new DateTime(2026, 2, 1));

        System.Data.DataTable ratings = RatingTable();
        AddRating(ratings, 1, 1, 8);   // three members rate Casablanca
        AddRating(ratings, 1, 2, 9);
        AddRating(ratings, 1, 3, 4);
        AddRating(ratings, 2, 1, 6);   // one member rates The Princess Bride
        // Arrival is unrated

        System.Data.DataTable users = UserTable();
        AddUser(users, 1, "Sanjay");
        AddUser(users, 2, "Mum");
        AddUser(users, 3, "Dad");

        // Viewer is user 3, who rated Casablanca a 4 and nothing else.
        List<Movie> all = MovieRepository.Assemble(movies, ratings, users, 3);
        Movie casablanca = Find(all, 1), bride = Find(all, 2), arrival = Find(all, 3);

        Check(all.Count == 3, "every movie comes back");
        Check(casablanca.RatingCount == 3, "Casablanca has three family ratings");
        Check(casablanca.FamilyAverageText == "7.0", "8, 9 and 4 average to 7.0");
        Check(casablanca.MyStars == 4, "the viewer's own score is picked out");
        Check(casablanca.AddedByName == "Mum", "the member who added it is named");
        Check(bride.MyStars == 0, "a movie the viewer has not rated shows no score of their own");
        Check(bride.FamilyAverageText == "6.0", "a single rating is its own average");
        Check(arrival.RatingCount == 0 && arrival.FamilyAverageText == "-", "unrated stays unrated");
        Check(!arrival.RatedByMe && casablanca.RatedByMe, "rated-by-me tracks the viewer only");

        // Another viewer sees the same family averages but their own score.
        List<Movie> asMum = MovieRepository.Assemble(movies, ratings, users, 2);
        Check(Find(asMum, 1).FamilyAverageText == "7.0", "the family average does not depend on who is looking");
        Check(Find(asMum, 1).MyStars == 9, "each viewer sees their own score");

        // --- ordering ---------------------------------------------------------
        List<Movie> byRating = MovieRepository.SortBy(MovieRepository.Assemble(movies, ratings, users, 3), "rating");
        Check(byRating[0].MovieId == 1 && byRating[1].MovieId == 2, "best family average first");
        Check(byRating[2].MovieId == 3, "unrated films sink to the bottom");

        List<Movie> byNewest = MovieRepository.SortBy(MovieRepository.Assemble(movies, ratings, users, 3), "recent");
        Check(byNewest[0].MovieId == 2 && byNewest[2].MovieId == 1, "newest addition first");

        List<Movie> byTitle = MovieRepository.SortBy(MovieRepository.Assemble(movies, ratings, users, 3), "title");
        Check(byTitle[0].Title == "Arrival" && byTitle[2].Title == "The Princess Bride", "alphabetical by title");

        List<Movie> byMine = MovieRepository.SortBy(MovieRepository.Assemble(movies, ratings, users, 2), "mine");
        Check(byMine[0].MovieId == 1, "my own highest score first");

        // --- searching --------------------------------------------------------
        List<Movie> pool = MovieRepository.Assemble(movies, ratings, users, 3);
        Check(MovieRepository.Match(pool, "princess").Count == 1, "title match ignores case");
        Check(MovieRepository.Match(pool, "bergman").Count == 1, "cast is searchable");
        Check(MovieRepository.Match(pool, "villeneuve").Count == 1, "director is searchable");
        Check(MovieRepository.Match(pool, "Comedy").Count == 1, "genre is searchable");
        Check(MovieRepository.Match(pool, "1942").Count == 1, "year is searchable");
        Check(MovieRepository.Match(pool, "zzzz").Count == 0, "no match returns nothing");
        Check(MovieRepository.Match(pool, "").Count == 3, "an empty search returns everything");
        Check(MovieRepository.Match(pool, null).Count == 3, "a missing search returns everything");

        // --- empty database, which is what a fresh install looks like ---------
        List<Movie> nothing = MovieRepository.Assemble(MovieTable(), RatingTable(), UserTable(), 1);
        Check(nothing.Count == 0, "an empty database assembles to an empty list, not a crash");
        Check(MovieRepository.SortBy(nothing, "rating").Count == 0, "sorting nothing is fine");

        // --- the watched category ---------------------------------------------
        // Marking a film watched retires it from the default library view but
        // keeps it, and its ratings, entirely intact.
        MarkWatched(movies, 1);
        List<Movie> withWatched = MovieRepository.Assemble(movies, ratings, users, 3);

        Check(Find(withWatched, 1).IsWatched, "the watched flag is read back");
        Check(!Find(withWatched, 2).IsWatched, "other films are untouched");
        Check(Find(withWatched, 1).FamilyAverageText == "7.0", "a watched film keeps its family rating");
        Check(Find(withWatched, 1).MyStars == 4, "and keeps the viewer's own score");

        List<Movie> toWatch = MovieRepository.OnlyWatched(
            MovieRepository.Assemble(movies, ratings, users, 3), MovieRepository.ShowToWatch);
        Check(toWatch.Count == 2, "the default view drops watched films");
        foreach (Movie unwatched in toWatch) Check(!unwatched.IsWatched, "and shows only unwatched ones");

        List<Movie> watchedOnly = MovieRepository.OnlyWatched(
            MovieRepository.Assemble(movies, ratings, users, 3), MovieRepository.ShowWatched);
        Check(watchedOnly.Count == 1 && watchedOnly[0].MovieId == 1, "the watched view shows just those");

        Check(MovieRepository.OnlyWatched(
                  MovieRepository.Assemble(movies, ratings, users, 3),
                  MovieRepository.ShowAll).Count == 3, "the everything view shows both");

        Check(MovieRepository.CleanShow(null) == MovieRepository.ShowToWatch, "no filter means still-to-watch");
        Check(MovieRepository.CleanShow("nonsense") == MovieRepository.ShowToWatch, "a bad filter falls back safely");
        Check(MovieRepository.CleanShow("WATCHED") == MovieRepository.ShowWatched, "the filter ignores case");

        // Who may retire a film: an administrator, or whoever added it.
        Movie someonesFilm = Find(withWatched, 1);          // added by user 2
        Check(MovieActions.CanManage(someonesFilm, Member(2)), "the member who added it may");
        Check(!MovieActions.CanManage(someonesFilm, Member(3)), "another member may not");
        Check(MovieActions.CanManage(someonesFilm, Admin(9)), "an administrator may");
        Check(!MovieActions.CanManage(someonesFilm, null), "a signed-out visitor may not");

        MarkWatched(movies, 1);   // leave the fixture as the later tests expect

        // --- categories -------------------------------------------------------
        System.Data.DataTable tagRows = TagTable();
        AddTag(tagRows, 10, "Comedy");
        AddTag(tagRows, 11, "Christmas");
        AddTag(tagRows, 12, "Real Life Story");

        System.Data.DataTable links = LinkTable();
        AddLink(links, 2, 10);   // The Princess Bride -> Comedy
        AddLink(links, 2, 11);   // and Christmas
        AddLink(links, 1, 12);   // Casablanca -> Real Life Story
        AddLink(links, 3, 99);   // a link to a category that no longer exists

        List<Movie> tagged = MovieRepository.AttachTags(
            MovieRepository.Assemble(movies, ratings, users, 3), links, tagRows);

        Check(Find(tagged, 2).Tags.Count == 2, "a film carries every category it is in");
        Check(Find(tagged, 2).Tags[0].TagName == "Christmas", "categories come back in name order");
        Check(Find(tagged, 1).Tags.Count == 1, "another film carries its own");
        Check(Find(tagged, 3).Tags.Count == 0, "a link to a deleted category is ignored");

        Check(Find(tagged, 2).HasTag("christmas"), "matching a category ignores case");
        Check(!Find(tagged, 2).HasTag("sad"), "a category it is not in does not match");
        Check(!Find(tagged, 2).HasTag(""), "an empty category name matches nothing");

        // Filtering the library down to one shelf.
        Check(MovieRepository.WithTag(tagged, "comedy").Count == 1, "filtering by category");
        Check(MovieRepository.WithTag(tagged, "Christmas")[0].MovieId == 2, "filtering ignores case");
        Check(MovieRepository.WithTag(tagged, "nonexistent").Count == 0, "an unused category shows nothing");
        Check(MovieRepository.WithTag(tagged, "").Count == 3, "no category filter shows everything");

        // Categories are searchable from the ordinary search box.
        Check(MovieRepository.Match(tagged, "christmas").Count == 1, "the search box finds a category");
        Check(MovieRepository.Match(tagged, "real life").Count == 1, "and matches part of one");

        // --- category names ---------------------------------------------------
        Check(Tag.ToKey("  Christmas  ") == "christmas", "the key is trimmed and lower-cased");
        Check(Tag.ToKey(null) == "", "a missing name has an empty key");
        Check(TagRepository.Clean("  Real   Life  Story ") == "Real Life Story", "runs of spaces collapse");
        Check(TagRepository.Clean(",Comedy,") == "Comedy", "stray separators are stripped");
        Check(TagRepository.Clean(new String('x', 80)).Length == 50, "an over-long name is cut to fit the column");

        List<string> split = TagRepository.SplitNames("Comedy, Christmas ; Comedy,,  Sad ");
        Check(split.Count == 3, "several categories can be typed at once");
        Check(split[0] == "Comedy" && split[2] == "Sad", "in the order they were typed");
        Check(TagRepository.SplitNames("").Count == 0, "an empty box adds nothing");
        Check(TagRepository.SplitNames(null).Count == 0, "a missing box adds nothing");
        Check(TagRepository.SplitNames(" , ; ,").Count == 0, "separators alone add nothing");

        // The starting shelf is what was asked for.
        Check(Has(TagRepository.DefaultTags, "Comedy") && Has(TagRepository.DefaultTags, "Sad") &&
              Has(TagRepository.DefaultTags, "Romantic") && Has(TagRepository.DefaultTags, "Family") &&
              Has(TagRepository.DefaultTags, "Christmas") && Has(TagRepository.DefaultTags, "SciFi") &&
              Has(TagRepository.DefaultTags, "Historical") &&
              Has(TagRepository.DefaultTags, "Real Life Story"),
              "every requested category ships as a default");

        // --- OMDb responses ---------------------------------------------------
        // A real search payload. JavaScriptSerializer decodes the nested array
        // as an ArrayList, not object[], and casting to object[] is what made
        // the Add a Movie search silently show "no results".
        string searchJson =
            "{\"Search\":[" +
            "{\"Title\":\"Casablanca\",\"Year\":\"1942\",\"imdbID\":\"tt0034583\",\"Type\":\"movie\"," +
            "\"Poster\":\"https://m.media-amazon.com/images/x.jpg\"}," +
            "{\"Title\":\"Casablanca Express\",\"Year\":\"1989\",\"imdbID\":\"tt0097129\"," +
            "\"Type\":\"movie\",\"Poster\":\"N/A\"}" +
            "],\"totalResults\":\"2\",\"Response\":\"True\"}";

        string omdbError;
        List<MovieSearchResult> hits = OmdbClient.ReadSearchResults(Decode(searchJson), out omdbError);

        Check(hits.Count == 2, "both search results are read");
        Check(omdbError == null, "a good search reports no error");
        Check(hits[0].Title == "Casablanca", "result title");
        Check(hits[0].Year == "1942", "result year");
        Check(hits[0].ImdbId == "tt0034583", "result imdb id, which is what Add posts back");
        Check(hits[0].HasPoster, "a real poster url is kept");
        Check(!hits[1].HasPoster, "OMDb's \"N/A\" poster is not treated as one");

        // OMDb's own "nothing found" shape.
        List<MovieSearchResult> none = OmdbClient.ReadSearchResults(
            Decode("{\"Response\":\"False\",\"Error\":\"Movie not found!\"}"), out omdbError);
        Check(none.Count == 0, "a failed search returns nothing");
        Check(omdbError == "Movie not found!", "and passes OMDb's own wording through");

        // Success with an unreadable body must never look like "no matches".
        OmdbClient.ReadSearchResults(Decode("{\"Response\":\"True\"}"), out omdbError);
        Check(omdbError != null, "a success with no readable list reports a problem, not silence");

        OmdbClient.ReadSearchResults(null, out omdbError);
        Check(omdbError != null, "a missing response reports a problem");

        // Full details, the shape the "add by hand" path already handled.
        string detailJson =
            "{\"Title\":\"The Princess Bride\",\"Year\":\"1987\",\"Rated\":\"PG\"," +
            "\"Runtime\":\"98 min\",\"Genre\":\"Adventure, Family, Fantasy\"," +
            "\"Director\":\"Rob Reiner\",\"Actors\":\"Cary Elwes, Mandy Patinkin\"," +
            "\"Plot\":\"A bedridden boy's grandfather reads him a story.\"," +
            "\"Poster\":\"https://m.media-amazon.com/images/y.jpg\",\"imdbRating\":\"8.0\"," +
            "\"imdbID\":\"tt0093779\",\"Response\":\"True\"}";

        string detailError = null;
        Movie detail = OmdbClient.ToMovie(Decode(detailJson), ref detailError);
        Check(detail != null, "details are read");
        Check(detail.Title == "The Princess Bride", "detail title");
        Check(detail.Director == "Rob Reiner", "detail director");
        Check(detail.Actors == "Cary Elwes, Mandy Patinkin", "detail cast");
        Check(detail.MpaaRating == "PG", "detail certificate");
        Check(detail.ImdbScore == "8.0", "detail imdb score");
        Check(detail.Plot.Length > 0 && detail.HasPoster, "detail plot and poster");

        string missingError = null;
        Check(OmdbClient.ToMovie(Decode("{\"Response\":\"False\",\"Error\":\"Incorrect IMDb ID.\"}"),
                                 ref missingError) == null, "a failed lookup returns no movie");
        Check(missingError == "Incorrect IMDb ID.", "and explains why");

        // --- escaping ---------------------------------------------------------
        Movie evil = new Movie();
        evil.MovieId = 1;
        evil.Title = "<script>alert('x')</script>";
        evil.AddedByName = "Mum";
        string card = Ui.MovieCard(evil, false, null);
        Check(card.IndexOf("<script>") < 0, "title markup is escaped in a card");
        Check(card.Contains("&lt;script&gt;"), "title is shown, encoded");

        Console.WriteLine(failures == 0 ? "\nAll checks passed." : "\n" + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }

    static System.Data.DataTable TagTable()
    {
        System.Data.DataTable t = new System.Data.DataTable();
        t.Columns.Add("TagId", typeof(object));
        t.Columns.Add("TagName", typeof(object));
        t.Columns.Add("TagKey", typeof(object));
        return t;
    }

    static void AddTag(System.Data.DataTable t, int id, string name)
    {
        t.Rows.Add(new object[] { id, name, name.ToLowerInvariant() });
    }

    static System.Data.DataTable LinkTable()
    {
        System.Data.DataTable t = new System.Data.DataTable();
        t.Columns.Add("MovieId", typeof(object));
        t.Columns.Add("TagId", typeof(object));
        return t;
    }

    static void AddLink(System.Data.DataTable t, int movieId, int tagId)
    {
        t.Rows.Add(new object[] { movieId, tagId });
    }

    static bool Has(string[] names, string wanted)
    {
        foreach (string name in names)
            if (String.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static FamilyUser Member(int id)
    {
        FamilyUser u = new FamilyUser();
        u.UserId = id;
        return u;
    }

    static FamilyUser Admin(int id)
    {
        FamilyUser u = Member(id);
        u.IsAdmin = true;
        return u;
    }

    /// Decodes JSON exactly the way OmdbClient does, so the tests exercise the
    /// real runtime types (ArrayList for arrays) rather than idealised ones.
    static object Decode(string json)
    {
        return new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json);
    }

    // ----- building the flat result sets the repository assembles from -----

    static System.Data.DataTable MovieTable()
    {
        System.Data.DataTable t = new System.Data.DataTable();
        string[] columns = { "MovieId", "Title", "ReleaseYear", "ImdbId", "PosterUrl", "Plot",
                             "Actors", "Director", "Genre", "Runtime", "MpaaRating", "ImdbScore",
                             "AddedByUserId", "AddedUtc", "IsWatched", "WatchedUtc" };
        foreach (string c in columns) t.Columns.Add(c, typeof(object));
        return t;
    }

    static void AddMovie(System.Data.DataTable t, int id, string title, string year,
                         int addedBy, string actors, string director, string genre, DateTime added)
    {
        System.Data.DataRow row = t.NewRow();
        row["MovieId"] = id;
        row["Title"] = title;
        row["ReleaseYear"] = year;
        row["Actors"] = actors;
        row["Director"] = director;
        row["Genre"] = genre;
        row["AddedByUserId"] = addedBy;
        row["AddedUtc"] = added;
        row["IsWatched"] = false;
        t.Rows.Add(row);
    }

    static System.Data.DataTable RatingTable()
    {
        System.Data.DataTable t = new System.Data.DataTable();
        t.Columns.Add("MovieId", typeof(object));
        t.Columns.Add("UserId", typeof(object));
        t.Columns.Add("Stars", typeof(object));
        return t;
    }

    static void AddRating(System.Data.DataTable t, int movieId, int userId, int stars)
    {
        t.Rows.Add(new object[] { movieId, userId, stars });
    }

    static System.Data.DataTable UserTable()
    {
        System.Data.DataTable t = new System.Data.DataTable();
        t.Columns.Add("UserId", typeof(object));
        t.Columns.Add("DisplayName", typeof(object));
        return t;
    }

    static void AddUser(System.Data.DataTable t, int id, string name)
    {
        t.Rows.Add(new object[] { id, name });
    }

    static void MarkWatched(System.Data.DataTable t, int movieId)
    {
        foreach (System.Data.DataRow row in t.Rows)
            if (Convert.ToInt32(row["MovieId"]) == movieId)
            {
                row["IsWatched"] = true;
                row["WatchedUtc"] = DateTime.UtcNow;
            }
    }

    static Movie Find(List<Movie> movies, int movieId)
    {
        foreach (Movie m in movies) if (m.MovieId == movieId) return m;
        throw new Exception("movie " + movieId + " missing from the assembled list");
    }

    // Db.TypeFor rather than Db.MakeParameter: mono has no real OleDb, so an
    // OleDbParameter cannot be constructed here. The mapping is the part worth
    // testing anyway.
    static System.Data.OleDb.OleDbType Type(string name, object value)
    {
        return Db.TypeFor(value);
    }

    static int CountOccurrences(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
