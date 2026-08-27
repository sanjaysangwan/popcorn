<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Default.aspx.cs"
         Inherits="DefaultPage" Title="What's New" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Hello, <%= H(FirstName) %></h1>
    <p>Here is what the rest of the family has been adding.</p>
</div>

<asp:PlaceHolder ID="phNothingNew" runat="server" Visible="false">
    <div class="empty">
        <p>You are all caught up - you have rated everything the family has added.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/AddMovie.aspx") %>">Add a movie of your own</a>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phAwaiting" runat="server" Visible="false">
    <h2>New from the family - waiting for your rating</h2>
    <p class="hint">Films someone else added that you have not scored yet. Pick your stars and save.</p>

    <div class="movie-grid">
        <asp:Repeater ID="rptAwaiting" runat="server">
            <ItemTemplate><%# Ui.MovieCard((Movie)Container.DataItem, true, "Default.aspx") %></ItemTemplate>
        </asp:Repeater>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phTopRated" runat="server" Visible="false">
    <h2 style="margin-top:2rem;">Best of the family</h2>
    <p class="hint">Ranked by the average of everyone's stars.</p>
    <div class="movie-grid">
        <asp:Repeater ID="rptTopRated" runat="server">
            <ItemTemplate><%# Ui.MovieCard((Movie)Container.DataItem, false, "Default.aspx") %></ItemTemplate>
        </asp:Repeater>
    </div>
</asp:PlaceHolder>

</asp:Content>
