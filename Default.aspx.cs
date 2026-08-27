using System;
using System.Collections.Generic;

/// <summary>
/// The landing page after signing in: everything other family members have
/// added that this member has not rated yet, each with its own star form.
/// </summary>
public partial class DefaultPage : PageBase
{
    public string FirstName { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        // A posted rating is handled first, then we redirect so that refreshing
        // the page does not save the same rating twice.
        string message;
        bool ok;
        if (RatingActions.TryHandle(Request, CurrentUser, out message, out ok))
        {
            SetFlash(message, ok ? "success" : "error");
            Go("~/Default.aspx");
            return;
        }

        if (Request.QueryString["denied"] == "1")
            SetFlash("That area is for administrators only.", "error");

        string[] names = (CurrentUser.DisplayName ?? "").Split(' ');
        FirstName = names.Length > 0 && names[0].Length > 0 ? names[0] : CurrentUser.DisplayName;

        List<Movie> awaiting = MovieRepository.AwaitingMyRating(CurrentUser.UserId, 24);
        if (awaiting.Count > 0)
        {
            phAwaiting.Visible = true;
            rptAwaiting.DataSource = awaiting;
            rptAwaiting.DataBind();
        }
        else
        {
            phNothingNew.Visible = true;
        }

        // A little "best of" strip, but only once a few ratings exist.
        List<Movie> rated = MovieRepository.Search(CurrentUser.UserId, null, "rating");
        List<Movie> top = new List<Movie>();
        foreach (Movie m in rated)
        {
            if (m.RatingCount == 0) break;      // the sort puts unrated films last
            top.Add(m);
            if (top.Count == 6) break;
        }

        if (top.Count > 0)
        {
            phTopRated.Visible = true;
            rptTopRated.DataSource = top;
            rptTopRated.DataBind();
        }
    }
}
