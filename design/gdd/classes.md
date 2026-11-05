# Classes

> **Status**: In Design
> **Author**: José + Claude
> **Last Updated**: 2026-11-05
> **Implements Pillar**: arena estilo Mercenários e campanha com a mesma mecânica

## Summary

Três classes do mesmo Freny, com os mesmos botões e os mesmos espaços: o Sobrevivente (guerreiro), o Flagelante (tanque) e o Herege (mago). O que muda é o jogo de armas. O Sobrevivente e o Flagelante existem no código; o Herege está só no desenho.

> **Quick reference** — Layer: `Gameplay` · Priority: `Vertical Slice` · Key deps: `Combate e armas, Armeira, Ranking`

## Overview

O jogador compra a classe na Armeira, uma vez, e escolhe qual usar no Inventário. A escolha vale a partir da próxima corrida. Nenhuma classe muda movimento, pontos ou número de corações. O piloto automático só joga de Sobrevivente.

## Player Fantasy

- **Sobrevivente:** improvisa com o que cai, a meia distância.
- **Flagelante:** segura a pancada de frente, chega perto e esmaga.
- **Herege:** bate de longe com magia proibida e foge quando chegam perto.

## Detailed Rules

| Espaço | Sobrevivente | Flagelante | Herege (ainda não feito) |
|---|---|---|---|
| Perto | Espada | Mangual | Adaga Ritual |
| Perto, do chão | Espadão | Mangual de Três Cabeças | Foice de Ceifa |
| Botão de atirar | Revólver | Escudo de Espinhos | Tomo de Fogo-Fátuo |
| Botão de atirar, do chão | Escopeta | Pavês de Ferro | Evangelho Negro |
| Área | Granada | Incensário de Brasas | Selo de Enxofre |
| Controle | Rede | Corrente de Açoite | Círculo de Sal |

**Escudo de Espinhos.** Segurar o botão de atirar bloqueia o que vem de frente: ele anda na metade da velocidade e não ataca. Cada bloqueio gasta o dano do golpe, e golpes em menos de 0,25 s contam uma vez. Em zero, o escudo quebra e volta em 1,6 s gastando 1 reparo. Quem bate de perto leva 1 de dano. Queda, ácido e golpe pelas costas passam pelo escudo.

**Armeira.** As melhorias valem por espaço: Dano da pistola vira espinhos, Tambor vira resistência, Reserva vira reparos (+2 por nível), Recarga vira a volta do escudo. A classe comprada continua depois de Renascer.

**Drops.** Mesmas chances. Balas viram reparos (1 por drop), cartuchos viram cargas, granada vira incensário, e assim por diante.

## Formulas

Os números ficam em `Assets/Sprites/Characters/Player/Resources/FrenyPoses/flagelante_kit.json`. O dano por segundo é o dano do combo dividido pelo tempo do combo, contra um alvo só.

| Medida | Flagelante | Sobrevivente | Herege |
|---|---|---|---|
| Arma de perto | 1, 1, 2 em 0,35 / 0,35 / 1,0 s = 2,35 | 1, 1, 1 em 0,25 / 0,25 / 0,65 s = 2,61 | 1, 1, 2 em 0,25 / 0,25 / 0,65 s = 3,48 |
| Fúria (+1 por golpe) | 4,12 | 5,22 | 6,09 |
| Drop de perto, dano total | 5 (4 golpes) | 6 (6 golpes) | 8 (6 golpes) |
| Botão de atirar, dano/s | Sem tiro | 4 / (0,6 + 1,6) = 1,82 | 6 / (0,9 + 1,8) = 2,22 |
| Drop do botão de atirar, dano/s | 2 / 0,5 = 4,0 | 3 / 0,6 = 5,0 | 3 / 0,55 = 5,45 |
| Arremesso, dano e raio | 2, raio 3 | 3, raio 2,5 | 4, raio 2,5 |
| Dano que aguenta com armadura cheia | 7 + 20 no escudo | 7 | 7 |
| Invulnerável depois de apanhar | 1,2 s | 1,2 s | 0,9 s (proposta) |

Escudo: (1 inicial + 4 reparos) × 4 de dano = 20.

## Edge Cases

- **Vida:** `vitals.js` recusa vida máxima fora de 4 a 9. Nenhuma classe muda os corações.
- **Sem dano por 240 s:** a API manda a corrida para revisão se ela entrar no ranking. Um bom jogador de escudo pode cair nisso.
- **Cajado do Mago:** substitui o escudo enquanto dura, e o botão volta a atirar.
- **Piloto ligado no meio da corrida:** ele continua batendo, mas o tiro não faz nada no Flagelante.
- **Chave desligada:** a classe some, e quem a usava volta ao Sobrevivente sem perder a compra.
- **Campanha:** puzzle e porta não podem exigir tiro, porque o Flagelante não atira.

## Dependencies

Combate e armas, Armeira (`FrenyProgress`), Inventário, Renascer, HUD, drops (`ItemPickup`), ranking (`freny-online`).

## Tuning Knobs

Todos os campos de `flagelante_kit.json`. A primeira coisa a olhar em teste é se o escudo atrapalha manter os 10 s do combo.

## Acceptance Criteria

- [ ] Sem comprar, o jogo fica igual ao atual, e as corridas passam no validador.
- [ ] De Flagelante, golpes de frente não tiram vida com o escudo erguido, e golpes de costas tiram.
- [ ] O escudo quebra em 4 de dano e volta gastando 1 reparo.
- [ ] A corrida de Flagelante até 5 mil pontos passa em `validateRun` e `checkVitals`.
- [ ] O equilíbrio perto do segundo Ciclope, que depende do José jogar.
