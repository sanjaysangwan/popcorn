<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="AddMovie.aspx.cs"
         Inherits="AddMoviePage" Title="Add a Movie" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Add a movie</h1>
    <p>Search for it and we will pull in the poster, synopsis and cast automatically.</p>
</div>

<asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
</asp:PlaceHolder>

<div class="panel">
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="search" />
        <div class="toolbar">
            <div class="field grow">
                <label for="q">Movie title</label>
                <input type="search" id="q" name="q" value="<%= Attr(Term) %>"
                       placeholder="e.g. The Princess Bride" autofocus="autofocus" />
            </div>
            <button type="submit" class="btn btn-primary">Search</button>
        </div>
    </form>

    <asp:PlaceHolder ID="phNoKey" runat="server" Visible="false">
        <p class="hint">
            No OMDb API key is set in web.config, so automatic lookups are switched off.
            You can still add films by hand below.
        </p>
    </asp:PlaceHolder>
</div>

<asp:PlaceHolder ID="phResults" runat="server" Visible="false">
    <h2><%= Results.Count %> result<%= Results.Count == 1 ? "" : "s" %> for &ldquo;<%= H(Term) %>&rdquo;</h2>
    <div class="result-grid">
        <asp:Repeater ID="rptResults" runat="server">
            <ItemTemplate>
                <div class="result">
                    <%# ResultPoster((MovieSearchResult)Container.DataItem) %>
                    <div class="result-body">
                        <h3><%# H(((MovieSearchResult)Container.DataItem).Title) %></h3>
                        <div class="muted"><%# H(((MovieSearchResult)Container.DataItem).Year) %></div>
                        <form method="post" action="">
                            <%# Csrf.Field %>
                            <input type="hidden" name="action" value="add" />
                            <input type="hidden" name="imdbId" value="<%# Attr(((MovieSearchResult)Container.DataItem).ImdbId) %>" />
                            <button type="submit" class="btn btn-primary btn-small">Add to the list</button>
                        </form>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phNoResults" runat="server" Visible="false">
    <h2>No results for &ldquo;<%= H(Term) %>&rdquo;</h2>
    <div class="empty">Nothing matched. Try a different spelling, or add it by hand below.</div>
</asp:PlaceHolder>

<div class="panel form-narrow" style="margin-top:1.5rem;">
    <h2>Or add one by hand</h2>
    <p class="hint">
        We will still try to fill in the poster and synopsis from the title you give.
    </p>
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="manual" />
        <div class="field">
            <label for="title">Title</label>
            <input type="text" id="title" name="title" maxlength="200" required="required" />
        </div>
        <div class="field">
            <label for="year">Year <span class="hint">(optional)</span></label>
            <input type="text" id="year" name="year" maxlength="12" />
        </div>
        <button type="submit" class="btn">Add it</button>
    </form>
</div>

</asp:Content>
