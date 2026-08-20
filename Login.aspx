<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Login.aspx.cs"
         Inherits="LoginPage" Title="Sign In" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="auth-wrap">
    <div class="page-head">
        <h1>Welcome back</h1>
        <p>Sign in to see what the family has been watching.</p>
    </div>

    <% if (!String.IsNullOrEmpty(Message)) { %>
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
    <% } %>

    <div class="panel">
        <form method="post" action="">
            <%= Csrf.Field %>

            <div class="field">
                <label for="email">Email address</label>
                <input type="email" id="email" name="email" value="<%= Attr(EmailValue) %>"
                       autocomplete="username" required="required" autofocus="autofocus" />
            </div>

            <div class="field">
                <label for="password">Password</label>
                <input type="password" id="password" name="password"
                       autocomplete="current-password" required="required" />
            </div>

            <div class="field">
                <label class="checkbox">
                    <input type="checkbox" name="remember" value="1" checked="checked" />
                    Keep me signed in on this device
                </label>
            </div>

            <button type="submit" class="btn btn-primary btn-block">Sign In</button>
        </form>

        <p class="auth-links">
            No account yet? <a href="<%= ResolveUrl("~/Register.aspx") %>">Request one</a> -
            an administrator approves new family members before they can sign in.
        </p>
    </div>
</div>

</asp:Content>
