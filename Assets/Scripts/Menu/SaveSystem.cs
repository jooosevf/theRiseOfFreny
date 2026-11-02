using System;
using System.Collections.Generic;
using UnityEngine;

// A snapshot of everything one playthrough keeps: coins, XP, upgrades, campaign stages, the
// arena record, where the player was and how long they played.
[Serializable]
public class SaveData {
    public int version = 1;
    public long savedAtTicks;      // UTC, decides which copy wins when syncing with the cloud
    public string lastScene;
    public float playSeconds;
    public int coins;
    public int xp;
    public int spentPoints;
    public float bestRunSeconds;
    public int bestArenaScore;     // the best arena run's score (it opens the Poço de Ácido); 0 in older saves
    public int campaignUnlocked;
    public List<Entry> upgrades = new List<Entry>();
    public List<Entry> bestTimes = new List<Entry>();
    public List<string> unequipped = new List<string>();   // bought, but left at home
    public List<string> gear = new List<string>();         // Armeira items that aren't upgrades (Loadout)
    public string stats;      // AccountStats' JSON (the Estatísticas); empty in older saves
    public string missions;   // DailyMissions' JSON (today's progress); empty in older saves
    public int rebirths;                                   // Renascer (Rebirth)
    public List<string> looks = new List<string>();        // looks bought with coins (FrenyLook)
    public List<string> lookWorn = new List<string>();     // the helmet, cape and colour worn

    [Serializable]
    public class Entry {
        public string id;
        public float value;
    }

    public bool HasProgress =>
        coins > 0 || xp > 0 || campaignUnlocked > 0 || upgrades.Count > 0 || bestTimes.Count > 0 || (gear != null && gear.Count > 0) ||
        rebirths > 0 || (looks != null && looks.Count > 0) ||
        !string.IsNullOrEmpty(lastScene);


    public int Level {
        get {
            int level = 1;
            for (int remaining = xp; remaining >= FrenyProgress.XpToNext(level); level++) {
                remaining -= FrenyProgress.XpToNext(level);
            }
            return level;
        }
    }

    // "1-4 · Nv 3 · 250 moedas · 12 min"
    public string Summary => ShortSummary + (HasProgress ? $" · {Mathf.FloorToInt(playSeconds / 60f)} min" : "");

    // "1-4 · Nv 3 · 250 moedas"
    public string ShortSummary {
        get {
            if (!HasProgress) {
                return "vazio";
            }
            string where = string.IsNullOrEmpty(lastScene) ? CampaignLevels.All[Mathf.Clamp(campaignUnlocked, 0, CampaignLevels.All.Count - 1)].id
                : CampaignLevels.IsStage(lastScene) ? lastScene : "Arena";
            return $"{where} · Nv {Level} · {coins} moedas";
        }
    }
}

// Three save slots. The game itself keeps reading and writing its PlayerPrefs keys
// (FrenyProgress, CampaignProgress); those keys are the live copy of the active slot. Saving
// copies them into the slot, loading another slot writes its copy back into them.
// A new progress key must be added to Capture and Apply, or it will be shared by all slots.
public static class SaveSystem {

    public const int SlotCount = 3;

    public const string LastSceneKey = "freny.lastScene";
    public const string PlaySecondsKey = "freny.playSeconds";
    private const string SlotKeyPrefix = "freny.save.slot";
    private const string ActiveSlotKey = "freny.save.active";

    // after every save, with the slot that was written (the cloud sync listens to it)
    public static event Action<int> Saved;

    private static float unsavedPlaySeconds;

    // each slot's savedAtTicks when it last matched the cloud, or when the game started: every
    // scene load saves (and so stamps the slot with "now"), so the live stamp can't tell the
    // cloud sync whether this copy or the cloud's is really the newer one
    private static readonly long[] syncedTicks = new long[SlotCount];

    public static long SyncedTicks(int slot) {
        return slot >= 0 && slot < SlotCount ? syncedTicks[slot] : 0L;
    }

    // the slot as it is now is the cloud's copy too (just uploaded or just downloaded)
    public static void MarkSynced(int slot) {
        if (slot >= 0 && slot < SlotCount) {
            SaveData data = Load(slot);
            syncedTicks[slot] = data != null && data.HasProgress ? data.savedAtTicks : 0L;
        }
    }

