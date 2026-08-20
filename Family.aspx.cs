using System;
using System.Collections.Generic;
using System.Data;

/// <summary>Read-only roll-call of approved family members and their activity.</summary>
public partial class FamilyPage : PageBase
{
    protected void Page_Load(object sender, EventArgs e)
    {
        List<MemberSummary> members = new List<MemberSummary>();

        foreach (FamilyUser user in UserRepository.ActiveMembers())
        {
            MemberSummary row = new MemberSummary
            {
                Name = user.DisplayName,
                IsAdmin = user.IsAdmin,
                Since = user.CreatedUtc.ToString("MMM yyyy")
            };

            row.MoviesAdded = Convert.ToInt32(
                Db.Scalar("SELECT COUNT(*) FROM Movies WHERE AddedByUserId = ?", user.UserId) ?? 0);
            row.RatingsGiven = RatingRepository.CountByUser(user.UserId);

            object average = Db.Scalar("SELECT AVG(Stars) FROM Ratings WHERE UserId = ?", user.UserId);
            if (average != null)
                row.AverageText = Convert.ToDouble(average).ToString("0.0") + " / 10";

            members.Add(row);
        }

        rptMembers.DataSource = members;
        rptMembers.DataBind();
    }
}
