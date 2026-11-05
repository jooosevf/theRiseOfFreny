# Systems Index: The Rise of Freny (campanha)

> **Status**: Draft, com as decisões do José de 2026-11-05 aplicadas
> **Created**: 2026-11-04
> **Last Updated**: 2026-11-05
> **Source Concept**: design/game-brief.md
> **Escopo**: só a campanha. Arena, ranking, co-op e integração com a Steam ficam de fora, e nada aqui pode mudar a API do ranking.
> **Rigor**: standard (`docs.density: balanced`, `review_mode: lean`). Por isso os gates TD-SYSTEM-BOUNDARY, PR-SCOPE e CD-SYSTEMS não rodaram.

---

## Overview

A campanha quer ser Hollow Knight na jogabilidade e nas fases, e Resident Evil no clima, nos puzzles e na escassez, tudo em Dark Fantasy. A premissa já confirmada pelo José é que Freny sobe das profundezas até a superfície e descobre o próprio passado no caminho, em 9 masmorras de 9 fases e um chefe final.

O código de hoje entrega outra coisa: uma lista linear de 18 fases, em que cada cena recarregada volta do zero. O combate, os inimigos e o terreno já servem. O que falta é a camada de **mundo que lembra**: salas ligadas, estado que persiste e recursos que acabam. Por isso o MVP não é conteúdo novo, é provar essa camada em poucas salas.

O que o código mostra hoje (lido em `a2340c2`, sem rodar o jogo):

- **Fases em fila:** `CampaignLevels.All` é uma lista, e `CampaignProgress` guarda só o índice da fase mais distante e os melhores tempos.
- **Ligação entre salas existe, mas ninguém usa:** `LevelDoor.Kind.Link` e `CampaignRun.Travel` existem, e nenhuma das 18 cenas preenche `targetScene`. `Travel` ainda apaga o checkpoint ao trocar de cena (`CampaignRun.cs:148`).
- **Nada passa de uma cena para outra:** ao carregar a cena o Freny volta com vida cheia e tambor cheio (`Player.cs:329`). As redes são "por fase" (`NetThrower.NetsPerStage = 3`).
- **Vasos reabastecem:** `Breakable.FromTilemaps` recria vasos, barris e caixas a cada carga, e eles sorteiam poção 18%, balas 10%, granada 8%, armadura 3% e diamante 12%. Morrer repõe tudo.
- **Pulo duplo vem da loja:** `FrenyUpgrade.DoubleJump` é comprado com moedas, que também saem da Arena, e vale em todo lugar. Isso impede usar o pulo como trava de caminho. Como a Armeira continua igual nos dois modos (decisão 2), as travas precisam vir de outras habilidades.
- **HUD e minimapa são de teste:** `CampaignHud` é IMGUI temporário com tempo, quedas e lista de fases no Tab. `CampaignMinimap` mostra inimigos e a saída, o que tira a exploração.
- **Chefes:** a 2-9 tranca a saída enquanto `Sorcerer_Supreme` vive. A 1-9 tem um objeto `Boss` e barra de vida, mas a porta dela não tem `bossName` no arquivo da cena. Não testei se o chefe funciona.
- **Puzzles:** não há nenhum sistema de alavanca, placa ou item-chave. `BossKey` é a chave do baú de ouro da Arena.
- **Sinalização Dark Fantasy:** `VhsOverlay` liga scanlines e granulação de VHS em todas as fases. Vale a bíblia de arte decidir se fica.

### O que cada sistema faz

**Já existem e são reaproveitados (sem GDD novo, o comportamento atual é a referência):**

- **Controle do Freny:** mover, pular, esquivar, subir escada e nadar. A campanha herda tudo da Arena.
- **Terreno e perigos:** escadas, líquidos, buracos, espinhos e blocos, montados a partir dos tiles por `StageTerrain`.
- **Combate e armas:** espada, revólver, escopeta, granada, rede, cajado e Fúria, iguais aos da Arena.
- **Inimigos:** `Enemy` e seus tipos. Já se comportam diferente fora da Arena (`ArenaBounds.Active`).

**MVP (provar o mundo que lembra):**

