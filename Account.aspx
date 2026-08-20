<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Account.aspx.cs"
         Inherits="AccountPage" Title="My Account" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>My account</h1>
</div>

<asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
</asp:PlaceHolder>

<div class="panel form-narrow">
    <h2>Your details</h2>
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="profile" />
        <div class="field">
            <label for="displayName">Display name</label>
            <input type="text" id="displayName" name="displayName" maxlength="80"
                   value="<%= Attr(CurrentUser.DisplayName) %>" required="required" />
            <span class="hint">Shown next to your ratings.</span>
        </div>
        <div class="field">
            <label>Email address</label>
            <input type="text" value="<%= Attr(CurrentUser.Email) %>" disabled="disabled" />
            <span class="hint">Ask an administrator if this needs changing.</span>
        </div>
        <button type="submit" class="btn btn-primary">Save</button>
    </form>
</div>

<div class="panel form-narrow">
    <h2>Change your password</h2>
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="password" />
        <div class="field">
            <label for="current">Current password</label>
            <input type="password" id="current" name="current" autocomplete="current-password" required="required" />
        </div>
        <div class="field">
            <label for="fresh">New password</label>
            <input type="password" id="fresh" name="fresh" autocomplete="new-password" required="required" />
            <span class="hint">At least 8 characters.</span>
        </div>
        <div class="field">
            <label for="confirm">Confirm new password</label>
            <input type="password" id="confirm" name="confirm" autocomplete="new-password" required="required" />
        </div>
        <button type="submit" class="btn btn-primary">Change password</button>
    </form>
</div>

</asp:Content>
