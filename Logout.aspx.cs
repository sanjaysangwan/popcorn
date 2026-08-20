using System;
using System.Web.UI;

/// <summary>Signs out and bounces straight back to the sign-in page.</summary>
public partial class LogoutPage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        AppSecurity.SignOut();
        Response.Redirect("~/Login.aspx?signedout=1", true);
    }
}
