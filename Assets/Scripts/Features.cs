// The switch board (José, 2026-11-01): every recent gameplay mechanic can be turned off here before a
// release. Change true to false, save, and that's it; docs/chaves-de-mecanicas.md says what each one
// leaves behind. A mechanic that is off stays out of the HUD, the menus, the Guia and the autopilot.
// None of them adds an enemy, makes one come earlier or spawns faster, and a kill's points go in the
// log the run already sends, so the ranking API (freny-online api/_lib/validate.js) accepts a run with
// any of them off, and rules.js doesn't change. A new mechanic gets its line here.
public static class Features {

    // ---- enemies and the Ciclope ----
    public static readonly bool SpiderMine = true;          // Mina-aranha
    public static readonly bool Cyclops = true;             // the boss at 50k; off, its missions and loot go too
    public static readonly bool BossEscalation = true;      // each Ciclope brings more helpers and company; off, every fight is like the first
    public static readonly bool EnemyToughening = true;     // each Ciclope killed gives the others more life and speed, up to 3x life; off, they stay as they are
    public static readonly bool CyclopsFlatScore = true;    // other kills are worth 1 point while a Ciclope is alive; off, full points

    // ---- Freny ----
    public static readonly bool Net = true;                 // Rede
    public static readonly bool MageStaff = true;           // Cajado do Mago
    public static readonly bool Fury = true;                // Fúria (the meter, the button and the Armeira's +5 s)
    public static readonly bool GrenadeFuse = true;         // 3 s fuse, bouncing, cooking; off, it bursts on impact
    public static readonly bool MultiKill = true;           // Double/Triple/Quadra/Penta Kill banners; nothing but the banner
    public static readonly bool WeaponBuffs = true;         // the Armeira's weapon levels and Crítico; the old weapon sales do not come back

    // ---- the rare drops (ArenaPowerUps), one switch each ----
    public static readonly bool InstaKill = true;           // Insta-kill
    public static readonly bool Nuke = true;                // Nuke
    public static readonly bool Magnet = true;              // Ímã (the drop; the Ímã de moedas blessing is Blessings)

    // ---- a run's variety ----
    public static readonly bool Blessings = true;           // Bênçãos (the golden chest stops offering them)
    public static readonly bool WeeklyMutators = false;     // Mutador da semana (José, 2026-11-02: off until there is a better idea for it)
    public static readonly bool GoldenChest = true;         // the Ciclope's key, the golden chest and its exclusive looks
    public static readonly bool AcidPit = false;            // Poço de Ácido (José, 2026-11-02: off for the release, only the main Arena runs)

    // ---- progress ----
    public static readonly bool DailyMissions = true;       // Missões do dia
    public static readonly bool Rebirth = true;             // Renascer (the button, the bonus and its looks)
    public static readonly bool Looks = true;               // Visual: helmets, capes and colours
    public static readonly bool BerserkerSuit = true;       // Traje Berserker, the whole skin (needs Looks)

    // the Armeira's shelves, the Inventário and Renascer's list ask here
    public static bool Sold(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.Net: return Net;
            case FrenyUpgrade.RageTime: return Fury && WeaponBuffs;
            case FrenyUpgrade.PistolDamage:
            case FrenyUpgrade.PistolDrum:
            case FrenyUpgrade.PistolReserve:
            case FrenyUpgrade.PistolReload:
            case FrenyUpgrade.ShotgunDamage:
            case FrenyUpgrade.ShotgunShells:
            case FrenyUpgrade.ShotgunSpread:
            case FrenyUpgrade.SwordDamage:
            case FrenyUpgrade.GreatSwordSwings:
            case FrenyUpgrade.GrenadeBlast:
            case FrenyUpgrade.GrenadePower:
            case FrenyUpgrade.GrenadeCarry:
            case FrenyUpgrade.Critical: return WeaponBuffs;
            default: return true;
        }
    }

    // the Controles and Ajuda screens list these
    public static bool HasAction(GameAction action) {
        switch (action) {
            case GameAction.Net: return Net;
            case GameAction.Fury: return Fury;
            default: return true;
        }
    }
}
