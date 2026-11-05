## Technical Debt Register
Last updated: 2026-11-04
Total items: 7 | Estimated total effort: 4 L + 1 XL (2 itens sem estimativa)

Scan de 2026-11-04: 184 arquivos `.cs` em `Assets/` (32.975 linhas). 0 marcadores TODO/FIXME/HACK/XXX/`[Obsolete]`/`@deprecated`.
Não verificado neste scan: funções com mais de 50 linhas, código duplicado, valores de jogabilidade no código, e se as chamadas `Find*`/`Camera.main` estão em caminhos quentes (`Update`).

| ID | Category | Description | Files | Effort | Impact | Priority | Status | Added | Sprint |
|----|----------|-------------|-------|--------|--------|----------|--------|-------|--------|
| TD-001 | Architecture | `ArenaAutopilot.cs` tem 1925 linhas (candidato a god object). Não refatorar sem o José pedir (CLAUDE.md). | Assets/Scripts/Arena/ArenaAutopilot.cs | L | Med | — | Open | 2026-11-04 | Backlog |
| TD-002 | Architecture | `Enemy.cs` tem 1526 linhas e é base de todos os inimigos. | Assets/Scripts/Enemy/Enemy.cs | L | Med | — | Open | 2026-11-04 | Backlog |
| TD-003 | Architecture | `Player.cs` tem 1123 linhas. | Assets/Scripts/Player/Player.cs | L | Med | — | Open | 2026-11-04 | Backlog |
| TD-004 | Architecture | Mais 10 arquivos entre 511 e 875 linhas: FrenyMotion (875), ArenaDirector (796), OnlineServices (777), CombatHud (669), InventoryScreens (569), AcidPitArena (535), ArenaMap (533), ItemPickup (514), AcidPuddle (512), Cyclops (511). | Assets/Scripts/Player/FrenyMotion.cs, Assets/Scripts/Arena/ArenaDirector.cs, Assets/Scripts/Online/OnlineServices.cs, Assets/Scripts/UI/CombatHud.cs, Assets/Scripts/Menu/InventoryScreens.cs, Assets/Scripts/Arena/AcidPit/AcidPitArena.cs, Assets/Scripts/Arena/ArenaMap.cs, Assets/Scripts/ItemPickup.cs, Assets/Scripts/Enemy/Cyclops/AcidPuddle.cs, Assets/Scripts/Enemy/Cyclops/Cyclops.cs | — | Low | — | Open | 2026-11-04 | Backlog |
| TD-005 | Test | Não existe `Assets/Tests/`: 0 testes automatizados. Só o CI de compilação e `.meta` protege os PRs. | Assets/Tests/ (ausente) | L | High | — | Open | 2026-11-04 | Backlog |
| TD-006 | Architecture | 9 singletons estáticos `Instance` (GameManager, CampaignRun, GameMenus, MusicPlayer, UIManager, ArenaAssets, AcidPitArena, OnlineServices, OnlineHUD). `.claude/rules/` proíbe em código novo; o existente fica como está sem pedido do José. | Assets/Scripts/GameManager.cs, Assets/Scripts/Campaign/CampaignRun.cs, Assets/Scripts/Menu/GameMenus.cs, Assets/Scripts/Menu/MusicPlayer.cs, Assets/Scripts/UI/UIManager.cs, Assets/Scripts/Arena/ArenaAssets.cs, Assets/Scripts/Arena/AcidPit/AcidPitArena.cs, Assets/Scripts/Online/OnlineServices.cs, Assets/Scripts/Online/OnlineHUD.cs | XL | Low | — | Open | 2026-11-04 | Backlog |
| TD-007 | Performance | 46 ocorrências de `Find*`/`Camera.main` em 33 arquivos. Não verificado se estão em `Update` ou só em inicialização; só vira dívida se estiverem em caminho quente. | Assets/Scripts (33 arquivos, ver `Grep` por `FindObjectOfType\|FindObjectsOfType\|FindFirstObjectByType\|FindAnyObjectByType\|GameObject.Find(\|FindWithTag\|Camera.main`) | — | — | — | Open | 2026-11-04 | Backlog |
