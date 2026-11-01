using System;
using UnityEngine;

// The Missões do dia (José, 2026-11-01): three Arena missions a day, one easy, one medium and one
// hard, the same for every player: the pick comes from the Brasília date (UTC-3, the same day
// as the daily ranking), not from a random seed on this PC. They count only in manual Arena runs,
// like the Conquistas (AchievementTracker.Counts): the autopilot already earns coins by itself
// while the player is away, and the missions are a reason to play by hand. A mission pays its
// coins the moment it is done (no button to remember), with a banner in the Arena (ProgressTracker).
// Progress lives in the save slot ("freny.missions", SaveSystem), so it goes to the cloud with the
// rest of the save and starts over when the day turns.
public static class DailyMissions {

    public enum Kind {
        Kills,         // enemies of the mission's kinds (any when none), summed over the day
        SwordKills,    // the same, the last hit by the sword
        ShotKills,     // the last hit by a bullet or shotgun pellet
        BackKills,     // the last hit round the Escudeiro's shield
        EyeHits,       // hits on the Ciclope's eye (any hit, not only the killing one)
        Combo,         // the combo shown on screen, in tenths (30 = 3.0x), in one run
        Score,         // points in one run
        ScoreNoHeal,   // points in one run before Freny heals for the first time (potion)
        Survive,       // seconds alive in one run
    }

    public class Mission {
        public string id;
        public string text;
        public Kind kind;
        public string[] enemies;   // prefab names (RunLog.PrefabName), null for any
        public int target;
        public int tier;           // 0 easy, 1 medium, 2 hard

        public int Reward => Rewards[tier];
        // "in one run": the progress is the best run of the day, not a sum
        public bool PerRun => kind == Kind.Combo || kind == Kind.Score || kind == Kind.ScoreNoHeal || kind == Kind.Survive;
    }

    // A Difícil run of ~2 min (65k points) brings ~260 coins (FrenyProgress' prices): the three
    // together (350) are about one more good run a day, under 1% of the whole coin shop, so they
    // help without replacing playing. The hard one asks for a run past the first Ciclope (50k).
    public static readonly int[] Rewards = { 50, 100, 200 };
    public static readonly string[] TierNames = { "Fácil", "Média", "Difícil" };

    private static readonly string[] Golems = { "Golem_Enemy", "Golem_Red" };
    private static readonly string[] Sorcerers = { "Sorcerer_Enemy", "Sorcerer_Supreme" };

