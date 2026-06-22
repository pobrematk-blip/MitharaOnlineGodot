# Regras para o Agente

## Sistemas LIBERADOS (pode mexer livremente)

- `ui/GuildCreateUI.cs` — criação de guilda
- `ui/GuildCreateUI.tscn` — cena de criação de guilda
- `ui/DialogUI.cs` — diálogos de NPC
- `ui/GuildUI.cs` — painel de guilda
- `characterSlots/OverheadUI.cs` — overhead do personagem
- `resources/Projetil/ProjetilArqueiro.tscn` — flecha
- `Network/GameNetwork.cs` — comunicação de rede
- `Network/Handlers/GameNetwork.Guild.cs` — handlers de guilda
- `server/` — servidor (criar/alterar arquivos)

## Regra geral

- Usuário deu permissão TOTAL para tudo relacionado a arrumar o sistema de guildas, diálogos, emblemas e flecha.
- Se precisar mexer em outra área, pergunte antes.

## Depurador (Godot Output)

- Quando eu pedir "leia e corrija erros" ou "olhe o depurador", você DEVE ler o output do Godot (console/debug) e corrigir os erros listados.
- Temos logs disponíveis no console do Godot. Use-os para diagnosticar problemas.

## Y-Sort (Ordenação por Profundidade)

- **RESOLVIDO**: Player, NPCs e mobs agora estão todos na Main.tscn como filhos diretos de `World`, que tem `y_sort_enabled = true`.
- Tilemaps base estão em `z_index = -1`, entidades em `z_index = 0`, tilemaps de cima em `z_index = 5`.
- Player fica atrás do mob/NPC quando está acima (Y menor) e na frente quando está abaixo (Y maior).

## Regra CRÍTICA: MMORPG Online

- **TODO** sistema novo deve funcionar exclusivamente através do servidor (`server/`).
- Nada pode ser feito apenas no cliente (local). Toda validação (gold, itens, criação de guilda, banco, etc.) deve passar pelo servidor.
- O cliente envia um pacote → servidor valida → servidor executa → servidor responde.
- **Não criar** sistemas locais `MostrarDialogoLocal` ou qualquer bypass que evite o servidor.
- NPCs devem usar `network_id` e `SendNpcInteract` → servidor envia `OnNpcDialog` com opções.
- Ações de NPC (comprar, vender, criar guilda, abrir banco) devem ser pacotes de rede, não chamadas diretas locais.
- O arquivo `server/Network/GameServer.Npc.cs` (e outros `GameServer.*.cs`) é o lugar correto para handlers de NPC.
- Sempre perguntar antes de criar novos arquivos no servidor (`server/`).

## PostgreSQL (Banco de Dados)

- **Database engine**: PostgreSQL 17 via `Npgsql` (NuGet)
- **Host**: `localhost:5432`
- **Database**: `mithara_db`
- **User**: `mithara` / senha: `Tk7142536@`
- **Serviço Windows**: `postgresql-x64-17` (inicia automaticamente)
- **Config**: `server/server_config.json` — campos `PgHost`, `PgPort`, `PgDatabase`, `PgUser`, `PgPassword`
- **Migration de dados**: SQLite (`data/mithara.db`) → PostgreSQL via `DatabaseMigration.cs`
- **Tools CLI**:
  - `"C:\Program Files\PostgreSQL\17\bin\psql.exe" -U mithara -d mithara_db`
  - `"C:\Program Files\PostgreSQL\17\bin\pg_ctl.exe" reload -D "C:\Program Files\PostgreSQL\17\data"`
- **`rank` NÃO é reservado no PostgreSQL**, diferente do MySQL — não precisa de backticks.
- **Sequences**: `accounts_id_seq`, `characters_id_seq`, `guilds_id_seq` — usar `setval()` se importar dados manuais.

## Servidor Externo / DNS / Launcher

- DNS oficial de teste externo: `mitharaonline.duckdns.org`
- Porta do jogo: `7777`
- Protocolo do jogo: UDP via LiteNetLib/ENet-like client (`NetClient`); liberar/redirecionar UDP `7777`.
- IP publico confirmado em 22/06/2026: `179.104.69.12` apontado pelo DuckDNS.
- O cliente deve conectar usando `server_endpoint.json` ao lado do executavel:
  ```json
  {
    "host": "mitharaonline.duckdns.org",
    "port": 7777
  }
  ```
