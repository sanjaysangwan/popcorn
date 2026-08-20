using System;

public partial class PendingPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }

    public bool IsNew { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        IsNew = Request.QueryString["new"] == "1";

        // An approved member has no business on this page.
        if (CurrentUser != null) Go("~/Default.aspx");
    }
}
