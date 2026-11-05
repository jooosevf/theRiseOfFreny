using System;
using UnityEngine;

// Clears every player's progress once when a new reset version ships (Alpha 0.0.1: everyone starts
// over). Runs at startup before SaveSystem reads the slots. Settings, key bindings and the online
// login stay; XP, coins, upgrades, gear, campaign, save slots and the local copies of the online
// save and best scores go. The cloud copies are cleared in the Firebase console at the same time.
public static class ProgressReset {

    // bump to wipe everyone's progress again
    private const int Version = 1;
    private const string VersionKey = "freny.progressReset";

    private static readonly string[] Keys = {
        FrenyProgress.XpKey, FrenyProgress.CoinsKey, FrenyProgress.BestWaveKey, FrenyProgress.SpentPointsKey,
        CampaignProgress.UnlockedKey, SaveSystem.LastSceneKey, SaveSystem.PlaySecondsKey,
        "freny.save.active", "online.save",
    };

    public static void RunIfNeeded() {
        if (PlayerPrefs.GetInt(VersionKey, 0) >= Version) {
            return;
        }
        foreach (string key in Keys) {
            PlayerPrefs.DeleteKey(key);
        }
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            PlayerPrefs.DeleteKey(FrenyProgress.LevelKeyPrefix + upgrade);
            PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + upgrade);
        }
        foreach (string gear in Loadout.GearIds) {
            PlayerPrefs.DeleteKey(Loadout.GearKeyPrefix + gear);
            PlayerPrefs.DeleteKey(FrenyProgress.UnequippedKeyPrefix + gear);
        }
        PlayerPrefs.DeleteKey(CampaignWorld.SavedKey);
        foreach (CampaignLevels.Stage stage in CampaignLevels.All) {
            PlayerPrefs.DeleteKey(CampaignProgress.BestTimeKeyPrefix + stage.id);
        }
        for (int i = 0; i < SaveSystem.SlotCount; i++) {
            PlayerPrefs.DeleteKey("freny.save.slot" + i);
        }
        foreach (ArenaLevel level in Enum.GetValues(typeof(ArenaLevel))) {
            PlayerPrefs.DeleteKey("online.best." + level);
        }
        PlayerPrefs.SetInt(VersionKey, Version);
        PlayerPrefs.Save();
    }
}
