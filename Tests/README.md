# Checking the site without Windows

This is a Web Site project, so nothing is compiled until IIS gets hold of it.
That is convenient for deployment and inconvenient for catching a typo, so
`check.sh` does the same job offline with mono.

```sh
apt-get install -y mono-devel     # once
sh Tests/check.sh                 # from the repository root
```

It does three things:

1. **Compiles the C#** — everything in `App_Code`, every `.aspx.cs`, and
   `Site.master.cs`. `gen_stubs.py` reads the markup and writes the control
   fields (`phEmpty`, `rptMovies`, …) that the ASP.NET page compiler would
   normally generate, so the code-behind compiles exactly as it will on the
   server.

2. **Compiles the markup expressions** — `gen_exprcheck.py` pulls every
   `<%= … %>` and `<%# … %>` out of the `.aspx` and `.master` files and
   compiles them inside their own page class. A misspelled property in the
   markup is otherwise a runtime surprise on the live site.

3. **Runs `Smoke.cs`** — the parts that need no database: password hashing,
   the star widget, the family-average formatting, and HTML escaping.

What it cannot check is the SQL, which only Jet/ACE can parse, or the
generated page classes themselves. Load the site once after deploying.
