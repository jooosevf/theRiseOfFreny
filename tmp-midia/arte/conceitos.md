# Conceitos de arte v1: cosméticos, armas e inimigos

Primeira leva de conceitos (José, 2026-11-01). **Nada aqui está no jogo ainda**: são propostas desenhadas na grade e na paleta reais do jogo, para escolher o que entra.

- Gerador: `Tools/ArtGen/concepts_v1.py` (determinístico; `python Tools/ArtGen/concepts_v1.py <raiz do repo>` refaz tudo igual)
- Paleta: `Tools/ArtGen/freny_palette.py` (rampas nomeadas), `Tools/ArtGen/freny_palette.gpl` (Aseprite/GIMP), `docs/arte/paleta.png`
- Inspirações e princípios: `docs/arte/inspiracoes.md`

## Folhas

| Folha | O que tem |
|---|---|
| `docs/arte/conceitos/folha_geral.png` | visão geral: os sprites atuais como referência, todos os conceitos e a escala relativa (Freny, inimigos novos e Golem no mesmo chão) |
| `docs/arte/conceitos/folha_cosmeticos.png` | 8 elmos/chapéus, 5 capas, 4 acessórios e 2 combinações |
| `docs/arte/conceitos/folha_armas.png` | 6 armas: ícone, na mão e projétil/efeito |
| `docs/arte/conceitos/folha_inimigos.png` | 7 inimigos: parado, ataque e projéteis |
| `docs/arte/conceitos/cosmeticos/compat_*.png` | um elmo, um acessório e uma capa em **todos os 71 frames** do Freny que têm elmo |

As folhas mostram cada peça ampliada 4x a partir da grade de arte (nearest), com o nome embaixo, e terminam com uma faixa a 1x sobre o fundo escuro do jogo (o teste de leitura). Os PNGs soltos em `docs/arte/conceitos/{cosmeticos,armas,inimigos}/` estão no **tamanho do jogo**: 4 texels por pixel de arte, prontos para importar a 100 PPU com filtro Point.

## Como foram feitos (e por que combinam com o jogo)

- Mesma grade do Freny e dos mobs: 1 pixel de arte = 4 texels, 100 PPU.
- Só cores de `freny_palette.py`; o gerador **para com erro** se aparecer qualquer outra (`check_palette`).
- Sem contorno preto: cada forma fecha com a cor mais escura da própria rampa, no lado da sombra.
- Os cosméticos são desenhados **sobre os frames reais**: o elmo é achado com `freny_heads.helmet` (o mesmo localizador do `freny_cosmetics.py`), repintado, e as peças são carimbadas em relação a ele. Por isso já funcionam em todas as poses (veja `compat_*.png`).
- As capas seguem o `CapeArt.cs`: três tiras (4x4, 5x4 e 6x5 pixels de arte) penduradas da borda de trás do elmo. O conceito só acrescenta uma estampa às tiras.
- Os inimigos olham para a esquerda, como as folhas dos mobs atuais. Corpo azul-marinho, brilho violeta nos olhos e pontos fracos, e o quadro de ataque **acende** algo (lilás, branco ou boca vermelha) para avisar antes do golpe.

---

## Cosméticos

### Elmos e chapéus (slot Elmo, mesmo sistema do `freny_cosmetics.py`)

