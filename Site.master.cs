using System;
using System.Configuration;
using System.Web;
using System.Web.UI;

public partial class SiteMaster : MasterPage
{
    private int _pendingApprovals = -1;

    public FamilyUser CurrentUser { get { return AppSecurity.CurrentUser; } }

    public string SiteName
    {
        get
        {
            string name = ConfigurationManager.AppSettings["SiteName"];
            return String.IsNullOrEmpty(name) ? "Popcorn" : name;
        }
    }

    public string H(string value) { return HttpUtility.HtmlEncode(value ?? ""); }

    /// <summary>
    /// A cache-busting stamp taken from the file's own timestamp, so uploading
    /// a new stylesheet is enough for browsers to pick it up. A hand-written
    /// number here would have to be remembered every single time, and would not
    /// be - which is exactly what happened to the first version of this.
    /// </summary>
    public string AssetVersion(string virtualPath)
    {
        try
        {
            string path = Server.MapPath(virtualPath);
            return System.IO.File.GetLastWriteTimeUtc(path).Ticks.ToString();
        }
        catch
        {
            return "1";
        }
    }

    /// <summary>Two letters for the little avatar circle in the side pane.</summary>
    public string Initials
    {
        get
        {
            FamilyUser user = CurrentUser;
            if (user == null || String.IsNullOrEmpty(user.DisplayName)) return "?";

            string[] parts = user.DisplayName.Trim()
                .Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }
    }

    /// <summary>Accounts waiting for an administrator, shown as a badge.</summary>
    public int PendingApprovals
    {
        get
        {
            if (_pendingApprovals < 0)
            {
                FamilyUser user = CurrentUser;
                try { _pendingApprovals = (user != null && user.IsAdmin) ? UserRepository.PendingCount() : 0; }
                catch { _pendingApprovals = 0; }
            }
            return _pendingApprovals;
        }
    }

    /// <summary>Marks the link for the page we are currently on.</summary>
    public string NavClass(string pageName)
    {
        string current = "";
        try { current = System.IO.Path.GetFileName(Request.AppRelativeCurrentExecutionFilePath ?? ""); }
        catch { }

        return String.Equals(current, pageName, StringComparison.OrdinalIgnoreCase)
                   ? "nav-link is-current" : "nav-link";
    }

    // ----- one-shot messages ----------------------------------------------

    public string FlashMessage { get; private set; }
    public string FlashKind { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (Session != null)
        {
            FlashMessage = Session["Popcorn.Flash"] as string;
            FlashKind = (Session["Popcorn.FlashKind"] as string) ?? "info";
            Session.Remove("Popcorn.Flash");
            Session.Remove("Popcorn.FlashKind");
        }
    }
}
