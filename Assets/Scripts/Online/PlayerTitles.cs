using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Online;

// The Títulos (José, 2026-11-01): the player picks one and it shows next to the name in the
// ranking, here and on the site. To keep the ranking as hard to cheat as it is, every title comes
// from something the server already checked:
//  - one per Conquista (and "Gladiador" for a manual run the API accepted): the signed fields of
//    achievements/{uid}, which anyone can read;
//  - "Rei da Arena": 1st on the Difícil board, all-time or today's, read from the boards themselves.
// The pick is the "title" field of achievements/{uid} (no new collection): firestore.rules only takes
// a Conquista's title when that Conquista's signed field is in the same document, and "rei" from
// anyone, because the game and the site only show it next to whoever is 1st at that moment. So an
// edited save or a forged request can at most pick a title nobody will show.
// The keys and names are the site's too (index.html, TITLES): change them together.
public static class PlayerTitles {

    public class Title {
        public string id;     // the Conquista's key, "played" or "rei"
        public string name;
        public string how;
    }

    public const string King = "rei";

    public static readonly Title[] All = {
        new Title { id = "played", name = "Gladiador", how = "Termine uma corrida manual que o servidor aceite." },
        new Title { id = King, name = "Rei da Arena", how = "Seja o 1º do Difícil, no geral ou no do dia. Só aparece enquanto você for o 1º." },
        new Title { id = "cyclops", name = "Matador de Gigantes", how = "Conquista Quanto Maior o Gigante." },
        new Title { id = "supreme", name = "Caçador de Magos", how = "Conquista Feitiço Contra o Feiticeiro." },
        new Title { id = "million", name = "Milionário", how = "Conquista Freny Milionário." },
        new Title { id = "eye", name = "Olho de Águia", how = "Conquista Na Menina dos Olhos." },
        new Title { id = "combo5", name = "Embalado", how = "Conquista Quinta Marcha." },
        new Title { id = "survivor", name = "Sobrevivente", how = "Conquista Duro de Matar." },
        new Title { id = "backstab", name = "Sorrateiro", how = "Conquista Escudo pra Quê?" },
        new Title { id = "grenade5", name = "Granadeiro", how = "Conquista Cinco Coelhos, Uma Granada." },
        new Title { id = "shotgun3", name = "Escopeteiro", how = "Conquista Doze é Demais." },
        new Title { id = "oops", name = "Desastrado", how = "Conquista Ops!" },
    };

    // the player's pick, for the whole game like the Conquistas (not per save slot)
    private const string ChosenKey = "freny.title";

    public static Title Of(string id) {
        foreach (Title t in All) {
            if (t.id == id) return t;
        }
        return null;
    }

    public static string Chosen => PlayerPrefs.GetString(ChosenKey, "");

    // what this player may pick: the Conquistas the server confirmed, or being 1st now
    public static bool Unlocked(string id) {
        if (id == "played") return Achievements.PlayedConfirmed;
        if (id == King) return IsKing(Self);
        foreach (Achievements.Info info in Achievements.All) {
            if (info.key == id) return Achievements.Confirmed(info.id);
        }
        return false;
    }

    // "" takes the title off
    public static void Choose(string id) {
        if (!string.IsNullOrEmpty(id) && Of(id) == null) return;
        PlayerPrefs.SetString(ChosenKey, id ?? "");
        PlayerPrefs.Save();
        OnlineServices online = OnlineServices.Instance;
        if (online != null && online.State == OnlineServices.Status.Online) {
            online.StartCoroutine(Push(online));
        }
    }

    // ---- the name in the ranking ----

    // THE one place a ranking name gets its decorations (the game's ranking; the site's
    // decorateName in index.html does the same). Other marks next to the name go here too.
    public static string Decorate(string name, string userId) {
        Title title = Of(Shown(userId));
        if (title == null) return name;
        return name + " <size=" + (MenuKit.SmallTextSize - 2) + "><color=#FFD84A>· " + title.name + "</color></size>";
    }

    // the title the ranking shows for this player, or null
    public static string Shown(string userId) {
        if (string.IsNullOrEmpty(userId)) return null;
        string id;
        Remote remote;
        if (userId == Self) {
            id = Chosen;   // the player sees his own pick at once, before it is uploaded
            return id == King ? (IsKing(userId) ? id : null) : (Unlocked(id) ? id : null);
        }
        if (!others.TryGetValue(userId, out remote) || string.IsNullOrEmpty(remote.title)) return null;
        id = remote.title;
        if (id == King) return IsKing(userId) ? id : null;
        return remote.has.Contains(id) ? id : null;
    }

    private static string Self {
        get {
            OnlineServices online = OnlineServices.Instance;
            return online != null && online.Auth != null ? online.Auth.UserId : null;
        }
    }

    public static bool IsKing(string userId) {
        if (string.IsNullOrEmpty(userId)) return false;
        if (userId == kingToday) return true;
        OnlineServices online = OnlineServices.Instance;
        List<LeaderboardEntry> top = online != null ? online.TopFor(ArenaLevel.Hard) : null;
        return top != null && top.Count > 0 && top[0].userId == userId;
    }

    // ---- what the other players picked (achievements/{uid}) ----

    private class Remote {
        public string title;
        public HashSet<string> has = new HashSet<string>();
    }

    private static readonly Dictionary<string, Remote> others = new Dictionary<string, Remote>();
    private static string kingToday;

