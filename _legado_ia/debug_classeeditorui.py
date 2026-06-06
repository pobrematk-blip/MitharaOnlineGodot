from pathlib import Path
path=Path('Editor/ClasseEditorUI.tscn')
text=path.read_text(encoding='utf-8')
print('length', len(text))
for i,c in enumerate(text):
    if c == 'Ã':
        print('index', i, 'char', repr(c), 'ord', ord(c))
        start = max(0, i-10)
        end = min(len(text), i+10)
        print(repr(text[start:end]))
        for j in range(start, end):
            print(j, repr(text[j]), ord(text[j]))
        break
