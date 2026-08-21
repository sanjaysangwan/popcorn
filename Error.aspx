<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Error.aspx.cs"
         Inherits="ErrorPage" Title="Something went wrong" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head"><h1>That did not work</h1></div>

<div class="panel">
    <p>Something went wrong while loading that page. Nothing has been lost.</p>
    <p class="auth-links">
        If this keeps happening, the usual causes on a fresh deployment are the
        <strong>App_Data</strong> folder not being writable, or the wrong OLE DB
        provider in <strong>web.config</strong>.
    </p>
    <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">Back to the movies</a>
</div>

<%-- Administrators get the real thing. There is no log to read on shared
     hosting, so the site has to be able to show its own failures. --%>
<asp:PlaceHolder ID="phDetail" runat="server" Visible="false">
    <div class="panel">
        <h2>What actually happened</h2>
        <p class="hint">
            Only administrators see this. It was recorded at <%= H(ErrorWhen) %>
            while loading <code><%= H(ErrorPath) %></code>.
        </p>
        <pre class="error-detail"><%= H(ErrorDetail) %></pre>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phNoDetail" runat="server" Visible="false">
    <div class="panel">
        <h2>No details recorded</h2>
        <p class="hint">
            Sign in as an administrator and trigger the problem again to see the
            underlying error here.
        </p>
    </div>
</asp:PlaceHolder>

</asp:Content>
