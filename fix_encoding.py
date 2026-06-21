# Fix corrupted Machado rows in items_data.tsv
import csv, os

tsv_path = r'C:\Users\Wesley\Documents\aprendizado\items_data.tsv'

# Correct Machado data (1033-1043, Normal + Elite)
def row(id, classe, slot, tipo, tag, nivel, nome, desc, f1_min, f1_max):
    return '\t'.join([str(id), classe, slot, tipo, tag, str(nivel), nome, desc, str(f1_min), str(f1_max)])

roldat = [
    (1033, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 1, 'Machado Quebra-Tronco', 'Pesado e simples, feito para quem prefere resolver problemas com um \u00fanico golpe.', 3, 5),
    (1033, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 1, 'Machado Quebra-Tronco', 'Pesado e simples, feito para quem prefere resolver problemas com um \u00fanico golpe.', 4, 6),
    (1034, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 10, 'Machado do Sangue Quente', 'Arma de guerreiros impulsivos, carrega marcas de batalhas curtas e barulhentas.', 13, 17),
    (1034, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 10, 'Machado do Sangue Quente', 'Arma de guerreiros impulsivos, carrega marcas de batalhas curtas e barulhentas.', 15, 20),
    (1035, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 20, 'Machado de Ferro Brutal', 'Sua l\u00e2mina larga foi criada para quebrar escudos e intimidar inimigos.', 22, 29),
    (1035, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 20, 'Machado de Ferro Brutal', 'Sua l\u00e2mina larga foi criada para quebrar escudos e intimidar inimigos.', 26, 34),
    (1036, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 30, 'Machado do Clamor', 'Cada golpe parece anunciar guerra, mesmo quando empunhado por um s\u00f3 guerreiro.', 32, 41),
    (1036, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 30, 'Machado do Clamor', 'Cada golpe parece anunciar guerra, mesmo quando empunhado por um s\u00f3 guerreiro.', 38, 48),
    (1037, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 40, 'Machado do Conquistador', 'Forjado para abrir caminho no campo de batalha, sem sutileza e sem recuo.', 42, 53),
    (1037, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 40, 'Machado do Conquistador', 'Forjado para abrir caminho no campo de batalha, sem sutileza e sem recuo.', 50, 63),
    (1038, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 50, 'Machado Presa-de-Fera', 'Decorado com presas de fera, favorece combatentes que lutam no limite da f\u00faria.', 52, 65),
    (1038, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 50, 'Machado Presa-de-Fera', 'Decorado com presas de fera, favorece combatentes que lutam no limite da f\u00faria.', 61, 77),
    (1039, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 60, 'Machado de A\u00e7o Negro', 'Metal escuro e corte brutal, feito para sobreviver ao peso da pr\u00f3pria viol\u00eancia.', 61, 77),
    (1039, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 60, 'Machado de A\u00e7o Negro', 'Metal escuro e corte brutal, feito para sobreviver ao peso da pr\u00f3pria viol\u00eancia.', 72, 91),
    (1040, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 70, 'Machado do Campe\u00e3o Rubro', 'Carregado por veteranos que venceram mais pela for\u00e7a do que pela sorte.', 71, 89),
    (1040, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 70, 'Machado do Campe\u00e3o Rubro', 'Carregado por veteranos que venceram mais pela for\u00e7a do que pela sorte.', 84, 105),
    (1041, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 80, 'Machado Imperial de Guerra', 'Uma arma de guerra refinada, criada para l\u00edderes que lutam na linha de frente.', 81, 101),
    (1041, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 80, 'Machado Imperial de Guerra', 'Uma arma de guerra refinada, criada para l\u00edderes que lutam na linha de frente.', 96, 119),
    (1042, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 90, 'Machado Ancestral dos Colossos', 'Inspirado nas armas gigantes dos antigos colossos das montanhas.', 90, 113),
    (1042, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 90, 'Machado Ancestral dos Colossos', 'Inspirado nas armas gigantes dos antigos colossos das montanhas.', 106, 133),
    (1043, 'Berserker', 'Arma Principal', 'Machado', 'Normal', 100, 'Machado do Berserker Lend\u00e1rio', 'Um machado comum apenas no nome; nas m\u00e3os certas, parece uma calamidade.', 100, 125),
    (1043, 'Berserker', 'Arma Principal', 'Machado', 'Elite', 100, 'Machado do Berserker Lend\u00e1rio', 'Um machado comum apenas no nome; nas m\u00e3os certas, parece uma calamidade.', 118, 148),
]

new_rows = [row(*r) for r in roldat]

# Read existing TSV
with open(tsv_path, 'r', encoding='utf-8') as f:
    lines = f.read().split('\n')

# Lines 67-88 (0-indexed) are the corrupted Machado rows
# Replace them with correct data
print(f'File has {len(lines)} lines')
print(f'Line 67: {repr(lines[67][:60])}')
print(f'Line 88: {repr(lines[88][:60])}')

# Rebuild: header (0) + existing rows (1-66) + fixed rows + existing (89-end)
fixed = [lines[0]] + lines[1:67] + new_rows + lines[89:]
# Remove trailing empty lines
while fixed and fixed[-1].strip() == '':
    fixed = fixed[:-1]

with open(tsv_path, 'w', encoding='utf-8', newline='\r\n') as f:
    f.write('\n'.join(fixed))
    f.write('\n')

print(f'Written {len(fixed)} lines')
