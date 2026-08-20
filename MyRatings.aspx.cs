using System;
using System.Collections.Generic;

public partial class MyRatingsPage : PageBase
{
    public int Count { get; private set; }
    public int AddedByMe { get; private set; }
    public string AverageText { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string message;
        bool ok;
        if (RatingActions.TryHandle(Request, CurrentUser, out message, out ok))
        {
            SetFlash(message, ok ? "success" : "error");
            Go("~/MyRatings.aspx");
            return;
        }

        List<Movie> mine = MovieRepository.RatedBy(CurrentUser.UserId);
        Count = mine.Count;

        int total = 0;
        foreach (Movie m in mine) total += m.MyStars;
        AverageText = Count == 0 ? "-" : ((double)total / Count).ToString("0.0");

        AddedByMe = MovieRepository.AddedBy(CurrentUser.UserId, CurrentUser.UserId).Count;

        rptRated.DataSource = mine;
        rptRated.DataBind();
        phEmpty.Visible = Count == 0;
    }
}
