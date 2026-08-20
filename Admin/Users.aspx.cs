using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

/// <summary>
/// The administrator's gate: approve new family members, switch accounts off,
/// hand out (or take back) administrator rights.
/// </summary>
public partial class AdminUsersPage : AdminPageBase
{
    public string Message { get; private set; }
    public string MessageKind { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        MessageKind = "error";

        if (String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            if (!Csrf.IsValid(Request))
                Message = "That form expired. Please try again.";
            else if (HandleAction())
                return;
        }

        Bind();
        phMessage.Visible = !String.IsNullOrEmpty(Message);
    }

    /// <summary>Returns true when the response has been redirected.</summary>
    private bool HandleAction()
    {
        int userId = FormInt("userId");
        string action = Posted("action");
        if (userId <= 0 || action.Length == 0) return false;

        FamilyUser target = UserRepository.GetById(userId);
        if (target == null)
        {
            Message = "That member no longer exists.";
            return false;
        }

        switch (action)
        {
            case "approve":
                UserRepository.Approve(userId);
                SetFlash(target.DisplayName + " can now sign in.", "success");
                break;

            case "disable":
                if (!GuardSelf(target, "switch off your own account")) return false;
                if (!GuardLastAdmin(target, "switch off")) return false;
                UserRepository.SetDisabled(userId, true);
                SetFlash(target.DisplayName + " has been switched off.", "success");
                break;

            case "enable":
                UserRepository.SetDisabled(userId, false);
                SetFlash(target.DisplayName + " has been switched back on.", "success");
                break;

            case "promote":
                UserRepository.SetAdmin(userId, true);
                SetFlash(target.DisplayName + " is now an administrator.", "success");
                break;

            case "demote":
                if (!GuardSelf(target, "remove your own administrator rights")) return false;
                if (!GuardLastAdmin(target, "demote")) return false;
                UserRepository.SetAdmin(userId, false);
                SetFlash(target.DisplayName + " is now an ordinary member.", "success");
                break;

            case "delete":
                if (!GuardSelf(target, "delete your own account")) return false;
                if (!GuardLastAdmin(target, "delete")) return false;
                UserRepository.Delete(userId);
                SetFlash(target.DisplayName + " has been removed, along with their ratings.", "success");
                break;

            default:
                return false;
        }

        Go("~/Admin/Users.aspx");
        return true;
    }

    private bool GuardSelf(FamilyUser target, string what)
    {
        if (target.UserId != CurrentUser.UserId) return true;
        Message = "You cannot " + what + ".";
        return false;
    }

    /// <summary>Stops the site being left with nobody able to approve anyone.</summary>
    private bool GuardLastAdmin(FamilyUser target, string what)
    {
        if (!target.IsAdmin || UserRepository.AdminCount() > 1) return true;
        Message = "You cannot " + what + " the only administrator. Promote someone else first.";
        return false;
    }

    private void Bind()
    {
        List<FamilyUser> all = UserRepository.All();

        List<FamilyUser> pending = new List<FamilyUser>();
        foreach (FamilyUser user in all)
            if (!user.IsApproved && !user.IsDisabled) pending.Add(user);

        if (pending.Count > 0)
        {
            phPending.Visible = true;
            rptPending.DataSource = pending;
            rptPending.DataBind();
        }
        else
        {
            phNoPending.Visible = true;
        }

        rptAll.DataSource = all;
        rptAll.DataBind();
    }

    // ----- markup helpers used by the repeater ------------------------------

    protected string StatusPill(FamilyUser user)
    {
        if (user.IsDisabled) return "<span class=\"pill pill-off\">switched off</span>";
        if (!user.IsApproved) return "<span class=\"pill pill-no\">awaiting approval</span>";
        return "<span class=\"pill pill-yes\">approved</span>";
    }

    protected string LastSeen(FamilyUser user)
    {
        return user.LastLoginUtc.HasValue ? Ui.When(user.LastLoginUtc.Value) : "never";
    }

    protected string RowActions(FamilyUser user)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<div class=\"row-actions\">");

        if (!user.IsApproved && !user.IsDisabled)
            sb.Append(Button(user.UserId, "approve", "Approve", "btn-primary", null));

        if (user.IsDisabled)
            sb.Append(Button(user.UserId, "enable", "Switch on", "", null));
        else if (user.UserId != CurrentUser.UserId)
            sb.Append(Button(user.UserId, "disable", "Switch off", "btn-danger",
                             "Stop " + user.DisplayName + " signing in?"));

        if (!user.IsAdmin)
            sb.Append(Button(user.UserId, "promote", "Make admin", "", null));
        else if (user.UserId != CurrentUser.UserId)
            sb.Append(Button(user.UserId, "demote", "Remove admin", "", null));

        if (user.UserId != CurrentUser.UserId)
            sb.Append(Button(user.UserId, "delete", "Delete", "btn-danger",
                             "Delete " + user.DisplayName + " and every rating they left?"));

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Button(int userId, string action, string label, string extraClass,
                                 string confirmText)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<form method=\"post\" action=\"\"");
        if (!String.IsNullOrEmpty(confirmText))
            sb.Append(" onsubmit=\"return confirm('")
              .Append(HttpUtility.HtmlAttributeEncode(confirmText.Replace("'", "\\'")))
              .Append("');\"");
        sb.Append(">");
        sb.Append(Csrf.Field);
        sb.Append("<input type=\"hidden\" name=\"action\" value=\"").Append(action).Append("\" />");
        sb.Append("<input type=\"hidden\" name=\"userId\" value=\"").Append(userId).Append("\" />");
        sb.Append("<button type=\"submit\" class=\"btn btn-small ").Append(extraClass).Append("\">")
          .Append(HttpUtility.HtmlEncode(label)).Append("</button>");
        sb.Append("</form>");
        return sb.ToString();
    }
}
