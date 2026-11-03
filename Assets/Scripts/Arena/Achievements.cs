using System;
using System.Collections.Generic;
using UnityEngine;

public enum Achievement { Million, Cyclops, Supreme, Grenade5, Oops, Shotgun3, Backstab, Eye, Combo50, Survivor, Cyclops3, Kills500, Marathon }

// The Conquistas (José, 2026-10-29; remapped 2026-11-03): feats of the Arena, kept in PlayerPrefs (freny.ach.<key>)
// for the whole game, not per save slot. Like the ranking, only a manual run counts: once the
// autopilot has driven in a run, nothing more is earned in it (AchievementTracker.Counts).
// An achievement earned in a run with a ticket from the ranking API is sent to it at the end of the
// run (OnlineServices); the API checks the run's log backs it up and signs it, and it goes to
// Firestore's achievements/{uid}, which the site counts to show how many players have each one.
// Keys, names and the conditions are the same in freny-online's api/_lib/achievements.js and the
// site's conquistas.json: change them together.
public static class Achievements {

    public class Info {
        public Achievement id;
        public string key;    // the icon's name (Resources/Achievements/<key>), the PlayerPrefs and Firestore field
        public string name;
        public string how;
    }

    public const long MillionScore = 1000000;
    public const int GrenadeKills = 5;
    public const int ShotgunKills = 3;
    public const int ComboStep = 489;        // the kill at 49.9x shows 50.0x after it
    public const float SurviveSeconds = 600f;
    public const float MarathonSeconds = 1800f;
    public const int BackstabKills = 10;
    public const int CyclopsKills = 3;
    public const int RunKills = 500;

    public static readonly Info[] All = {
        new Info { id = Achievement.Million, key = "million", name = "Freny Milionário",
            how = "Faça 1 milhão de pontos numa corrida." },
        new Info { id = Achievement.Cyclops, key = "cyclops", name = "Quanto Maior o Gigante",
            how = "Derrote o Ciclope." },
        new Info { id = Achievement.Supreme, key = "supreme", name = "Feitiço Contra o Feiticeiro",
            how = "Derrote o Feiticeiro Supremo." },
        new Info { id = Achievement.Grenade5, key = "grenade5", name = "Cinco Coelhos, Uma Granada",
            how = "Derrote 5 inimigos com uma granada só." },
        new Info { id = Achievement.Oops, key = "oops", name = "Ops!",
            how = "Caia com a sua própria granada." },
        new Info { id = Achievement.Shotgun3, key = "shotgun3", name = "Doze é Demais",
            how = "Derrote 3 inimigos com um tiro só da escopeta." },
        new Info { id = Achievement.Backstab, key = "backstab", name = "Escudo pra Quê?",
            how = "Derrote 10 Escudeiros numa corrida, acertando onde o escudo não cobre." },
        new Info { id = Achievement.Eye, key = "eye", name = "Na Menina dos Olhos",
            how = "Dê o golpe final no Ciclope acertando o olho dele." },
        new Info { id = Achievement.Combo50, key = "combo50", name = "Quinta Marcha",
            how = "Chegue ao combo 50x." },
        new Info { id = Achievement.Survivor, key = "survivor", name = "Duro de Matar",
            how = "Fique vivo 10 minutos numa corrida." },
        new Info { id = Achievement.Cyclops3, key = "cyclops3", name = "Caça-Ciclopes",
            how = "Derrote 3 Ciclopes numa corrida." },
        new Info { id = Achievement.Kills500, key = "kills500", name = "Exterminador",
            how = "Derrote 500 inimigos numa corrida." },
        new Info { id = Achievement.Marathon, key = "marathon", name = "Maratonista",
            how = "Fique vivo 30 minutos numa corrida." },
    };

    public const string KeyPrefix = "freny.ach.";
    private const string ConfirmedSuffix = ".online";
    // the player finished a manual run the API accepted: the site counts players by it
    private const string PlayedKey = KeyPrefix + "played" + ConfirmedSuffix;

    public static event Action<Achievement> Unlocked;
    // a moment the API has to see in the run's log to accept an achievement (the grenade's or the
    // shotgun's kills, the hit on the Escudeiro's back or the Ciclope's eye, the own grenade)
    public static event Action<string, int> Happened;

    // earned in the current run, so the end of the run knows there is something to send
    public static readonly List<Achievement> EarnedThisRun = new List<Achievement>();

    public static Info Of(Achievement a) => All[(int)a];

    public static bool Has(Achievement a) => PlayerPrefs.GetInt(KeyPrefix + Of(a).key, 0) == 1;

    // the API signed it and it is in Firestore
    public static bool Confirmed(Achievement a) => PlayerPrefs.GetInt(KeyPrefix + Of(a).key + ConfirmedSuffix, 0) == 1;

