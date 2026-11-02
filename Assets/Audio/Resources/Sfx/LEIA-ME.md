# Efeitos sonoros

Cada arquivo aqui é tocado pelo `Sfx` (Assets/Scripts/Audio/Sfx.cs) pelo nome: `Sfx.Play("coin")`.
Para trocar um som, substitua o arquivo mantendo o nome. Variações ficam lado a lado como
`nome_1`, `nome_2`...; o jogo sorteia uma a cada vez. Sem arquivo, aquele som fica mudo.
Volume e intervalo mínimo de cada som estão na tabela `Settings` no topo do `Sfx.cs`.

Os inimigos usam o prefixo do tipo (`bug`, `golem`, `licker`, `bat`, `sorcerer`, `supreme`)
com `_windup`, `_attack`, `_hurt`, `_death`, `_step`, `_cast`, `_laugh` e `_scream`. Um inimigo
novo só precisa dos arquivos e do `SoundName` no script dele.

Todos vieram do pacote gratuito Sonniss GDC Game Audio Bundle (uso comercial liberado, sem
atribuição obrigatória), cortados, convertidos para mono 44,1 kHz e normalizados.

| Arquivo | Origem (pacote / arquivo, trecho usado) |
|---|---|
| `freny_jump` | NoizBoy - Hong Kong Action Kit - Hit Kit #1 / Seq 2.27 whoosh #1 96 HK1.wav (0–0.33 s) |
| `freny_jump_air` | Eiravaein Works - Vaeyan II / FencingFoil,swoosh,roomambience,lively,quick,aggressive.M.wav (0–0.4 s) |
| `freny_dash` | Mechanical Wave - Hits Whoosh / Fast Action Swish_HW 05.wav (0–0.45 s) |
| `sword_swing_1` | Timothy McHugh - Gorification [HD] / gore - metal knife swing - 4.wav (0.15–0.45 s) |
| `sword_swing_2` | Timothy McHugh - Gorification [HD] / gore - metal knife swing - 5.wav (0.22–0.62 s) |
| `sword_swing_3` | Timothy McHugh - Gorification [HD] / gore - metal knife swing - 17.wav (0–0.33 s) |
| `sword_combo1` | `sword_swing_1` com tom 12% mais agudo (1º golpe do combo) |
| `sword_combo2` | `sword_swing_3` com tom 25% mais agudo (2º golpe do combo) |
| `sword_combo3` | `sword_swing_2` 18% mais grave + `sword_swing_1` 32% mais grave por baixo, graves reforçados (3º golpe, o forte) |
| `sword_hit_1` | Timothy McHugh - Gorification [HD] / gore - medium metal blade wet impact in to flesh and removed - 6.wav (0–0.6 s) |
| `sword_hit_2` | Timothy McHugh - Gorification [HD] / gore - crunchy flesh impact and blood squirt - hard -  8.wav (0–0.46 s) |
| `pistol_shot_1` | Coll Anderson - Guns / EFX EXT .38 Plus Power Shots 04 B.wav (1–2 s) |
| `pistol_shot_2` | Coll Anderson - Guns / EFX EXT .38 Plus Power Shots 04 B.wav (9.23–10.23 s) |
| `shotgun_shot` | Coll Anderson - Guns / EFX EXT .50 Cal Pistol Shots 02 A.wav (1–2.4 s) |
| `grenade_explode` | Coll Anderson - Guns / EFX EXT Mortar Explosion 08.wav (0.22–2.4 s) |
| `pistol_empty` | Coll Anderson - Guns / EFX INT Unloading Magazine Singles 01.wav (2–2.36 s) |
| `pistol_reload` | Coll Anderson - Guns / EFX INT AR-15 M4 Charging Handle With Mag 01_A.wav (0–1.21 s) |
| `pickup_ammo` | Coll Anderson - Guns / EFX INT Unloading Magazine Singles 01.wav (0.22–1.03 s) |
| `grenade_throw` | Soundopolis - Gore Toolkit HD / Whoosh_Swing_Fishing Pole_Full_Fienup_CLASSIC_001.wav (0–0.3 s) |
| `freny_hurt_1` | Timothy McHugh - Gorification [HD] / gore - wood beating flesh wet blood splat - soft - 14.wav (0–0.46 s) |
| `freny_hurt_2` | Soundopolis - Gore Toolkit HD / Punch_Spurt Blood_Fienup_001.wav (0–0.25 s) |
| `freny_death` | SoundMorph - Bloody Nightmare / Bloody Nightmare - Death Pacts_32.wav (0–2.2 s) |
| `freny_rage` | o grito da Fúria (tecla F): `freny_death` 0–1,5 s 8% mais grave, com `supreme_scream` 0,3–1,9 s baixo e abafado por baixo (2026-11-01) |
| `blood_moon` | o começo da Lua de Sangue: dois uivos de lobo sintetizados (Python, sem gravação) com eco, e a risada do Supremo (`supreme_laugh`) 40% mais grave entrando no fim (2026-11-01) |
| `narrator_welcome` | "Bem-vindo à arena": voz Microsoft Daniel (pt-BR) do Windows, bem grave e um pouco mais rápida (`Tools/VoiceGen/narrador.ps1`). **Versão provisória**: confirmar a licença ou trocar por voz própria (2026-11-02) |
| `narrator_double`, `_triple`, `_quadra`, `_penta`, `_multi` | as falas dos multi-kills: ainda sem áudio. Solte aqui os arquivos com esses nomes e elas passam a tocar (2026-11-02) |
| `multi_kill` | o Double/Triple/Quadra/Penta/Multi Kill: sino grave e taiko sintetizados (`Tools/MusicGen/dark_fantasy.py multi_kill`), mais agudo a cada banner (2026-11-02) |
| `freny_land` | Timothy McHugh -  Barefoot on Metal / FOOTSTEP - Metal Plate Land Barefoot Male - 1.wav (0–0.5 s) |
| `freny_step_1` | Timothy McHugh -  Barefoot on Metal / FOOTSTEP - Metal Plate Walk Barefoot Male - 1.wav (0–0.3 s) |
| `freny_step_2` | Timothy McHugh -  Barefoot on Metal / FOOTSTEP - Metal Plate Walk Barefoot Male - 2.wav (0–0.3 s) |
| `freny_step_3` | Timothy McHugh -  Barefoot on Metal / FOOTSTEP - Metal Plate Walk Barefoot Male - 3.wav (0–0.3 s) |
| `freny_stun` | Coll Anderson - Slapstick Comedy / EFX SD Heads Hit Together 02.wav (0–0.42 s) |
| `potion` | Eiravaein Works - Vaeyan II / Liquids,gingerbeer,pour,plop,gurgling,fizzy,effervescent,close.M.wav (0.26–1.12 s) |
| `weapon_switch` | Eiravaein Works - Vaeyan II / Switchblades,OTF,quality,close,unsheath,mechanical,tight,spring,ringing.M.wav (0–0.4 s) |
| `arena_wrap` | Membrans - Scanners Sound Pack 01 / Scan 10.wav (0–0.55 s) |
| `bug_attack` | RDGSFX002 - Creatures / Hisses Dry 11.M.wav (0.17–0.9 s) |
| `bug_hurt` | RDGSFX002 - Creatures / Creature Hiss 4.M.wav (0.26–0.9 s) |
| `bug_death_1` | Mattia Cellotto - The Borax Experiment / Borax,Impact,Slime,Gore,Various05.wav (0–0.76 s) |
| `bug_death_2` | Mattia Cellotto - Crunch Mode / Egg,Break,Creature,Crunch,Crack,Various02.wav (0–0.9 s) |
| `golem_windup` | RDGSFX002 - Creatures / Growl Dry 14.M.wav (0.18–1.2 s) |
| `golem_attack` | Mechanical Wave - Rock Brick and Dirt / Smash Rock On Debris_RBD 04.wav (0–0.83 s) |
| `golem_hurt` | Mechanical Wave - Rock Brick and Dirt / Hit Rock Debris_RBD 02.wav (0–0.7 s) |
| `golem_death` | Mechanical Wave - Rock Brick and Dirt / Drop Rock On Rock Debris_RBD 03.wav (0.3–2.62 s) |
| `golem_step_1` | SoundMorph - Robotic Lifeforms / footstep_a_mech_mega_01.wav (0–0.6 s) |
| `golem_step_2` | SoundMorph - Robotic Lifeforms / footstep_a_mech_mega_02.wav (0–0.6 s) |
| `licker_attack` | Mattia Cellotto - The Borax Experiment / Gore,Slime,Creature,Meat,Blood,Short,Movement,Various13.wav (0–0.7 s) |
| `licker_hurt` | Eiravaein Works - Epona / Epona,growling,gutteral,turning toward,exhale.M.wav (0–1 s) |
| `licker_death` | Mattia Cellotto - The Borax Experiment / Gore,Slime,Creature,Meat,Blood,Long,Movement,Various19.wav (0–2 s) |
| `bat_attack` | RDGSFX002 - Creatures / Creature Screeches 12.M.wav (0.96–1.7 s) |
| `bat_death` | TheLibrarybyEmptySea - SeaMonsters 2 / TheLibrarybyMTC_TinyMonster_Pain_199_96k.wav (0–1.2 s) |
| `sorcerer_cast` | SoundMorph - Future Weapons / Utilities-Mine or System activation-13.wav (0–1.2 s) |
| `supreme_cast` | SoundMorph - Future Weapons / Utilities-Mine or System activation-13.wav (0–1.2 s) |
| `hexball_launch` | TheLibrarybyEmptySea - Robobiotics / TheLibrarybyMTC_Robo_LaserBlast_Medium_027.wav (0–0.62 s) |
| `hexball_impact` | Mechanical Wave - Hits Whoosh / Heavy Sci-Fi Hit_HW 09.wav (0.85–2 s) |
| `sorcerer_hurt` | Membrans - Monster Pack 01 / Monster 20.wav (0.14–1 s) |
| `sorcerer_death` | Membrans - Monster Pack 01 / Monster 31.wav (0.15–2 s) |
| `supreme_hurt` | TheLibrarybyEmptySea - SeaMonsters 2 / TheLibrarybyMTC_GrowlMonster_Roar_26_96k.wav (0–1.2 s) |
| `supreme_death` | Mechanical Wave - Sci-Fi-Invaders / Heavy Magical Explosion_SI 03.wav (0.14–2.27 s) |
| `supreme_scream` | Varazuvi - Hollywood Dragons / Dragon Roaring 4.wav (0.2–3 s) |
| `supreme_laugh` | Sound Ex Machina - Laughs / Male cackling and giggling 03.M.wav (0.36–1.9 s) |
| `portal` | SoundMorph - Sinematic / Sinematic - Risers_02.wav (0–1.4 s) |
| `coin` | Varazuvi - California Foley / coins2.wav (0.24–0.57 s) |
| `chest_break` | Mechanical Wave - Vegetable Massacres / Break and Crack_VM 03.wav (0–0.9 s) |
| `checkpoint` | Eiravaein Works - Start Select / StartSelect,UI,set1,strings,ethnic,tonal,plucked,aura,hold_release.wav (0–1.42 s) |
| `door` | TheLibrarybyEmptySea - Gateway Part1 / patio_door_wood_glass_INT_hinge_creak_open_close_08.wav (0.1–1.6 s) |
| `door_locked` | TheLibrarybyEmptySea - Gateway Part1 / hotel_door_metal_deadbolt_unlock_02.wav (0.34–0.83 s) |
| `stage_clear` | Soundopolis - Percussion 01 / Timpani_Hit_Fienup_001.wav (0–3 s) |
| `shop_open` | Soundopolis - Percussion 01 / Bell_Waiter_Fienup_001.wav (0–1.73 s) |
| `shop_buy` | Robert Cordova - Base / Abacus 02.wav (0–0.28 s) |
| `level_up` | Eiravaein Works - Start Select / StartSelect,UI,set17,schick,mechanical,percussive,ding,shudder,holdrelease.wav (0–0.88 s) |
| `upgrade` | SoundMorph - Users of Tommorow / confirm_beepy_02.wav (0–0.94 s) |
| `combo` | Membrans - Pops Sound Pack 01 / Pop 31.wav (0–0.12 s) |
| `pit_fall` | Coll Anderson - Slapstick Comedy / EFX INT Airborne Rising and Falling 02.wav (0–1.92 s) |
| `ui_select` | Kpow Sounds - UI SOUNDPACKS / UI_SoundPack8_Select_v2.wav (0.48–0.8 s) |
| `ui_move` | Kpow Sounds - UI SOUNDPACKS / UI_SoundPack8_Scroll_v2.wav (0.48–0.75 s) |
| `ui_back` | Kpow Sounds - UI SOUNDPACKS / UI_SoundPack8_Back_v3.wav (0.48–0.75 s) |
| `ui_error` | Kpow Sounds - UI SOUNDPACKS / UI_SoundPack8_Error_v1.wav (0.48–0.85 s) |
| `ui_pause` | Alexander Kopeikin - Transitions HD / transition shutter 002.wav (0–0.73 s) |
| `game_over` | SoundMorph - Bloody Nightmare / Bloody Nightmare - Zenith Stingers_22.wav (0.2–4 s) |
| `new_record` | Sound Ex Machina - Basketball Game Pro / Crowd Applause 4 - Light Applause, Cheer.wav (0.28–3 s) |