- **Estado do mundo (inferido):** um registro de chave e valor do que já aconteceu: porta aberta, atalho, item pego, inimigo morto, chefe derrotado. Volta ao último checkpoint quando o Freny morre e só vai para o disco no save point. Quase tudo depende dele, então é o maior gargalo.
- **Mapa da masmorra:** define as salas de uma masmorra e como se ligam, incluindo atalhos de mão única, e a transição entre cenas. Substitui a fila de `CampaignLevels`, sem quebrar os saves atuais.
- **Mochila da campanha (inferido):** vida, munição, granadas e redes passam de uma sala para outra. Inclui as vidas que o Freny carrega e usa quando quiser (decisão 3). Hoje ele carrega no máximo uma poção (`GameManager.HasPotion`). É a condição para existir escassez.
- **Morte e retorno:** volta ao último checkpoint, e só renasce o que morreu depois dele. Define também como se usa o save point e o que ele oferece. Substitui a regra atual de recarregar a cena.
- **Melhorias entre modos (inferido):** a Armeira é a mesma nos dois modos. Falta decidir só o que isso muda nas fases, como deixar cada sala boa com e sem pulo duplo.
- **Escassez:** os itens caem no jogo como na Arena (decisão 2), e a escassez vem de inimigos e vasos que não voltam. Define as tabelas de queda e quantos itens fixos cada masmorra tem.
- **Interativos e puzzles:** alavanca, placa, porta trancada e bloco movível, com a regra de como salvam no Estado do mundo. Começa pelo que abre um atalho.
- **Construtor de fases:** o `Tools/LevelBuilder` aprende a gerar portas `Link`, interativos e checagem de caminho entre salas. Sem isso, cada sala nova é trabalho manual na cena.

**Vertical Slice (uma masmorra inteira e bem acabada):**

- **Habilidades de travessia:** habilidades achadas no mundo que abrem caminhos antigos, ao estilo Hollow Knight.
- **Encontros e bestiário:** onde cada inimigo fica e quais tipos aparecem em cada masmorra, poucos e colocados à mão.
- **Itens-chave:** chaves e objetos de puzzle que o Freny carrega, ao estilo Resident Evil.
- **Chefes de masmorra:** o molde da luta (porta, barra de vida, recompensa, nunca volta depois de morto) e cada chefe.
- **Save e Continuar:** a tela de Continuar e o aviso de sair sem salvar, além da migração dos saves atuais e da sincronização na nuvem.
- **HUD da campanha:** troca o IMGUI temporário. Decide se pontuação, combo, tempo e quedas continuam aparecendo.
- **Mapa do jogador:** troca o minimapa de teste por um mapa que se descobre andando.
- **Tela de inventário:** mostra a Mochila e os itens-chave. Parte da `InventoryScreens` que já existe.
- **Kit visual da masmorra:** tileset, paleta e luz de cada masmorra, a partir da bíblia de arte.
- **Som por masmorra:** ambiente e música. `AmbiencePlayer` e `MusicPlayer` já procuram `masmorraN`; só existem 1, 2 e `chefe`.

**Alpha (todas as masmorras, ainda sem polimento):**

- **Ligação entre masmorras:** como as 9 se conectam, se dá para voltar e como se chega ao chefe final.
- **Mercador:** hoje só existe na 1-5 e na 1-9, com a interface antiga. Passa a seguir as regras de escassez.
- **Narrativa ambiental:** o passado do Freny contado em textos, objetos e personagens, masmorra a masmorra. A história ainda está em aberto.

**Full Vision:**

- **Tutorial no mundo**, **Conquistas da campanha**, **Acessibilidade da campanha** e **Side quest do Runner** (só se fizer sentido, como o José disse).

---

## Decisões do José (2026-11-05)

