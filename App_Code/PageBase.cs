using System;
using System.Configuration;
using System.Web;
using System.Web.UI;

/// <summary>
/// Base for every page on the site. Handles three things so the individual
/// pages do not have to: sending people to Setup.aspx before the database
/// exists, resolving the signed-in member, and bouncing accounts that an
/// administrator has not approved (or has since switched off).
/// </summary>
public class PageBase : Page
{
    /// <summary>Pages that anonymous visitors may see.</summary>
    protected virtual bool AllowAnonymous { get { return false; } }

    /// <summary>Pages only administrators may see.</summary>
    protected virtual bool RequireAdmin { get { return false; } }

    /// <summary>Setup.aspx sets this so it does not redirect to itself.</summary>
    protected virtual bool SkipInstallCheck { get { return false; } }

    public FamilyUser CurrentUser { get { return AppSecurity.CurrentUser; } }

    public static string SiteName
    {
        get
        {
            string name = ConfigurationManager.AppSettings["SiteName"];
            return String.IsNullOrEmpty(name) ? "Popcorn" : name;
        }
    }

    protected override void OnPreInit(EventArgs e)
    {
        base.OnPreInit(e);

        if (!SkipInstallCheck && !Db.IsInstalled())
        {
            Response.Redirect("~/Setup.aspx", true);
            return;
        }

        if (AllowAnonymous) return;

        if (CurrentUser == null)
        {
            // Authenticated cookie but no usable account: either the account was
            // never approved, or it has been switched off since they signed in.
            if (Request.IsAuthenticated)
            {
                AppSecurity.SignOut();
                Response.Redirect("~/Pending.aspx", true);
            }
            else
            {
                Response.Redirect("~/Login.aspx?returnUrl=" +
                                  HttpUtility.UrlEncode(Request.RawUrl), true);
            }
            return;
        }

        if (RequireAdmin && !CurrentUser.IsAdmin)
        {
            Response.Redirect("~/Default.aspx?denied=1", true);
            return;
        }
    }

    // ----- little helpers used by the .aspx markup -------------------------

    /// <summary>HTML-encodes a value for output.</summary>
    public string H(string value)
    {
        return HttpUtility.HtmlEncode(value ?? "");
    }

    public string Attr(string value)
    {
        return HttpUtility.HtmlAttributeEncode(value ?? "");
    }

    public string Url(string value)
    {
        return HttpUtility.UrlEncode(value ?? "");
    }

    /// <summary>A one-shot message shown at the top of the next page.</summary>
    protected void SetFlash(string message, string kind)
    {
        Session["Popcorn.Flash"] = message;
        Session["Popcorn.FlashKind"] = kind;
    }

    protected void Go(string url)
    {
        Response.Redirect(url, true);
    }

    /// <summary>True for a POST that carries a valid anti-forgery token.</summary>
    protected bool IsValidPost()
    {
        return String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
               && Csrf.IsValid(Request);
    }

    protected string Posted(string name)
    {
        return (Request.Form[name] ?? "").Trim();
    }

    protected int FormInt(string name)
    {
        int value;
        return Int32.TryParse(Posted(name), out value) ? value : 0;
    }

    protected int QueryInt(string name)
    {
        int value;
        return Int32.TryParse(Request.QueryString[name] ?? "", out value) ? value : 0;
    }
}

/// <summary>Base for the pages under /Admin.</summary>
public class AdminPageBase : PageBase
{
    protected override bool RequireAdmin { get { return true; } }
}
