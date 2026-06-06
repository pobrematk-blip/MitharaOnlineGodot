from pathlib import Path
import re
pattern = re.compile(r'Ã.')
seen = set()
for path in sorted(Path('.').rglob('*')):
    if path.suffix.lower() not in {'.cs', '.md', '.tres', '.tscn', '.txt'}:
        continue
    try:
        text = path.read_text(encoding='utf-8')
    except Exception:
        continue
    for m in pattern.finditer(text):
        seen.add(text[m.start():m.end()])
for s in sorted(seen):
    print(repr(s))
