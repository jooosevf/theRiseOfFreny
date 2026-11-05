using System;
using System.Collections.Generic;
using UnityEngine;

// What the campaign world remembers (design/gdd/estado-do-mundo.md): a list of keys, holding only
// what differs from the rooms as they were built. There are three copies: the live one, the one
// taken at the last checkpoint (dying goes back to it) and the one on disk (written by a save point).
// The bag holds the coins and XP found since the last save point, so that going back also takes
// back what was earned: without it, killing, collecting and dying would farm coins forever.
public class WorldState {

    [Serializable]
    private class Entry {
        public string key;
        public int value;
    }

    [Serializable]
    private class Saved {
        public int version = 1;
        public List<Entry> entries = new List<Entry>();
        public int bagCoins;
        public int bagXp;
        public string scene;
        public bool hasPosition;
        public float x;
        public float y;
    }

    private Dictionary<string, int> live = new Dictionary<string, int>();
    private Dictionary<string, int> marked = new Dictionary<string, int>();
    private int markedCoins;
    private int markedXp;

    public int BagCoins { get; set; }
    public int BagXp { get; set; }

    // where a death goes back to; no position means the room's own start
    public string CheckpointScene { get; private set; }
    public bool HasCheckpointPosition { get; private set; }
    public Vector2 CheckpointPosition { get; private set; }

    public WorldState(string firstScene) {
        CheckpointScene = firstScene;
    }

    public bool Has(string key) {
        return live.ContainsKey(key);
    }

    public int Get(string key, int fallback = 0) {
        return live.TryGetValue(key, out int value) ? value : fallback;
    }

    public void Set(string key, int value = 1) {
        live[key] = value;
    }

    public void MarkCheckpoint(string scene, Vector2 position) {
        marked = new Dictionary<string, int>(live);
        markedCoins = BagCoins;
        markedXp = BagXp;
        CheckpointScene = scene;
        HasCheckpointPosition = true;
        CheckpointPosition = position;
    }

    public void RestoreCheckpoint() {
        live = new Dictionary<string, int>(marked);
        BagCoins = markedCoins;
        BagXp = markedXp;
    }

    // the save point moved the bag into the account
    public void EmptyBag() {
        BagCoins = BagXp = 0;
        markedCoins = markedXp = 0;
    }

    public string ToJson() {
        Saved saved = new Saved {
            bagCoins = BagCoins,
            bagXp = BagXp,
            scene = CheckpointScene,
            hasPosition = HasCheckpointPosition,
            x = CheckpointPosition.x,
            y = CheckpointPosition.y,
        };
        foreach (KeyValuePair<string, int> pair in live) {
            saved.entries.Add(new Entry { key = pair.Key, value = pair.Value });
        }
        return JsonUtility.ToJson(saved);
    }

    // null when there is nothing saved
    public static WorldState FromJson(string json) {
        if (string.IsNullOrEmpty(json)) {
            return null;
        }
        Saved saved = JsonUtility.FromJson<Saved>(json);
        if (saved == null || string.IsNullOrEmpty(saved.scene)) {
            return null;
        }
        WorldState state = new WorldState(saved.scene) {
            BagCoins = saved.bagCoins,
            BagXp = saved.bagXp,
            HasCheckpointPosition = saved.hasPosition,
            CheckpointPosition = new Vector2(saved.x, saved.y),
        };
        foreach (Entry entry in saved.entries) {
            state.live[entry.key] = entry.value;
        }
        state.marked = new Dictionary<string, int>(state.live);
        state.markedCoins = state.BagCoins;
        state.markedXp = state.BagXp;
        return state;
    }
}