| # | Decisão | O que ele disse | Como isto foi aplicado |
|---|---------|-----------------|------------------------|
| 1 | Como as fases se ligam | Ir e vir: poder entrar em lugares e voltar. Todas as fases serão refeitas. | Cada masmorra é um mapa interligado de cerca de 9 salas. As 18 cenas atuais viram material de partida. Não foi dito se as 9 masmorras formam um mundo único: fica para Ligação entre masmorras. |
| 2 | Arena e campanha | Usa tudo igual, e os itens caem dentro do jogo. Separar a Armeira da campanha ficou como "talvez". | Armeira única, sem limite. As quedas seguem a ideia da Arena. Pulo duplo não pode ser trava de caminho. |
| 3 | Morte | Save point e checkpoint. Inimigo morto fica morto, a não ser que o checkpoint volte para antes de ele morrer. Dá para carregar vidas e usar quando quiser. | Regras em `design/gdd/estado-do-mundo.md`. Vidas carregadas entram na Mochila da campanha. |
| 4 | Masmorra de teste | Pode fazer a masmorra de teste, seguindo a recomendação. | Cisternas (masmorra 2), porque as cenas saem do construtor em Python. |

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 1 | Controle do Freny | Core | MVP | Implemented | — | — |
| 2 | Terreno e perigos | Core | MVP | Implemented | — | Controle do Freny |
| 3 | Combate e armas | Gameplay | MVP | Implemented | — | Controle do Freny |
| 4 | Inimigos | Gameplay | MVP | Implemented | — | Terreno e perigos, Combate e armas |
| 5 | Estado do mundo (inferred) | Persistence | MVP | In Design | design/gdd/estado-do-mundo.md | — |
| 6 | Mapa da masmorra | World | MVP | Not Started | — | Estado do mundo |
| 7 | Mochila da campanha (inferred) | Economy | MVP | Not Started | — | Estado do mundo, Combate e armas |
| 8 | Morte e retorno | Gameplay | MVP | Not Started | — | Estado do mundo, Mapa da masmorra, Mochila da campanha |
| 9 | Melhorias entre modos (inferred) | Progression | MVP | Not Started | — | Mochila da campanha |
| 10 | Escassez | Economy | MVP | Not Started | — | Mochila da campanha, Morte e retorno, Melhorias entre modos |
| 11 | Interativos e puzzles | Gameplay | MVP | Not Started | — | Estado do mundo, Mapa da masmorra |
| 12 | Construtor de fases (inferred) | World | MVP | Not Started | — | Mapa da masmorra, Interativos e puzzles |
| 13 | Habilidades de travessia | Progression | Vertical Slice | Not Started | — | Controle do Freny, Mapa da masmorra, Melhorias entre modos |
| 14 | Encontros e bestiário | Gameplay | Vertical Slice | Not Started | — | Inimigos, Morte e retorno, Escassez |
| 15 | Itens-chave | Gameplay | Vertical Slice | Not Started | — | Interativos e puzzles, Mochila da campanha |
| 16 | Chefes de masmorra | Gameplay | Vertical Slice | Not Started | — | Inimigos, Estado do mundo, Morte e retorno |
| 17 | Save e Continuar (inferred) | Persistence | Vertical Slice | Not Started | — | Estado do mundo, Mochila da campanha |
| 18 | HUD da campanha | UI | Vertical Slice | Not Started | — | Mochila da campanha, Morte e retorno |
| 19 | Mapa do jogador (inferred) | UI | Vertical Slice | Not Started | — | Mapa da masmorra, Estado do mundo |
| 20 | Tela de inventário (inferred) | UI | Vertical Slice | Not Started | — | Mochila da campanha, Itens-chave |
| 21 | Kit visual da masmorra (inferred) | World | Vertical Slice | Not Started | — | Construtor de fases, bíblia de arte |
| 22 | Som por masmorra (inferred) | Audio | Vertical Slice | Not Started | — | Mapa da masmorra |
| 23 | Ligação entre masmorras | World | Alpha | Not Started | — | Mapa da masmorra, Habilidades de travessia, Chefes de masmorra |
| 24 | Mercador | Economy | Alpha | Not Started | — | Escassez, Melhorias entre modos |
| 25 | Narrativa ambiental | Narrative | Alpha | Not Started | — | Mapa da masmorra, Itens-chave |
| 26 | Tutorial no mundo (inferred) | Meta | Full Vision | Not Started | — | Interativos e puzzles, HUD da campanha |
| 27 | Conquistas da campanha (inferred) | Meta | Full Vision | Not Started | — | Estado do mundo |
| 28 | Acessibilidade da campanha (inferred) | Meta | Full Vision | Not Started | — | HUD da campanha, Morte e retorno |
| 29 | Side quest do Runner | Meta | Full Vision | Not Started | — | Ligação entre masmorras |

---

## Categories

