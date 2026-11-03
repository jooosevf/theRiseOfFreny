# The Rise of Freny -- Claude Code Game Studios (versão enxuta)

Jogo de ação 2D em Unity, com arena, campanha e online. Este arquivo junta o kit
Claude Code Game Studios 1.2.0 com as regras do projeto. Instalamos 9 dos 49
agentes (ver "Agentes").

## Regras do Freny (vencem as do kit)

Quando uma regra do kit ou um skill discordar destas, valem estas:

- **Direção de arte:** tudo (música, arte, UI, menus, inimigos) segue Dark Fantasy.
  Sinalize o que fugir disso.
- **Combate:** o retorno ao jogador cresce e recompensa (tamanho, cor, som, voz).
  Esse retorno fica fora da pontuação, a não ser que a API de ranking mude junto.
- **Chaves de mecânica:** toda mecânica nova ganha uma linha em
  `Assets/Scripts/Features.cs`. Desligar só pode remover coisas. Ver
  `docs/chaves-de-mecanicas.md`.
- **Branches:** PR sempre para `dev`, nunca para `master`. Ver `CONTRIBUTING.md`.
- **`.meta` e cenas:** siga `CONTRIBUTING.md`. Cada `.meta` vai no mesmo commit do
  arquivo, e o GUID nunca muda.
- **Versão da Unity:** exatamente `6000.6.7f1`. O CI recusa outra.
- **Web:** nada de build WebGL, deploy na Vercel ou push em `freny-online` sem o
  José pedir com as próprias palavras.
- **Código existente:** as regras de `.claude/rules/` valem para código novo. Não
  refatore o que já existe só para cumpri-las (por exemplo `GameManager` ou valores
  no código) sem o José pedir.
- **Autonomia:** o "May I write" e o "No commits without user instruction" abaixo
  valem quando o José estiver rodando um skill do kit nesta sessão. Fora disso,
  mudanças reversíveis em branch seguem sem pedir licença.
- **Mensagens de commit:** siga o estilo do histórico do repo (`Menus: moedas ...`),
  não o Conventional Commits do kit.
- **Validar:** toda mudança visível é testada na Unity local antes do merge. O CI
  (`.ci/CompileCheck`) checa compilação e `.meta` em todo PR.

## Technology Stack

- **Engine**: Unity 6000.6.7f1 (Unity 6, URP, 2D)
- **Language**: C#
- **Version Control**: Git, com branches curtas e PR para `dev`
- **Build System**: Unity Build Pipeline (`Assets/Editor/WebGLBuild.cs` para o site)
- **Asset Pipeline**: Unity Asset Import Pipeline (sem Addressables), geradores em `Tools/`
- **Online**: Firebase por REST (`Assets/Scripts/Online`, `firebase/`) e API na Vercel
  (repositório `freny-online`)

## Project Structure

Pastas reais do jogo:

- `Assets/Scripts/`: `Player`, `Enemy`, `Arena`, `Campaign`, `Shop`, `Variety`,
  `Environment`, `Audio` (jogabilidade), `UI`, `Menu` (interface), `Online` (rede),
  `Rendering` e os utilitários soltos na raiz
- `Assets/Runner/`: scripts, cenas e prefabs do Runner
- `Assets/Scenes/`, `Assets/Prefabs/`, `Assets/Shaders/`, `Assets/Sprites/`, `Assets/Audio/`
- `firebase/`: regras do Firestore. `Tools/`: geradores de arte, música, voz e fases
- `.ci/`, `.github/`: checagem de compilação e de `.meta`
- `design/`, `production/`, `docs/`: documentos do kit, criados conforme o uso

Onde o documento abaixo disser `assets/`, `tools/` ou `tests/`, leia `Assets/`,
`Tools/` e `Assets/Tests/`.

@.claude/docs/directory-structure.md

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: the line below is engine-specific. /setup-engine
     rewrites it to @docs/engine-reference/<engine>/VERSION.md for the chosen
     engine. -->
@docs/engine-reference/unity/VERSION.md

## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`.

## Agentes

Instalados: `creative-director`, `technical-director`, `producer`, `game-designer`,
`gameplay-programmer`, `ui-programmer`, `unity-specialist`, `qa-tester`,
`network-programmer`.

Os skills do kit ainda citam os agentes removidos. Quando um skill pedir um deles,
use o substituto e siga em frente, sem tratar isso como erro:

| Pedido pelo skill | Use |
|---|---|
| `lead-programmer`, `engine-programmer`, `tools-programmer`, `devops-engineer`, `security-engineer`, `performance-analyst` | `technical-director` |
| `ai-programmer`, `prototyper` | `gameplay-programmer` |
| `qa-lead` | `qa-tester` |
| `systems-designer`, `economy-designer`, `level-designer`, `live-ops-designer`, `analytics-engineer` | `game-designer` |
| `art-director`, `audio-director`, `sound-designer`, `narrative-director`, `writer`, `world-builder`, `community-manager` | `creative-director` |
| `ux-designer`, `accessibility-specialist`, `unity-ui-specialist`, `localization-lead` | `ui-programmer` |
| `technical-artist`, `unity-shader-specialist`, `unity-dots-specialist`, `unity-addressables-specialist` | `unity-specialist` |
| `release-manager` | `producer` |

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution** (ver "Autonomia" acima).
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing and cited by name
elsewhere in the repo, so they are restated here rather than lost:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.** A script
  that scores or judges will eventually contradict a mode or override it cannot
  see. (Cited by `artifact-check.sh` and `adr-dep-graph.sh`.)
