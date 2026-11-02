using UnityEngine;

// The Armeiro's buffs for the weapons (José, 2026-11-01: "comprar buff pra esses itens"). The
// weapons themselves are no longer sold: the pistol is always carried, and the shotgun, the great
// sword, the grenades and the helmet come from the floor and chests. Each buff is a FrenyUpgrade
// with its own levels (prices in FrenyProgress):
//   Pistola   dano +25%/nível (3), tambor +1 bala (2), reserva +8 (2), recarga -20% (2)
//   Escopeta  dano +25%/nível (3), +10 cartuchos no máximo (2), leque de 5 tiros (1)
//   Espada    dano +25%/nível (3, the great sword too), Espadão do chão +3 golpes (2)
//   Granada   explosão 30% maior (1), 4 de dano (1), carrega +2 (2)
//   Crítico   +5% de chance por nível (3) de dano dobrado em golpe ou tiro
//   Fúria     +5 s por nível (2)
public static class WeaponLevels {

    private static int L(FrenyUpgrade upgrade) {
        return Mathf.Min(FrenyProgress.GetLevel(upgrade), FrenyProgress.MaxLevel(upgrade));
    }

    // ---- pistol ----
    public static float PistolPower => 1f + 0.25f * L(FrenyUpgrade.PistolDamage);
    public static int DrumBonus => L(FrenyUpgrade.PistolDrum);
    public static int ReserveBonus => 8 * L(FrenyUpgrade.PistolReserve);
    public static float ReloadTime => 1f - 0.2f * L(FrenyUpgrade.PistolReload);

    // ---- shotgun ----
    private static readonly float[] ThreePellets = { 10f, 0f, -10f };
    private static readonly float[] FivePellets = { 16f, 8f, 0f, -8f, -16f };
    public static float[] ShotgunAngles => L(FrenyUpgrade.ShotgunSpread) > 0 ? FivePellets : ThreePellets;
    public static float ShotgunPower => 1f + 0.25f * L(FrenyUpgrade.ShotgunDamage);
    public static int ShellsBonus => 10 * L(FrenyUpgrade.ShotgunShells);

    // ---- sword ----
    public const float GreatSwordScale = 1.8f;
    public static float SwordPower => 1f + 0.25f * L(FrenyUpgrade.SwordDamage);
    public static int GreatSwordExtraSwings => 3 * L(FrenyUpgrade.GreatSwordSwings);

    // ---- grenade ----
    public static float GrenadeRadius => Grenade.BlastRadius * (L(FrenyUpgrade.GrenadeBlast) > 0 ? 1.3f : 1f);
    public static int GrenadeDamage => L(FrenyUpgrade.GrenadePower) > 0 ? 4 : Grenade.EnemyDamage;
    public static int GrenadeCarryBonus => 2 * L(FrenyUpgrade.GrenadeCarry);

    // ---- critical hits and Rage ----
    public static float CritChance => 0.05f * L(FrenyUpgrade.Critical);
    public const float CritPower = 2f;
    public static float RageBonusSeconds => 5f * L(FrenyUpgrade.RageTime);

    // one hit's power with the crit roll: x2 now and then, with a yellow spark where it lands
    public static float Roll(float power, Vector3 at) {
        if (CritChance > 0f && Random.value < CritChance) {
            Fx.Burst(at, new Color(1f, 0.95f, 0.3f), 14, 3.5f, 0.09f, 0.35f);
            return power * CritPower;
        }
        return power;
    }
}
