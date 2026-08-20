import re, os, sys
root = os.getcwd(); out = sys.argv[1]
chunks = ['// AUTO-GENERATED: type-checks every <%= %> / <%# %> expression in the markup.',
          'using System;']
n = 0
for dirpath, dirnames, filenames in os.walk(root):
    if '.git' in dirpath: continue
    for fn in sorted(filenames):
        if not (fn.endswith('.aspx') or fn.endswith('.master')): continue
        path = os.path.join(dirpath, fn)
        text = open(path, encoding='utf-8').read()
        text = re.sub(r'<%--.*?--%>', '', text, flags=re.S)      # server-side comments
        m = re.search(r'Inherits="([^"]+)"', text)
        if not m: continue
        cls = m.group(1)
        exprs = re.findall(r'<%([=#])(.*?)%>', text, flags=re.S)
        if not exprs: continue
        n += len(exprs)
        body = []
        for kind, e in exprs:
            e = e.strip()
            if not e: continue
            body.append('        __Sink(%s);   // %s' % (e, os.path.relpath(path, root)))
        chunks.append('public partial class %s {\n'
                      '    static void __Sink(object __o) { }\n'
                      '    void __CheckMarkup() {\n'
                      '        System.Web.UI.WebControls.RepeaterItem Container = null;\n'
                      '        string line = "";   // loop variable from a <%% foreach %%> block\n'
                      '        if (Container == null && line == null) { }\n'
                      '%s\n    }\n}' % (cls, '\n'.join(body)))
open(out, 'w').write('\n'.join(chunks) + '\n')
print('%d expressions across %d classes' % (n, len(chunks) - 1))
