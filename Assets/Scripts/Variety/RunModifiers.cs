using System.Collections.Generic;
using UnityEngine;

// Everything the Bênçãos, the Mutador da semana and Renascer change in Freny and the enemies, in
// one place, so the game's classes each ask with one call (Player.ApplyUpgrades, Enemy.Init and
// MoveSpeed, Enemy.ScaledHit, Coin, ItemPickup). Blessings and mutators only exist inside an arena
// run; the rebirth bonus (Rebirth) is Freny's for good, in the campaign too.
public static class RunModifiers {

    // ---- Freny ----

    public static float DamageMultiplier => Rebirth.DamageMultiplier * (Blessings.Has(Blessing.Strength) ? 1.25f : 1f);

    public static float CoinMultiplier => Rebirth.CoinMultiplier * (WeeklyMutators.Active(Mutator.CoinRain) ? 2f : 1f);

    public static int BonusMaxHealth => Blessings.Has(Blessing.ExtraHeart) ? 1 : 0;

    public static float SpeedMultiplier =>
        (Blessings.Has(Blessing.LightFeet) ? 1.15f : 1f) * (WeeklyMutators.Active(Mutator.SwiftFeet) ? 1.2f : 1f);

    public static float AttackCooldownMultiplier => Blessings.Has(Blessing.QuickHands) ? 0.8f : 1f;

    // "Só espada": the revolver and the shotgun don't fire (Player.GunReadyFor), and their ammo
    // stops dropping (Player.WantsBullets / WantsShells)
    public static bool NoGuns => WeeklyMutators.Active(Mutator.OnlySword);

    // the fractions of a coin the multipliers leave over, so +5% still adds up over a run
    private static float coinRemainder;

    // coins picked up (a coin, a diamond): what they are worth with the bonuses
    public static int ScaleCoins(int coins) {
        float multiplier = CoinMultiplier;
        if (coins <= 0 || Mathf.Approximately(multiplier, 1f)) {
            return coins;
        }
        float value = coins * multiplier + coinRemainder;
        int whole = Mathf.FloorToInt(value);
        coinRemainder = value - whole;
        return whole;
    }

    // ---- enemies ----

    public static float EnemySpeedMultiplier =>
        (WeeklyMutators.Active(Mutator.FastEnemies) ? 1.3f : 1f) * (1f + SlainSpeed * CyclopsSlain);

    // José, 2026-11-02: every Ciclope killed in the run leaves the others harder, with no end: +25%
    // life, +3% speed and +4% attack rate each, one more enemy on the floor (up to the arena's limit),
    // and every 8 Ciclopes their hits take 1 more heart. It grows in a line, not compounding, so it
    // tightens for as long as a good player lasts instead of walling the run at one point. Kill
    // values, the combo and the spawn pace stay, so the ranking API's checks hold
    private const float SlainHealth = 0.25f;
    private const float SlainSpeed = 0.03f;
    private const float SlainAttackRate = 0.04f;
    private const int SlainPerExtraHeart = 8;
    private const int SlainPerSmallFryHeart = 10;   // what had 1 of life: 2 at once, then slowly more
    public static int CyclopsSlain { get; set; }
    private static readonly HashSet<Enemy> toughened = new HashSet<Enemy>();

    public static float HealthMultiplier(int slain) {
        return 1f + SlainHealth * slain;
    }

    // on the time between its attacks (Enemy.DifficultyCooldown)
    public static float EnemyCooldownMultiplier => 1f / (1f + SlainAttackRate * CyclopsSlain);

    // hearts on top of each hit (Enemy.TryHitFreny, HexballEffect)
    public static int SlainDamage => CyclopsSlain / SlainPerExtraHeart;

    private static void Toughen(Enemy enemy) {
        if (CyclopsSlain <= 0 || enemy == null || enemy is Cyclops) {
            return;
        }
        toughened.RemoveWhere(e => e == null);
        if (toughened.Add(enemy)) {
            int health = Mathf.CeilToInt(enemy.Health * HealthMultiplier(CyclopsSlain));
            enemy.Health = enemy.Health <= 1 ? Mathf.Min(health, 2 + CyclopsSlain / SlainPerSmallFryHeart) : health;
        }
    }

    private const float GiantScale = 1.3f;
    private const float GiantHealth = 1.5f;
    private static readonly HashSet<Enemy> grown = new HashSet<Enemy>();

    // Enemy.Init, after its health is set: "Gigantes" draws it bigger and gives it more life. Only
    // the sprite grows (the root's scale is the facing flip, reset all the time), lifted so the feet
    // stay on the floor; the bosses and the one-hit enemies are left as they are
    public static void OnEnemyInit(Enemy enemy) {
        Toughen(enemy);
        if (!WeeklyMutators.Active(Mutator.Giants) || enemy == null || enemy is Cyclops || enemy.Health <= 1) {
            return;
        }
        grown.RemoveWhere(e => e == null);
        if (!grown.Add(enemy)) {
            return;
        }
        enemy.Health = Mathf.CeilToInt(enemy.Health * GiantHealth);
        SpriteRenderer sprite = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sprite == null || sprite.transform == enemy.transform) {
            return;
        }
        Transform t = sprite.transform;
        float below = enemy.Feet.y - t.position.y;   // the feet (collider bottom), from the sprite's pivot
        t.localScale *= GiantScale;
        t.position += Vector3.up * (below * (1f - GiantScale));
    }
}
