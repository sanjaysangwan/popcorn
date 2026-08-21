using System;
using System.Collections.Generic;
using System.Data;
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
            AddedUtc        DATETIME  NOT NULL,
            IsWatched       YESNO     NOT NULL,
            WatchedUtc      DATETIME
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
        @"CREATE UNIQUE INDEX IX_Ratings_MovieUser ON Ratings (MovieId, UserId)",

        @"CREATE TABLE Tags (
            TagId           AUTOINCREMENT PRIMARY KEY,
            TagName         TEXT(50) NOT NULL,
            TagKey          TEXT(50) NOT NULL,
            CreatedByUserId LONG,
            CreatedUtc      DATETIME NOT NULL
          )",

        // TagKey is the lower-cased name, so "Christmas" and "christmas" are
        // recognised as the same category however somebody types it.
        @"CREATE UNIQUE INDEX IX_Tags_Key ON Tags (TagKey)",

        @"CREATE TABLE MovieTags (
            MovieTagId      AUTOINCREMENT PRIMARY KEY,
            MovieId         LONG     NOT NULL,
            TagId           LONG     NOT NULL,
            AddedUtc        DATETIME NOT NULL
          )",

        @"CREATE UNIQUE INDEX IX_MovieTags ON MovieTags (MovieId, TagId)"
    };

    /// <summary>
    /// Tables added after the first release, for databases that predate them.
    /// Each entry is the table name and the statements that build it.
    /// </summary>
    private static readonly string[][] TablesAddedLater = new string[][]
    {
        new string[] { "Tags", "Tags", "IX_Tags_Key" },
        new string[] { "MovieTags", "MovieTags", "IX_MovieTags" }
    };

    /// <summary>
    /// Columns added to Movies after the first release. A database created
    /// before they existed picks them up automatically the first time it runs
    /// the newer code, so nobody has to remember to re-run Setup.
    /// </summary>
    private static readonly string[][] MovieColumnsAddedLater = new string[][]
    {
        new string[] { "IsWatched",  "ALTER TABLE Movies ADD COLUMN IsWatched YESNO" },
        new string[] { "WatchedUtc", "ALTER TABLE Movies ADD COLUMN WatchedUtc DATETIME" }
    };

    /// <summary>
    /// Brings an existing database up to the current schema. Safe to call
    /// repeatedly - it only adds what is missing. Returns what it did.
    /// </summary>
    public static List<string> ApplyMigrations()
    {
        List<string> log = new List<string>();

        using (OleDbConnection cn = Db.Open())
        {
            // Tables first: a database created before tags existed has none.
            List<string> tables = new List<string>();
            DataTable tableSchema = cn.GetSchema("Tables");
            foreach (DataRow row in tableSchema.Rows)
                tables.Add(Convert.ToString(row["TABLE_NAME"]).ToLowerInvariant());

            foreach (string[] table in TablesAddedLater)
            {
                if (tables.Contains(table[0].ToLowerInvariant())) continue;

                foreach (string statement in Ddl)
                {
                    if (!MentionsTable(statement, table[0])) continue;

                    try
                    {
                        using (OleDbCommand cmd = new OleDbCommand(statement, cn))
                            cmd.ExecuteNonQuery();
                        log.Add("Created " + Describe(statement) + ".");
                    }
                    catch (OleDbException ex)
                    {
                        if (ex.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) < 0)
                            log.Add("Could not create " + Describe(statement) + ": " + ex.Message);
                    }
                }
            }

            List<string> existing = new List<string>();
            DataTable schema = cn.GetSchema("Columns", new string[] { null, null, "Movies", null });
            foreach (DataRow row in schema.Rows)
                existing.Add(Convert.ToString(row["COLUMN_NAME"]).ToLowerInvariant());

            foreach (string[] column in MovieColumnsAddedLater)
            {
                if (existing.Contains(column[0].ToLowerInvariant())) continue;

                try
                {
                    using (OleDbCommand cmd = new OleDbCommand(column[1], cn))
                        cmd.ExecuteNonQuery();
                    log.Add("Added the " + column[0] + " column to Movies.");
                }
                catch (OleDbException ex)
                {
                    log.Add("Could not add " + column[0] + " to Movies: " + ex.Message);
                }
            }
        }

        return log;
    }

    /// <summary>True when a CREATE statement builds this table or an index on it.</summary>
    private static bool MentionsTable(string statement, string tableName)
    {
        return Regex.IsMatch(statement,
            @"CREATE\s+TABLE\s+" + Regex.Escape(tableName) + @"\b", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(statement,
            @"CREATE\s+(UNIQUE\s+)?INDEX\s+\w+\s+ON\s+" + Regex.Escape(tableName) + @"\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>Full path of the .mdb/.accdb named by the connection string.</summary>
    public static string DatabaseFilePath()
    {
        Match m = Regex.Match(Db.ConnectionString, @"Data\s*Source\s*=\s*([^;]+)",
                              RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    /// <summary>The OLE DB provider named by the connection string.</summary>
    public static string ProviderName()
    {
        Match m = Regex.Match(Db.ConnectionString, @"Provider\s*=\s*([^;]+)",
                              RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    /// <summary>
    /// ADOX.Catalog.Create is far pickier than the runtime connection string:
    /// hand it anything beyond Provider and Data Source - "Persist Security
    /// Info" being the classic - and it answers "Multiple-step OLE DB operation
    /// generated errors", which says nothing about the real cause. So the
    /// creation string is rebuilt from just those two values.
    /// </summary>
    public static string CreationConnectionString()
    {
        return "Provider=" + ProviderName() + ";Data Source=" + DatabaseFilePath() + ";";
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
        {
            try { Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                return "The folder " + folder + " does not exist and could not be created: " +
                       ex.Message;
            }
        }

        // Checked up front, because a folder Jet cannot write to produces a
        // completely misleading error from ADOX rather than a permissions one.
        string folderError = ServerInfo.FolderWriteError(folder);
        if (folderError != null)
            return folderError + " Grant that account Modify permission on the folder - on " +
                   "DiscountASP.NET this is done from the Control Panel, not over FTP - then " +
                   "run this page again.";

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
                                     new object[] { CreationConnectionString() });
            return null;
        }
        catch (Exception ex)
        {
            return "Creating the database file failed: " + ServerInfo.Explain(ex) + " " + Advice();
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

    /// <summary>The three things that actually cause a failed Create, in order.</summary>
    private static string Advice()
    {
        string provider = ProviderName();
        List<string> hints = new List<string>();

        if (provider.StartsWith("Microsoft.Jet", StringComparison.OrdinalIgnoreCase)
            && ServerInfo.Is64BitProcess)
            hints.Add("this application pool is 64-bit and the Jet provider only exists in " +
                      "32-bit, so either switch the pool to 32-bit or move to the ACE provider " +
                      "and an .accdb file");

        if (!ServerInfo.HasProvider(provider))
            hints.Add("the provider '" + provider + "' is not among those registered on this " +
                      "server - the list further down this page shows what is");

        hints.Add("the account the site runs as (" + ServerInfo.ApplicationIdentity + ") may " +
                  "not have Modify permission on the folder");

        return "Most likely: " + String.Join("; or ", hints.ToArray()) + ".";
    }

    private static string Describe(string statement)
    {
        Match m = Regex.Match(statement, @"CREATE\s+(UNIQUE\s+)?(TABLE|INDEX)\s+(\w+)",
                              RegexOptions.IgnoreCase);
        if (!m.Success) return "statement";
        return m.Groups[2].Value.ToLowerInvariant() + " " + m.Groups[3].Value;
    }
}