| Conceito | Descrição | Ideia de jogo / desbloqueio | Cores |
|---|---|---|---|
| **Elmo emplumado** (`elmo_emplumado_*`) | Elmo de aço com uma pluma vermelha que cai para trás. | Cosmético de moedas, o "cavaleiro clássico". | aço `STEEL_*`, pluma `RED`/`WINE`/`SALMON` |
| **Capuz do caçador** (`capuz_cacador_*`) | Capuz de couro com ponta caída e o rosto na sombra; um olho pega a luz. | Combina com a besta; prêmio por kills a distância. | `ROPE`/`WOOD`/`WOOD_DARK`, rosto `INK`, brilho `CHALK` |
| **Chapéu do mago** (`chapeu_mago_*`) | Chapéu pontudo ameixa, inclinado para trás, com faixa dourada. | Prêmio ligado ao Cajado do Feiticeiro (fase 4). | `PLUM`/`PLUM_DARK`, faixa `GOLD`/`GOLD_LIGHT`/`AMBER` |
| **Elmo de caveira** (`elmo_caveira_*`) | Elmo pintado de osso, órbitas escuras com uma faísca vermelha e dentes. | Conquista de sobrevivência ou de muitas mortes na Arena. | `BONE`/`BONE_SHADE`/`HORN`, `INK`, `HOT_RED` |
| **Troféu do Ciclope** (`elmo_ciclope_*`) | Elmo azul-marinho com o olho violeta do Ciclope na frente e cravos no topo. | Troféu de chefe: derrotar o Ciclope X vezes. É exceção de propósito: cor de inimigo usada como troféu. | `SLATE`/`DEEP`/`ABYSS`, olho `VIOLET`/`LILAC`, `INDIGO` |
| **Máscara da peste** (`mascara_peste_*`) | Máscara de médico da peste: bico de osso, lente dourada e chapéu de aba larga. | Prêmio do Poço de Ácido (sobreviver X ondas lá); conversa com o Rato da peste. | `PLUM`/`PLUM_DARK`/`INK`, bico `BONE`/`HORN`, lente `GOLD`/`YELLOW` |
| **Elmo do demônio** (`elmo_demonio_*`) | Elmo cor de ferrugem com chifres de osso de pontas vermelhas e uma fenda em brasa. | Rebirth alto, ou combo com Fúria. Usa a rampa do Golem vermelho. | `RUST_*`, chifres `BONE_SHADE`/`HORN`/`RED`, fenda `HOT_RED` |
| **Elmo do cruzado** (`elmo_cruzado_*`) | Elmo de balde de topo reto, fenda de visão escura e cruz dourada. | Cosmético caro de moedas; silhueta "quadrada" bem diferente da original. | aço `STEEL_*`, `INK`, `GOLD`/`GOLD_LIGHT` |

Nota técnica: entram como novas entradas em `HELMETS` do `freny_cosmetics.py` (repintura + `addons`). Na escalada (o Freny de costas), o `freny_heads.py` já esconde os enfeites da frente; o bico da peste e a fenda do cruzado devem seguir essa regra (no `compat_mascara_peste.png` dá para ver o bico aparecendo nos dois frames de escalada).

### Capas (slot Capa, `CapeArt.cs` + estampa)

| Conceito | Descrição | Ideia de jogo | Cores (cor / dobra / barra) |
|---|---|---|---|
| **Capa esfarrapada** (`capa_esfarrapada_*`) | Pano cinza com um furo e a barra rasgada. | Capa "inicial" barata, ou a que aparece depois de muitas mortes. | `STONE` / `STONE_DARK` / `SAND` |
| **Manto do Poço** (`manto_do_poco_*`) | Verde-ácido escuro, com gotas de ácido pingando da barra. | Prêmio do Poço de Ácido. As gotas podem pingar de verdade (partícula). | `ACID_DEEP` / `ACID_DARK` / `ACID_LIGHT`, gotas `ACID` |
| **Manto de pele** (`manto_de_pele_*`) | Couro marrom com gola de pele clara. | Combina com o Elmo viking. | `WOOD` / `WOOD_DARK` / `STRAW`, gola `BONE`/`BONE_SHADE` |
| **Capa do abismo** (`capa_do_abismo_*`) | Azul-noite com estrelas violeta e lilás. | Troféu do Feiticeiro Supremo (usa a rampa dele). | `NIGHT3` / `NIGHT` / `VIOLET`, estrelas `LILAC` |
| **Capa do brasão** (`capa_do_brasao_*`) | Carmesim com um losango dourado na tira do meio. | Rebirth, ou o par da Capa real. | `CRIMSON` / `BLOOD` / `GOLD`, brasão `YELLOW` |

Nota técnica: o `CapeArt.Segment` hoje pinta cor, dobra e barra. Para as estampas bastaria um campo `pattern` em `FrenyLook.Item` e alguns pixels a mais por tira (furo, gola, gotas, estrelas, brasão), exatamente como em `pattern_paint()` do gerador. As tiras soltas estão em `*_tiras.png`.

### Acessórios (slot NOVO: "Acessório")

| Conceito | Descrição | Ideia de jogo | Cores |
|---|---|---|---|
| **Cachecol** (`cachecol_*`) | Cachecol vermelho no pescoço com duas pontas caídas para trás. As pontas podem balançar como a capa. | O acessório mais "barato" e mais visível. | `RED`/`CRIMSON`/`SALMON` |
| **Ombreiras** (`ombreiras_*`) | Duas ombreiras de aço com rebite dourado nas pontas do casaco. | Visual de armadura; par natural da Armadura Berserker. | `STEEL_*`, rebite `GOLD` |
| **Pena de grifo** (`pena_de_grifo_*`) | Pena branca e azul presa atrás do elmo. Combina com qualquer elmo. | Recompensa de conquista rara. | `CHALK`/`ICE`/`SKY` |
| **Amuleto do olho** (`amuleto_do_olho_*`) | Cordão com um olho violeta engastado em ouro, no peito. | Troféu de inimigo; pode brilhar quando há inimigo invisível ou elite por perto. | cordão `ROPE`, `GOLD`/`AMBER`, `VIOLET`/`LILAC` |

