using System;
using System.Collections.Generic;
using UnityEngine;

// Renascer (José, 2026-11-01): once everything the Armeiro sells for coins is bought (and at its
// last level), Freny can trade it all, the autopilot's brain and his level too, for a bonus that
// stays: +5% damage and +5% coins per rebirth, adding up, and a star next to his name on the
// rankings (the game's and the site's). The count is part of the save (SaveSystem, so the cloud
// keeps it too) and it unlocks some looks (FrenyLook).
//
// What it takes is read from the game's own lists, so new upgrades and new levels count by
// themselves: every FrenyUpgrade that is not the autopilot's (FrenyProgress.IsBrain) at
// FrenyProgress.MaxLevel, and every Armeiro gear (Loadout.GearIds).
// The level goes back to 1 with the brain: the brain is bought with level points, and keeping the
// level would hand them all back at once.
public static class Rebirth {

    public const string CountKey = "freny.rebirths";
    public const float BonusPerRebirth = 0.05f;

    public static int Count {
        get => Mathf.Max(0, PlayerPrefs.GetInt(CountKey, 0));
        set {
            if (value > 0) PlayerPrefs.SetInt(CountKey, value); else PlayerPrefs.DeleteKey(CountKey);
        }
    }

    public static float DamageMultiplier => 1f + BonusPerRebirth * Count;
    public static float CoinMultiplier => 1f + BonusPerRebirth * Count;

    // what is still missing at the Armeiro, by name (empty: Freny can be reborn)
    public static List<string> Missing() {
        List<string> missing = new List<string>();
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            if (!FrenyProgress.IsBrain(upgrade) && !FrenyProgress.IsMaxed(upgrade)) {
                int max = FrenyProgress.MaxLevel(upgrade);
                missing.Add(FrenyProgress.DisplayName(upgrade) +
                            (max > 1 ? " (" + FrenyProgress.GetLevel(upgrade) + "/" + max + ")" : ""));
            }
        }
        foreach (string gear in Loadout.GearIds) {
            if (!Loadout.Owns(gear)) {
                missing.Add(Loadout.DisplayName(gear));
            }
        }
        return missing;
    }

    public static bool CanRebirth => Missing().Count == 0;

    // the trade: every upgrade (both tracks) and the gear go, XP and spent points go back to zero,
    // the count goes up. Coins, the campaign, the records, the achievements and the looks stay.
    public static bool Perform() {
        if (!CanRebirth) {
            return false;
        }
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            PlayerPrefs.DeleteKey(FrenyProgress.LevelKeyPrefix + upgrade);
            PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + upgrade);
        }
        foreach (string gear in Loadout.GearIds) {
            PlayerPrefs.DeleteKey(Loadout.GearKeyPrefix + gear);
            PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + gear);
        }
        PlayerPrefs.DeleteKey(FrenyProgress.XpKey);
        PlayerPrefs.DeleteKey(FrenyProgress.SpentPointsKey);
        Count = Count + 1;
        PlayerPrefs.Save();
        FrenyProgress.Coins = FrenyProgress.Coins;   // raises FrenyProgress.Changed for whoever shows it
        SaveSystem.SaveNow();                         // and the cloud gets it with the slot
        if (Online.OnlineServices.Instance != null) {
            Online.OnlineServices.Instance.PushRebirths();   // the star on the boards
        }
        Sfx.Ui("upgrade");
        Sfx.Play("level_up");
        return true;
    }

    // ---- the star on the rankings ----
    // RankingName is the one point where the rankings decorate a player's name: the game's ranking
    // (OnlineScreens) and the site (freny-online index.html, rebirthStar) draw the same "★" / "★3"
    // after it, from the "rebirths" field of the board entry (LeaderboardService). Another
    // decoration (a title, phase 2) joins here.

    public static string Star(int rebirths) {
        return rebirths <= 0 ? "" : rebirths == 1 ? "★" : "★" + rebirths;
    }

    public static string RankingName(string name, int rebirths) {
        string star = Star(rebirths);
        return string.IsNullOrEmpty(star) ? name : name + " <color=#FFD84A>" + star + "</color>";
    }
}
