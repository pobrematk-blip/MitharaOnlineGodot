from pathlib import Path
import re
pattern = re.compile(r'Ã.')
files = [
    Path('characters/inimigos/Inimigo.cs'),
    Path('characters/Player/LpcSpriteFramesBuilder.cs'),
    Path('Editor/ClasseEditorUI.tscn'),
    Path('scenes/SpawnerInimigo.cs'),
]
for path in files:
    text = path.read_text(encoding='utf-8')
    seqs = sorted({text[m.start():m.end()] for m in pattern.finditer(text)})
    print('---', path)
    print(seqs)
