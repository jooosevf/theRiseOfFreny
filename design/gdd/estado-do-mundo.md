# Estado do mundo

> **Status**: In Design
> **Author**: José + Claude
> **Last Updated**: 2026-11-05
> **Protótipo**: as salas T-1 a T-4 (`Assets/Scripts/Campaign/World`). A campanha antiga não usa este sistema.
> **Last Verified**: 2026-11-05
> **Implements Pillar**: campanha de Hollow Knight + Resident Evil: um mundo que lembra o que o jogador fez

## Summary

Guarda o que já aconteceu na campanha (inimigo morto, vaso quebrado, item pego, porta aberta) e define o que volta quando o Freny morre e o que fica gravado no save point.

> **Quick reference** — Layer: `Foundation` · Priority: `MVP` · Key deps: `None`

## Overview

Hoje cada cena recarregada volta do zero, então nada do que o jogador faz dura. Este sistema é o registro que resolve isso. Ele mantém três cópias do estado da campanha: a **viva**, que muda enquanto se joga; a do **checkpoint**, para onde o Freny volta ao morrer; e a do **save point**, a única gravada no disco. A regra do José sai direto disso: o inimigo morto fica morto, a não ser que o checkpoint volte para antes de ele morrer.

## Detailed Design

### Core Rules

**O que é guardado**

1. O estado é uma lista de pares chave e valor, e só registra o que difere da cena original. Uma sala sem registros é a sala como foi construída.
2. A chave é `sala/tipo/id`, por exemplo `2-3/enemy/licker_02`. O tipo é `enemy`, `prop`, `item`, `door`, `switch` ou `flag`. O sistema não interpreta a chave: quem a usa é quem define o nome.
3. O id de um objeto vem da construção da sala e nunca é reaproveitado. Vasos, barris e caixas criados a partir dos tiles usam a célula do tile como id.
4. Só o que foi colocado na sala tem registro. Inimigo invocado e item solto que caiu de um inimigo ou vaso não têm.

**As três cópias**

5. **Viva:** recebe toda mudança na hora. Só existe na memória.
6. **Checkpoint:** cópia da viva feita ao tocar um checkpoint. Só existe na memória. A sala e a posição desse checkpoint vão junto.
7. **Save point:** usar um save point copia a viva para o checkpoint e também para o disco, no slot ativo. Todo save point vale como checkpoint.

**O que cada evento faz**

8. **Morrer:** a viva volta a ser a cópia do checkpoint, e o Freny reaparece nele. Quem morreu antes do checkpoint continua morto, e quem morreu depois volta.
9. **Fechar o jogo ou sair para o menu:** a memória é descartada. Ao continuar, a viva e o checkpoint vêm do disco, e o Freny aparece no último save point.
10. **Trocar de sala:** não muda nenhuma cópia. O checkpoint continua valendo em qualquer sala da campanha, o que acaba com o comportamento de apagá-lo ao trocar de cena (`CampaignRun.cs:148`).
11. **Voltar a uma sala:** os objetos consultam o estado ao carregar. Um inimigo morto não aparece, e um inimigo vivo volta ao ponto inicial com vida cheia.

**Tudo volta junto**

12. A volta vale para tudo o que está no estado, não só para inimigos: vaso quebrado, item pego, alavanca e atalho aberto depois do checkpoint também voltam. Assim um estado nunca fica pela metade, com um atalho aberto por uma alavanca que voltou atrás.
13. **Bolsa da campanha:** moedas e XP ganhos na campanha ficam numa bolsa que faz parte do estado, e só entram na conta do jogador no save point. Sem isso, dava para matar um inimigo, pegar as moedas, morrer e repetir sem fim, porque o inimigo volta. Morrer devolve a bolsa ao valor do checkpoint, e sair sem salvar perde o que estava nela.
14. A Mochila da campanha guarda seus dados neste estado e segue as mesmas cópias. No protótipo o Freny reaparece com a vida cheia, e o resto volta como estava no checkpoint. A decisão final é do GDD de Morte e retorno.

**Interface**

15. Os outros sistemas só usam estas operações: `Get`, `Set`, `Remove`, `MarkCheckpoint`, `RestoreCheckpoint`, `SaveToDisk` e `LoadFromDisk`. É uma classe C# comum, sem depender de cena, para poder ser testada fora da Unity.
16. O estado só existe na campanha. A Arena não o lê nem o escreve, e nada aqui passa pela API do ranking.
17. O sistema ganha a chave `Features.CampaignWorld`. Desligada, a campanha continua recarregando tudo a cada cena, como hoje.

### States and Transitions

| De | Evento | Viva | Checkpoint | Disco | Freny aparece em |
|---|---|---|---|---|---|
| Jogando | Mudança no mundo | Atualiza | — | — | — |
| Jogando | Toca checkpoint | — | Copia a viva | — | — |
| Jogando | Usa save point | Bolsa zera | Copia a viva | Copia a viva, e a bolsa vai para a conta | — |
| Jogando | Morre | Copia o checkpoint | — | — | Último checkpoint |
| Jogando | Sai ou fecha o jogo | Descartada | Descartado | — | — |
| Menu | Continuar | Copia o disco | Copia o disco | — | Último save point |
| Menu | Novo jogo no slot | Vazia | Vazio | Apagado | Início da campanha |

Antes do primeiro checkpoint, o ponto de retorno é a entrada da campanha, com o estado vazio.

### Interactions with Other Systems

