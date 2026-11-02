using System;
using UnityEngine;

public enum FrenyUpgrade {
    // Freny's attributes: bought with coins, valid everywhere (arena, by hand, campaign)
    MaxHealth,
    MoveSpeed,
    AttackSpeed,
    DoubleJump,
    Shotgun,
    GreatSword,
    Grenade,
    // the autopilot's brain: bought with points from levelling up, arena only
    Reaction,
    DashDodge,
    AutoPotion,
    SmartTarget,
    // added 2026-11-01 (after the brain on purpose: saves keep their names, IsBrain checks a range).
    // Pistol counts the pistol's levels above 1 (WeaponLevels); Net is the Rede (NetThrower)
    Pistol,
    Net,
    // 2026-11-01 (José): the Armeiro sells buffs, not weapons. Espadão, Escopeta, Granada and the
    // Capacete only come from the floor and chests; these make the weapons better (WeaponLevels).
    // Shotgun, GreatSword, Grenade, Pistol and AutoPotion above are retired (MaxLevel 0, refunded
    // by MigrateShop) but keep their places so old saves still read.
    PistolDamage,
    PistolDrum,
    PistolReserve,
    PistolReload,
    ShotgunDamage,
    ShotgunShells,
    ShotgunSpread,
    SwordDamage,
    GreatSwordSwings,
    GrenadeBlast,
    GrenadePower,
    GrenadeCarry,
    Critical,
    RageTime,
    // the autopilot's brain, added with the buffs
    ItemSense,
    BossHunter,
}

// XP, levels and the two upgrade tracks from the GDD ("Arena idle"), saved with PlayerPrefs.
public static class FrenyProgress {

    public static event Action Changed;

    public const string XpKey = "freny.xp";
    public const string SpentPointsKey = "freny.spentPoints";
    public const string LevelKeyPrefix = "freny.upgrade.";
    public const string CoinsKey = "freny.coins";
    public const string BestWaveKey = "freny.arena.bestRunSeconds";
    public const string UnequippedKeyPrefix = "freny.unequipped.";

    // ---- coins (shared by arena, shop and campaign) ----