Nota técnica: o slot novo seria um `LookSlot.Accessory` em `FrenyLook.cs`. A arte é gerada como os elmos (uma folha por item, um sprite por frame do corpo), ancorada no pescoço (pele logo abaixo do elmo) e na linha dos ombros. Veja `compat_cachecol.png` e `compat_ombreiras.png`.

---

## Armas

Ícones no padrão de `Items 100x100.png` (cabem na célula de 25x25 pixels de arte, sem contorno, aço/madeira/ouro). O "na mão" é um mock sobre os frames reais do Freny (parado ou mirando).

| Arma | Descrição | Ideia de jogo | Projétil / efeito | Cores |
|---|---|---|---|---|
| **Besta** (`besta_*`) | Coronha de madeira, arco de aço e virote carregado. | Tiro lento e forte que **atravessa** 2 inimigos; recarga longa. Boa contra o Escudeiro (de cima) e o Esqueleto arqueiro. | `virote`: haste de madeira, ponta de aço, penas vermelhas | `ROPE`/`WOOD`/`WOOD_DARK`, `STEEL_*`, `RED` |
| **Lança** (`lanca_*`) | Haste longa, ponta em folha e uma fita vermelha. | Estocada reta de longo alcance (mais longa que a espada, sem arco). No ar, mira para baixo (pogo). | `estocada`: rastro reto claro | `ROPE`/`WOOD`, `STEEL_*`, `RED`/`WINE` |
| **Chicote de corrente** (`chicote_corrente_*`) | Cabo de couro com pomo dourado, corrente e gancho na ponta. | Alcance horizontal enorme e fino; o gancho **puxa** inimigos leves (Morcego, Rato) para perto. | `chicotada`: corrente esticada com faísca no gancho | `ROPE`/`WOOD`, `STEEL_*`, `GOLD`, faísca `YELLOW`/`CHALK` |
| **Mangual** (`mangual_*`) | Cabo, corrente curta e bola cravejada. | Golpe lento em arco que **quebra escudo** (Escudeiro, Carniçal blindado) e atordoa. | `impacto`: estrela de faíscas | `ROPE`/`WOOD`, `STEEL_*`, `YELLOW`/`GOLD_LIGHT`/`CHALK` |
| **Machado de arremesso** (`machado_arremesso_*`) | Machadinha de cabo curto e lâmina larga. | Arremesso em parábola que **volta** se não acertar nada; dá para ter 2 no ar. | `giro_0..3`: 4 quadros girando 90° | `WOOD`/`ROPE`, `STEEL_*` |
| **Cajado-bomba** (`cajado_bomba_*`) | Cajado com gaiola dourada segurando uma bomba de pavio aceso. | Lança uma bomba que quica uma vez e explode em área (parente da Granada, mas com alcance de cajado). Cuidado com a própria explosão. | `bomba` + `explosao_0`, `explosao_1` | bomba `INK`/`PLUM_DARK`/`PLUM`, pavio `ROPE`, faísca `YELLOW`/`HOT_RED`, gaiola `GOLD`/`GOLD_DARK`, explosão `GOLD_PALE`/`YELLOW`/`GOLD_LIGHT`/`RED`, fumaça `STONE`/`STONE_DARK` |

---

## Inimigos

Todos olham para a esquerda. A escala relativa está no fim de `folha_geral.png`.

