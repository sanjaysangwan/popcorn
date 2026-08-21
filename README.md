# Popcorn — a family movie list

A small private website where everyone in the family adds the films they have
seen, gives each one a score out of ten, and sees the household's verdict as
the average of everybody's scores.

Built to drop straight onto a **DiscountASP.NET** Windows plan: ASP.NET Web
Forms as a *Web Site* project (no compilation step — upload the files by FTP
and IIS compiles them), with a **Microsoft Access** database in `App_Data` and
film details pulled from the free **OMDb** API.

## What it does

| Asked for | How it works |
| --- | --- |
| Accounts need administrator approval | `Register.aspx` creates the account with `IsApproved = false`. Nothing is visible until an administrator approves it on **Members**, and the side pane shows them a badge with the number waiting. |
| Side navigation that wraps to the top on mobile | `Site.master` renders one `<aside class="sidenav">`. Above 900px it is a sticky left column; below that the same markup becomes a band across the top whose links wrap onto as many rows as the screen needs. No duplicate markup, no JavaScript. |
| Members add movies and rate them 1–10 | **Add a Movie** searches OMDb and stores the result; the star picker is ten radio buttons styled with CSS, so it works without JavaScript. |
| Poster, synopsis and cast from a free API | `App_Code/OmdbClient.cs` calls OMDb once when a film is added and caches the details in the database, so viewing a film costs no API calls. The free key allows 1,000 lookups a day. |
| Microsoft Access database | `App_Code/Db.cs` talks to Jet/ACE over `System.Data.OleDb` with parameterised commands throughout. |
| Everyone sees other members' films and adds their own rating | The **What's New** dashboard lists films somebody else added that you have not scored, each with its own star form. |
| Final rating is the family average | Nothing is ever stored as a "final" score — `MovieRepository.Assemble` averages the `Ratings` rows on every read, so it is always current. |
| New films to rate on sign-in | Signing in lands on **What's New**, which is exactly that list. |

## The pages

| Page | Who | What |
| --- | --- | --- |
| `Setup.aspx` | one-off, setup key | Creates the Access file, its tables and the first administrator |
| `Register.aspx` | anyone | Requests an account (pending until approved) |
| `Login.aspx` | anyone | Sign in |
| `Default.aspx` | members | What's new from the family, waiting for your stars |
| `AddMovie.aspx` | members | OMDb search, or add by hand |
| `Movies.aspx` | members | The library, searchable and sortable, split into still-to-watch and watched |
| `MovieDetails.aspx` | members | Poster, synopsis, cast, every member's rating, yours |
| `Tags.aspx` | members | Every category, with counts; renaming and deleting are the administrator's |
| `MyRatings.aspx` | members | Everything you have scored |
| `Family.aspx` | members | Who is on the list and how active they are |
| `Account.aspx` | members | Change your name or password |
| `Admin/Users.aspx` | administrators | Approve, switch off, promote, delete members |

## How the data fits together

```
Users ──< Ratings >── Movies >── MovieTags ──< Tags
```

* `Ratings` has a unique index on `(MovieId, UserId)`, so a member has exactly
  one score per film and rating again updates it rather than stacking up.
* A film's headline number is `AVG(Stars)` across that film's rows.
* **Categories** live in their own table rather than a comma-separated column,
  so "Christmas" typed by one member is the same shelf as "christmas" typed by
  another, films can be browsed by category, and each one can be counted. A
  site starts with Comedy, Sad, Romantic, Family, Christmas, SciFi, Historical
  and Real Life Story; anyone can invent more while tagging a film. A new film
  is put onto any shelf whose name matches a genre OMDb reported — matching
  existing categories only, so the family's own shelf stays theirs.
* Marking a film **watched** retires it from the default library view without
  touching its ratings — it stays under the "Already watched" filter and can
  still be rated. Whoever added a film, and any administrator, can move it
  either way, the same rule as deleting one.
* Deleting a member or a film clears their ratings too — Access has no
  cascading deletes to lean on, so the repositories do it explicitly.
* Every query is a plain `SELECT` over one table. Access accepts or rejects
  correlated sub-selects, aggregates in the field list and ordering by a
  computed alias depending on whether Jet or ACE is behind it, and a rejection
  surfaces as a generic error far from its cause. The averages and the
  viewer's own score are assembled in memory instead, which for a family list
  costs nothing and is covered by tests.

## Security

* Passwords are PBKDF2 (25,000 iterations, per-user salt), never stored or
  logged in the clear.
* Forms authentication cookies carry only the user id; the account is
  re-read from the database on every request, so revoking access takes effect
  immediately rather than when the cookie expires.
* Every form posts a per-session anti-forgery token that is checked before
  anything is written.
* All SQL goes through OleDb parameters; all output is HTML-encoded.
* `App_Data` is a folder ASP.NET never serves from, and carries its own
  `web.config` denying every request on top of that, so the database file
  cannot be downloaded.

## Getting it running

See **[DEPLOYMENT.md](DEPLOYMENT.md)** for the DiscountASP.NET walkthrough. The
short version:

1. Get a free OMDb key at <https://www.omdbapi.com/apikey.aspx>.
2. Put it in `web.config` as `OmdbApiKey`, and set a `SetupKey` of your own.
3. Upload everything by FTP.
4. Open `/Setup.aspx`, create the database and the administrator account.
5. Blank out `SetupKey` in `web.config`.

## Layout

```
/                     the pages
/Admin                administrator-only pages
/App_Code             compiled at runtime by IIS — data access, models, helpers
/App_Data             the Access database lives here (never committed)
/Content              site.css
Site.master           shell: the side navigation pane
web.config            connection string, OMDb key, authentication
```