    public static int ActiveSlot {
        get => Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
        private set => PlayerPrefs.SetInt(ActiveSlotKey, value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init() {
        ProgressReset.RunIfNeeded();
        for (int i = 0; i < SlotCount; i++) {
            MarkSynced(i);
        }
        // progress from before the slots existed becomes slot 1
        bool anySlot = false;
        for (int i = 0; i < SlotCount; i++) {
            anySlot |= PlayerPrefs.HasKey(SlotKeyPrefix + i);
        }
        if (!anySlot && Capture().HasProgress) {
            ActiveSlot = 0;
            SaveNow();
        }
    }

    // ---- slots ----

    // null when the slot was never used
    public static SaveData Load(int slot) {
        string json = PlayerPrefs.GetString(SlotKeyPrefix + slot, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
    }

    public static string LoadJson(int slot) {
        return PlayerPrefs.GetString(SlotKeyPrefix + slot, "");
    }

    // the active slot as it is right now (live keys), not as it was last saved
    public static SaveData Current => Capture();

    public static void SaveNow() {
        FlushPlayTime();
        SaveData data = Capture();
        data.savedAtTicks = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(SlotKeyPrefix + ActiveSlot, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Saved?.Invoke(ActiveSlot);
    }

    // saves the current game, then makes `slot` the live one
    public static void SwitchTo(int slot) {
        if (slot == ActiveSlot) {
            return;
        }
        SaveNow();
        ActiveSlot = slot;
        Apply(Load(slot) ?? new SaveData());
        PlayerPrefs.Save();
    }

    // wipes `slot` and plays it from the start
    public static void NewGame(int slot) {
        if (slot != ActiveSlot) {
            SaveNow();
            ActiveSlot = slot;
        }
        unsavedPlaySeconds = 0f;
        Apply(new SaveData());
        SaveNow();
    }

    // a copy that came from somewhere else (the cloud); applyLive also puts it into play when
    // it is the active slot, which is only safe while no stage is running
    public static void Replace(int slot, SaveData data, bool applyLive) {
        PlayerPrefs.SetString(SlotKeyPrefix + slot, JsonUtility.ToJson(data));
        if (slot == ActiveSlot && applyLive) {
            unsavedPlaySeconds = 0f;
            Apply(data);
        }
        PlayerPrefs.Save();
    }

    // ---- bookkeeping, called by GameMenus ----

    public static void AddPlayTime(float seconds) {
        unsavedPlaySeconds += seconds;
    }

    public static void SetLastScene(string scene) {
        PlayerPrefs.SetString(LastSceneKey, scene);
    }

    private static void FlushPlayTime() {
        if (unsavedPlaySeconds > 0f) {
            PlayerPrefs.SetFloat(PlaySecondsKey, PlayerPrefs.GetFloat(PlaySecondsKey, 0f) + unsavedPlaySeconds);
            unsavedPlaySeconds = 0f;
        }
    }

    // ---- live keys <-> snapshot ----

    private static SaveData Capture() {
        SaveData data = new SaveData {
            lastScene = PlayerPrefs.GetString(LastSceneKey, ""),
            playSeconds = PlayerPrefs.GetFloat(PlaySecondsKey, 0f) + unsavedPlaySeconds,
            coins = PlayerPrefs.GetInt(FrenyProgress.CoinsKey, 0),
            xp = PlayerPrefs.GetInt(FrenyProgress.XpKey, 0),
            spentPoints = PlayerPrefs.GetInt(FrenyProgress.SpentPointsKey, 0),
            bestRunSeconds = PlayerPrefs.GetFloat(FrenyProgress.BestWaveKey, 0f),
            bestArenaScore = PlayerPrefs.GetInt(ArenaVariant.BestScoreKey, 0),
            campaignUnlocked = PlayerPrefs.GetInt(CampaignProgress.UnlockedKey, 0),
            stats = PlayerPrefs.GetString(AccountStats.Key, ""),
            missions = PlayerPrefs.GetString(DailyMissions.Key, ""),
            rebirths = Rebirth.Count,
            looks = FrenyLook.BoughtIds,
            lookWorn = FrenyLook.WornIds(),
        };
        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            int level = PlayerPrefs.GetInt(FrenyProgress.LevelKeyPrefix + upgrade, 0);
            if (level > 0) {
                data.upgrades.Add(new SaveData.Entry { id = upgrade.ToString(), value = level });
            }
            if (PlayerPrefs.HasKey(FrenyProgress.UnequippedKeyPrefix + upgrade)) {
                data.unequipped.Add(upgrade.ToString());
            }
        }
        foreach (string item in Loadout.GearIds) {
            if (Loadout.Owns(item)) {
                data.gear.Add(item);
            }
            if (PlayerPrefs.HasKey(FrenyProgress.UnequippedKeyPrefix + item)) {
                data.unequipped.Add(item);
            }
        }
        foreach (CampaignLevels.Stage stage in CampaignLevels.All) {
            float best = PlayerPrefs.GetFloat(CampaignProgress.BestTimeKeyPrefix + stage.id, 0f);
            if (best > 0f) {
                data.bestTimes.Add(new SaveData.Entry { id = stage.id, value = best });
            }
        }
        return data;
    }

    private static void Apply(SaveData data) {
        SetOrDelete(LastSceneKey, data.lastScene);
        SetOrDelete(PlaySecondsKey, data.playSeconds);
        SetOrDelete(FrenyProgress.CoinsKey, data.coins);
        SetOrDelete(FrenyProgress.XpKey, data.xp);
        SetOrDelete(FrenyProgress.SpentPointsKey, data.spentPoints);
        SetOrDelete(FrenyProgress.BestWaveKey, data.bestRunSeconds);
        SetOrDelete(ArenaVariant.BestScoreKey, data.bestArenaScore);
        SetOrDelete(CampaignProgress.UnlockedKey, data.campaignUnlocked);
        SetOrDelete(AccountStats.Key, data.stats);
        SetOrDelete(DailyMissions.Key, data.missions);
        Rebirth.Count = data.rebirths;
        FrenyLook.Load(data.looks, data.lookWorn);   // old saves: none

        foreach (FrenyUpgrade upgrade in Enum.GetValues(typeof(FrenyUpgrade))) {
            SaveData.Entry entry = data.upgrades.Find(e => e.id == upgrade.ToString());
            SetOrDelete(FrenyProgress.LevelKeyPrefix + upgrade, entry != null ? Mathf.RoundToInt(entry.value) : 0);
            SetOrDelete(FrenyProgress.UnequippedKeyPrefix + upgrade, Has(data.unequipped, upgrade.ToString()) ? 1 : 0);
        }
        foreach (string item in Loadout.GearIds) {
            SetOrDelete(Loadout.GearKeyPrefix + item, Has(data.gear, item) ? 1 : 0);
            SetOrDelete(FrenyProgress.UnequippedKeyPrefix + item, Has(data.unequipped, item) ? 1 : 0);
        }
        foreach (CampaignLevels.Stage stage in CampaignLevels.All) {
            SaveData.Entry entry = data.bestTimes.Find(e => e.id == stage.id);
            SetOrDelete(CampaignProgress.BestTimeKeyPrefix + stage.id, entry != null ? entry.value : 0f);
        }
        // a slot saved before the Armeira sold buffs: its helmet and weapons go back as coins
        if (Has(data.gear, Loadout.Helmet)) {
            PlayerPrefs.SetInt(Loadout.GearKeyPrefix + Loadout.Helmet, 1);
        }
        // and one saved when the cosmetic helmet was Armeira gear: it stays his, as a look
        if (Has(data.gear, Loadout.CosmeticHelmet)) {
            PlayerPrefs.SetInt(Loadout.GearKeyPrefix + Loadout.CosmeticHelmet, 1);
            if (Has(data.unequipped, Loadout.CosmeticHelmet)) {
                PlayerPrefs.SetInt(FrenyProgress.UnequippedKeyPrefix + Loadout.CosmeticHelmet, 1);
            }
        }
        FrenyProgress.MigrateShop();
    }

    // old saves (and the cloud copies) have no list at all
    private static bool Has(List<string> list, string id) {
        return list != null && list.Contains(id);
    }

    private static void SetOrDelete(string key, int value) {
        if (value != 0) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
    }

    private static void SetOrDelete(string key, float value) {
        if (value != 0f) PlayerPrefs.SetFloat(key, value); else PlayerPrefs.DeleteKey(key);
    }

    private static void SetOrDelete(string key, string value) {
        if (!string.IsNullOrEmpty(value)) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key);
    }
}
