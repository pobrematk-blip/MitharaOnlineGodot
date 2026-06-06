from pathlib import Path
import re
pattern=re.compile(r'(Ã.|â.)')
paths=[
    'PersonagemEscolhido.cs',
    'Banco/BancoComponent.cs',
    'characters/inimigos/Inimigo.cs',
    'skills/TalentTreeResource.cs',
]
for p in paths:
    path=Path(p)
    if not path.exists():
        print('missing', p)
        continue
    text=path.read_text(encoding='utf-8')
    print('---', p)
    for m in pattern.finditer(text):
        start=max(0,m.start()-30)
        end=min(len(text), m.end()+30)
        print(repr(text[start:end]))
