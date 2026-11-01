using UnityEngine;

// The Armeiro's levels 2 and 3 for the weapons (José, 2026-11-01: "Armeiro turbinado"). Level 1 of
// the shotgun, the great sword and the grenade is buying them; the pistol is always carried, so its
// level 1 is free and FrenyUpgrade.Pistol counts the levels above it. Only a bought (and equipped)
// weapon gets them: a shotgun from a chest or a great sword from the floor stays at level 1.
//   Pistola  nv. 2: shots 25% quicker, reload 30% quicker   nv. 3: shots 40% quicker, reload twice as fast
//   Escopeta nv. 2: 5 pellets                                nv. 3: 5 pellets, 30% quicker, +4 shells a stage
//   Espadão  nv. 2: cut 2.2x (instead of 1.8x)               nv. 3: and +1 damage on every hit
//   Granada  nv. 2: blast 30% wider, +1 grenade a stage      nv. 3: and 4 damage, +2 grenades a stage
public static class WeaponLevels {

    public const int MaxTier = 3;

    // 1..3 for the pistol; 0 (not bought or left at home) .. 3 for the others
    public static int Tier(FrenyUpgrade weapon) {
        if (weapon == FrenyUpgrade.Pistol) {
            return 1 + FrenyProgress.GetLevel(FrenyUpgrade.Pistol);
        }
        return FrenyProgress.IsEquipped(weapon) ? FrenyProgress.GetLevel(weapon) : 0;
    }

    public static bool HasTiers(FrenyUpgrade upgrade) {
        return upgrade == FrenyUpgrade.Pistol || upgrade == FrenyUpgrade.Shotgun ||
               upgrade == FrenyUpgrade.GreatSword || upgrade == FrenyUpgrade.Grenade;
    }

    // the level a weapon is at in the shop (owned or not, equipped or not): "nv. 2/3"
    public static int ShopTier(FrenyUpgrade upgrade) {
        return FrenyProgress.GetLevel(upgrade) + (upgrade == FrenyUpgrade.Pistol ? 1 : 0);
    }

    // ---- pistol ----
    public static float PistolCooldown => Pick(FrenyUpgrade.Pistol, 1f, 0.75f, 0.6f);
    public static float ReloadTime => Pick(FrenyUpgrade.Pistol, 1f, 0.7f, 0.5f);

    // ---- shotgun ----
    private static readonly float[] ThreePellets = { 10f, 0f, -10f };
    private static readonly float[] FivePellets = { 16f, 8f, 0f, -8f, -16f };
    public static float[] ShotgunAngles => Tier(FrenyUpgrade.Shotgun) >= 2 ? FivePellets : ThreePellets;
    public static float ShotgunCooldown => Tier(FrenyUpgrade.Shotgun) >= 3 ? 0.7f : 1f;
    public static int ExtraShells => Tier(FrenyUpgrade.Shotgun) >= 3 ? 4 : 0;

    // ---- great sword (only the bought one) ----
    public const float GreatSwordScale = 1.8f;
    public static float GreatSwordCut => Tier(FrenyUpgrade.GreatSword) >= 2 ? 2.2f : GreatSwordScale;
    public static int GreatSwordBonus => Tier(FrenyUpgrade.GreatSword) >= 3 && FrenyProgress.HasGreatSword ? 1 : 0;

    // ---- grenade ----
    public static float GrenadeRadius => Grenade.BlastRadius * (Tier(FrenyUpgrade.Grenade) >= 2 ? 1.3f : 1f);
    public static int GrenadeDamage => Tier(FrenyUpgrade.Grenade) >= 3 ? 4 : Grenade.EnemyDamage;
    public static int ExtraGrenades => Mathf.Max(0, Tier(FrenyUpgrade.Grenade) - 1);

    private static float Pick(FrenyUpgrade weapon, float one, float two, float three) {
        int tier = Tier(weapon);
        return tier >= 3 ? three : tier == 2 ? two : one;
    }

    // what the next level brings, for the Armeiro's row and the Guia
    public static string NextLevelText(FrenyUpgrade weapon) {
        int next = ShopTier(weapon) + 1;
        switch (weapon) {
            case FrenyUpgrade.Pistol:
                return next == 2 ? "Pistola nv. 2: tiro e recarga mais rápidos" : "Pistola nv. 3: tiro 40% e recarga 2x mais rápidos";
            case FrenyUpgrade.Shotgun:
                return next == 2 ? "Escopeta nv. 2: 5 tiros no leque" : "Escopeta nv. 3: mais rápida, +4 cartuchos";
            case FrenyUpgrade.GreatSword:
                return next == 2 ? "Espadão nv. 2: corte 2,2x maior" : "Espadão nv. 3: +1 de dano por golpe";
            case FrenyUpgrade.Grenade:
                return next == 2 ? "Granada nv. 2: explosão maior, +1 granada" : "Granada nv. 3: 4 de dano, +2 granadas";
            default:
                return FrenyProgress.DisplayName(weapon);
        }
    }

    // the whole weapon at its top level, for when it is maxed
    public static string MaxedText(FrenyUpgrade weapon) {
        switch (weapon) {
            case FrenyUpgrade.Pistol: return "Pistola nv. 3";
            case FrenyUpgrade.Shotgun: return "Escopeta nv. 3";
            case FrenyUpgrade.GreatSword: return "Espadão nv. 3";
            case FrenyUpgrade.Grenade: return "Granada nv. 3";
            default: return FrenyProgress.DisplayName(weapon);
        }
    }
}
