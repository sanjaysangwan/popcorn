using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>
/// Creates the Microsoft Access database file and its tables.
///
/// The .mdb/.accdb file is created through ADOX (the "Microsoft ADO Ext."
/// COM library that ships with Windows) using late binding, so the project
/// needs no interop assembly and no build step. If ADOX is unavailable on the
/// host, upload an empty database by hand and re-run Setup.aspx - the table
/// creation step works over plain OleDb.
/// </summary>
public static class DatabaseInstaller
{
    /// <summary>DDL is kept here so Setup.aspx and App_Data/Schema.sql agree.</summary>
    public static readonly string[] Ddl = new string[]
    {
        @"CREATE TABLE Users (
            UserId          AUTOINCREMENT PRIMARY KEY,
            Email           TEXT(150) NOT NULL,
            DisplayName     TEXT(80)  NOT NULL,
            PasswordHash    TEXT(120) NOT NULL,
            PasswordSalt    TEXT(60)  NOT NULL,
            IsApproved      YESNO     NOT NULL,
            IsAdmin         YESNO     NOT NULL,
            IsDisabled      YESNO     NOT NULL,
            CreatedUtc      DATETIME  NOT NULL,
            ApprovedUtc     DATETIME,
            LastLoginUtc    DATETIME
          )",

        @"CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)",

        @"CREATE TABLE Movies (
            MovieId         AUTOINCREMENT PRIMARY KEY,
            Title           TEXT(200) NOT NULL,
            ReleaseYear     TEXT(12),
            ImdbId          TEXT(20),
            PosterUrl       TEXT(255),
            Plot            MEMO,
            Actors          TEXT(255),
            Director        TEXT(255),
            Genre           TEXT(150),
            Runtime         TEXT(40),
            MpaaRating      TEXT(20),
            ImdbScore       TEXT(10),
            AddedByUserId   LONG      NOT NULL,
            AddedUtc        DATETIME  NOT NULL
          )",

        @"CREATE INDEX IX_Movies_Added ON Movies (AddedUtc)",

        @"CREATE TABLE Ratings (
            RatingId        AUTOINCREMENT PRIMARY KEY,
            MovieId         LONG      NOT NULL,
            UserId          LONG      NOT NULL,
            Stars           LONG      NOT NULL,
            Review          MEMO,
            CreatedUtc      DATETIME  NOT NULL,
            UpdatedUtc      DATETIME  NOT NULL
          )",

        // One rating per family member per movie.
        @"CREATE UNIQUE INDEX IX_Ratings_MovieUser ON Ratings (MovieId, UserId)"
    };

    /// <summary>Full path of the .mdb/.accdb named by the connection string.</summary>
    public static string DatabaseFilePath()
    {
        Match m = Regex.Match(Db.ConnectionString, @"Data\s*Source\s*=\s*([^;]+)",
                              RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    public static bool DatabaseFileExists()
    {
        string path = DatabaseFilePath();
        return !String.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>
    /// Creates the physical database file via ADOX. Returns null on success or
    /// a human-readable explanation when the host does not expose ADOX.
    /// </summary>
    public static string CreateDatabaseFile()
    {
        string path = DatabaseFilePath();
        if (String.IsNullOrEmpty(path))
            return "Could not work out the database file name from the connection string.";
        if (File.Exists(path))
            return null;

        string folder = Path.GetDirectoryName(path);
        if (!String.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        Type catalogType = Type.GetTypeFromProgID("ADOX.Catalog");
        if (catalogType == null)
            return "ADOX (Microsoft ADO Ext.) is not registered on this server, so the " +
                   "database file cannot be created automatically. Create an empty Access " +
                   "database on your PC, name it '" + Path.GetFileName(path) + "', upload it " +
                   "to App_Data and run this page again.";

        object catalog = null;
        try
        {
            catalog = Activator.CreateInstance(catalogType);
            catalogType.InvokeMember("Create", BindingFlags.InvokeMethod, null, catalog,
                                     new object[] { Db.ConnectionString });
            return null;
        }
        catch (Exception ex)
        {
            Exception root = ex;
            while (root.InnerException != null) root = root.InnerException;
            return "Creating the database file failed: " + root.Message;
        }
        finally
        {
            if (catalog != null && catalog.GetType().IsCOMObject)
            {
                try { System.Runtime.InteropServices.Marshal.ReleaseComObject(catalog); }
                catch { }
            }
        }
    }

    /// <summary>
    /// Runs the DDL, skipping anything that already exists. Returns the log of
    /// what happened so Setup.aspx can show it.
    /// </summary>
    public static List<string> CreateTables()
    {
        List<string> log = new List<string>();

        using (OleDbConnection cn = Db.Open())
        {
            foreach (string statement in Ddl)
            {
                string label = Describe(statement);
                try
                {
                    using (OleDbCommand cmd = new OleDbCommand(statement, cn))
                        cmd.ExecuteNonQuery();
                    log.Add("Created " + label + ".");
                }
                catch (OleDbException ex)
                {
                    // -1303 / "already exists" simply means we have run before.
                    if (ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                        log.Add(label + " already exists - skipped.");
                    else
                        log.Add("FAILED on " + label + ": " + ex.Message);
                }
            }
        }
        return log;
    }

    private static string Describe(string statement)
    {
        Match m = Regex.Match(statement, @"CREATE\s+(UNIQUE\s+)?(TABLE|INDEX)\s+(\w+)",
                              RegexOptions.IgnoreCase);
        if (!m.Success) return "statement";
        return m.Groups[2].Value.ToLowerInvariant() + " " + m.Groups[3].Value;
    }
}
