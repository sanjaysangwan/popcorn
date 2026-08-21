# Deploying to DiscountASP.NET

Nothing here needs Visual Studio or a build server. This is an ASP.NET *Web
Site* project: IIS compiles `App_Code` and the `.aspx.cs` files itself the
first time a page is requested, so deployment is "copy the files up".

---

## 1. Before you upload

### Get an OMDb key (free)

<https://www.omdbapi.com/apikey.aspx> — pick the free tier, confirm the email,
and you get a key good for 1,000 lookups a day. The site works without one,
but nobody gets posters or synopses.

### Edit `web.config`

```xml
<add key="OmdbApiKey" value="your-key-here" />
<add key="SetupKey"   value="a-long-random-string-only-you-know" />
<add key="SiteName"   value="Popcorn" />
```

`SetupKey` is what guards `Setup.aspx`. Pick something long, and blank it out
once the site is installed.

### Choose the right database provider

This is the one decision that actually matters on shared hosting, because it
decides which Access file format you can use.

| File | Provider in `web.config` | Requirement |
| --- | --- | --- |
| `FamilyMovies.mdb` (Access 97–2003) | `Microsoft.Jet.OLEDB.4.0` | The application pool must run **32-bit**. Jet has no 64-bit build. |
| `FamilyMovies.accdb` (Access 2007+) | `Microsoft.ACE.OLEDB.12.0` | The ACE redistributable must be installed on the server. |

The shipped `web.config` is set up for `.mdb` + Jet, with the ACE version
commented out just below it. If you would rather use `.accdb`, comment out the
Jet entry and uncomment the ACE one.

In the DiscountASP.NET Control Panel, **Web Options → Application Pool** has
the setting for 32-bit mode (it may be called "Enable 32-bit applications").
Turn it on if you are using Jet. If you are unsure which providers your server
has, open a support ticket and ask — they answer this one often.

---

## 2. Upload

FTP the whole folder to your site root (`/wwwroot`). Upload everything except
the `.git` folder:

```
Default.aspx          Login.aspx         Register.aspx      Pending.aspx
Logout.aspx           Movies.aspx        MovieDetails.aspx  AddMovie.aspx
MyRatings.aspx        Family.aspx        Account.aspx       Setup.aspx
Error.aspx            Site.master        Site.master.cs     Global.asax
web.config            (and every matching .aspx.cs file)
Admin/                App_Code/          App_Data/          Content/
```

Keep the folder structure exactly as it is — `App_Code` and `App_Data` are
names IIS treats specially.

---

## 3. Make `App_Data` writable

This is the step that trips most deployments up, so do it before running setup.

Access needs to write the database file **and** a lock file (`.ldb`/`.laccdb`)
next to it, so the *folder* needs write permission, not just the file. FTP
cannot grant that — file permissions are an NTFS setting, and on shared
hosting they are set from the hosting control panel.

On DiscountASP.NET this lives in the Control Panel under the file/permission
tools, not in your FTP client. Grant **Modify** (read + write) on `App_Data`.

You do not have to guess which account to grant it to. Open `Setup.aspx` and
read the **This server** panel — the **Runs as** line names the exact Windows
account the site executes as, and the **App_Data** line writes and deletes a
probe file, reporting either `writable` or the precise failure including that
account name.

If the permission cannot be granted for some reason, the other way out is to
put the database somewhere that already is writable: change `Data Source` in
the connection string to that folder's path. Nothing else in the site cares
where the file lives.

---

## 4. Run setup

Browse to `https://yoursite.com/Setup.aspx`. It shows you:

* which provider the connection string names,
* where it expects the database file and whether it exists,
* whether `App_Data` is writable, and which Windows account the site runs as,
* whether the tables are there,
* whether the OMDb key is configured,
* whether the process is 32- or 64-bit, whether ADOX is registered, and every
  OLE DB provider actually installed on the server.

Then:

1. **Create the database** — type the `SetupKey` and submit. This creates the
   `.mdb`/`.accdb` through ADOX and adds the `Users`, `Movies` and `Ratings`
   tables. Running it twice is harmless; it skips whatever already exists.
2. **Create the administrator** — name, email, password and the setup key
   again. You are signed straight in.

### If the database file cannot be created

Some hosts do not register ADOX. Setup says so plainly. In that case:

1. On your PC, create an empty database in Access named exactly
   `FamilyMovies.mdb` (or `.accdb` if you switched providers).
2. Optionally run the statements in `App_Data/Schema.sql` — one at a time, in
   Access's SQL view.
3. FTP it into `App_Data`.
4. Re-run `Setup.aspx`. It sees the file, adds any missing tables, and carries
   on.

---

## 5. Close setup

Edit `web.config` and blank the setup key:

```xml
<add key="SetupKey" value="" />
```

`Setup.aspx` refuses to do anything from then on. (It also locks itself to
signed-in administrators as soon as the site has accounts, so the key is the
second lock, not the only one.)

---

## 6. Invite the family

Send everyone the address. They use **Request an account**, and you approve
them under **Members** — the side pane shows a badge with how many are waiting.
Until you approve someone they cannot see a single film.

---

## Backing up

The whole database is one file: `App_Data/FamilyMovies.mdb`. FTP a copy down
now and then. Nobody is signed in at 3am, so any time is a good time.

---

## Troubleshooting

**"The 'Microsoft.Jet.OLEDB.4.0' provider is not registered on the local
machine."**
The application pool is running 64-bit. Either switch it to 32-bit in the
Control Panel, or move to `.accdb` and the ACE provider.

**"Multiple-step OLE DB operation generated errors. Check each OLE DB status
value, if available. No work was done."**
This comes from the database *file* creation step, and it is a generic OLE DB
failure rather than a specific one. In order of likelihood:

1. `App_Data` is not writable by the account on the **Runs as** line — see
   step 3. This is by far the most common cause, and Setup now checks it
   before it even calls ADOX, so if you are still seeing this message the
   probe passed and the cause is one of the two below.
2. The application pool is 64-bit and the connection string names Jet, which
   only exists as a 32-bit provider. The **This server** panel flags this
   explicitly. Switch the pool to 32-bit, or move to ACE and `.accdb`.
3. The provider in the connection string is not installed. The **This server**
   panel lists every OLE DB provider registered for this process — the one
   you named has to appear there.

**"Data type mismatch in criteria expression"**
A parameter reached Access as the wrong type. Every parameter is now given an
explicit OLE DB type rather than an inferred one, which is what caused this:
a `DateTime` was inferred as `DBTimeStamp` and rejected by a `DATETIME`
column. Error messages from the database now name the statement that produced
them, and the **What the database actually contains** panel on `Setup.aspx`
lists the column types Jet or ACE really created, so a column that came out
wrong is visible rather than inferred.

**"Operation must use an updateable query" / "cannot open for writing"**
`App_Data` is not writable. Fix the folder permission — see step 3.

**Posters and synopses never appear**
Check `OmdbApiKey` in `web.config`. If it is right, the host may block
outbound HTTP; `AddMovie.aspx` reports the exact error it got. `OmdbClient`
already retries over plain HTTP when HTTPS fails.

**A yellow ASP.NET error page instead of the site**
Set `<customErrors mode="Off" />` in `web.config` temporarily to see the real
message, then put it back to `RemoteOnly`.

**Everything redirects to `/Setup.aspx`**
The tables are not there yet, or the database cannot be opened at all. The
setup page itself will say which.
