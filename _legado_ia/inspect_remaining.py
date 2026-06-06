from pathlib import Path
files = [
    Path('characters/inimigos/Inimigo.cs'),
    Path('characters/Player/LpcSpriteFramesBuilder.cs'),
    Path('Editor/ClasseEditorUI.tscn'),
    Path('scenes/SpawnerInimigo.cs'),
]
for path in files:
    text = path.read_text(encoding='utf-8')
    print('---', path)
    for i, c in enumerate(text):
        if c == 'Ã':
            start = max(0, i - 20)
            end = min(len(text), i + 20)
            print(repr(text[start:end]))
