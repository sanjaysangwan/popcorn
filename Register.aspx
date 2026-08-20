<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Register.aspx.cs"
         Inherits="RegisterPage" Title="Request an Account" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="auth-wrap">
    <div class="page-head">
        <h1><%= FirstAccount ? "Set up the site" : "Request an account" %></h1>
        <p>
            <% if (FirstAccount) { %>
                No accounts exist yet, so this first one becomes the administrator
                and is approved straight away.
            <% } else { %>
                Fill this in and the family administrator will be asked to approve you.
                You will be able to sign in once they do.
            <% } %>
        </p>
    </div>

    <% if (!String.IsNullOrEmpty(Message)) { %>
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
    <% } %>

    <div class="panel">
        <form method="post" action="">
            <%= Csrf.Field %>

            <div class="field">
                <label for="displayName">Your name</label>
                <input type="text" id="displayName" name="displayName" maxlength="80"
                       value="<%= Attr(NameValue) %>" required="required" autofocus="autofocus" />
                <span class="hint">This is how your ratings are shown to everyone else.</span>
            </div>

            <div class="field">
                <label for="email">Email address</label>
                <input type="email" id="email" name="email" maxlength="150"
                       value="<%= Attr(EmailValue) %>" autocomplete="username" required="required" />
            </div>

            <div class="field">
                <label for="password">Password</label>
                <input type="password" id="password" name="password"
                       autocomplete="new-password" required="required" />
                <span class="hint">At least 8 characters.</span>
            </div>

            <div class="field">
                <label for="confirm">Confirm password</label>
                <input type="password" id="confirm" name="confirm"
                       autocomplete="new-password" required="required" />
            </div>

            <button type="submit" class="btn btn-primary btn-block">
                <%= FirstAccount ? "Create the administrator account" : "Request an account" %>
            </button>
        </form>

        <p class="auth-links">
            Already have an account? <a href="<%= ResolveUrl("~/Login.aspx") %>">Sign in</a>.
        </p>
    </div>
</div>

</asp:Content>
