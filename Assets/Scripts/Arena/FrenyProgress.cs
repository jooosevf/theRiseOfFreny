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
        return upgrade >= FrenyUpgrade.Reaction;
    }

    public static int GetLevel(FrenyUpgrade upgrade) {
        return PlayerPrefs.GetInt(LevelKeyPrefix + upgrade, 0);
    }

    public static int MaxLevel(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.MaxHealth: return 4;
            case FrenyUpgrade.MoveSpeed: return 5;
            case FrenyUpgrade.AttackSpeed: return 5;
            case FrenyUpgrade.Reaction: return 5;
            default: return 1;
        }
    }

    // Prices (rebalanced 2026-11-01 from real Difícil runs: a 65k-point run of ~2 min brings ~260
    // coins, 200k ~640, 1M ~2,800). The whole coin shop is ~67,600: ~260 typical runs, ~25 of the
    // best ones, so buying everything takes a while. The first buy (Pulo duplo) is ~4 runs away.
    private static readonly int[] HealthCosts = { 1500, 3000, 6000, 10000 };
    private static readonly int[] TrainingCosts = { 800, 1500, 2500, 4000, 6000 };   // Velocidade and Recarga

    // coins for attributes, brain points for the autopilot (27 points for all of it)
    public static int Cost(FrenyUpgrade upgrade) {
        int level = GetLevel(upgrade);
        switch (upgrade) {
            case FrenyUpgrade.DoubleJump: return 1000;
            case FrenyUpgrade.Grenade: return 2000;
            case FrenyUpgrade.GreatSword: return 3500;
            case FrenyUpgrade.Shotgun: return 5000;
            case FrenyUpgrade.MaxHealth: return HealthCosts[Mathf.Min(level, HealthCosts.Length - 1)];
            case FrenyUpgrade.MoveSpeed:
            case FrenyUpgrade.AttackSpeed: return TrainingCosts[Mathf.Min(level, TrainingCosts.Length - 1)];
            case FrenyUpgrade.Reaction: return level + 1;   // 1, 2, 3, 4, 5
            case FrenyUpgrade.DashDodge: return 3;
            case FrenyUpgrade.AutoPotion: return 4;
            case FrenyUpgrade.SmartTarget: return 5;
            default: return 1000;
        }
    }

    public static string CostText(FrenyUpgrade upgrade) {
        int cost = Cost(upgrade);
        return IsBrain(upgrade) ? (cost == 1 ? "1 ponto" : cost + " pontos") : cost.ToString();
    }

    public static string DisplayName(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.MaxHealth: return "Vida máxima +1";
            case FrenyUpgrade.MoveSpeed: return "Velocidade +8%";
            case FrenyUpgrade.AttackSpeed: return "Recarga -10%";
            case FrenyUpgrade.DoubleJump: return "Pulo duplo (pule de novo no ar)";
            case FrenyUpgrade.Shotgun: return "Escopeta (3 tiros)";
            case FrenyUpgrade.GreatSword: return "Espadão (golpe mais largo)";
            case FrenyUpgrade.Grenade: return "Granada (" + InputBindings.KeyName(InputBindings.Keyboard(GameAction.Grenade)) + ")";
            case FrenyUpgrade.Reaction: return "Reação mais rápida";
            case FrenyUpgrade.DashDodge: return "Esquiva com dash";
            case FrenyUpgrade.AutoPotion: return "Usar poção sozinho";
            case FrenyUpgrade.SmartTarget: return "Escolher alvo melhor";
            default: return upgrade.ToString();
        }
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
        return upgrade == FrenyUpgrade.DoubleJump || upgrade == FrenyUpgrade.Shotgun ||
               upgrade == FrenyUpgrade.GreatSword || upgrade == FrenyUpgrade.Grenade;
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
    public static float SpeedMultiplier => 1f + 0.08f * GetLevel(FrenyUpgrade.MoveSpeed);
    public static float AttackCooldownMultiplier => 1f - 0.1f * GetLevel(FrenyUpgrade.AttackSpeed);
    public static bool HasDoubleJump => Carries(FrenyUpgrade.DoubleJump);
    public static bool HasShotgun => Carries(FrenyUpgrade.Shotgun);
    public static bool HasGreatSword => Carries(FrenyUpgrade.GreatSword);
    public static bool HasGrenade => Carries(FrenyUpgrade.Grenade);

    // seconds between autopilot decisions: 0.6 s untrained, 0.1 s maxed
    public static float AutopilotReactionTime => 0.6f - 0.1f * GetLevel(FrenyUpgrade.Reaction);
    public static bool HasDashDodge => GetLevel(FrenyUpgrade.DashDodge) > 0;
    public static bool HasAutoPotion => GetLevel(FrenyUpgrade.AutoPotion) > 0;
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
