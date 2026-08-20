<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="MyRatings.aspx.cs"
         Inherits="MyRatingsPage" Title="My Ratings" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>My ratings</h1>
    <p>Everything you have scored, best first. Change a rating any time - the family
       average updates straight away.</p>
</div>

<div class="stat-row">
    <div class="stat"><div class="n"><%= Count %></div><div class="l">movies you have rated</div></div>
    <div class="stat"><div class="n"><%= AverageText %></div><div class="l">your average score</div></div>
    <div class="stat"><div class="n"><%= AddedByMe %></div><div class="l">movies you added</div></div>
</div>

<asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
    <div class="empty">
        <p>You have not rated anything yet.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">See what the family added</a>
    </div>
</asp:PlaceHolder>

<div class="movie-grid">
    <asp:Repeater ID="rptRated" runat="server">
        <ItemTemplate><%# Ui.MovieCard((Movie)Container.DataItem, true, "MyRatings.aspx") %></ItemTemplate>
    </asp:Repeater>
</div>

</asp:Content>
