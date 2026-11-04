# Game Brief: The Rise of Freny

> **Reverse-documented from implementation** — gerado por `/reverse-document`
> a partir de `Assets/` em 2026-11-04, no commit `a2340c2` (com `docs/engine-reference/unity/VERSION.md` modificado e não commitado).
> Isto registra o que o código **faz**; o que estiver marcado `INTENT UNKNOWN` foi
> inferido, não confirmado pelo José. Onde este documento e o código discordarem,
> não presuma que o documento é o requisito.
>
> **Cobertura:** li `Features.cs`, `Tuning.cs`, `ArenaDirector` (até a linha 220), `ArenaDifficulty`, `ArenaRunManager`, `CampaignLevels`, `CampaignRun` (início), `FrenyProgress` (início) e a tela `GuideScreen` (linhas 60-260). **Não li:** `Online/*`, `Variety/*`, `Menu/*` (quase todo), `Runner/*` (só o início de `Player.cs`), cenas, prefabs, `Tools/`, `firebase/`. Valores citados vêm do código ou do texto do Guia dentro do jogo; não rodei o jogo.

**One-sentence pitch:** Freny, um herói de ação 2D, luta sozinho numa arena contra ondas cada vez mais duras (até o chefe Ciclope) para bater recordes no ranking online, e também tem uma campanha de fases por masmorras.

## Core loop
- **Arena:** lutar (espada, revólver, escopeta, granada, rede, cajado) → ganhar pontos, moedas e XP → a dificuldade sobe sozinha com o tempo e a pontuação → morrer e começar outra corrida.
- **Entre corridas:** gastar moedas na Armeira (vida, velocidade, pulo duplo, melhorias de arma, visuais) e XP/pontos de nível no "cérebro" do piloto automático → voltar mais forte.
- **Meta:** Missões do dia, conquistas, Renascer (bônus de 5%) e visuais dão motivo para repetir.
- **Campanha:** atravessar fases por masmorras com checkpoint, portas, escadas e líquidos; cair num buraco custa um coração; a porta de saída abre a próxima fase.

## Player goal & fail state — what "working" looks like
- **Arena:** maior pontuação possível, em três níveis (Fácil, Médio, "Freny" = o difícil oficial), manual ou automático; a corrida acaba quando o Freny morre (4 corações base + armadura). Vale para o ranking só a corrida jogada à mão do início ao fim; o automático tem ranking próprio e ganha 1/3 das moedas e metade do XP.
- **Campanha:** chegar à porta de saída de cada fase; morrer volta ao último checkpoint. Existem as masmorras 1 e 2, com 9 fases cada, a última com chefe. O código cita 9 masmorras mais um chefe final, mas só 2 existem.

## MVP — what must exist to be the game
1. Arena com Freny (mover, pular, esquiva, espada em combo de 3 golpes, revólver) e o `ArenaDirector` que faz a corrida esquentar.
2. Inimigos: inseto, morcego, Golem, Golem Vermelho (≥50 mil pontos), Escudeiro, Feiticeiro, Mina-aranha, e o Ciclope (50 mil pontos, depois a cada 150 mil após o anterior morrer; dois de uma vez só a partir de 1 milhão).
3. Drops por necessidade (poção, balas, granadas, armadura), moedas, baús e vasos.
4. Progressão salva: moedas, XP/nível, Armeira, Missões do dia, Renascer, visuais.
5. Ranking online por nível, com conta, validação de corrida no servidor (`freny-online`) e regras do Firestore.
6. Campanha: masmorras 1 e 2 (18 fases).
7. Piloto automático (jogar sozinho para farmar).

## Out of scope — not building this
- Em `Features.cs` estão **desligados**: Poço de Ácido, Mutador da semana, visuais extras (outros elmos, capas, torsos branco e dourado; "próxima atualização").
- A Armeira **não vende armas** (código removido em 2026-11-01); armas só caem do chão e de baús.
- Nenhuma mecânica pode mudar a API do ranking: desligar só remove coisas.
- Fora desta versão (por regra do projeto): build WebGL, deploy na Vercel e push em `freny-online` sem o José pedir.
- Masmorras 3 a 9 e o chefe final: citados no código, sem fase.

## Build order
Estado atual, não plano: arena e inimigos já existem, depois progressão e loja, ranking online, campanha (2 de 9 masmorras). `INTENT UNKNOWN` sobre o que vem a seguir.
1. Fechar o que está ligado hoje (checar cada chave de `Features.cs` na Unity local).
2. Ligar `ExtraLooks` quando a próxima atualização sair.
3. Decidir o destino do Poço de Ácido e do Mutador da semana.
4. Masmorras 3+ da campanha (`CampaignLevels.cs`: uma linha por fase mais a cena).

---
**Who it's for / what they feel:** `INTENT UNKNOWN` — jogador de ação 2D que quer subir no ranking e ver o número crescer (combo que cresce e treme, banners de Double a Penta Kill, narrador).

**Art & audio direction:** Dark Fantasy (regra do `CLAUDE.md`); não verifiquei a arte nem o áudio no código. O Capacete e o Traje Berserker, o Ciclope e a Lua de Sangue combinam com isso.

**Reference game:** `INTENT UNKNOWN` — o código não diz. Pelo formato (arena de pontos que esquenta, drops raros, chefe recorrente, meta de moedas), lembra jogos de sobrevivência em arena, mas é palpite meu.

## Números que o código fixa (para os stories; fonte entre parênteses)
- Rampa: 0→1 em `RampSeconds` = 300 s × fator do nível (Fácil 1,6; Médio 1; Freny 0,7); pausa no 1º Ciclope (50 mil) e volta em 1 milhão (`ArenaDirector`, `Tuning`).
- Inimigos vivos: `Lerp(3×min(1,Pico), 12×Pico, dificuldade) + overtime + ciclopes mortos`, teto 24. Pico: 0,6 / 1 / 1,5. Intervalo entre spawns: `Lerp(3, 0,8, dif) × fator / (1 + 0,15×overtime)`.
- Nível do jogador: de n para n+1 custa `200·n²` XP (`FrenyProgress`).
- Dano: golpe comum 1 coração; Golem e bola do Feiticeiro 2; Ciclope 100 de vida (Guia).

## Intenção não confirmada (o código não diz o porquê)
- `INTENT UNKNOWN`: o autopiloto ganhar menos (1/3 de moedas) é para impedir farm ocioso ou só para separar o ranking?
- `INTENT UNKNOWN`: a dificuldade "Freny" ser o nível oficial e a escala sem fim depois do pico (overtime, `EnemyToughening`) é desenho final ou ainda será ajustada? Há um comentário de José de 2026-10-29 dizendo que ficou difícil demais rápido.
- `INTENT UNKNOWN`: campanha e arena são dois pilares iguais, ou a campanha é secundária? Só 2 de 9 masmorras existem.
- `INTENT UNKNOWN`: o `Runner` (`Assets/Runner/`) é parte do jogo ou protótipo antigo? Não li.
- Violações das regras do projeto que notei e **não mexi** (`.claude/rules/` vale só para código novo): valores de jogo fixos no código (como `Mathf.Lerp(3f, 0.8f, ...)`) em vez de dados externos.
