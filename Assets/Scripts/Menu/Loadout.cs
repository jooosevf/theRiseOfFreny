using System.Collections;
using UnityEngine;

// What Freny takes into a stage or an arena run. Weapons and moves bought with coins are
// FrenyUpgrades (equipped or not: FrenyProgress.IsEquipped). The Armeiro also sells gear that
// is not an upgrade, kept here: the Berserker helmet, which starts every stage with full armor.
// Drops picked up in a stage (great sword, helmet, ammo) still last only for that stage.
public static class Loadout {

    public const string Helmet = "Helmet";
    public const string GearKeyPrefix = "freny.gear.";
    public const int HelmetCost = 6000;

    public static readonly string[] GearIds = { Helmet };

    public static bool Owns(string gear) {
        return PlayerPrefs.GetInt(GearKeyPrefix + gear, 0) > 0;
    }

    public static bool IsEquipped(string gear) {
        return Owns(gear) && PlayerPrefs.GetInt(FrenyProgress.UnequippedKeyPrefix + gear, 0) == 0;
    }

    public static void SetEquipped(string gear, bool equipped) {
        if (equipped) {
            PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + gear);
        } else {
            PlayerPrefs.SetInt(FrenyProgress.UnequippedKeyPrefix + gear, 1);
        }
        PlayerPrefs.Save();
    }

    public static int Cost(string gear) {
        return HelmetCost;
    }

    public static string DisplayName(string gear) {
        return "Capacete Berserker";
    }

    public static bool CanBuy(string gear) {
        return !Owns(gear) && FrenyProgress.Coins >= Cost(gear);
    }

    public static bool TryBuy(string gear) {
        if (!CanBuy(gear)) {
            return false;
        }
        FrenyProgress.Coins -= Cost(gear);
        PlayerPrefs.SetInt(GearKeyPrefix + gear, 1);
        PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + gear);
        PlayerPrefs.Save();
        Sfx.Ui("upgrade");
        return true;
    }

    // GameMenus runs this when a stage or an arena run starts: the helmet goes on once Freny is up
    public static IEnumerator ApplyAtStageStart() {
        if (!IsEquipped(Helmet)) {
            yield break;
        }
        Player player = null;
        for (int frame = 0; frame < 120 && player == null; frame++) {
            yield return null;
            player = Object.FindAnyObjectByType<Player>();
        }
        yield return null;   // after the Player's own Start
        if (player != null) {
            player.GiveArmor();
        }
    }
}
