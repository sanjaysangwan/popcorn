using System;
using System.Text.RegularExpressions;

public partial class RegisterPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }

    public string Message { get; private set; }
    public string MessageKind { get; private set; }
    public string NameValue { get; private set; }
    public string EmailValue { get; private set; }

    /// <summary>
    /// The very first account has to be approved automatically and made an
    /// administrator, otherwise there would be nobody able to approve anyone.
    /// </summary>
    public bool FirstAccount { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";
        NameValue = "";
        EmailValue = "";

        try { FirstAccount = UserRepository.Count() == 0; }
        catch { FirstAccount = false; }

        if (CurrentUser != null) { Go("~/Default.aspx"); return; }
        if (!String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return;

        if (!Csrf.IsValid(Request))
        {
            Message = "That form expired. Please try again.";
            return;
        }

        string displayName = Posted("displayName");
        string email = Posted("email");
        string password = Request.Form["password"] ?? "";
        string confirm = Request.Form["confirm"] ?? "";

        NameValue = displayName;
        EmailValue = email;

        if (displayName.Length < 2)
        {
            Message = "Please tell us your name.";
            return;
        }
        if (!LooksLikeEmail(email))
        {
            Message = "That does not look like an email address.";
            return;
        }
        if (password.Length < 8)
        {
            Message = "Please choose a password of at least 8 characters.";
            return;
        }
        if (password != confirm)
        {
            Message = "The two passwords do not match.";
            return;
        }

        try
        {
            if (UserRepository.EmailInUse(email))
            {
                Message = "There is already an account with that email address.";
                return;
            }

            // Re-check inside the same request: whoever gets here first while the
            // table is empty becomes the administrator.
            bool first = UserRepository.Count() == 0;
            int userId = UserRepository.Create(email, displayName, password, first, first);

            if (first)
            {
                FamilyUser created = UserRepository.GetById(userId);
                AppSecurity.SignIn(created, true);
                SetFlash("Welcome. You are the administrator - new family members will " +
                         "appear on the Members page for you to approve.", "success");
                Go("~/Default.aspx");
                return;
            }
        }
        catch (Exception ex)
        {
            Message = "Sorry, the account could not be created: " + ex.Message;
            return;
        }

        Go("~/Pending.aspx?new=1");
    }

    private static bool LooksLikeEmail(string value)
    {
        if (String.IsNullOrEmpty(value) || value.Length > 150) return false;
        return Regex.IsMatch(value, @"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$");
    }
}
