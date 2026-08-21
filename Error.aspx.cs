using System;

public partial class ErrorPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }
    protected override bool SkipInstallCheck { get { return true; } }

    public string ErrorDetail { get; private set; }
    public string ErrorPath { get; private set; }
    public string ErrorWhen { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        ErrorDetail = "";
        ErrorPath = "";
        ErrorWhen = "";

        // Only an administrator, and only ever from their own session - the
        // details are stashed by Global.asax when the failure happens.
        FamilyUser user = null;
        try { user = CurrentUser; } catch { user = null; }
        if (user == null || !user.IsAdmin) return;

        if (Session == null) return;

        string detail = Session["Popcorn.LastError"] as string;
        if (String.IsNullOrEmpty(detail))
        {
            phNoDetail.Visible = true;
            return;
        }

        ErrorDetail = detail;
        ErrorPath = Session["Popcorn.LastErrorPath"] as string ?? "";
        ErrorWhen = Session["Popcorn.LastErrorWhen"] as string ?? "";
        phDetail.Visible = true;

        // Shown once; the next failure records a fresh one.
        Session.Remove("Popcorn.LastError");
        Session.Remove("Popcorn.LastErrorPath");
        Session.Remove("Popcorn.LastErrorWhen");
    }
}
