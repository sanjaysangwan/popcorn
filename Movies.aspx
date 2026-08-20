<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Movies.aspx.cs"
         Inherits="MoviesPage" Title="Movie Library" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Movie library</h1>
    <p>Everything the family has recommended, with the average of everyone's stars.</p>
</div>

<div class="panel">
    <form method="get" action="<%= ResolveUrl("~/Movies.aspx") %>">
        <div class="toolbar">
            <div class="field grow">
                <label for="q">Search title, cast, director or genre</label>
                <input type="search" id="q" name="q" value="<%= Attr(Term) %>" />
            </div>
            <div class="field">
                <label for="sort">Order by</label>
                <select id="sort" name="sort">
                    <option value="recent" <%= Sort == "recent" ? "selected=\"selected\"" : "" %>>Recently added</option>
                    <option value="rating" <%= Sort == "rating" ? "selected=\"selected\"" : "" %>>Family rating</option>
                    <option value="title"  <%= Sort == "title"  ? "selected=\"selected\"" : "" %>>Title</option>
                </select>
            </div>
            <button type="submit" class="btn btn-primary">Show</button>
            <% if (!String.IsNullOrEmpty(Term)) { %>
            <a class="btn" href="<%= ResolveUrl("~/Movies.aspx") %>">Clear</a>
            <% } %>
        </div>
    </form>
</div>

<p class="hint"><%= Count %> movie<%= Count == 1 ? "" : "s" %><%= String.IsNullOrEmpty(Term) ? "" : " matching \"" + H(Term) + "\"" %>.</p>

<asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
    <div class="empty">
        <p>Nothing here yet.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/AddMovie.aspx") %>">Add the first movie</a>
    </div>
</asp:PlaceHolder>

<div class="movie-grid">
    <asp:Repeater ID="rptMovies" runat="server">
        <ItemTemplate><%# Ui.MovieCard((Movie)Container.DataItem, true, ReturnUrl) %></ItemTemplate>
    </asp:Repeater>
</div>

</asp:Content>
