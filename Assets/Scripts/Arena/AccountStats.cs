using System;
using System.Collections.Generic;
using UnityEngine;

// The Estatísticas (José, 2026-11-01): what this save has done in the Arena, kept in the save
// slot ("freny.stats", SaveSystem) so it goes to the cloud with the rest of the save. Every
// Arena run counts here, autopilot ones too (they are part of the save's story; the ranking and
// the Conquistas are where only manual runs count). The play time is the save's own (SaveData).
public static class AccountStats {

    [Serializable]
    public class Count {
        public string id;
        public int n;
    }

    [Serializable]
    public class Data {
        public int runs;
        public int manualRuns;
        public int kills;
        public List<Count> byEnemy = new List<Count>();
        public int bestComboTenths;              // 10 = 1.0x
        public List<Count> bestScore = new List<Count>();   // id: ArenaLevel name
        public float longestRun;                 // seconds
        public int eyeHits;
        public int heals;
    }

    public const string Key = "freny.stats";

    // the enemies in the order the Estatísticas page lists them, with their names in the game
    public static readonly string[,] Enemies = {
        { "Bug_Enemy", "Insetos" },
        { "Bat_Enemy", "Morcegos" },
        { "Licker_Enemy", "Linguarudos" },
        { "Golem_Enemy", "Golems" },
        { "Golem_Red", "Golems Vermelhos" },
        { "Shielded_Enemy", "Escudeiros" },
        { "Sorcerer_Enemy", "Feiticeiros" },
        { "Sorcerer_Supreme", "Feiticeiros Supremos" },
        { "Cyclops_Boss", "Ciclopes" },
    };

    private static Data data;
    private static string dataRaw;

    // the live copy is the PlayerPrefs key (SaveSystem swaps it with the save slot): read again
    // whenever it changed under us
    public static Data Current {
        get {
            string raw = PlayerPrefs.GetString(Key, "");
            if (data == null || raw != dataRaw) {
                data = null;
                if (!string.IsNullOrEmpty(raw)) {
                    try { data = JsonUtility.FromJson<Data>(raw); } catch (ArgumentException) { data = null; }
                }
                data = data ?? new Data();
                data.byEnemy = data.byEnemy ?? new List<Count>();
                data.bestScore = data.bestScore ?? new List<Count>();
                dataRaw = raw;
            }
            return data;
        }
    }

    private static void Store() {
        dataRaw = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(Key, dataRaw);
    }

    public static int Kills(string prefab) {
        Count c = Current.byEnemy.Find(e => e.id == prefab);
        return c != null ? c.n : 0;
    }

    public static int BestScore(ArenaLevel level) {
        Count c = Current.bestScore.Find(e => e.id == level.ToString());
        return c != null ? c.n : 0;
    }

    // ---- what an Arena run reports (ProgressTracker) ----

    public static void Kill(string prefab, int comboTenths) {
        Data d = Current;
        d.kills++;
        Count c = d.byEnemy.Find(e => e.id == prefab);
        if (c == null) {
            c = new Count { id = prefab };
            d.byEnemy.Add(c);
        }
        c.n++;
        d.bestComboTenths = Mathf.Max(d.bestComboTenths, comboTenths);
        Store();
    }

    public static void EyeHit() {
        Current.eyeHits++;
        Store();
    }

    public static void Healed() {
        Current.heals++;
        Store();
    }

    public static void RunEnded(ArenaLevel level, int score, float seconds, bool manual) {
        Data d = Current;
        d.runs++;
        if (manual) d.manualRuns++;
        d.longestRun = Mathf.Max(d.longestRun, seconds);
        Count c = d.bestScore.Find(e => e.id == level.ToString());
        if (c == null) {
            c = new Count { id = level.ToString() };
            d.bestScore.Add(c);
        }
        c.n = Mathf.Max(c.n, score);
        Store();
    }
}