| Category | Description | Systems here |
|----------|-------------|--------------|
| **Core** | Base de que tudo depende | Controle, terreno |
| **Gameplay** | O que faz o jogo ser divertido | Combate, inimigos, morte, puzzles, chefes |
| **World** | Categoria própria: como o mundo é montado e ligado | Mapa, construtor, kit visual, ligação entre masmorras |
| **Progression** | Como o Freny cresce | Melhorias entre modos, habilidades |
| **Economy** | Criação e gasto de recursos | Mochila, escassez, Mercador |
| **Persistence** | O que continua existindo | Estado do mundo, save |
| **UI** | O que o jogador lê na tela | HUD, mapa do jogador, inventário |
| **Audio** | Som e música | Som por masmorra |
| **Narrative** | História | Narrativa ambiental |
| **Meta** | Fora do ciclo principal | Tutorial, conquistas, acessibilidade, Runner |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Poucas salas das Cisternas ligadas entre si, com um atalho aberto por puzzle, recursos que acabam e morte com a regra nova. Responde "isso é divertido?". | Primeiro protótipo jogável da campanha | Design FIRST |
| **Vertical Slice** | Uma masmorra inteira, do início ao chefe, com a qualidade final. Serve de demo. | Demo | Design SECOND |
| **Alpha** | As 9 masmorras jogáveis e a história contada, com conteúdo provisório aceito. | Alpha | Design THIRD |
| **Full Vision** | Polimento e extras. | Lançamento na Steam | Design as needed |

---

## Dependency Map

Os sistemas 1 a 4 já existem e não entram na ordem. Quem depende deles precisa manter a Arena como está: a campanha entra por chave em `Features.cs` e por checagens de modo, nunca mudando o combate da Arena sem querer.

### Foundation Layer (no dependencies)

1. Estado do mundo: é um registro genérico por chave, e não precisa saber o que é uma sala.

### Core Layer (depends on foundation)

1. Mapa da masmorra: depends on Estado do mundo
2. Mochila da campanha: depends on Estado do mundo, Combate e armas
3. Morte e retorno: depends on Estado do mundo, Mapa da masmorra, Mochila da campanha

### Feature Layer (depends on core)

1. Melhorias entre modos: depends on Mochila da campanha
2. Escassez: depends on Mochila da campanha, Morte e retorno, Melhorias entre modos
3. Interativos e puzzles: depends on Estado do mundo, Mapa da masmorra
4. Construtor de fases: depends on Mapa da masmorra, Interativos e puzzles
5. Habilidades de travessia: depends on Controle do Freny, Mapa da masmorra, Melhorias entre modos
6. Encontros e bestiário: depends on Inimigos, Morte e retorno, Escassez
7. Itens-chave: depends on Interativos e puzzles, Mochila da campanha
8. Chefes de masmorra: depends on Inimigos, Estado do mundo, Morte e retorno
9. Save e Continuar: depends on Estado do mundo, Mochila da campanha
10. Ligação entre masmorras: depends on Mapa da masmorra, Habilidades de travessia, Chefes de masmorra
11. Mercador: depends on Escassez, Melhorias entre modos

### Presentation Layer (depends on features)

1. HUD da campanha: depends on Mochila da campanha, Morte e retorno
2. Mapa do jogador: depends on Mapa da masmorra, Estado do mundo
3. Tela de inventário: depends on Mochila da campanha, Itens-chave
4. Kit visual da masmorra: depends on Construtor de fases, bíblia de arte
5. Som por masmorra: depends on Mapa da masmorra
6. Narrativa ambiental: depends on Mapa da masmorra, Itens-chave

### Polish Layer (depends on everything)

1. Tutorial no mundo: depends on Interativos e puzzles, HUD da campanha
2. Conquistas da campanha: depends on Estado do mundo
3. Acessibilidade da campanha: depends on HUD da campanha, Morte e retorno
4. Side quest do Runner: depends on Ligação entre masmorras

**Gargalos:** Estado do mundo (8 dependentes diretos), Mapa da masmorra (8) e Mochila da campanha (7). Um erro neles se espalha, então vêm primeiro.

---