| Sistema | O que usa |
|---|---|
| Mapa da masmorra | Define os nomes das salas. Guarda atalhos e salas visitadas. |
| Mochila da campanha | Guarda vida, munição, itens e vidas carregadas. |
| Morte e retorno | Chama `RestoreCheckpoint` e `MarkCheckpoint`. Define o que o jogador vê. |
| Inimigos | Ao carregar a sala, consultam se estão mortos. Ao morrer, registram. |
| Interativos e puzzles, Itens-chave, Chefes | Registram o que foi resolvido, pego ou derrotado. |
| Escassez | Vasos quebrados e itens colocados não voltam, então a quantidade é finita. |
| Save e Continuar | Chama `SaveToDisk` e `LoadFromDisk`, e cuida das telas. |
| `SaveSystem` existente | O estado entra no `SaveData` como um campo novo, por slot, em `Capture` e `Apply`. |

## Formulas

**Tamanho do estado:** `S = R × E × B`

| Variável | Significado | Valor estimado |
|---|---|---|
| `R` | Salas da campanha | 81 |
| `E` | Registros por sala no pior caso | 40 |
| `B` | Bytes por registro em JSON | 40 |
| `S` | Tamanho do estado | cerca de 130 KB |

Os valores de `E` e `B` são estimativas, ainda não medidas. O limite que importa é o da cópia na nuvem, a ser medido no protótipo.

## Edge Cases

- **Item solto no chão:** some ao sair da sala ou morrer.
- **Vaso que volta depois da morte:** sorteia a queda de novo. Ganha quem morre de propósito, mas gasta tempo e o que usou desde o checkpoint.
- **Morrer no mesmo instante de tocar um checkpoint:** morto não marca checkpoint.
- **Inimigo morto por outro inimigo ou por buraco:** conta como morto.
- **Sala refeita numa atualização:** registros de ids que sumiram são ignorados, e um id novo aparece como original. Saves antigos seguem funcionando.
- **Saves atuais:** não têm estado e têm `campaignUnlocked` de fases que serão refeitas. Moedas, XP e Armeira ficam, e a campanha recomeça no início. Isso precisa ser avisado nas notas da versão.
- **Fechar o jogo à força:** igual a sair sem salvar. O disco só é tocado no save point, então não fica um estado pela metade.
- **Trocar de slot ou receber cópia da nuvem durante a campanha:** só permitido fora de uma fase, regra que o `SaveSystem.Replace` já segue.
- **Renascer:** é da conta e não mexe no estado da campanha.
- **Contadores da conta, como Missões do dia e Estatísticas:** continuam somando na hora, então matar o mesmo inimigo depois de morrer conta de novo. Não afeta o ranking. A ver em Melhorias entre modos.
- **Comprar com a bolsa:** o Mercador é da Alpha, e o GDD dele define se aceita bolsa e conta.

## Dependencies

Nenhuma dependência de design: este sistema é a base. Dependem dele Mapa da masmorra, Mochila da campanha, Morte e retorno, Interativos e puzzles, Chefes de masmorra, Save e Continuar, Mapa do jogador e Conquistas da campanha.

No código, muda `CampaignRun` (checkpoint), `Checkpoint`, `Breakable`, `Enemy`, `Player.AddCoins` e `SaveData`. `Player` e `Enemy` são usados pela Arena, então cada mudança ali precisa de teste nos dois modos.

## Visual/Audio Requirements

O checkpoint e o save point precisam ser diferentes à primeira vista, em Dark Fantasy. O visual fica para o Kit visual da masmorra e a bíblia de arte. Usar o save point precisa ter um som próprio e uma confirmação clara.

## Game Feel

Salvar precisa passar segurança, e a morte não pode parecer injusta: ao reaparecer, o jogador deve reconhecer o mundo como estava no checkpoint.

## UI Requirements

O HUD mostra a bolsa separada do saldo da conta. O menu avisa ao sair sem salvar: "O que você fez desde o último save point será perdido". O texto final fica para Save e Continuar.

## Cross-References

- `design/gdd/systems-index.md`: decisões do José de 2026-11-05
- `design/game-brief.md`: direção do produto

## Acceptance Criteria

Testes automáticos da classe, sem Unity (ainda não existem: o projeto não tem pasta de testes, e o protótipo foi conferido pelo teste na Unity):

- [ ] Depois de `MarkCheckpoint`, um `Set` seguido de `RestoreCheckpoint` devolve o valor anterior.
- [ ] Um `Set` feito antes de `MarkCheckpoint` continua valendo depois de `RestoreCheckpoint`.
- [ ] `SaveToDisk` seguido de `LoadFromDisk` devolve exatamente o mesmo estado, incluindo a bolsa.
- [ ] Um JSON de save sem estado carrega como estado vazio, sem erro, com moedas e XP intactos.

Testes na Unity, com captura guardada em `production/qa/evidence/`:

- [ ] Matar um inimigo, tocar um checkpoint e morrer: o inimigo continua morto.
- [ ] Tocar um checkpoint, matar um inimigo e morrer: o inimigo volta, e as moedas dele saem da bolsa.
- [ ] Matar um inimigo na sala A, ir à sala B, tocar um checkpoint, morrer e voltar à sala A: o inimigo continua morto.
- [ ] Matar um inimigo, tocar um checkpoint, fechar o jogo sem salvar e continuar: o inimigo volta.
- [ ] Matar um inimigo, usar um save point, fechar o jogo e continuar: o inimigo continua morto, e as moedas estão na conta.
- [ ] Repetir matar, pegar moedas e morrer não aumenta o saldo da conta.
- [ ] Depois de jogar a campanha, uma partida da Arena pontua e é aceita pelo ranking como antes.
- [ ] Com `Features.CampaignWorld` desligada, a campanha funciona como hoje.

## Open Questions

1. **Tamanho real do estado na nuvem:** medir no protótipo.

Decidido pelo José em 2026-11-05: tudo volta junto ao morrer, inclusive atalhos (regra 12), e moedas e XP ficam na bolsa até o save point (regra 13).