    public static int Coins {
        get => PlayerPrefs.GetInt(CoinsKey, 0);
        set {
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value));
            Changed?.Invoke();
        }
    }

    // longest arena run so far, in seconds
    public static float BestRunSeconds {
        get => PlayerPrefs.GetFloat(BestWaveKey, 0f);
        set => PlayerPrefs.SetFloat(BestWaveKey, value);
    }

    // ---- the autopilot earns less ----
    // José, 2026-11-01: the autopilot farms on its own, so while it drives the enemies and chests
    // drop about a third of the coins (fewer coins on the floor, not cheaper ones) and kills give
    // half the XP. Playing by hand earns everything.
    public const float AutoCoinShare = 1f / 3f;
    public const float AutoXpShare = 0.5f;

    public static float CoinShare => FrenyInput.AutopilotDrivingNow ? AutoCoinShare : 1f;

    // one coin drop: dropped or not (a big pile, like the Ciclope's, shrinks instead)
    public static int CoinDrop(int coins) {
        if (!FrenyInput.AutopilotDrivingNow || coins <= 0) {
            return coins;
        }
        if (coins >= 10) {
            return Mathf.Max(1, Mathf.RoundToInt(coins * AutoCoinShare));
        }
        return UnityEngine.Random.value < AutoCoinShare ? coins : 0;
    }

    public static void AddKillXp(int amount) {
        AddXp(FrenyInput.AutopilotDrivingNow ? Mathf.Max(1, Mathf.RoundToInt(amount * AutoXpShare)) : amount);
    }

    // ---- XP and levels ----

    public static int Xp => PlayerPrefs.GetInt(XpKey, 0);

    // level 1 at 0 XP; level n -> n + 1 costs 200 * n² XP (2026-11-01 rebalance: was 100 * n, and one
    // 2-minute run on Difícil (~18k XP) bought the whole autopilot). Levels 2-5 still come in the
    // first run; level 10 takes ~57k XP (3 runs of 65k points), level 28 (every brain upgrade) ~1.4M.
    public static int Level {
        get {
            int level = 1;
            int remaining = Xp;
            while (remaining >= XpToNext(level)) {
                remaining -= XpToNext(level);
                level++;
            }
            return level;
        }
    }

    public static int XpIntoLevel {
        get {
            int remaining = Xp;
            for (int level = 1; remaining >= XpToNext(level); level++) {
                remaining -= XpToNext(level);
            }
            return remaining;
        }
    }

    public static int XpToNext(int level) {
        return 200 * level * level;
    }

    // one brain point per level gained
    public static int BrainPoints => Mathf.Max(0, Level - 1 - PlayerPrefs.GetInt(SpentPointsKey, 0));

    public static void AddXp(int amount) {
        if (amount <= 0) {
            return;
        }
        int levelBefore = Level;
        PlayerPrefs.SetInt(XpKey, Xp + amount);
        if (Level > levelBefore) {
            Sfx.Play("level_up");
        }
        Changed?.Invoke();
    }

    // ---- upgrades ----

    public static bool IsBrain(FrenyUpgrade upgrade) {
        return (upgrade >= FrenyUpgrade.Reaction && upgrade <= FrenyUpgrade.SmartTarget) ||
               upgrade == FrenyUpgrade.ItemSense || upgrade == FrenyUpgrade.BossHunter;
    }

    // no longer sold (2026-11-01): what was bought of them is refunded (MigrateShop)
    public static bool IsRetired(FrenyUpgrade upgrade) {
        return upgrade == FrenyUpgrade.Shotgun || upgrade == FrenyUpgrade.GreatSword || upgrade == FrenyUpgrade.Grenade ||
               upgrade == FrenyUpgrade.Pistol || upgrade == FrenyUpgrade.AutoPotion;
    }

    public static int GetLevel(FrenyUpgrade upgrade) {
        return PlayerPrefs.GetInt(LevelKeyPrefix + upgrade, 0);
    }

    // the coin price of each level, in order; MaxLevel is its length
    private static readonly int[] HealthCosts = { 1500, 3000, 6000, 10000 };
    private static int[] Prices(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.MaxHealth: return HealthCosts;
            case FrenyUpgrade.MoveSpeed: return new[] { 4000 };                 // one level, +5%
            case FrenyUpgrade.AttackSpeed: return new[] { 1500, 4000 };         // the sword, -10% each
            case FrenyUpgrade.DoubleJump: return new[] { 1000 };
            case FrenyUpgrade.Net: return new[] { 3000 };
            case FrenyUpgrade.PistolDamage: return new[] { 1200, 3500, 7000 };
            case FrenyUpgrade.PistolDrum: return new[] { 1000, 3000 };
            case FrenyUpgrade.PistolReserve: return new[] { 800, 2500 };
            case FrenyUpgrade.PistolReload: return new[] { 1000, 3000 };
            case FrenyUpgrade.ShotgunDamage: return new[] { 1500, 4000, 8000 };
            case FrenyUpgrade.ShotgunShells: return new[] { 1000, 3000 };
            case FrenyUpgrade.ShotgunSpread: return new[] { 5000 };
            case FrenyUpgrade.SwordDamage: return new[] { 1500, 4000, 8000 };
            case FrenyUpgrade.GreatSwordSwings: return new[] { 1500, 3500 };
            case FrenyUpgrade.GrenadeBlast: return new[] { 2500 };
            case FrenyUpgrade.GrenadePower: return new[] { 5000 };
            case FrenyUpgrade.GrenadeCarry: return new[] { 1000, 3000 };
            case FrenyUpgrade.Critical: return new[] { 2000, 5000, 10000 };
            case FrenyUpgrade.RageTime: return new[] { 1500, 4000 };
            default: return null;
        }
    }

    public static int MaxLevel(FrenyUpgrade upgrade) {
        if (IsRetired(upgrade)) {
            return 0;
        }
        switch (upgrade) {
            case FrenyUpgrade.Reaction: return 5;
            case FrenyUpgrade.DashDodge:
            case FrenyUpgrade.SmartTarget:
            case FrenyUpgrade.ItemSense:
            case FrenyUpgrade.BossHunter: return 1;
        }
        int[] prices = Prices(upgrade);
        return prices != null ? prices.Length : 1;
    }

    // coins for the Armeiro's buffs (132,000 for all of it), brain points for the autopilot (33)
    public static int Cost(FrenyUpgrade upgrade) {
        return CostAt(upgrade, GetLevel(upgrade));
    }

    // the price of the next level when `level` levels are already bought (the Guia lists them all)
    public static int CostAt(FrenyUpgrade upgrade, int level) {
        switch (upgrade) {
            case FrenyUpgrade.Reaction: return level + 1;   // 1, 2, 3, 4, 5
            case FrenyUpgrade.DashDodge: return 3;
            case FrenyUpgrade.ItemSense: return 4;
            case FrenyUpgrade.SmartTarget: return 5;
            case FrenyUpgrade.BossHunter: return 6;
            case FrenyUpgrade.AutoPotion: return 4;           // retired; what it cost, for the refund
        }
        int[] prices = Prices(upgrade);
        return prices != null ? prices[Mathf.Clamp(level, 0, prices.Length - 1)] : OldCost(upgrade, level);
    }

    // what the retired weapons cost (the dev prices, and the fases branch's levels 2-3), to refund them
    private static int OldCost(FrenyUpgrade upgrade, int level) {
        int[] old;
        switch (upgrade) {
            case FrenyUpgrade.Grenade: old = new[] { 2000, 3000, 6000 }; break;
            case FrenyUpgrade.GreatSword: old = new[] { 3500, 4500, 7500 }; break;
            case FrenyUpgrade.Shotgun: old = new[] { 5000, 6000, 9000 }; break;
            case FrenyUpgrade.Pistol: old = new[] { 2500, 5000 }; break;
            default: return 1000;
        }
        return old[Mathf.Clamp(level, 0, old.Length - 1)];
    }

    // the old Velocidade and Recarga levels (5 each) that the new ones no longer have
    private static readonly int[] OldTrainingCosts = { 800, 1500, 2500, 4000, 6000 };

    public static string CostText(FrenyUpgrade upgrade) {
        int cost = Cost(upgrade);
        return IsBrain(upgrade) ? (cost == 1 ? "1 ponto" : cost + " pontos") : cost.ToString();
    }

    public static string DisplayName(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.MaxHealth: return "Vida máxima +1";
            case FrenyUpgrade.MoveSpeed: return "Velocidade +5%";
            case FrenyUpgrade.AttackSpeed: return "Espada mais rápida -10%";
            case FrenyUpgrade.DoubleJump: return "Pulo duplo (pule de novo no ar)";
            case FrenyUpgrade.Net: return "Rede (" + InputBindings.KeyName(InputBindings.Keyboard(GameAction.Net)) + ")";
            case FrenyUpgrade.PistolDamage: return "Pistola: dano +25%";
            case FrenyUpgrade.PistolDrum: return "Pistola: +1 bala no tambor";
            case FrenyUpgrade.PistolReserve: return "Pistola: reserva +8 balas";
            case FrenyUpgrade.PistolReload: return "Pistola: recarga -20%";
            case FrenyUpgrade.ShotgunDamage: return "Escopeta: dano +25%";
            case FrenyUpgrade.ShotgunShells: return "Escopeta: +10 cartuchos no máximo";
            case FrenyUpgrade.ShotgunSpread: return "Escopeta: leque de 5 tiros";
            case FrenyUpgrade.SwordDamage: return "Espada: dano +25%";
            case FrenyUpgrade.GreatSwordSwings: return "Espadão: +3 golpes";
            case FrenyUpgrade.GrenadeBlast: return "Granada: explosão 30% maior";
            case FrenyUpgrade.GrenadePower: return "Granada: 4 de dano";
            case FrenyUpgrade.GrenadeCarry: return "Granada: carrega +2";
            case FrenyUpgrade.Critical: return "Crítico: +5% de chance";
            case FrenyUpgrade.RageTime: return "Fúria: +5 s";
            case FrenyUpgrade.Reaction: return "Reação mais rápida";
            case FrenyUpgrade.DashDodge: return "Esquiva com dash";
            case FrenyUpgrade.ItemSense: return "Pegar o item certo";
            case FrenyUpgrade.SmartTarget: return "Escolher alvo melhor";
            case FrenyUpgrade.BossHunter: return "Caçar chefe";
            case FrenyUpgrade.Shotgun: return "Escopeta";
            case FrenyUpgrade.GreatSword: return "Espadão";
            case FrenyUpgrade.Grenade: return "Granada";
            case FrenyUpgrade.Pistol: return "Pistola";
            case FrenyUpgrade.AutoPotion: return "Usar poção sozinho";
            default: return upgrade.ToString();
        }
    }

    // The Armeiro stopped selling weapons (2026-11-01): everything bought of a retired upgrade, the
    // Capacete Berserker and the Velocidade/Recarga levels past the new maximum goes back as coins
    // (brain points for the poção sozinho). Run whenever the saved progress may have changed (menu
    // start, a cloud slot loaded): a save from the cloud with old levels is refunded the same way.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void MigrateAtStart() {
        MigrateShop();
    }

    public static void MigrateShop() {
        FrenyLook.MigrateBerserker();
        int coins = 0;
        bool changed = false;
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            int level = GetLevel(upgrade);
            int keep = IsRetired(upgrade) ? 0
                : upgrade == FrenyUpgrade.MoveSpeed || upgrade == FrenyUpgrade.AttackSpeed ? Mathf.Min(level, MaxLevel(upgrade))
                : level;
            if (level <= keep) {
                continue;
            }
            for (int l = keep; l < level; l++) {
                if (upgrade == FrenyUpgrade.AutoPotion) {
                    PlayerPrefs.SetInt(SpentPointsKey, Mathf.Max(0, PlayerPrefs.GetInt(SpentPointsKey, 0) - 4));
                } else if (upgrade == FrenyUpgrade.MoveSpeed || upgrade == FrenyUpgrade.AttackSpeed) {
                    coins += OldTrainingCosts[Mathf.Min(l, OldTrainingCosts.Length - 1)];
                } else {
                    coins += CostAt(upgrade, l);
                }
            }
            if (keep > 0) {
                PlayerPrefs.SetInt(LevelKeyPrefix + upgrade, keep);
            } else {
                PlayerPrefs.DeleteKey(LevelKeyPrefix + upgrade);
                PlayerPrefs.DeleteKey(UnequippedKeyPrefix + upgrade);
            }
            changed = true;
        }
        if (PlayerPrefs.GetInt(Loadout.GearKeyPrefix + Loadout.Helmet, 0) > 0) {
            coins += Loadout.HelmetCost;
            PlayerPrefs.DeleteKey(Loadout.GearKeyPrefix + Loadout.Helmet);
            PlayerPrefs.DeleteKey(UnequippedKeyPrefix + Loadout.Helmet);
            changed = true;
        }
        if (!changed) {
            return;
        }
        if (coins > 0) {
            Debug.Log("[Armeiro] " + coins + " moedas devolvidas das armas que a loja não vende mais");
        }
        PlayerPrefs.SetInt(CoinsKey, PlayerPrefs.GetInt(CoinsKey, 0) + coins);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static bool IsMaxed(FrenyUpgrade upgrade) {
        return GetLevel(upgrade) >= MaxLevel(upgrade);
    }

    public static bool CanBuy(FrenyUpgrade upgrade, Player player) {
        if (IsMaxed(upgrade)) {
            return false;
        }
        if (IsBrain(upgrade)) {
            return BrainPoints >= Cost(upgrade);
        }
        // from the menu (no Player in the scene) the saved coins pay
        int coins = player != null ? player.coins : Coins;
        return coins >= Cost(upgrade);
    }

    public static bool TryBuy(FrenyUpgrade upgrade, Player player) {
        if (!CanBuy(upgrade, player)) {
            return false;
        }

        if (IsBrain(upgrade)) {
            PlayerPrefs.SetInt(SpentPointsKey, PlayerPrefs.GetInt(SpentPointsKey, 0) + Cost(upgrade));
        } else if (player != null) {
            player.AddCoins(-Cost(upgrade));
        } else {
            Coins -= Cost(upgrade);
        }

        PlayerPrefs.SetInt(LevelKeyPrefix + upgrade, GetLevel(upgrade) + 1);
        PlayerPrefs.DeleteKey(UnequippedKeyPrefix + upgrade);   // what you buy goes on equipped
        PlayerPrefs.Save();
        Sfx.Ui("upgrade");
        Changed?.Invoke();
        return true;
    }

    // gives one level back and refunds what it cost (for playtesting the shop)
    public static bool Refund(FrenyUpgrade upgrade, Player player) {
        int level = GetLevel(upgrade);
        if (level <= 0) {
            return false;
        }
        PlayerPrefs.SetInt(LevelKeyPrefix + upgrade, level - 1);
        if (IsBrain(upgrade)) {
            PlayerPrefs.SetInt(SpentPointsKey, Mathf.Max(0, PlayerPrefs.GetInt(SpentPointsKey, 0) - Cost(upgrade)));
        } else if (player != null) {
            player.AddCoins(Cost(upgrade)); // Cost() now reports the price of the level just removed
        } else {
            Coins += Cost(upgrade);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    // ---- equipment ----

    // weapons and moves bought with coins can be left at home (the menu's Inventário)
    public static bool IsEquipment(FrenyUpgrade upgrade) {
        return upgrade == FrenyUpgrade.DoubleJump || upgrade == FrenyUpgrade.Net;
    }

    public static bool IsEquipped(FrenyUpgrade upgrade) {
        return PlayerPrefs.GetInt(UnequippedKeyPrefix + upgrade, 0) == 0;
    }

    public static void SetEquipped(FrenyUpgrade upgrade, bool equipped) {
        if (equipped) {
            PlayerPrefs.DeleteKey(UnequippedKeyPrefix + upgrade);
        } else {
            PlayerPrefs.SetInt(UnequippedKeyPrefix + upgrade, 1);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static bool Carries(FrenyUpgrade upgrade) {
        return GetLevel(upgrade) > 0 && IsEquipped(upgrade);
    }

    // ---- effects ----

    public static int BonusMaxHealth => GetLevel(FrenyUpgrade.MaxHealth);
    public static float SpeedMultiplier => 1f + 0.05f * Mathf.Min(GetLevel(FrenyUpgrade.MoveSpeed), MaxLevel(FrenyUpgrade.MoveSpeed));
    // the sword's swing only now (the guns have their own: PistolReload, the shotgun's cooldown)
    public static float AttackCooldownMultiplier => 1f - 0.1f * Mathf.Min(GetLevel(FrenyUpgrade.AttackSpeed), MaxLevel(FrenyUpgrade.AttackSpeed));
    public static bool HasDoubleJump => Carries(FrenyUpgrade.DoubleJump);
    public static bool HasNet => Carries(FrenyUpgrade.Net);

    // seconds between autopilot decisions: 0.6 s untrained, 0.1 s maxed
    public static float AutopilotReactionTime => 0.6f - 0.1f * GetLevel(FrenyUpgrade.Reaction);
    public static bool HasDashDodge => GetLevel(FrenyUpgrade.DashDodge) > 0;
    public static bool HasItemSense => GetLevel(FrenyUpgrade.ItemSense) > 0;
    public static bool HasBossHunter => GetLevel(FrenyUpgrade.BossHunter) > 0;
    public static bool HasSmartTarget => GetLevel(FrenyUpgrade.SmartTarget) > 0;

    // for testing in the Editor
    public static void ResetAll() {
        PlayerPrefs.DeleteKey(XpKey);
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(BestWaveKey);
        PlayerPrefs.DeleteKey(SpentPointsKey);
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            PlayerPrefs.DeleteKey(LevelKeyPrefix + upgrade);
            PlayerPrefs.DeleteKey(UnequippedKeyPrefix + upgrade);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
