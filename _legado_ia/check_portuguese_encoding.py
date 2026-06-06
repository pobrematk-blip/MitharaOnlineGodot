from pathlib import Path
import re
pattern = re.compile(r'[ÃâÂ]')
for path in sorted(Path('.').rglob('*')):
    if path.suffix.lower() not in {'.cs', '.md', '.tres', '.tscn', '.txt'}:
        continue
    try:
        text = path.read_text(encoding='utf-8')
    except Exception:
        continue
    if pattern.search(text):
        print(path)
