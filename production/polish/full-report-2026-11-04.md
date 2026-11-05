## Performance Profile: Full (análise estática, sem profiler)
Generated: 2026-11-04
Enforcement: `warn` (padrão). Violações seriam apenas achados, mas nenhum orçamento está definido.

### Inputs
| Input | Estado |
|---|---|
| `performance.target_framerate` (project.yaml) | ABSENT |
| `performance.frame_budget_ms` | ABSENT |
| `performance.draw_call_limit` | ABSENT |
| `performance.memory_ceiling_mb` | ABSENT |
| Orçamentos em design docs / CLAUDE.md | ABSENT (`.claude/docs/technical-preferences.md` diz `[TO BE CONFIGURED]`; `design/game-brief.md` não cita orçamento) |
| Dados de profiler (Unity Profiler, Frame Debugger, memória) | ABSENT |
| Código-fonte `Assets/Scripts` | FOUND (leitura parcial, ver abaixo) |

### Performance Budgets
| Metric | Budget | Estimated Current | Status |
|--------|--------|-------------------|--------|
| Frame time | não definido | sem medição | NOT ASSESSED — NO DATA |
| Memory | não definido | sem medição | NOT ASSESSED — NO DATA |
| Load time | não definido | sem medição | NOT ASSESSED — NO DATA |
| Draw calls | não definido | sem medição | NOT ASSESSED — NO DATA |

Folga contra orçamento: `no budget set — headroom not assessed`, nas quatro linhas.
Não há veredito de "dentro do orçamento". Não houve medição e ninguém escolheu um número.

### Escopo da análise estática (o que foi e o que NÃO foi verificado)
Verificado: Grep de `Find*`/`Camera.main`/`FindCompat.All` em `Assets/Scripts` (os 80 primeiros resultados) e leitura do contexto de 12 arquivos. Contagem de `Update/LateUpdate/FixedUpdate`: 94 ocorrências em 79 arquivos.
Não verificado: corpo dos 94 métodos de update, alocações por quadro (`new`, LINQ, strings), `OnGUI` além dos trechos citados, física (raycasts/overlaps), partículas, shaders, tamanho de textura, `Assets/Runner`, `Assets/Scripts/Online` em runtime, tempo de carga de cena. Também não li o resto das ocorrências além das 80 primeiras.

### Hotspots Identified
Todos são candidatos por leitura de código. Nenhum foi medido.

| # | Location | Issue | Estimated Impact | Fix Effort |
|---|----------|-------|------------------|------------|
| 1 | `Assets/Scripts/Campaign/LevelDoor.cs:25` (chamado de `CampaignHud.cs:74`, dentro de `OnGUI`, que começa na linha 59) | A propriedade `Locked` faz `FindObjectsByType<Enemy>` (varre a cena e aloca um array) a cada chamada. `OnGUI` roda mais de uma vez por quadro (Layout e Repaint). Só ocorre com o jogador perto de uma porta com `bossName` e `pressUp`. | Desconhecido. Cresce com o nº de inimigos na cena. Só na campanha, perto da porta. | S |
| 2 | `Assets/Scripts/Arena/AcidPit/FallingSpike.cs:103` (`HitThings`, chamado de `Update`, linha 94) | `FindAnyObjectByType<Player>()` todo quadro enquanto o espinho cai. Há N espinhos. | Desconhecido. Varredura de cena por espinho por quadro. | S |
| 3 | `Assets/Scripts/Enemy/Cyclops/AcidPuddle.cs:192` e `:292`, `Shockwave.cs:48` | `FindAnyObjectByType<Player>()` dentro de `if (freny == null)`, guardado por cache. Só custa se o jogador estiver morto ou ausente (o cache fica nulo e a busca repete a cada quadro). | Baixo. Só no caso de borda. | S |
| 4 | `Assets/Scripts/Audio/Sfx.cs:146` (`Pan`), `UI/DamageNumbers.cs:42` e `UI/CombatHud.cs:247` (ambos em `OnGUI`) | `Camera.main` repetido. Desde a Unity 2020 é cacheado internamente, então o custo é pequeno, mas `Sfx.Pan` roda a cada som tocado. | Baixo | S |
| 5 | `Assets/Scripts/UI/CombatHud.cs:43` | `GetComponent<Player>()` duas vezes na mesma linha dentro de `OnGUI`, mas só enquanto `player == null`. | Baixo | S |

Descartados (lidos e sem problema aparente):
- `WallRescue.cs:26`: o resultado fica em cache e só é refeito quando uma cena troca ou um mapa fica obsoleto.
- `CombatHud.cs:336/366`: guardados por `coinsSearched` e `scoreSearched`, ou seja, buscas únicas.
- `CrumbleBlock.cs:42` e `MovingBlock.cs:69`: ficam em `Start`.
- `CampaignMinimap`, `StageTerrain`, `ArenaBootstrap`, `ArenaDayNight`, `AcidPitArena`: usam `FindCompat.All` no setup. Não conferi se alguma dessas chamadas está em método chamado a cada quadro.

### Optimization Recommendations (Priority Order)
1. **Guardar o estado "chefe vivo" em vez de varrer a cena** — `LevelDoor.Locked`
   - Location: `LevelDoor.cs:25`, chamador `CampaignHud.cs:74`
   - Expected gain: não estimado (sem medição)
   - Risk: Low
   - Approach: guardar o resultado em cache e atualizar com timer curto ou evento de morte do chefe. Mudança só de código novo/local, sem tocar em `GameManager`.
2. **Cachear `Player` no `Start` do `FallingSpike`** (como `CrumbleBlock` e `MovingBlock` já fazem)
   - Location: `FallingSpike.cs:103`
   - Expected gain: não estimado
   - Risk: Low
   - Approach: campo `hero`, resolvido uma vez, com reresolução apenas se nulo.
3. Itens 3 a 5: deixar para depois. O ganho esperado é pequeno.

Nenhuma dessas mudanças foi aplicada. Pelo CLAUDE.md, não refatoro código existente sem o José pedir.

### Quick Wins (< 1 hour each)
- Itens 1 e 2 acima (cada um é uma mudança local).

### Requires Investigation
- Tudo o que está em "Escopo… Não verificado". Principalmente `Update` dos inimigos e do `ArenaDirector`, `OnGUI` (IMGUI aloca e roda várias vezes por quadro) e o custo de `Light2D`/Tilemap na arena.
- Rodar o Unity Profiler no alvo real (web/WebGL, desktop?). A plataforma-alvo e o piso de hardware não estão registrados em lugar nenhum.
- Relacionado: `docs/tech-debt-register.md` TD-007 (46 ocorrências de `Find*` marcadas como "não verificado"). Este relatório verifica parte delas: 2 em caminho quente (itens 1 e 2), o restante em inicialização ou com cache.

### Deferred to Polish
Nenhum. A Fase 5 (decisão de escopo) não rodou porque nenhum hotspot tem esforço M ou L.

### Verdict
**COMPLETE** — perfil estático gerado. As quatro linhas de orçamento são **NOT ASSESSED — NO DATA**: nenhum número foi definido e nenhuma medição foi feita. Este relatório não diz que o jogo está dentro ou fora do orçamento.
