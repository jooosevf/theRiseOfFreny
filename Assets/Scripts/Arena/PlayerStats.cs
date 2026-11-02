using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Online;

// The Perfil's numbers (José, 2026-11-02): what this ACCOUNT did, on any device. AccountStats is
// the per-save story that travels with the save slot; this one is per login. StatsTracker feeds it
// during the run and it is written once when the run ends, to stats/{uid} (firestore.rules: only
// the owner, counters only go up, one write every 20 s). It is read once when the player logs in.
//
// Each counter is kept twice in PlayerPrefs, per user id: the total as last known, and what was
// played since the last upload (pending). So a run offline or a refused write isn't lost: the next
// login gives cloud + pending, and two devices add up instead of overwriting each other.
public static class PlayerStats {

    // keys of stats/{uid}; every one must be listed in firebase/firestore.rules (statsField)
    public const string RunsManual = "runsManual", RunsAuto = "runsAuto";
    public const string SecondsManual = "secManual", SecondsAuto = "secAuto";
    public const string LongestRun = "longestRun";
    public const string Kills = "kills", KillsAuto = "killsAuto";
    public const string Deaths = "deaths";
    public const string BestCombo = "bestCombo";          // in tenths: 10 = 1.0x
    public const string Nights = "nights", BloodMoons = "bloodMoons";
    public const string CoinsEarned = "coinsEarned", CoinsSpent = "coinsSpent";
    public const string Chests = "chests";

    // the weapons that can land the killing blow (Enemy.LastHitSource)
    public const string WeaponSword = "sword", WeaponGreatSword = "greatsword", WeaponFury = "fury",
        WeaponGun = "gun", WeaponShotgun = "shotgun", WeaponGrenade = "grenade", WeaponStaff = "staff", WeaponOther = "other";

    // what can take his last heart besides an enemy's own hit (Player.NextDamageSource)
    public const string CauseFall = "fall", CauseGrenade = "grenade", CauseMine = "mine", CauseSpike = "spike",
        CauseVomit = "vomit", CauseBeam = "beam", CauseShockwave = "shock", CauseAcid = "acid", CauseOther = "other";

    // enemy prefab -> its key and its name in the game (the same names as the Guia and the Bestiário)
    public static readonly string[,] Enemies = {
        { "Bug_Enemy", "bug", "Insetos" },
        { "Bat_Enemy", "bat", "Morcegos" },
        { "Licker_Enemy", "licker", "Linguarudos" },
        { "Golem_Enemy", "golem", "Golems" },
        { "Golem_Red", "golemRed", "Golems Vermelhos" },
        { "Shielded_Enemy", "shield", "Escudeiros" },
        { "Sorcerer_Enemy", "sorcerer", "Feiticeiros" },
        { "Sorcerer_Supreme", "supreme", "Feiticeiros Supremos" },
        { "Cyclops_Boss", "cyclops", "Ciclopes" },
    };

    public static readonly string[,] Weapons = {
        { WeaponSword, "Espada" }, { WeaponGreatSword, "Espadão" }, { WeaponFury, "Fúria" }, { WeaponGun, "Revólver" },
        { WeaponShotgun, "Escopeta" }, { WeaponGrenade, "Granada" }, { WeaponStaff, "Cajado" }, { WeaponOther, "Outras" },
    };

    public static readonly string[,] Causes = {
        { CauseGrenade, "A própria granada" }, { CauseVomit, "Vômito do Ciclope" }, { CauseBeam, "Raio do Ciclope" },
        { CauseShockwave, "Onda de choque" }, { CauseMine, "Minas" }, { CauseSpike, "Espinhos" }, { CauseAcid, "Ácido" },
        { CauseFall, "Queda" }, { CauseOther, "Outras causas" },
    };

    public static string KillKey(string enemyKey) => "k_" + enemyKey;
    public static string WeaponKey(string weapon) => "w_" + weapon;
    public static string DeathKey(string cause) => "d_" + cause;
    public static string RunsKey(ArenaLevel level) => "runs" + level;
    public static string BestKey(ArenaLevel level, bool auto = false) => (auto ? "bestAuto" : "best") + level;

