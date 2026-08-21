using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// One-off installer. Everything it does is guarded by the SetupKey value in
/// web.config, and it closes itself entirely once the site has accounts and
/// the key has been blanked out.
/// </summary>
public partial class SetupPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }
    protected override bool SkipInstallCheck { get { return true; } }

    public string Message { get; private set; }
    public string MessageKind { get; private set; }
    public List<string> Log = new List<string>();

    public bool Blocked { get; private set; }
    public string BlockedReason { get; private set; }

    public bool Installed { get; private set; }
    public bool FileExists { get; private set; }
    public bool HasUsers { get; private set; }
    public int UserCount { get; private set; }
    public string DatabaseFile { get; private set; }
    public string ProviderName { get; private set; }
    public string DataFolderState { get; private set; }
    public string DataFolder { get; private set; }
    public string Bitness { get; private set; }
    public string Identity { get; private set; }
    public bool AdoxAvailable { get; private set; }
    public bool ProviderInstalled { get; private set; }
    public List<string> Providers = new List<string>();

    public string OmdbResult { get; private set; }
    public string OmdbResultKind { get; private set; }

    public List<string> UserColumns = new List<string>();
    public List<string> MovieColumns = new List<string>();
    public List<string> RatingColumns = new List<string>();

    /// <summary>Jet has no 64-bit build, so a 64-bit pool can never load it.</summary>
    public bool ShowJetBitnessWarning
    {
        get
        {
            return ServerInfo.Is64BitProcess &&
                   (ProviderName ?? "").StartsWith("Microsoft.Jet", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string SetupKey
    {
        get { return (ConfigurationManager.AppSettings["SetupKey"] ?? "").Trim(); }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";
        Refresh();

        // Once the site is up and running, only an administrator may come back here.
        if (Installed && HasUsers && (CurrentUser == null || !CurrentUser.IsAdmin))
        {
            Blocked = true;
            BlockedReason = "This site is already installed. Sign in as an administrator if " +
                            "you need to run setup again.";
            return;
        }

        if (SetupKey.Length == 0)
        {
            Blocked = true;
            BlockedReason = "No SetupKey is configured. Put a value in the SetupKey app setting " +
                            "in web.config before running setup, and blank it out again afterwards.";
            return;
        }

        if (!String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return;

        if (!Csrf.IsValid(Request))
        {
            Message = "That form expired. Please try again.";
            return;
        }
        if (!String.Equals(Posted("key"), SetupKey, StringComparison.Ordinal))
        {
            Message = "That is not the setup key from web.config.";
            return;
        }

        switch (Posted("action"))
        {
            case "install": Install(); break;
            case "admin": CreateAdministrator(); break;
            case "omdb": TestOmdb(); break;
        }

        Refresh();
        phOmdb.Visible = !String.IsNullOrEmpty(OmdbResult);
    }

    private void Refresh()
    {
        DatabaseFile = DatabaseInstaller.DatabaseFilePath();
        DataFolder = String.IsNullOrEmpty(DatabaseFile) ? "" : Path.GetDirectoryName(DatabaseFile);
        FileExists = DatabaseInstaller.DatabaseFileExists();
        ProviderName = ReadProvider();

        string folderError = ServerInfo.FolderWriteError(DataFolder);
        DataFolderState = folderError ?? "writable";

        Bitness = ServerInfo.Bitness;
        Identity = ServerInfo.ApplicationIdentity;
        AdoxAvailable = ServerInfo.AdoxAvailable;
        Providers = ServerInfo.OleDbProviders();
        ProviderInstalled = ServerInfo.HasProvider(ProviderName);

        rptProviders.DataSource = Providers;
        rptProviders.DataBind();

        Installed = Db.IsInstalled();
        UserCount = 0;
        if (Installed)
        {
            try { UserCount = UserRepository.Count(); } catch { UserCount = 0; }

            UserColumns = Db.DescribeColumns("Users");
            MovieColumns = Db.DescribeColumns("Movies");
            RatingColumns = Db.DescribeColumns("Ratings");

            rptUserColumns.DataSource = UserColumns;
            rptUserColumns.DataBind();
            rptMovieColumns.DataSource = MovieColumns;
            rptMovieColumns.DataBind();
            rptRatingColumns.DataSource = RatingColumns;
            rptRatingColumns.DataBind();

            phSchema.Visible = true;
        }
        HasUsers = UserCount > 0;
    }

    private void Install()
    {
        if (!FileExists)
        {
            string error = DatabaseInstaller.CreateDatabaseFile();
            if (error != null)
            {
                Message = error;
                return;
            }
            Log.Add("Created the database file " + Path.GetFileName(DatabaseFile) + ".");
        }
        else
        {
            Log.Add("Database file already present - left alone.");
        }

        try
        {
            Log.AddRange(DatabaseInstaller.CreateTables());
        }
        catch (Exception ex)
        {
            Message = "The tables could not be created: " + ex.Message;
            return;
        }

        if (Db.IsInstalled())
        {
            Message = "The database is ready.";
            MessageKind = "success";
        }
        else
        {
            Message = "Some tables are still missing - see the log below.";
        }
    }

    private void CreateAdministrator()
    {
        if (!Db.IsInstalled())
        {
            Message = "Create the database first.";
            return;
        }
        if (UserRepository.Count() > 0)
        {
            Message = "This site already has accounts, so the administrator cannot be created here.";
            return;
        }

        string displayName = Posted("displayName");
        string email = Posted("email");
        string password = Request.Form["password"] ?? "";

        if (displayName.Length < 2) { Message = "Please give a name."; return; }
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$"))
        {
            Message = "That does not look like an email address.";
            return;
        }
        if (password.Length < 8) { Message = "Use a password of at least 8 characters."; return; }

        try
        {
            int userId = UserRepository.Create(email, displayName, password, true, true);
            FamilyUser admin = UserRepository.GetById(userId);
            AppSecurity.SignIn(admin, true);
            SetFlash("Setup finished. Remember to blank out the SetupKey in web.config.", "success");
            Go("~/Default.aspx");
        }
        catch (Exception ex)
        {
            Message = "The administrator account could not be created: " + ex.Message;
        }
    }

    /// <summary>
    /// A live lookup against OMDb. "Search does not work" has three quite
    /// different causes - no key, a rejected key, or a host that blocks
    /// outbound web requests - and only an actual call tells them apart.
    /// </summary>
    private void TestOmdb()
    {
        OmdbResultKind = "error";

        if (!OmdbClient.IsConfigured)
        {
            OmdbResult = "No OmdbApiKey is set in web.config, so lookups are switched off. " +
                         "Movies can still be added by hand.";
            return;
        }

        string error;
        Movie found = OmdbClient.GetByTitle("The Princess Bride", "1987", out error);

        if (found != null)
        {
            OmdbResultKind = "success";
            OmdbResult = "Working. OMDb returned \"" + found.Title + "\" (" + found.ReleaseYear +
                         "), directed by " + found.Director +
                         (found.HasPoster ? ", with a poster." : ", but with no poster.");
            return;
        }

        OmdbResult = error ?? "The lookup failed without saying why.";

        if (OmdbResult.IndexOf("Invalid API key", StringComparison.OrdinalIgnoreCase) >= 0)
            OmdbResult += "  Check the key in web.config - a free key has to be activated from " +
                          "the link in the email OMDb sends you.";
        else if (OmdbResult.IndexOf("Could not reach", StringComparison.OrdinalIgnoreCase) >= 0)
            OmdbResult += "  This usually means the host blocks outbound web requests. " +
                          "DiscountASP.NET support can confirm and enable it.";
    }

    private static string ReadProvider()
    {
        try
        {
            Match m = Regex.Match(Db.ConnectionString, @"Provider\s*=\s*([^;]+)",
                                  RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "(not specified)";
        }
        catch (Exception ex) { return "(unreadable: " + ex.Message + ")"; }
    }

}