## Recommended Design Order

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | Estado do mundo | MVP | Foundation | game-designer, technical-director | M |
| 2 | Mapa da masmorra | MVP | Core | game-designer, technical-director | L |
| 3 | Mochila da campanha | MVP | Core | game-designer, gameplay-programmer | M |
| 4 | Morte e retorno | MVP | Core | game-designer | M |
| 5 | Melhorias entre modos | MVP | Feature | game-designer | S |
| 6 | Escassez | MVP | Feature | game-designer | M |
| 7 | Interativos e puzzles | MVP | Feature | game-designer, gameplay-programmer | M |
| 8 | Construtor de fases | MVP | Feature | technical-director | S |
| 9 | Habilidades de travessia | Vertical Slice | Feature | game-designer | M |
| 10 | Encontros e bestiário | Vertical Slice | Feature | game-designer | M |
| 11 | Itens-chave | Vertical Slice | Feature | game-designer | S |
| 12 | Chefes de masmorra | Vertical Slice | Feature | game-designer, gameplay-programmer | L |
| 13 | Save e Continuar | Vertical Slice | Feature | technical-director, network-programmer | S |
| 14 | HUD da campanha | Vertical Slice | Presentation | ui-programmer | S |
| 15 | Mapa do jogador | Vertical Slice | Presentation | ui-programmer, game-designer | M |
| 16 | Tela de inventário | Vertical Slice | Presentation | ui-programmer | S |
| 17 | Kit visual da masmorra | Vertical Slice | Presentation | creative-director, unity-specialist | S |
| 18 | Som por masmorra | Vertical Slice | Presentation | creative-director | S |
| 19 | Ligação entre masmorras | Alpha | Feature | game-designer | M |
| 20 | Mercador | Alpha | Feature | game-designer | S |
| 21 | Narrativa ambiental | Alpha | Presentation | creative-director | L |
| 22 | Tutorial no mundo | Full Vision | Polish | game-designer | S |
| 23 | Conquistas da campanha | Full Vision | Polish | game-designer | S |
| 24 | Acessibilidade da campanha | Full Vision | Polish | ui-programmer | S |
| 25 | Side quest do Runner | Full Vision | Polish | game-designer | S |

---

## Circular Dependencies

Nenhum ciclo ficou no mapa. Dois quase existiam e foram quebrados por contrato:

- **Mapa da masmorra e Estado do mundo:** o mapa precisa saber se um atalho está aberto, e o estado guarda dados de salas. O estado aceita qualquer chave, e o mapa define os nomes das chaves.
- **Mochila da campanha e Morte e retorno:** a morte decide o que se perde, e a mochila guarda o que se tem. A mochila oferece "gravar" e "restaurar", e a morte decide quando chamar.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| Conteúdo das 9 masmorras | Scope | Com o redesign, as 81 salas precisam ser desenhadas: 63 nem têm cena, e as 18 atuais só servem de ponto de partida. O José desenvolve sozinho. Não estimei prazo. | Medir quanto custa uma masmorra no Vertical Slice e só então confirmar o número final de masmorras e salas. |
| Melhorias entre modos | Design | Com a Armeira igual e sem limite, quem farma na Arena pode atravessar a campanha sem sentir escassez. Saves atuais já têm pulo duplo comprado. | Testar o MVP com um save farmado e um save novo. Se a escassez sumir, voltar à ideia de separar ou limitar. |
| Mapa da masmorra | Technical | Trocar a fila por um mapa mexe em `CampaignLevels`, `CampaignProgress`, nos menus e nos saves atuais. Cada sala é uma cena e ainda não medi o tempo de troca. | Prototipar a troca entre 3 salas antes do GDD final e medir no build de Windows. |
| Estado do mundo | Technical | É usado por quase tudo, e uma chave mudada depois invalida saves. | Definir nomes estáveis e versão do save desde o início. |
| Interativos e puzzles | Design | O jogo não tem nenhum puzzle hoje, e não há prova de que combinem com o ritmo do combate. | Começar por um puzzle simples no MVP e testar com jogadores. |
| Código compartilhado com a Arena | Technical | A campanha mexe em `Player` e `Enemy`, usados pelo modo ranqueado, e o projeto não tem testes automáticos. | Chave em `Features.cs` para cada sistema e testar a Arena na Unity antes de cada merge. |
| Narrativa ambiental | Scope | Não existe roteiro: o passado do Freny está em aberto. | Fechar um resumo da história antes de desenhar a masmorra 3. |

---

## Progress Tracker

| Metric | Count |
|--------|-------|
| Total systems identified | 29 (4 já existem, 25 novos) |
| Design docs started | 1 |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 0/8 |
| Vertical Slice systems designed | 0/10 |

---

## Next Steps

- [x] José responde às decisões
- [ ] Design MVP-tier systems first (`estado-do-mundo` em revisão; o próximo é `mapa-da-masmorra`)
- [ ] Run `/design-review` on each completed GDD
- [ ] Run `/gate-check technical-setup` when MVP systems are designed
- [ ] Validate the highest-risk systems with `/vertical-slice` before committing to Production
