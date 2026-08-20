<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="MovieDetails.aspx.cs"
         Inherits="MovieDetailsPage" Title="Movie" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<asp:PlaceHolder ID="phNotFound" runat="server" Visible="false">
    <div class="empty">
        <p>That movie is not on the family list.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Movies.aspx") %>">Back to the library</a>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phFilm" runat="server" Visible="false">

<div class="detail">

    <div class="detail-poster"><%= Ui.PosterTag(Film, "poster") %></div>

    <div class="detail-main">
        <div class="page-head">
            <h1><%= H(Film.Title) %> <span class="movie-year"><%= H(Film.YearText) %></span></h1>
            <p>Added by <strong><%= H(Film.AddedByName) %></strong> <%= H(Ui.When(Film.AddedUtc)) %>.</p>
        </div>

        <div class="family-score">
            <span class="score-number"><%= H(Film.FamilyAverageText) %></span>
            <span class="score-detail">
                <%= Ui.StarDisplay(Film.FamilyAverage, Film.RatingCount) %>
                <span class="score-votes"><%= H(AverageCaption) %></span>
            </span>
        </div>

        <%= PlotMarkup %>

        <ul class="detail-facts"><%= FactsMarkup %></ul>
    </div>
</div>

<div class="panel" style="margin-top:1.5rem;">
    <h2><%= Film.RatedByMe ? "Your rating" : "Add your rating" %></h2>
    <p class="hint">
        Everyone in the family scores a movie out of 10; the headline number above is
        simply the average of those scores.
    </p>

    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="rate" />
        <input type="hidden" name="movieId" value="<%= Film.MovieId %>" />

        <div class="field">
            <label>Stars</label><br />
            <%= Ui.StarInput(Film.MovieId.ToString(), Film.MyStars) %>
        </div>

        <div class="field">
            <label for="review">A few words <span class="hint">(optional)</span></label>
            <textarea id="review" name="review" maxlength="2000"><%= H(MyReview) %></textarea>
        </div>

        <div class="form-actions">
            <button type="submit" class="btn btn-primary">
                <%= Film.RatedByMe ? "Update my rating" : "Save my rating" %>
            </button>
            <asp:PlaceHolder ID="phUnrate" runat="server" Visible="false">
                <button type="submit" name="action" value="unrate" class="btn btn-danger">Remove my rating</button>
            </asp:PlaceHolder>
        </div>
    </form>
</div>

<div class="panel">
    <h2>What the family thought</h2>

    <asp:PlaceHolder ID="phNoRatings" runat="server" Visible="false">
        <p class="hint">Nobody has rated this yet. Be the first.</p>
    </asp:PlaceHolder>

    <asp:PlaceHolder ID="phRatings" runat="server" Visible="false">
        <ul class="rating-list">
            <asp:Repeater ID="rptRatings" runat="server">
                <ItemTemplate>
                    <li>
                        <span class="rating-who"><%# H(((MovieRating)Container.DataItem).DisplayName) %></span>
                        <span class="rating-score"><%# ((MovieRating)Container.DataItem).Stars %>/10</span>
                        <%# Ui.StarDisplay(((MovieRating)Container.DataItem).Stars, 1) %>
                        <%# ReviewMarkup((MovieRating)Container.DataItem) %>
                    </li>
                </ItemTemplate>
            </asp:Repeater>
        </ul>
        <p class="hint" style="margin-top:.75rem;">
            Family average: <strong><%= H(Film.FamilyAverageText) %></strong> out of 10
            across <%= Ratings.Count %> rating<%= Ratings.Count == 1 ? "" : "s" %>.
        </p>
    </asp:PlaceHolder>
</div>

<div class="form-actions">
    <a class="btn" href="<%= ResolveUrl("~/Movies.aspx") %>">Back to the library</a>

    <form method="post" action="" style="display:inline;">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="refresh" />
        <input type="hidden" name="movieId" value="<%= Film.MovieId %>" />
        <button type="submit" class="btn btn-small">Refresh details from OMDb</button>
    </form>

    <asp:PlaceHolder ID="phDelete" runat="server" Visible="false">
        <form method="post" action="" style="display:inline;"
              onsubmit="return confirm('Remove this movie and every rating for it?');">
            <%= Csrf.Field %>
            <input type="hidden" name="action" value="delete" />
            <input type="hidden" name="movieId" value="<%= Film.MovieId %>" />
            <button type="submit" class="btn btn-danger btn-small">Remove this movie</button>
        </form>
    </asp:PlaceHolder>
</div>

</asp:PlaceHolder>

</asp:Content>
