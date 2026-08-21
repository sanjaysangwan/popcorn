<%@ Page Language="C#" MasterPageFile="~/Site.master" CodeFile="Setup.aspx.cs"
         Inherits="SetupPage" Title="Set Up" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">

<div class="page-head">
    <h1>Set up <%= H(SiteName) %></h1>
    <p>One-off installation: create the Access database, its tables and the first administrator.</p>
</div>

<% if (!String.IsNullOrEmpty(Message)) { %>
<div class="alert alert-<%= H(MessageKind) %>"><%= H(Message) %></div>
<% } %>

<% if (Blocked) { %>
    <div class="panel">
        <h2>Setup is closed</h2>
        <p><%= H(BlockedReason) %></p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">Go to the site</a>
    </div>
<% } else { %>

<div class="panel">
    <h2>Where things stand</h2>
    <ul class="detail-facts">
        <li><strong>Provider</strong> <%= H(ProviderName) %>
            <%= ProviderInstalled ? "" : " &mdash; NOT registered on this server" %></li>
        <li><strong>Database</strong> <%= H(DatabaseFile) %></li>
        <li><strong>File exists</strong> <%= FileExists ? "yes" : "no - it will be created below" %></li>
        <li><strong>App_Data</strong> <%= H(DataFolderState) %></li>
        <li><strong>Tables</strong> <%= Installed ? "Users, Movies and Ratings are present" : "not created yet" %></li>
        <li><strong>OMDb key</strong> <%= OmdbClient.IsConfigured ? "configured" : "not set - lookups are off" %></li>
    </ul>
</div>

<div class="panel">
    <h2>This server</h2>
    <ul class="detail-facts">
        <li><strong>Process</strong> <%= H(Bitness) %>
            <%= ShowJetBitnessWarning ? " &mdash; the Jet provider is 32-bit only, so this will not work" : "" %></li>
        <li><strong>Runs as</strong> <%= H(Identity) %></li>
        <li><strong>ADOX</strong> <%= AdoxAvailable ? "registered - the database file can be created here"
                                                   : "not registered - upload an empty database instead" %></li>
    </ul>

    <p class="hint">
        <strong>Runs as</strong> is the Windows account that needs Modify permission on the
        <code>App_Data</code> folder. FTP cannot grant that - it is set from the
        DiscountASP.NET Control Panel.
    </p>

    <h3>OLE DB providers registered here</h3>
    <ul class="detail-facts">
        <asp:Repeater ID="rptProviders" runat="server">
            <ItemTemplate><li><%# H(Convert.ToString(Container.DataItem)) %></li></ItemTemplate>
        </asp:Repeater>
    </ul>
    <p class="hint">
        Your connection string has to name one of these. If neither
        <code>Microsoft.Jet.OLEDB.4.0</code> nor <code>Microsoft.ACE.OLEDB.12.0</code> is on the
        list, ask support which one this server has - the list only shows providers built for a
        <%= H(Bitness) %> process.
    </p>
</div>

<% if (Log.Count > 0) { %>
<div class="panel">
    <h2>What just happened</h2>
    <ul class="detail-facts">
        <% foreach (string line in Log) { %>
        <li><%= H(line) %></li>
        <% } %>
    </ul>
</div>
<% } %>

<div class="panel form-narrow">
    <h2>Step 1 &amp; 2 - create the database</h2>
    <p class="hint">
        Creates <%= H(System.IO.Path.GetFileName(DatabaseFile)) %> if it is missing, then adds the
        Users, Movies and Ratings tables. Safe to run more than once.
    </p>
    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="install" />
        <div class="field">
            <label for="key">Setup key</label>
            <input type="password" id="key" name="key" required="required" />
            <span class="hint">The <code>SetupKey</code> value from web.config.</span>
        </div>
        <button type="submit" class="btn btn-primary">Create the database</button>
    </form>
</div>

<div class="panel form-narrow">
    <h2>Movie lookups</h2>
    <p class="hint">
        Fetches one known film from OMDb and reports exactly what came back.
    </p>

    <asp:PlaceHolder ID="phOmdb" runat="server" Visible="false">
        <div class="alert alert-<%= H(OmdbResultKind) %>"><%= H(OmdbResult) %></div>
    </asp:PlaceHolder>

    <form method="post" action="">
        <%= Csrf.Field %>
        <input type="hidden" name="action" value="omdb" />
        <div class="field">
            <label for="omdbKey">Setup key</label>
            <input type="password" id="omdbKey" name="key" required="required" />
        </div>
        <button type="submit" class="btn">Test the connection to OMDb</button>
    </form>
</div>

<asp:PlaceHolder ID="phSchema" runat="server" Visible="false">
<div class="panel">
    <h2>What the database actually contains</h2>
    <p class="hint">
        The column types Jet or ACE settled on. If one of these is not what the
        schema asked for, that is what a later "data type mismatch" is about.
    </p>
    <h3>Users</h3>
    <ul class="detail-facts">
        <asp:Repeater ID="rptUserColumns" runat="server">
            <ItemTemplate><li><%# H(Convert.ToString(Container.DataItem)) %></li></ItemTemplate>
        </asp:Repeater>
    </ul>
    <h3>Movies</h3>
    <ul class="detail-facts">
        <asp:Repeater ID="rptMovieColumns" runat="server">
            <ItemTemplate><li><%# H(Convert.ToString(Container.DataItem)) %></li></ItemTemplate>
        </asp:Repeater>
    </ul>
    <h3>Ratings</h3>
    <ul class="detail-facts">
        <asp:Repeater ID="rptRatingColumns" runat="server">
            <ItemTemplate><li><%# H(Convert.ToString(Container.DataItem)) %></li></ItemTemplate>
        </asp:Repeater>
    </ul>
</div>
</asp:PlaceHolder>

<% if (Installed) { %>
<div class="panel form-narrow">
    <h2>Step 3 - the administrator account</h2>
    <% if (HasUsers) { %>
        <p>There <%= UserCount == 1 ? "is already 1 account" : "are already " + UserCount + " accounts" %>
           on this site, so there is nothing more to do here.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Login.aspx") %>">Sign in</a>
    <% } else { %>
        <p class="hint">
            Create the first account now. It is approved immediately and can approve
            everyone else. (If you skip this, whoever registers first becomes the
            administrator instead.)
        </p>
        <form method="post" action="">
            <%= Csrf.Field %>
            <input type="hidden" name="action" value="admin" />
            <div class="field">
                <label for="adminName">Your name</label>
                <input type="text" id="adminName" name="displayName" maxlength="80" required="required" />
            </div>
            <div class="field">
                <label for="adminEmail">Email address</label>
                <input type="email" id="adminEmail" name="email" maxlength="150" required="required" />
            </div>
            <div class="field">
                <label for="adminPassword">Password</label>
                <input type="password" id="adminPassword" name="password" required="required" />
                <span class="hint">At least 8 characters.</span>
            </div>
            <div class="field">
                <label for="adminKey">Setup key</label>
                <input type="password" id="adminKey" name="key" required="required" />
            </div>
            <button type="submit" class="btn btn-primary">Create the administrator</button>
        </form>
    <% } %>
</div>

<div class="panel">
    <h2>Last step</h2>
    <p>
        Once you can sign in, open <strong>web.config</strong> and blank out the
        <code>SetupKey</code> value. That closes this page for good.
    </p>
</div>
<% } %>

<% } %>

</asp:Content>
