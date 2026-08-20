<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Family.aspx.cs"
         Inherits="FamilyPage" Title="Family" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>The family</h1>
    <p>Who is on the list, what they have added and how generous they are with stars.</p>
</div>

<div class="panel">
    <div class="table-wrap">
        <table class="grid">
            <thead>
                <tr>
                    <th>Member</th>
                    <th>Movies added</th>
                    <th>Ratings given</th>
                    <th>Average score</th>
                    <th>Member since</th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptMembers" runat="server">
                    <ItemTemplate>
                        <tr>
                            <td>
                                <strong><%# H(((MemberSummary)Container.DataItem).Name) %></strong>
                                <%# ((MemberSummary)Container.DataItem).IsAdmin ? "<span class=\"pill pill-yes\">admin</span>" : "" %>
                            </td>
                            <td><%# ((MemberSummary)Container.DataItem).MoviesAdded %></td>
                            <td><%# ((MemberSummary)Container.DataItem).RatingsGiven %></td>
                            <td><%# H(((MemberSummary)Container.DataItem).AverageText) %></td>
                            <td><%# H(((MemberSummary)Container.DataItem).Since) %></td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>
    </div>
</div>

<p class="hint">
    A movie's headline score is the average of every family member's rating, so the
    more of you who vote, the better it reflects the household.
</p>

</asp:Content>