    // the order inside each tier is part of the daily pick: add new ones at the end of their tier
    public static readonly Mission[] Pool = {
        // easy: any run on any difficulty gets there
        new Mission { id = "bugs50", tier = 0, kind = Kind.Kills, enemies = new[] { "Bug_Enemy" }, target = 50, text = "Derrote 50 insetos" },
        new Mission { id = "bats30", tier = 0, kind = Kind.Kills, enemies = new[] { "Bat_Enemy" }, target = 30, text = "Derrote 30 morcegos" },
        new Mission { id = "kills150", tier = 0, kind = Kind.Kills, target = 150, text = "Derrote 150 inimigos" },
        new Mission { id = "sword60", tier = 0, kind = Kind.SwordKills, target = 60, text = "Derrote 60 inimigos com a espada" },
        new Mission { id = "score20k", tier = 0, kind = Kind.Score, target = 20000, text = "Faça 20 mil pontos numa corrida" },
        // medium: a good run, or a plan
        new Mission { id = "golems15", tier = 1, kind = Kind.Kills, enemies = Golems, target = 15, text = "Derrote 15 Golems" },
        new Mission { id = "lickers10", tier = 1, kind = Kind.Kills, enemies = new[] { "Licker_Enemy" }, target = 10, text = "Derrote 10 Linguarudos" },
        new Mission { id = "sorcsword5", tier = 1, kind = Kind.SwordKills, enemies = Sorcerers, target = 5, text = "Derrote 5 feiticeiros com a espada" },
        new Mission { id = "shots25", tier = 1, kind = Kind.ShotKills, target = 25, text = "Derrote 25 inimigos com tiros" },
        new Mission { id = "combo3", tier = 1, kind = Kind.Combo, target = 30, text = "Faça um combo de 3.0x" },
        new Mission { id = "noheal30k", tier = 1, kind = Kind.ScoreNoHeal, target = 30000, text = "Passe de 30 mil pontos sem se curar" },
        new Mission { id = "survive4", tier = 1, kind = Kind.Survive, target = 240, text = "Fique vivo 4 minutos numa corrida" },
        // hard: the Ciclope (50k points), the Supremo (100k) or a long run
        new Mission { id = "eye3", tier = 2, kind = Kind.EyeHits, target = 3, text = "Acerte o olho do Ciclope 3 vezes" },
        new Mission { id = "cyclops", tier = 2, kind = Kind.Kills, enemies = new[] { "Cyclops_Boss" }, target = 1, text = "Derrote um Ciclope" },
        new Mission { id = "combo5", tier = 2, kind = Kind.Combo, target = 50, text = "Faça um combo de 5.0x" },
        new Mission { id = "back5", tier = 2, kind = Kind.BackKills, enemies = new[] { "Shielded_Enemy" }, target = 5, text = "Derrote 5 Escudeiros pelas costas" },
        new Mission { id = "score100k", tier = 2, kind = Kind.Score, target = 100000, text = "Faça 100 mil pontos numa corrida" },
        new Mission { id = "supreme2", tier = 2, kind = Kind.Kills, enemies = new[] { "Sorcerer_Supreme" }, target = 2, text = "Derrote 2 Feiticeiros Supremos" },
    };

    public const string Key = "freny.missions";

    // a mission was just done and paid (ProgressTracker shows the banner)
    public static event Action<Mission> Completed;

    [Serializable]
    private class State {
        public string day;
        public int[] progress = new int[3];
        public bool[] done = new bool[3];
    }

    private static State state;
    private static string stateRaw;

    // ---- the day and its three missions ----

    // yyyyMMdd in Brasília, like LeaderboardService.DailyCollection
    public static string Today => DateTime.UtcNow.AddHours(-3).ToString("yyyyMMdd");

    // how long until midnight in Brasília, when new missions come (for "novas em 5 h")
    public static TimeSpan UntilNextDay {
        get {
            DateTime brt = DateTime.UtcNow.AddHours(-3);
            return brt.Date.AddDays(1) - brt;
        }
    }

    public static Mission[] For(string day) {
        int number = DayNumber(day);
        Mission[] picked = new Mission[3];
        for (int tier = 0; tier < 3; tier++) {
            int count = 0;
            foreach (Mission m in Pool) if (m.tier == tier) count++;
            int pick = (int)(Mix((uint)number * 3u + (uint)tier) % (uint)count);
            foreach (Mission m in Pool) {
                if (m.tier != tier) continue;
                if (pick-- == 0) {
                    picked[tier] = m;
                    break;
                }
            }
        }
        return picked;
    }

    public static Mission[] Current => For(Today);

