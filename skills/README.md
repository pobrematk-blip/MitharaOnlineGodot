# Sistema de Árvore de Talentos

Este sistema implementa:
- `TalentNodeResource` — nó de talento/skill
- `TalentTreeResource` — árvore de talentos com nós e dependências
- `TalentTreeComponent` — componente de progresso ligado ao player

## Como usar

1. Crie um `TalentTreeResource` no editor e adicione `TalentNodeResource` para cada nó.
2. Defina `NodeId` único para cada nó.
3. Configure dependências em `Requisitos` e `NivelMinimo`.
4. Atribua o recurso `ArvoreTalentos` em uma `ClasseCustomResource`.
5. O `Player` cria/atualiza automaticamente um `TalentTreeComponent` quando a classe é aplicada.

## Como desbloquear

- Use `TalentTreeComponent.DesbloquearNo(nodeId, nivelAtual)` para abrir um nó.
- Use `TalentTreeComponent.AdicionarPontos(qtd)` para conceder pontos de talento.
- `TalentTreeComponent.ObterNosDisponiveis(nivelAtual)` retorna nós que podem ser desbloqueados.

## Observação

O sistema foi construído para suportar grandes árvores ramificadas e caminhos exclusivos por classe. Cada `ClasseCustomResource` pode ter sua própria `ArvoreTalentos`, garantindo que somente os nós daquela classe sejam acessíveis.
