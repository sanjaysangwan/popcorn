<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Tags.aspx.cs"
         Inherits="TagsPage" Title="Categories" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Categories</h1>
    <p>Every shelf in the library. Pick one to see what is on it.</p>
</div>

<asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
</asp:PlaceHolder>

<div class="panel">
    <div class="tag-cloud"><%= Cloud %></div>
    <p class="hint">
        Categories are added while tagging a movie - open any film and tick the
        ones that fit, or type a new one.
    </p>
</div>

<div class="panel form-narrow">
    <h2>Add a category</h2>
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="add" />
        <div class="field">
            <label for="names">Name</label>
            <input type="text" id="names" name="names" maxlength="200"
                   placeholder="Rainy Sunday, Grandma&#39;s favourite" required="required" />
            <span class="hint">Separate several with commas.</span>
        </div>
        <button type="submit" class="btn btn-primary">Add</button>
    </form>
</div>

<asp:PlaceHolder ID="phManage" runat="server" Visible="false">
<div class="panel">
    <h2>Tidying up</h2>
    <p class="hint">
        Administrators only. Renaming a category keeps it on every film that
        has it; deleting takes it off them.
    </p>
    <div class="table-wrap">
        <table class="grid">
            <thead>
                <tr><th>Category</th><th>Movies</th><th>Rename</th><th></th></tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptTags" runat="server">
                    <ItemTemplate>
                        <tr>
                            <td><strong><%# H(((Tag)Container.DataItem).TagName) %></strong></td>
                            <td><%# ((Tag)Container.DataItem).MovieCount %></td>
                            <td><%# RenameForm((Tag)Container.DataItem) %></td>
                            <td><%# DeleteForm((Tag)Container.DataItem) %></td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>
    </div>
</div>
</asp:PlaceHolder>

</asp:Content>
