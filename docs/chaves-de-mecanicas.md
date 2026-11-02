# Chaves de mecânicas

Todas as chaves ficam em um só arquivo: [`Assets/Scripts/Features.cs`](../Assets/Scripts/Features.cs).

Para desligar uma mecânica, troque `true` por `false` na linha dela e salve. A chave vale para a build seguinte, e no Editor vale assim que a Unity recompilar. Mecânica nova ganha uma linha nesse arquivo.

## O que cada chave faz

| Chave | Mecânica | Desligada |
|---|---|---|
| `SpiderMine` | Mina-aranha | Ela não entra na lista de inimigos da Arena. Sai do Guia. |
| `Cyclops` | Ciclope | Nenhum Ciclope aparece. As duas missões dele saem do sorteio do dia. A Lua de Sangue continua. |
| `BossEscalation` | Inimigos a mais por Ciclope | Toda luta traz 2 feiticeiros, sem outros inimigos de companhia (o morcego e o inseto continuam). |
| `EnemyToughening` | Inimigos mais fortes a cada Ciclope morto | Eles continuam com a vida e a velocidade normais, e o aviso "ficaram mais fortes" não aparece. |
| `CyclopsFlatScore` | 1 ponto por abate com Ciclope vivo | Os abates valem os pontos normais durante a luta. |
| `Net` | Rede | Sai do Armeiro, do Inventário, do HUD, dos Controles e dos botões de toque. |
| `MageStaff` | Cajado do Mago | Os feiticeiros não deixam mais o cajado. |
| `Fury` | Fúria | Some a barra, o botão, o "+5 s" do Armeiro e a Fúria do baú dourado. |
| `GrenadeFuse` | Pavio de 5 s da granada | A granada explode onde bater (no máximo 1 s no ar), sem segurar para queimar o pavio. |
| `WeaponBuffs` | Armeiro por níveis | As melhorias de arma e o Crítico saem da loja e param de valer, mesmo para quem já comprou. Ficam vida, velocidade, pulo duplo, espada mais rápida e Rede. **As armas não voltam à venda**: esse código foi removido em 2026-11-01. |
| `InstaKill`, `Nuke`, `Magnet` | Drops raros | Aquele drop para de cair; os outros mantêm as chances. Cada um sai do Guia. |
| `Blessings` | Bênçãos | O baú dourado para de oferecer bênçãos. |
| `WeeklyMutators` | Mutador da semana (**desligado** desde 2026-11-02, a pedido do José) | Nenhuma regra extra. Some do menu da Arena. |
| `GoldenChest` | Chave e baú dourado | O Ciclope só deixa o baú prateado. Os visuais exclusivos somem da tela Visual e, como as bênçãos só vêm desse baú, também não há bênçãos. |
| `AcidPit` | Poço de Ácido | Só existe a arena clássica, mesmo para quem já abriu o Poço. |
| `DailyMissions` | Missões do dia | Some o botão do menu e nada mais conta nem paga. |
| `Rebirth` | Renascer | Some o botão, o bônus de 5% para de valer e os visuais de renascer somem. A estrela de quem já renasceu continua no ranking. |
| `Looks` | Visual | Some a tela e o Freny aparece com o visual original. O que foi comprado continua salvo. |
| `BerserkerSuit` | Traje Berserker | Some da tela Visual e o Freny volta à roupa normal. Quem comprou continua com ele salvo. |

## Ranking

Nenhuma chave precisa de mudança na API. O validador (`api/_lib/validate.js` no freny-online) confere os abates que a corrida envia, e desligar uma mecânica só tira coisas: nenhum inimigo novo, nenhum inimigo mais cedo, nenhum spawn mais rápido. Com `CyclopsFlatScore` desligada, o abate vai sem a marca de 1 ponto e a API soma os pontos normais.

Sem bênção ou sem mutador, a corrida envia o campo vazio, e a API só anota esse campo no log.

Testado no validador real com corridas até 5 mil pontos, com tudo ligado e com tudo desligado.

## O que continua aparecendo

- O Guia ainda cita "rede e cajado" na explicação do piloto automático, e os níveis do Armeiro nas armas ("40 ou 50 com o Armeiro"), mesmo com essas chaves desligadas.
- Com `Cyclops` desligada, as conquistas do Ciclope e do olho continuam na lista, mas não dá para ganhá-las nessa versão.
- A Lua de Sangue, o Escudeiro, o Golem Vermelho e o Feiticeiro Supremo não têm chave.
