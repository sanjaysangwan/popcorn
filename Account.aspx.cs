using System;

public partial class AccountPage : PageBase
{
    public string Message { get; private set; }
    public string MessageKind { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";

        if (String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Csrf.IsValid(Request))
            {
                Message = "That form expired. Please try again.";
            }
            else
            {
                switch (Posted("action"))
                {
                    case "profile": SaveProfile(); break;
                    case "password": ChangePassword(); break;
                }
            }
        }

        phMessage.Visible = !String.IsNullOrEmpty(Message);
    }

    private void SaveProfile()
    {
        string displayName = Posted("displayName");
        if (displayName.Length < 2)
        {
            Message = "Please give a name of at least two characters.";
            return;
        }

        Db.Execute("UPDATE Users SET DisplayName = ? WHERE UserId = ?",
                   Db.Trim(displayName, 80), CurrentUser.UserId);

        SetFlash("Your details have been saved.", "success");
        Go("~/Account.aspx");
    }

    private void ChangePassword()
    {
        string current = Request.Form["current"] ?? "";
        string fresh = Request.Form["fresh"] ?? "";
        string confirm = Request.Form["confirm"] ?? "";

        if (UserRepository.Authenticate(CurrentUser.Email, current) == null)
        {
            Message = "That is not your current password.";
            return;
        }
        if (fresh.Length < 8)
        {
            Message = "Choose a new password of at least 8 characters.";
            return;
        }
        if (fresh != confirm)
        {
            Message = "The two new passwords do not match.";
            return;
        }

        UserRepository.SetPassword(CurrentUser.UserId, fresh);
        SetFlash("Your password has been changed.", "success");
        Go("~/Account.aspx");
    }
}
