#!/bin/sh
#
# Offline check for a site that is only ever compiled by IIS on Windows.
#
# 1. Type-checks App_Code, every code-behind and the master page.
# 2. Type-checks every <%= %> / <%# %> expression in the .aspx and .master
#    markup, by pulling them out and compiling them against their page class.
# 3. Runs the smoke tests over the logic that needs no database.
#
# Needs mono:  apt-get install -y mono-devel
# Run from the repository root:  sh Tests/check.sh
#
set -e

MONO_LIB=${MONO_LIB:-/usr/lib/mono/4.5}
OUT=${OUT:-./.check}
mkdir -p "$OUT"

REFS="-r:$MONO_LIB/System.dll -r:$MONO_LIB/System.Core.dll -r:$MONO_LIB/System.Data.dll \
-r:$MONO_LIB/System.Web.dll -r:$MONO_LIB/System.Web.Extensions.dll \
-r:$MONO_LIB/System.Configuration.dll -r:$MONO_LIB/System.Web.ApplicationServices.dll"

echo "==> generating the control fields the ASP.NET page compiler would emit"
python3 Tests/gen_stubs.py "$OUT/Designer.g.cs"

echo "==> extracting markup expressions"
python3 Tests/gen_exprcheck.py "$OUT/ExprCheck.g.cs"

echo "==> compiling the site"
mcs -target:library -out:"$OUT/site.dll" -nowarn:0169,0414,0649,0219 $REFS \
    App_Code/*.cs *.aspx.cs Admin/*.aspx.cs Site.master.cs \
    "$OUT/Designer.g.cs" "$OUT/ExprCheck.g.cs"

echo "==> smoke tests"
mcs -out:"$OUT/smoke.exe" -nowarn:0169,0414,0649 $REFS App_Code/*.cs Tests/Smoke.cs
mono "$OUT/smoke.exe"