    // the key of an enemy prefab, or null for something the list doesn't know
    public static string EnemyKey(string prefab) {
        for (int i = 0; i < Enemies.GetLength(0); i++) {
            if (Enemies[i, 0] == prefab) return Enemies[i, 1];
        }
        return null;
    }

    // only what firestore.rules lists: one unknown field and the whole document is refused
    public static string CauseOf(string source) {
        if (string.IsNullOrEmpty(source)) return CauseOther;
        string enemy = EnemyKey(source);
        if (enemy != null) return enemy;
        for (int i = 0; i < Causes.GetLength(0); i++) {
            if (Causes[i, 0] == source) return source;
        }
        return CauseOther;
    }

    // these keep their highest value; everything else adds up
    private static bool IsPeak(string key) => key == LongestRun || key == BestCombo || key.StartsWith("best", StringComparison.Ordinal);

    // ---- what is in memory ----

    private const string TotalPrefix = "online.stats.";       // + user id
    private const string PendingPrefix = "online.statsPending.";
    private const string CreatedPrefix = "online.statsCreated.";

    [Serializable] private class Saved {
        public List<string> keys = new List<string>();
        public List<long> values = new List<long>();
    }

    private static string owner = "";   // the user id these belong to; "" before the first login
    private static Dictionary<string, long> totals;
    private static Dictionary<string, long> pending;
    private static bool dirty;
    private static bool cloudRead;      // the document was read since the login (or there is none yet)
    private static bool cloudExists;
    private static float lastWrite = -100f;
    private const float WriteEvery = 21f;   // firestore.rules: one write every 20 s

    public static DateTime? CreatedAt { get; private set; }

    private static void Ready() {
        if (totals != null) return;
        totals = Read(TotalPrefix + owner);
        pending = Read(PendingPrefix + owner);
    }

    public static long Get(string key) {
        Ready();
        return totals.TryGetValue(key, out long value) ? value : 0;
    }

    public static long Seconds => Get(SecondsManual) + Get(SecondsAuto);
    public static long Runs => Get(RunsManual) + Get(RunsAuto);

    // ---- what the game reports (StatsTracker) ----

    public static void Add(string key, long amount = 1) {
        if (amount <= 0) return;
        Ready();
        totals.TryGetValue(key, out long total);
        totals[key] = total + amount;
        pending.TryGetValue(key, out long waiting);
        pending[key] = waiting + amount;
        dirty = true;
    }

    public static void Peak(string key, long value) {
        Ready();
        totals.TryGetValue(key, out long total);
        if (value <= total) return;
        totals[key] = value;
        pending[key] = value;
        dirty = true;
    }

    // FrenyProgress.Coins: the difference every time the coins change, in the Arena or in a shop
    public static void CoinsChanged(int delta) {
        if (delta > 0) Add(CoinsEarned, delta);
        else if (delta < 0) Add(CoinsSpent, -delta);
    }

    public static void SaveLocal() {
        Ready();
        if (!dirty) return;
        dirty = false;
        Write(TotalPrefix + owner, totals);
        Write(PendingPrefix + owner, pending);
        PlayerPrefs.Save();
    }

    private static Dictionary<string, long> Read(string pref) {
        Dictionary<string, long> map = new Dictionary<string, long>();
        string json = PlayerPrefs.GetString(pref, "");
        if (string.IsNullOrEmpty(json)) return map;
        Saved saved = null;
        try {
            saved = JsonUtility.FromJson<Saved>(json);
        } catch (ArgumentException) {
        }
        if (saved == null) return map;
        for (int i = 0; i < saved.keys.Count && i < saved.values.Count; i++) {
            map[saved.keys[i]] = saved.values[i];
        }
        return map;
    }

    private static void Write(string pref, Dictionary<string, long> map) {
        Saved saved = new Saved();
        foreach (KeyValuePair<string, long> pair in map) {
            saved.keys.Add(pair.Key);
            saved.values.Add(pair.Value);
        }
        PlayerPrefs.SetString(pref, JsonUtility.ToJson(saved));
    }

    // ---- the cloud ----

