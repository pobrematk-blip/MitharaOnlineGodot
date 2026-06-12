# Regras para o Agente

## Sistemas CONGELADOS (não mexer sem autorização explícita do usuário)

- **UI/Menus** (`ui/`, `resources/Inventario/`, `characterSlots/`, `Banco/`) — toda a interface
- **EntityManager** (`scenes/EntityManager.cs`) — spawn e rede de entidades
- **GameNetwork** (`Network/GameNetwork.cs`) — toda comunicação de rede
- **Player** (`characters/Player/`) — personagem do jogador
- **Main.tscn** — cena principal
- **project.godot** — configurações do projeto
- **Qualquer TSCN de UI** (InventarioUI.tscn, CharacterUI.tscn, etc.)

## Sistemas que podem ser alterados SEM autorização

- Nenhum. Sempre perguntar antes.

## Regra geral

- Toda alteração deve ser autorizada explicitamente pelo usuário.
- Se houver dúvida se um arquivo pode ser alterado, pergunte primeiro.
- Não corrigir warnings/erros não solicitados.
- Não otimizar ou refatorar sem pedido.
- Não criar novos arquivos sem pedido.

## Regra CRÍTICA: MMORPG Online

- **TODO** sistema novo deve funcionar exclusivamente através do servidor (`server/`).
- Nada pode ser feito apenas no cliente (local). Toda validação (gold, itens, criação de guilda, banco, etc.) deve passar pelo servidor.
- O cliente envia um pacote → servidor valida → servidor executa → servidor responde.
- **Não criar** sistemas locais `MostrarDialogoLocal` ou qualquer bypass que evite o servidor.
- NPCs devem usar `network_id` e `SendNpcInteract` → servidor envia `OnNpcDialog` com opções.
- Ações de NPC (comprar, vender, criar guilda, abrir banco) devem ser pacotes de rede, não chamadas diretas locais.
- O arquivo `server/Network/GameServer.Npc.cs` (e outros `GameServer.*.cs`) é o lugar correto para handlers de NPC.
- Sempre perguntar antes de criar novos arquivos no servidor (`server/`).
