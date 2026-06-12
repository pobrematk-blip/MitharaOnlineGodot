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