    // after a login: this account's copy, plus what this device played before it got online
    public static IEnumerator Load(FirestoreClient db, FirebaseAuthService auth) {
        if (!auth.IsSignedIn) yield break;
        Bind(auth.UserId);
        yield return ReadCloud(db, auth);
    }

    private static void Bind(string uid) {
        Ready();
        if (owner == uid) return;
        SaveLocal();
        Dictionary<string, long> before = owner == "" ? pending : null;   // played before the first login
        owner = uid;
        totals = Read(TotalPrefix + owner);
        pending = Read(PendingPrefix + owner);
        cloudRead = false;
        cloudExists = false;
        CreatedAt = null;
        string created = PlayerPrefs.GetString(CreatedPrefix + owner, "");
        if (DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when)) CreatedAt = when;
        if (before != null && before.Count > 0) {
            foreach (KeyValuePair<string, long> pair in before) {
                if (IsPeak(pair.Key)) Peak(pair.Key, pair.Value);
                else Add(pair.Key, pair.Value);
            }
            PlayerPrefs.DeleteKey(TotalPrefix);
            PlayerPrefs.DeleteKey(PendingPrefix);
            SaveLocal();
        }
    }

    private static IEnumerator ReadCloud(FirestoreClient db, FirebaseAuthService auth) {
        bool ok = false;
        string body = null;
        yield return db.Get("stats/" + auth.UserId, (success, text) => {
            ok = success;
            body = text;
        });
        if (!ok) yield break;   // offline, or the rules aren't published: the local copy stays
        FsDoc doc = FsDoc.Parse(body);
        cloudRead = true;
        cloudExists = doc != null;
        if (doc == null) yield break;
        Dictionary<string, long> merged = new Dictionary<string, long>(doc.Ints);
        foreach (KeyValuePair<string, long> pair in pending) {
            merged.TryGetValue(pair.Key, out long cloud);
            merged[pair.Key] = IsPeak(pair.Key) ? Math.Max(cloud, pair.Value) : cloud + pair.Value;
        }
        totals = merged;
        if (doc.Times.TryGetValue("createdAt", out DateTime created)) {
            CreatedAt = created;
            PlayerPrefs.SetString(CreatedPrefix + owner, created.ToString("o", CultureInfo.InvariantCulture));
        }
        dirty = true;
        SaveLocal();
    }

    // the end of a run: one write, with everything; what could not go now goes with the next run
    public static IEnumerator Upload(FirestoreClient db, FirebaseAuthService auth) {
        SaveLocal();
        if (!auth.IsSignedIn || db == null) yield break;
        Bind(auth.UserId);
        if (!cloudRead) yield return ReadCloud(db, auth);
        if (!cloudRead || pending.Count == 0) yield break;
        if (Time.realtimeSinceStartup - lastWrite < WriteEvery) yield break;
        lastWrite = Time.realtimeSinceStartup;

        Dictionary<string, long> sent = new Dictionary<string, long>(pending);
        string uid = owner;
        FirestoreFields fields = new FirestoreFields();
        foreach (KeyValuePair<string, long> pair in totals) {
            fields.Set(pair.Key, pair.Value);
        }
        fields.SetServerTime("lastSeen");
        if (!cloudExists) fields.SetServerTime("createdAt");
        bool written = false;
        yield return db.Patch("stats/" + uid, fields, ok => written = ok);
        if (!written || owner != uid) yield break;
        cloudExists = true;
        if (CreatedAt == null) {
            CreatedAt = DateTime.UtcNow;
            PlayerPrefs.SetString(CreatedPrefix + owner, CreatedAt.Value.ToString("o", CultureInfo.InvariantCulture));
        }
        // what came in while it was on its way stays pending
        foreach (KeyValuePair<string, long> pair in sent) {
            pending.TryGetValue(pair.Key, out long now);
            if (IsPeak(pair.Key) ? now <= pair.Value : now == pair.Value) pending.Remove(pair.Key);
            else if (!IsPeak(pair.Key)) pending[pair.Key] = now - pair.Value;
        }
        dirty = true;
        SaveLocal();
    }
}