- `project.godot` tambem deve manter `network/server_host="mitharaonline.duckdns.org"` e `network/server_port=7777`.
- Se amigos receberem "Desconectado do servidor", verificar nesta ordem:
  1. Servidor `Mithara.Server` rodando.
  2. `netstat -ano | Select-String ':7777'` mostrando UDP `0.0.0.0:7777`.
  3. `Resolve-DnsName mitharaonline.duckdns.org` apontando para o IP publico atual.
  4. DuckDNS atualizado se o IP publico mudar.
  5. Port forwarding do roteador para o IP LAN correto da maquina do servidor.
  6. Firewall do Windows liberando entrada UDP `7777`.
- O launcher busca a release mais recente no GitHub:
  `pobrematk-blip/MitharaOnlineGodot`
- Release publicada com DNS DuckDNS: `v0.1.7`.
- Para atualizar endpoint sem reexportar o jogo inteiro, atualizar `server_endpoint.json` na pasta de build usada pelo launcher, rodar `tools/Build-MitharaUpdate.ps1` com nova versao e publicar nova release com `manifest.json` e `MitharaOnline_Update.zip`.

## Regras de Criação de Itens (MITTHARA ONLINE)

### Padrão Geral
- **Tipo**: Equipamento Normal
- **Obtido em**: monstros comuns e mapas abertos
- **Atributos Fixos**: valores são aleatórios dentro da faixa definida (Min/Max)
- **Afixos**: NÃO pré-definidos nos itens. Sorteados apenas quando o item é obtido.
- **Sem duplicata**: mesmo afixo não pode aparecer duas vezes no mesmo item.
- **Pool de Afixos** depende do tipo de arma.

### Quantidade de Afixos por Nível
- Níveis 1 a 25: 0 a 2 afixos
- Níveis 30 a 45: 2 a 4 afixos
- Níveis 50 a 100: 3 a 6 afixos

### Armas do Arqueiro
- **Arcos**: Atributos Fixos = BaseAttack, Destreza
- **Arcos**: Pool = ChanceCritica, DanoCriticoBonus, Precisao, VelocidadeAtaque, Agilidade, PenetracaoArmadura, Evasao
- **Aljavas**: Atributos Fixos = Destreza, Agilidade
- **Aljavas**: Pool = ChanceCritica, DanoCriticoBonus, Precisao, VelocidadeAtaque, Evasao, VelocidadeMovimento, PenetracaoArmadura

### Armas do Assassino
- **Adaga Principal**: Atributos Fixos = BaseAttack, Destreza
- **Adaga Principal**: Pool = ChanceCritica, DanoCriticoBonus, VelocidadeAtaque, Evasao, Precisao, Agilidade, RouboVida, PenetracaoArmadura
- **Adaga Secundária**: Atributos Fixos = Destreza, Agilidade
- **Adaga Secundária**: Pool = ChanceCritica, VelocidadeAtaque, Evasao, Precisao, RouboVida, RouboMana, VelocidadeMovimento, PenetracaoArmadura

### Armas do Berserker
- **Machado Duas Mãos**: Atributos Fixos = BaseAttack, Forca
- **Machado Duas Mãos**: Pool = ChanceCritica, DanoCriticoBonus, PenetracaoArmadura, RouboVida, Tenacidade, Agilidade, Precisao
- **Bumerangue**: Atributos Fixos = Forca, Agilidade
- **Bumerangue**: Pool = ChanceCritica, Precisao, VelocidadeAtaque, PenetracaoArmadura, Evasao, VelocidadeMovimento

### Armas do Guardião
- **Espada**: Atributos Fixos = BaseAttack, Forca
- **Espada**: Pool = ChanceCritica, Tenacidade, PenetracaoArmadura, Precisao, Agilidade, RouboVida
- **Escudo**: Atributos Fixos = Defense, Forca, Agilidade
- **Escudo**: Pool = Tenacidade, Hp, DefesaFisica, Evasao, RegeneracaoVida, Precisao

### Armas do Mago
- **Cajado**: Atributos Fixos = BaseAttack, Inteligencia
- **Cajado**: Pool = DanoMagico, RouboMana, RegeneracaoMana, ReducaoCooldown, Precisao, ChanceCritica, DanoCriticoBonus
- **Orbe**: Atributos Fixos = Inteligencia, Destreza
- **Orbe**: Pool = RouboMana, RegeneracaoMana, ReducaoCooldown, ChanceCritica, DanoCriticoBonus, Precisao, Mana

### Armas do Clérigo
- **Martelo**: Atributos Fixos = BaseAttack, Forca, Inteligencia
- **Martelo**: Pool = RouboVida, RouboMana, RegeneracaoVida, RegeneracaoMana, Tenacidade, Precisao, ChanceCritica
- **Escudo Sagrado**: Atributos Fixos = Defense, Forca, Inteligencia
- **Escudo Sagrado**: Pool = Tenacidade, RegeneracaoVida, RegeneracaoMana, Hp, Mana, ReducaoCooldown, Evasao