| Inimigo | Conceito (comportamento em 1 linha) | Onde | Quadros | Cores |
|---|---|---|---|---|
| **Esqueleto arqueiro** (`esqueleto_arqueiro_*`) | Fica parado longe, puxa o arco (a ponta da flecha acende) e atira em linha reta; foge um passo se o Freny chega perto. | Arena, plataformas altas | parado, ataque, `flecha` | ossos `SLATE`/`DEEP`/`ABYSS`, olhos `VIOLET` (ataque `LILAC`), arco `SAND`/`STONE`, flecha com ponta `VIOLET`/`LILAC` |
| **Carniçal blindado** (`carnical_blindado_*`) | Anda curvado com placas nas costas que **bloqueiam por cima e por trás**; dá um bote com a boca aberta, que é o ponto fraco (só leva dano de frente ou durante o bote). | Arena e Poço | parado, ataque | corpo `SLATE`/`DEEP`/`ABYSS`, placas `INDIGO`, boca `BLOOD`/`WINE`/`RED`, garras e dentes `BONE`/`BONE_SHADE` |
| **Lodo ácido** (`lodo_acido_*`) | Pula na direção do Freny e cospe ácido; ao morrer **se divide em 2 filhotes**, que também deixam uma poça pequena. | Poço de Ácido | parado, ataque (esticado), `filhote`, `cuspe` | gosma `ACID_DARK`/`ACID_DEEP`/`ACID`/`ACID_LIGHT`/`ACID_FOAM`, núcleo `ABYSS`/`DEEP` com olho `VIOLET`/`LILAC` |
| **Cultista voador** (`cultista_voador_*`) | Flutua fora do alcance, ergue os braços e forma um orbe violeta que persegue o Freny devagar. Some e reaparece (teleporte curto) quando leva dano. | Arena (ondas altas) | parado, ataque, `orbe` | manto `SLATE`/`DEEP`/`ABYSS` com faixa `INDIGO`, rosto `ABYSS`, olhos e mãos `VIOLET`/`LILAC`, orbe `LILAC`/`CHALK` |
| **Gárgula** (`gargula_*`) | Fica **pousada** e imóvel como estátua (invulnerável) no alto; quando o Freny passa embaixo, mergulha em diagonal e volta a pousar. | Arena, beirais | pousada, mergulho | pedra `SLATE`/`DEEP`/`ABYSS`, asas `DEEP` com nervuras `INDIGO`, olho `VIOLET` (mergulho `LILAC`), boca `BLOOD`, garras `BONE_SHADE` |
| **Rato da peste** (`rato_da_peste_*`) | Vem em **bando** de 3 a 5, rápido e frágil; a mordida solta uma nuvenzinha verde que dá dano contínuo por 1 s. | Poço (sai das poças), Arena | parado, ataque (bote), `nuvem` | pelo `SLATE`/`DEEP`/`ABYSS`, focinho e rabo `MAUVE`, feridas `ACID`/`ACID_LIGHT`, olho `VIOLET`/`LILAC`, nuvem `ACID_*` |
| **Arauto do Poço** (chefe, `arauto_do_poco_*`) | **Fase 1**: gigante de armadura índigo com máscara de fenda; anda e esmaga, e só o coração violeta no peito toma dano. **Fase 2** (metade da vida): a armadura racha, a máscara cai e mostra o núcleo violeta, ele vaza ácido e solta um anel de espinhos violeta (o ataque) a partir do núcleo. | Chefe do Poço de Ácido | fase 1, fase 2, fase 2: ataque | placas `INDIGO` sobre `SLATE`/`DEEP`/`ABYSS`, núcleo `VIOLET`/`LILAC`/`CHALK`, rachaduras `ABYSS`, ácido `ACID`/`ACID_LIGHT` |

Nota sobre o Arauto: o conceito tem 32x30 pixels de arte, só um pouco maior que o Golem, para fixar silhueta e fases. A versão final de chefe deveria ser redesenhada maior (na faixa do Ciclope), com as mesmas regras.

Nota de jogo: todo inimigo novo que pontua precisa da entrada correspondente em `api/_lib/rules.js` na API (nome, pontos, desbloqueio), senão o servidor recusa as runs.

---

## Sugestão de ordem para entrar no jogo

1. **Elmos novos** (cruzado, peste, demônio, emplumado): custo quase zero. São só entradas novas em `freny_cosmetics.py` e no catálogo do `FrenyLook.cs`, e o sistema de repintura já cobre todos os frames.
2. **Lodo ácido** e **Rato da peste**: dão vida ao Poço de Ácido, são pequenos e simples de animar, e reaproveitam as poças e o dano de ácido que já existem.
3. **Besta** e **Machado de arremesso**: armas de distância com mecânica nova (atravessar, voltar) e sem animação nova do corpo (usam as poses de tiro e de arremesso de granada).
4. **Esqueleto arqueiro** e **Gárgula**: o primeiro inimigo de longa distância "parado" e o primeiro que fica de emboscada. Variam o ritmo da Arena.
5. **Cachecol** e **estampas de capa**: pedem uma pequena mudança no `CapeArt`/`FrenyLook` (campo de estampa, slot de acessório).
6. Depois: Mangual e Lança (pedem poses de golpe novas), Cultista, Carniçal e o **Arauto** como chefe do Poço.