    public static bool PlayedConfirmed => PlayerPrefs.GetInt(PlayedKey, 0) == 1;

    // ---- the new ones (José, 2026-11-03): earned in a run and not yet seen on the Conquistas page ----
    // The run's banner only shows the icon and the name; the "nova" on the Conquistas button says
    // there is something to read there, until the page is opened.
    private const string NewKey = KeyPrefix + "new";

    public static bool IsNew(Achievement a) => Array.IndexOf(NewKeys(), Of(a).key) >= 0;

    public static int NewCount => NewKeys().Length;

    private static string[] NewKeys() {
        string keys = PlayerPrefs.GetString(NewKey, "");
        return keys.Length == 0 ? new string[0] : keys.Split(',');
    }

    // the Conquistas page was opened: all of them seen
    public static void MarkSeen() {
        if (PlayerPrefs.HasKey(NewKey)) {
            PlayerPrefs.DeleteKey(NewKey);
            PlayerPrefs.Save();
        }
    }

    // "Conquistas", "Conquistas (nova!)", "Conquistas (2 novas)"
    public static string ButtonLabel {
        get {
            int n = NewCount;
            return n == 0 ? "Conquistas" : n == 1 ? "Conquistas (nova!)" : "Conquistas (" + n + " novas)";
        }
    }

    public static int Count {
        get {
            int n = 0;
            foreach (Info info in All) {
                if (Has(info.id)) n++;
            }
            return n;
        }
    }

    public static Sprite Icon(Achievement a) => Resources.Load<Sprite>("Achievements/" + Of(a).key);

    // something for the API at the end of this run
    public static bool NeedsClaim => EarnedThisRun.Count > 0 || !PlayedConfirmed;

    public static void Earn(Achievement a, string moment = null, int count = 0) {
        if (!AchievementTracker.Counts) {
            return;
        }
        if (moment != null) {
            Happened?.Invoke(moment, count);
        }
        if (Has(a)) {
            return;
        }
        Grant(a);
    }

    // a moment for the run's log that doesn't earn anything yet (the API counts them)
    public static void Moment(string moment, int count) {
        if (AchievementTracker.Counts) {
            Happened?.Invoke(moment, count);
        }
    }

    private static void Grant(Achievement a) {
        PlayerPrefs.SetInt(KeyPrefix + Of(a).key, 1);
        if (!IsNew(a)) {
            string keys = PlayerPrefs.GetString(NewKey, "");
            PlayerPrefs.SetString(NewKey, keys.Length == 0 ? Of(a).key : keys + "," + Of(a).key);
        }
        PlayerPrefs.Save();
        if (!EarnedThisRun.Contains(a)) EarnedThisRun.Add(a);
        Unlocked?.Invoke(a);
    }

    // the API's answer, or the player's own achievements/{uid} read back after a login
    public static void MarkConfirmed(string key) {
        if (key == "played") {
            PlayerPrefs.SetInt(PlayedKey, 1);
            return;
        }
        foreach (Info info in All) {
            if (info.key == key) {
                PlayerPrefs.SetInt(KeyPrefix + key, 1);
                PlayerPrefs.SetInt(KeyPrefix + key + ConfirmedSuffix, 1);
            }
        }
    }

    // ---- the moments the weapons report ----

    // Grenade.Explode: the enemies one blast killed
    public static void GrenadeBlast(int kills) {
        if (kills >= GrenadeKills) {
            Earn(Achievement.Grenade5, "grenade", kills);
        }
    }

    // Freny fell to his own grenade
    public static void OwnGrenade() {
        Earn(Achievement.Oops, "oops", 1);
    }

    private static int lastShot;
    private static int countedShot;
    private static int shotKills;

    // Player.Shoot: one number for the three pellets of a shotgun shot
    public static int NextShot() => ++lastShot;

    // RangedAttack: a pellet of that shot killed an enemy
    public static void ShotKill(int shot) {
        if (shot != countedShot) {
            countedShot = shot;
            shotKills = 0;
        }
        shotKills++;
        if (shotKills >= ShotgunKills) {
            Earn(Achievement.Shotgun3, "shotgun", shotKills);
        }
    }

#if UNITY_EDITOR
    // tests in the editor: earn one at once, whatever the run
    public static void DebugEarn(Achievement a) {
        Grant(a);
    }

    public static void DebugClear() {
        foreach (Info info in All) {
            PlayerPrefs.DeleteKey(KeyPrefix + info.key);
            PlayerPrefs.DeleteKey(KeyPrefix + info.key + ConfirmedSuffix);
        }
        PlayerPrefs.DeleteKey(PlayedKey);
        PlayerPrefs.DeleteKey(NewKey);
    }
#endif
}
