using System;
using System.Web;

public partial class LoginPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }

    public string Message { get; private set; }
    public string MessageKind { get; private set; }
    public string EmailValue { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";
        EmailValue = "";

        if (CurrentUser != null) { Go("~/Default.aspx"); return; }

        if (!IsPostBackForm())
        {
            if (Request.QueryString["signedout"] == "1")
            {
                Message = "You are signed out. See you at the next movie night.";
                MessageKind = "success";
            }
            return;
        }

        if (!Csrf.IsValid(Request))
        {
            Message = "That form expired. Please try again.";
            return;
        }

        string email = Posted("email");
        string password = Request.Form["password"] ?? "";
        EmailValue = email;

        if (email.Length == 0 || password.Length == 0)
        {
            Message = "Please fill in both your email address and your password.";
            return;
        }

        FamilyUser user;
        try { user = UserRepository.Authenticate(email, password); }
        catch (Exception ex)
        {
            Message = "The site could not reach its database: " + ex.Message;
            return;
        }

        if (user == null)
        {
            // Deliberately vague, so this cannot be used to discover which
            // addresses have accounts.
            Message = "That email address and password do not match.";
            return;
        }

        if (user.IsDisabled)
        {
            Message = "That account has been switched off. Please speak to the family administrator.";
            return;
        }

        if (!user.IsApproved)
        {
            Go("~/Pending.aspx");
            return;
        }

        AppSecurity.SignIn(user, Posted("remember") == "1");
        Go(SafeReturnUrl());
    }

    private bool IsPostBackForm()
    {
        return String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Only ever bounce back to a page on this site.</summary>
    private string SafeReturnUrl()
    {
        string target = Request.QueryString["returnUrl"];
        if (String.IsNullOrEmpty(target)) return "~/Default.aspx";

        target = HttpUtility.UrlDecode(target);
        if (target.StartsWith("//") || target.Contains(":") || target.Contains("\\"))
            return "~/Default.aspx";
        if (!target.StartsWith("/") && !target.StartsWith("~/"))
            return "~/Default.aspx";
        return target;
    }
}
