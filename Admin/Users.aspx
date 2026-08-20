<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Users.aspx.cs"
         Inherits="AdminUsersPage" Title="Members" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Members</h1>
    <p>New sign-ups wait here until you approve them. Nobody can see the family's
       movies until you do.</p>
</div>

<asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
    <div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phPending" runat="server" Visible="false">
    <div class="panel">
        <h2>Waiting for approval</h2>
        <div class="table-wrap">
            <table class="grid">
                <thead>
                    <tr><th>Name</th><th>Email</th><th>Requested</th><th>Actions</th></tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptPending" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td><strong><%# H(((FamilyUser)Container.DataItem).DisplayName) %></strong></td>
                                <td><%# H(((FamilyUser)Container.DataItem).Email) %></td>
                                <td><%# H(Ui.When(((FamilyUser)Container.DataItem).CreatedUtc)) %></td>
                                <td>
                                    <div class="row-actions">
                                        <form method="post" action="">
                                            <%# Csrf.Field %>
                                            <input type="hidden" name="action" value="approve" />
                                            <input type="hidden" name="userId" value="<%# ((FamilyUser)Container.DataItem).UserId %>" />
                                            <button type="submit" class="btn btn-primary btn-small">Approve</button>
                                        </form>
                                        <form method="post" action=""
                                              onsubmit="return confirm('Delete this request for good?');">
                                            <%# Csrf.Field %>
                                            <input type="hidden" name="action" value="delete" />
                                            <input type="hidden" name="userId" value="<%# ((FamilyUser)Container.DataItem).UserId %>" />
                                            <button type="submit" class="btn btn-danger btn-small">Decline</button>
                                        </form>
                                    </div>
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
        </div>
    </div>
</asp:PlaceHolder>

<asp:PlaceHolder ID="phNoPending" runat="server" Visible="false">
    <div class="alert alert-success">No accounts are waiting for approval.</div>
</asp:PlaceHolder>

<div class="panel">
    <h2>Everyone</h2>
    <div class="table-wrap">
        <table class="grid">
            <thead>
                <tr>
                    <th>Name</th><th>Email</th><th>Status</th><th>Role</th>
                    <th>Last signed in</th><th>Actions</th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptAll" runat="server">
                    <ItemTemplate>
                        <tr>
                            <td><strong><%# H(((FamilyUser)Container.DataItem).DisplayName) %></strong></td>
                            <td><%# H(((FamilyUser)Container.DataItem).Email) %></td>
                            <td><%# StatusPill((FamilyUser)Container.DataItem) %></td>
                            <td><%# ((FamilyUser)Container.DataItem).IsAdmin ? "Administrator" : "Member" %></td>
                            <td><%# H(LastSeen((FamilyUser)Container.DataItem)) %></td>
                            <td><%# RowActions((FamilyUser)Container.DataItem) %></td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>
    </div>
</div>

</asp:Content>