### Numeração de IDs dos Itens
- Arcos: 1001-1021 (níveis 1-100, a cada 5)
- Aljavas: 1051-1071 (níveis 1-100, a cada 5)
- Adagas: 2001-2021, AdagasSecundarias: 2051-2071
- Machados: 3001-3021, Bumerangues: 3051-3071
- Espadas: 4001-4021, Escudos: 4051-4071
- Cajados: 5001-5021, Orbes: 5051-5071
- Martelos: 6001-6021, EscudosClerigo: 6051-6071
- Consumíveis/Pergaminhos: 101-199
- Itens de Missão/Especiais: 200-999

### Estrutura de Pastas (organizada por TipoEquipamento)
- `Itens/Armas/` — Armas (arcos, adagas, machados, espadas, cajados, martelos)
- `Itens/Escudos/` — Escudos (escudos, aljavas, escudos sagrados)
- `Itens/Capacetes/` — Capacete
- `Itens/Peitorais/` — Peitoral
- `Itens/Cintos/` — Cinto
- `Itens/Luvas/` — Luvas
- `Itens/Calcas/` — Calça
- `Itens/Botas/` — Botas
- `Itens/Colares/` — Colar
- `Itens/Aneis/` — Anel
- `Itens/Brincos/` — Brinco
- `Itens/Runas/` — Runa
- `Itens/Asas/` — Asa
- `Itens/Montarias/` — Montaria
- `Itens/Pets/` — Pet
- `Itens/Skins/` — Skin
- `Itens/Consumiveis/` — poções, pergaminhos
- `Itens/Feiticos/` — Feitiço
- `Itens/Moedas/` — Moeda
- `Itens/Outros/` — fallback para tipos não categorizados
- `Itens/Recursos/` — minério, madeira, etc. (scripts de coleta)
- `Itens/Incones/` — ícones dos itens
- `Itens/Emblema de Guild/` — emblemas de guildas

### Ao criar itens
1. Criar arquivo .tres na pasta correta
2. Definir apenas **Atributos Fixos** no .tres (com Min/Max)
3. Definir **PoolDeAfixos** no .tres (string separada por vírgula)
4. Registrar no servidor com `AffixPool` em `ItemDefinitions`
5. Nome do arquivo = nome do item sem espaços + .tres

## Regras de Drops: Monstro Elite x Item Elite

- **NUNCA confundir categoria do monstro com categoria do item.** Um monstro Elite não derruba automaticamente um item Elite.
- Mobs normais **não derrubam armas nem armaduras**. Eles derrubam apenas poções de HP, poções de mana e gold.
- Somente mobs/monstros Elite derrubam equipamentos Normais (armas e armaduras).
- Mob Elite de nível 1 a 9 derruba equipamento Normal de nível 1.
- Mob Elite de nível 10 a 19 derruba equipamento Normal de nível 10.
- Continuar a progressão por faixa: nível 20 a 29 → item nível 20, 30 a 39 → item nível 30, e assim por diante.
- Item do tipo **Normal** pode sortear somente as raridades: Comum, Incomum e Raro.
- Itens do tipo **Elite** são exclusivos de Boss de mapa, World Boss e Boss de dungeon.
- Item do tipo **Elite** pode sortear somente: Comum, Incomum, Épico, Lendário e Mítico. Conforme a regra atual, item Elite não sorteia Raro.
- Equipamentos iniciais de personagem são sempre do tipo Normal e raridade Comum.
- Toda seleção de drop, tipo do item, nível e raridade deve ser validada e sorteada exclusivamente pelo servidor.

## Idioma: SEMPRE português

- Quando der opções ou perguntar algo para o usuário, use SEMPRE português.
- O usuário não sabe outro idioma.

## Regra ABSOLUTA: SOMENTE ONLINE

- **O jogo é 100% online. Não existe nada offline.** Tudo deve funcionar exclusivamente pelo servidor.
- O cliente é APENAS um visualizador/input: envia pacote → servidor valida → servidor executa → servidor responde.
- Nada de `AplicarProgressaoSalva`, saves locais de progresso, lógica de XP/level local, ou qualquer sistema que funcione sem o servidor. **Tudo** vem do servidor via pacotes (`S2C_EnterWorld`, `S2C_GainExp`, `S2C_LevelUp`, etc.).
- Se um sistema está quebrado no modo online, a correção NUNCA deve ser fazer ele funcionar localmente. A correção deve sempre ser no servidor ou na comunicação cliente-servidor.
- Diretórios/arquivos exclusivamente offline (ex: saves locais, `AplicarProgressaoSalva`) devem ser ignorados/removidos.