    // after the boards were fetched (OnlineScreens): the titles of everyone on them, today's 1st
    // on the Difícil, and the player's own pick if it isn't online yet
    public static IEnumerator Refresh(OnlineServices online) {
        if (online == null || online.Db == null || online.Auth == null || !online.Auth.IsSignedIn) yield break;
        string root = online.Db.DocRoot;

        string daily = LeaderboardService.DailyCollection(ArenaLevel.Hard);
        yield return online.Db.RunQuery("{\"from\":[{\"collectionId\":\"" + daily + "\"}]," +
            "\"orderBy\":[{\"field\":{\"fieldPath\":\"score\"},\"direction\":\"DESCENDING\"}],\"limit\":1}", (ok, body) => {
                if (!ok) return;
                kingToday = null;
                foreach (QueryItem item in Parse(body)) {
                    kingToday = DocId(item.document.name);
                }
            });

        List<string> ids = new List<string>();
        foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
            foreach (LeaderboardEntry entry in online.TopFor(level)) {
                if (!ids.Contains(entry.userId)) ids.Add(entry.userId);
            }
        }
        if (!string.IsNullOrEmpty(kingToday) && !ids.Contains(kingToday)) ids.Add(kingToday);
        string self = online.Auth.UserId;
        if (!ids.Contains(self)) ids.Add(self);

        bool readOwn = false;
        // "IN" takes up to 30 values at a time
        for (int start = 0; start < ids.Count; start += 30) {
            StringBuilder values = new StringBuilder();
            for (int i = start; i < Mathf.Min(ids.Count, start + 30); i++) {
                if (values.Length > 0) values.Append(',');
                values.Append("{\"referenceValue\":\"").Append(root).Append("/achievements/").Append(ids[i]).Append("\"}");
            }
            string query = "{\"from\":[{\"collectionId\":\"achievements\"}]," +
                           "\"where\":{\"fieldFilter\":{\"field\":{\"fieldPath\":\"__name__\"},\"op\":\"IN\"," +
                           "\"value\":{\"arrayValue\":{\"values\":[" + values + "]}}}}}";
            yield return online.Db.RunQuery(query, (ok, body) => {
                if (!ok) return;
                readOwn = true;   // a player with no document yet is in no result
                foreach (QueryItem item in Parse(body)) {
                    others[DocId(item.document.name)] = Read(item.document.fields);
                }
            });
        }

        // the pick made offline, or on another device that lost it
        others.TryGetValue(self, out Remote own);
        string uploaded = own != null ? own.title ?? "" : "";
        if (readOwn && uploaded != Chosen && CanUpload(Chosen)) {
            yield return Push(online);
        }
    }

    // what firestore.rules takes: no title, "rei", or a Conquista that is signed in the document
    private static bool CanUpload(string id) {
        return string.IsNullOrEmpty(id) || id == King || (Of(id) != null && Unlocked(id));
    }

    private static IEnumerator Push(OnlineServices online) {
        string id = Chosen;
        if (!CanUpload(id) || online.Db == null || online.Auth == null || !online.Auth.IsSignedIn) yield break;
        FirestoreFields fields = new FirestoreFields().Set("title", id).SetServerTime("updatedAt");
        bool written = false;
        yield return online.Db.Patch("achievements/" + online.Auth.UserId, fields, ok => written = ok);
        if (written) {
            if (!others.TryGetValue(online.Auth.UserId, out Remote own)) {
                own = new Remote();
                others[online.Auth.UserId] = own;
            }
            own.title = id;
        }
    }

    // AchievementService.Load after a login: the pick from another device comes back here
    public static void ReadOwn(string body) {
        if (string.IsNullOrEmpty(body)) return;
        Doc doc = JsonUtility.FromJson<Doc>(body);
        string title = doc?.fields?.title?.stringValue;
        if (!string.IsNullOrEmpty(title) && string.IsNullOrEmpty(Chosen) && Of(title) != null) {
            PlayerPrefs.SetString(ChosenKey, title);
        }
    }

    // ---- Firestore's JSON ----

    private static QueryItem[] Parse(string body) {
        if (string.IsNullOrEmpty(body)) return new QueryItem[0];
        QueryResponse res = JsonUtility.FromJson<QueryResponse>("{\"items\":" + body + "}");
        List<QueryItem> found = new List<QueryItem>();
        if (res?.items != null) {
            foreach (QueryItem item in res.items) {
                if (item.document != null && !string.IsNullOrEmpty(item.document.name)) found.Add(item);
            }
        }
        return found.ToArray();
    }

    private static string DocId(string name) => name.Substring(name.LastIndexOf('/') + 1);

    private static Remote Read(Fields f) {
        Remote remote = new Remote { title = f?.title?.stringValue };
        if (f == null) return remote;
        FsString[] signed = { f.played, f.million, f.cyclops, f.supreme, f.grenade5, f.oops, f.shotgun3, f.backstab, f.eye, f.combo5, f.survivor };
        string[] keys = { "played", "million", "cyclops", "supreme", "grenade5", "oops", "shotgun3", "backstab", "eye", "combo5", "survivor" };
        for (int i = 0; i < keys.Length; i++) {
            if (signed[i] != null && !string.IsNullOrEmpty(signed[i].stringValue)) remote.has.Add(keys[i]);
        }
        return remote;
    }

    [Serializable] private class QueryResponse { public QueryItem[] items; }
    [Serializable] private class QueryItem { public Doc document; }
    [Serializable] private class Doc {
        public string name;
        public Fields fields;
    }
    [Serializable] private class Fields {
        public FsString title, played, million, cyclops, supreme, grenade5, oops, shotgun3, backstab, eye, combo5, survivor;
    }
}
