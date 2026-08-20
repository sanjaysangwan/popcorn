using System;

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
        Check(CountOccurrences(input, "<input type=\"radio\"") == 10, "picker has 10 radios");
        Check(input.Contains("id=\"s42-6\" value=\"6\" checked=\"checked\""), "current score preselected");
        Check(CountOccurrences(input, "checked=\"checked\"") == 1, "exactly one preselected");
        Check(Ui.StarInput("42", 0).IndexOf("checked") < 0, "unrated picker has nothing selected");
        Check(input.IndexOf("value=\"10\"") < input.IndexOf("value=\"1\"" ), "rendered 10 down to 1 for the CSS");

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

    static int CountOccurrences(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
