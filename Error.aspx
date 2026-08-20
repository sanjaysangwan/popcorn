<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Error.aspx.cs"
         Inherits="ErrorPage" Title="Something went wrong" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="auth-wrap">
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
</div>

</asp:Content>
