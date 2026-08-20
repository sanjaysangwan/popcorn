using System;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;

/// <summary>Sign-in / sign-out plus the per-request "who is this" lookup.</summary>
public static class AppSecurity
{
    private const string ItemsKey = "Popcorn.CurrentUser";

    public static void SignIn(FamilyUser user, bool persistent)
    {
        FormsAuthentication.SetAuthCookie(user.UserId.ToString(), persistent);
        HttpContext.Current.Items[ItemsKey] = user;
        UserRepository.RecordLogin(user.UserId);
    }

    public static void SignOut()
    {
        FormsAuthentication.SignOut();
        HttpContext context = HttpContext.Current;
        if (context != null)
        {
            context.Items.Remove(ItemsKey);
            if (context.Session != null) context.Session.Abandon();
        }
    }

    /// <summary>
    /// The signed-in member, or null. Resolved from the database once per
    /// request so that an administrator revoking access takes effect straight
    /// away rather than when the cookie expires.
    /// </summary>
    public static FamilyUser CurrentUser
    {
        get
        {
            HttpContext context = HttpContext.Current;
            if (context == null) return null;

            if (context.Items.Contains(ItemsKey))
                return context.Items[ItemsKey] as FamilyUser;

            FamilyUser user = null;
            if (context.User != null && context.User.Identity.IsAuthenticated)
            {
                int userId;
                if (Int32.TryParse(context.User.Identity.Name, out userId))
                {
                    try { user = UserRepository.GetById(userId); }
                    catch { user = null; }        // database not installed yet
                }
                if (user != null && !user.CanSignIn) user = null;
            }

            context.Items[ItemsKey] = user;
            return user;
        }
    }

    public static bool IsSignedIn { get { return CurrentUser != null; } }
}

/// <summary>
/// Per-session anti-forgery token. Every form on the site posts it back in a
/// hidden field and the receiving page checks it before touching the database.
/// </summary>
public static class Csrf
{
    private const string Key = "Popcorn.CsrfToken";
    public const string FieldName = "__csrf";

    public static string Token
    {
        get
        {
            HttpContext context = HttpContext.Current;
            if (context == null || context.Session == null) return "";

            string token = context.Session[Key] as string;
            if (String.IsNullOrEmpty(token))
            {
                byte[] buffer = new byte[24];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                    rng.GetBytes(buffer);
                token = Convert.ToBase64String(buffer);
                context.Session[Key] = token;
            }
            return token;
        }
    }

    /// <summary>Hidden input to drop inside every &lt;form&gt;.</summary>
    public static string Field
    {
        get
        {
            return "<input type=\"hidden\" name=\"" + FieldName + "\" value=\"" +
                   HttpUtility.HtmlAttributeEncode(Token) + "\" />";
        }
    }

    public static bool IsValid(HttpRequest request)
    {
        HttpContext context = HttpContext.Current;
        if (context == null || context.Session == null) return false;

        string expected = context.Session[Key] as string;
        string posted = request.Form[FieldName];
        if (String.IsNullOrEmpty(expected) || String.IsNullOrEmpty(posted)) return false;

        // Constant-time comparison.
        if (expected.Length != posted.Length) return false;
        int diff = 0;
        for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ posted[i];
        return diff == 0;
    }
}
