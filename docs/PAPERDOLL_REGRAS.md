# Paperdoll - Estado Correto

Este documento registra o estado correto dos paperdolls depois da correcao validada em jogo.

## Regra principal

Os paperdolls atuais estao corretos. Nao alterar o sistema de alinhamento/posicao das roupas sem testar em jogo e comparar com o personagem usando a arma correspondente.

As roupas acompanham a animacao do personagem armado, mas o spritesheet do paperdoll continua sendo lido como frames de 64x64.

## Classes e animacoes corretas

- Arqueiro usa armadura media e animacao de arco.
- Assassino usa armadura media e animacao de adaga. Esta classe esta correta e nao deve ser mexida sem pedido direto.
- Mago usa armadura leve e animacao de cajado. Somente o Mago deve usar os frames antigos do `cajado_attack` no perfil `Cajado.tres`.
- Clerigo usa armadura leve e animacao de maca/escudo.
- Berserker usa armadura pesada e animacao de machado de guerra.
- Guardiao usa armadura pesada e animacao de espada/escudo.

Mesmo quando duas classes compartilham o mesmo tipo de armadura, a animacao de ataque e diferente:

- Leve: Mago e Clerigo usam ataques diferentes.
- Media: Arqueiro e Assassino usam ataques diferentes.
- Pesada: Berserker e Guardiao usam ataques diferentes.

## Regra especifica do Mago

Quando ajustar o Mago, mexer somente no perfil de cajado:

- `characters/Player/AnimationProfiles/Cajado.tres`

O paperdoll do Mago deve acompanhar a mesma animacao de `cajado_attack`/`mago_attack` usada pelo corpo.

Frames corretos atuais do Cajado:

- `AttackUpRow = 18`
- `AttackLeftRow = 19`
- `AttackDownRow = 20`
- `AttackRightRow = 21`
- `AttackUpFrames = 8`
- `AttackLeftFrames = 8`
- `AttackDownFrames = 8`
- `AttackRightFrames = 8`

Nao alterar Assassino, Arqueiro, Clerigo, Berserker ou Guardiao ao corrigir o Mago.

## Implementacao validada

Arquivo principal:

- `characters/Player/Player.cs`

Para os paperdolls de Clerigo, Berserker e Guardiao, o caminho validado e usar o construtor direto:

- `LpcSpriteFramesBuilder.Construir(sheet, prefixoAtaque)`

Isso evita deslocamento da roupa, inversao errada de direcao e frames fora do corpo.

## Nao fazer novamente

Nao recriar frames de paperdoll em textura maior para tentar acompanhar sprites armados de 128x128 ou 192x192.

Especialmente nao usar esse tipo de correcao para:

- `machado_guerra`
- `espada_escudo`
- `maca_escudo`

Essas tres ficaram corretas somente quando voltaram ao construtor direto do paperdoll.

## Checklist antes de mexer

Antes de qualquer alteracao futura em paperdoll:

1. Testar Berserker atacando com machado.
2. Testar Guardiao atacando com espada e escudo.
3. Testar Clerigo atacando com maca e escudo.
4. Testar Assassino atacando com adaga.
5. Testar Mago atacando com cajado.
6. Confirmar se idle, walk e death continuam acompanhando o corpo.
7. Confirmar se outro player ve a mesma roupa replicada.

Se uma classe estiver correta, nao mexer nela.
