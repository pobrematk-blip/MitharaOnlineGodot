from pathlib import Path

repl = {
    'Â·': '·',
    'â€”': '—', 'â€œ': '“', 'â€�': '”', 'â€¦': '…', 'â€¢': '•',
    'â˜…': '✓', 'âœ…': '✓', 'âš¡': '✘', 'âš ï¸': '✘',
    'â†ªï¸': '↑', 'â†©ï¸': '↓', 'â¬†ï¸': '★', 'â€™': '’', 'â€˜': '‘', 'â„¢': '™'
}
exts = {'.cs', '.md', '.tres', '.tscn', '.txt'}
base = Path('.')
modified = []


def fix_mojibake(text):
    out = []
    i = 0
    while i < len(text):
        if text[i] == 'Ã' and i + 1 < len(text):
            pair = text[i:i+2]
            try:
                fixed = pair.encode('latin1').decode('utf-8')
                out.append(fixed)
                i += 2
                continue
            except (UnicodeEncodeError, UnicodeDecodeError):
                pass
        out.append(text[i])
        i += 1
    return ''.join(out)

for path in sorted(base.rglob('*')):
    if path.suffix.lower() not in exts:
        continue
    try:
        text = path.read_text(encoding='utf-8')
    except Exception:
        continue
    new = fix_mojibake(text)
    for old, newv in repl.items():
        new = new.replace(old, newv)
    if new != text:
        path.write_text(new, encoding='utf-8')
        modified.append(str(path))
for f in modified:
    print(f)
