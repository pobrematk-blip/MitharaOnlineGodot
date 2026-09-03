# Paperdoll de Armas

As armas devem usar paperdoll separado do corpo e separado da roupa.

Nao usar sprites de personagem inteiro segurando arma para desenhar a arma por cima da roupa. O personagem armado serve apenas como referencia de perfil/animacao. A textura visual do corpo continua sendo o personagem base, e a arma visual vem somente de spritesheets proprios na pasta:

- `Itens/paperdoll armas/`

Cada arma pode ter duas camadas:

- `Frente`: desenha por cima do personagem e da roupa.
- `Tras`: desenha atras do personagem.

Formato atual recomendado, sem nivel:

- `Adaga Frente.png`
- `Adaga Atras.png`
- `Arco Frente.png`
- `Arco Tras.png`
- `Escudo Guardiao Frente.png`
- `Escudo Prist Frente.png`

O visual sem nivel vale do level 1 ate o level 100.

Formatos por nivel tambem continuam aceitos para uso futuro:

- `Adaga Frente Lv1.png`
- `Adaga Tras Lv1.png`
- `Adaga_Frente_Lv1.png`
- `Adaga_Tras_Lv1.png`
- `Frente Adaga Lv1.png`
- `Tras Adaga Lv1.png`

O mesmo vale para `Arco`, `Machado`, `Machadao`, `Espada`, `Cajado`, `Maca`, `Martelo` e `Escudo`.

O sistema tambem tenta usar o nome exato do item. Exemplo:

- `Machado Sombrio Frente Lv10.png`
- `Machado Sombrio Tras Lv10.png`

Camadas usadas no player:

- `ArmaTrasOverlay`: atras do corpo.
- `ArmaFrenteOverlay`: acima da roupa.
- `EscudoTrasOverlay`: atras do corpo.
- `EscudoFrenteOverlay`: acima da roupa.

Camadas usadas em jogadores remotos:

- `RemotePaperdoll_ArmaTras`
- `RemotePaperdoll_ArmaFrente`
- `RemotePaperdoll_EscudoTras`
- `RemotePaperdoll_EscudoFrente`

Essas camadas sincronizam a mesma animacao, frame e velocidade do corpo. Se trocar uma animacao do corpo, testar arma e roupa juntas.

Regra de seguranca: alterar paperdoll de arma nao pode alterar offsets, linhas ou frames das armaduras ja aprovadas.
