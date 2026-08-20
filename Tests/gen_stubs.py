import re, os, sys

root = os.getcwd()
out = sys.argv[1]

typemap = {
    'placeholder': 'System.Web.UI.WebControls.PlaceHolder',
    'repeater':    'System.Web.UI.WebControls.Repeater',
    'literal':     'System.Web.UI.WebControls.Literal',
    'label':       'System.Web.UI.WebControls.Label',
    'panel':       'System.Web.UI.WebControls.Panel',
    'contentplaceholder': 'System.Web.UI.WebControls.ContentPlaceHolder',
}

pages = []
for dirpath, dirnames, filenames in os.walk(root):
    if '.git' in dirpath: continue
    for fn in filenames:
        if not (fn.endswith('.aspx') or fn.endswith('.master')): continue
        path = os.path.join(dirpath, fn)
        text = open(path, encoding='utf-8').read()
        m = re.search(r'Inherits="([^"]+)"', text)
        if not m: continue
        cls = m.group(1)
        ctrls = {}
        for tag, attrs in re.findall(r'<asp:(\w+)([^>]*)', text):
            if 'runat="server"' not in attrs: continue
            idm = re.search(r'\bID="(\w+)"', attrs)
            if not idm: continue
            t = typemap.get(tag.lower())
            if not t: continue
            ctrls[idm.group(1)] = t
        pages.append((cls, ctrls))

lines = ['// AUTO-GENERATED stub of the control fields the ASP.NET page compiler emits.',
         '// Used only to type-check the code-behind offline; not part of the site.']
for cls, ctrls in pages:
    lines.append('public partial class %s {' % cls)
    for cid, ctype in sorted(ctrls.items()):
        lines.append('    protected global::%s %s;' % (ctype, cid))
    lines.append('}')
open(out, 'w').write('\n'.join(lines) + '\n')
print('\n'.join('%s: %d controls' % (c, len(d)) for c, d in pages))
