<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Pending.aspx.cs"
         Inherits="PendingPage" Title="Waiting for Approval" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="auth-wrap">
    <div class="page-head">
        <h1>Almost there</h1>
    </div>

    <div class="panel">
        <p>
            <% if (IsNew) { %>
                Thanks - your account has been created and is now waiting for the
                family administrator to approve it.
            <% } else { %>
                Your account is not approved yet, so there is nothing to see here just
                the moment.
            <% } %>
        </p>
        <p>
            Give whoever runs the site a nudge; as soon as they approve you, sign in
            and you will see everything the family has been watching.
        </p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Login.aspx") %>">Back to sign in</a>
    </div>
</div>

</asp:Content>