    // days since 2026-01-01 (any fixed day works; this one keeps the numbers small)
    private static int DayNumber(string day) {
        DateTime date = DateTime.ParseExact(day, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return (int)(date - new DateTime(2026, 1, 1)).TotalDays;
    }

    // a fixed integer hash (the "lowbias32" mixer), so every platform picks the same: the site can
    // do the same in JavaScript with Math.imul and >>> 0
    private static uint Mix(uint x) {
        x ^= x >> 16;
        x *= 0x7feb352dU;
        x ^= x >> 15;
        x *= 0x846ca68bU;
        x ^= x >> 16;
        return x;
    }

    // ---- progress ----

    public static int Progress(int index) => Load().progress[index];
    public static bool Done(int index) => Load().done[index];

    public static int DoneCount {
        get {
            State s = Load();
            int n = 0;
            for (int i = 0; i < 3; i++) if (s.done[i]) n++;
            return n;
        }
    }

    // the live copy is the PlayerPrefs key (SaveSystem swaps it with the save slot): read again
    // whenever it changed under us, and start over when it is from another day
    private static State Load() {
        string raw = PlayerPrefs.GetString(Key, "");
        if (state == null || raw != stateRaw) {
            state = null;
            if (!string.IsNullOrEmpty(raw)) {
                try { state = JsonUtility.FromJson<State>(raw); } catch (ArgumentException) { state = null; }
            }
            stateRaw = raw;
        }
        string today = Today;
        if (state == null || state.day != today || state.progress == null || state.progress.Length != 3 ||
            state.done == null || state.done.Length != 3) {
            state = new State { day = today };
            Store();
        }
        return state;
    }

    private static void Store() {
        stateRaw = JsonUtility.ToJson(state);
        PlayerPrefs.SetString(Key, stateRaw);
    }

    private static bool Matches(Mission m, string prefab) {
        return m.enemies == null || Array.IndexOf(m.enemies, prefab) >= 0;
    }

    // ---- what a manual Arena run reports (ProgressTracker) ----

    public static void Kill(string prefab, string weapon, string mark) {
        Mission[] today = Current;
        for (int i = 0; i < 3; i++) {
            Mission m = today[i];
            if (!Matches(m, prefab)) continue;
            bool counts = m.kind == Kind.Kills
                       || (m.kind == Kind.SwordKills && weapon == Enemy.SwordHit)
                       || (m.kind == Kind.ShotKills && weapon == Enemy.ShotHit)
                       || (m.kind == Kind.BackKills && mark == Enemy.BackHit);
            if (counts) Add(i, 1);
        }
    }

    public static void EyeHit() {
        Mission[] today = Current;
        for (int i = 0; i < 3; i++) {
            if (today[i].kind == Kind.EyeHits) Add(i, 1);
        }
    }

    // the run as it stands: points, seconds alive, the combo on screen (tenths) and whether he healed
    public static void RunTick(int score, float seconds, int comboTenths, bool healed) {
        Mission[] today = Current;
        for (int i = 0; i < 3; i++) {
            switch (today[i].kind) {
                case Kind.Score: Reach(i, score); break;
                case Kind.ScoreNoHeal: if (!healed) Reach(i, score); break;
                case Kind.Survive: Reach(i, Mathf.FloorToInt(seconds)); break;
                case Kind.Combo: Reach(i, comboTenths); break;
            }
        }
    }

    private static void Add(int index, int amount) {
        State s = Load();
        if (s.done[index]) return;
        Set(index, s.progress[index] + amount);
    }

    private static void Reach(int index, int value) {
        State s = Load();
        if (s.done[index] || value <= s.progress[index]) return;
        Set(index, value);
    }

    private static void Set(int index, int value) {
        State s = Load();
        Mission m = Current[index];
        s.progress[index] = Mathf.Min(value, m.target);
        if (s.progress[index] >= m.target) {
            s.done[index] = true;
            Pay(m.Reward);
        }
        Store();
        if (s.done[index]) {
            Completed?.Invoke(m);
        }
    }

    // in the Arena the Player holds the coins in memory (and writes them back); elsewhere the save does
    private static void Pay(int coins) {
        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player != null) {
            player.AddCoins(coins);
        } else {
            FrenyProgress.Coins += coins;
        }
    }

    // For the final test (reflection): moves today's mission `index` (0-2) on by `amount` as if a
    // manual run did it, paying it when it gets there.
    private static void TestAdvance(int index, int amount) {
        if (Current[index].PerRun) Reach(index, Progress(index) + amount); else Add(index, amount);
    }
}
