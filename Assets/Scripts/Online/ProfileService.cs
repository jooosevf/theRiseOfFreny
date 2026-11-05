using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Online {
    // A public profile, perfis/{uid}: what the site's Perfil, the friends list and the friends
    // ranking show. No email in it. Read with FsDoc, written by ProfileService.
    public class PerfilInfo {
        public string uid;
        public string apelido;
        public string codigo;
        public string helmet, cape, color, suit;
        public int rebirths;
        public DateTime? lastSeen;
        public readonly Dictionary<string, long> numbers = new Dictionary<string, long>();

        public long Number(string name) => numbers.TryGetValue(name, out long value) ? value : 0;
        public long Best(ArenaLevel level) => Number("best" + level);

        public static PerfilInfo From(FsDoc doc) {
            if (doc == null) return null;
            PerfilInfo perfil = new PerfilInfo {
                uid = doc.Id,
                apelido = doc.Text("apelido"),
                codigo = doc.Text("codigo"),
                helmet = doc.Text("helmet"),
                cape = doc.Text("cape"),
                color = doc.Text("color"),
                suit = doc.Text("suit"),
                rebirths = (int)doc.Int("rebirths")
            };
            if (doc.Times.TryGetValue("lastSeen", out DateTime seen)) perfil.lastSeen = seen;
            foreach (KeyValuePair<string, long> pair in doc.Ints) perfil.numbers[pair.Key] = pair.Value;
            return perfil;
        }

        // "agora", "há 5 min", "há 3 h", "há 2 dias"
        public string SeenText() {
            if (lastSeen == null) return "nunca";
            TimeSpan ago = DateTime.UtcNow - lastSeen.Value;
            if (ago.TotalMinutes < 3) return "agora";
            if (ago.TotalMinutes < 60) return "há " + (int)ago.TotalMinutes + " min";
            if (ago.TotalHours < 24) return "há " + (int)ago.TotalHours + " h";
            int days = (int)ago.TotalDays;
            return days == 1 ? "há 1 dia" : "há " + days + " dias";
        }
    }

    // The nickname, the friend code and the public profile of an account (not of a guest):
    //   apelidos/{chave} = {uid}   who owns a nickname; only created, and only its owner deletes it
    //   codigos/{FRENY-XXXX} = {uid}   the same for the friend code
    //   (one of each per account: created only in the commit that puts them on the profile, Publish)
    //   perfis/{uid}               the public profile, written when a run ends and at login
    // firestore.rules has the same rules for what a nickname can be (keep them together: also the
    // site's nickKey in index.html).
    public class ProfileService {
        public const int MinNickLength = 3;
        public const int MaxNickLength = OnlineServices.MaxNameLength;
        public const string SiteUrl = "https://freny-online.vercel.app/";

        private const string CachePref = "online.me";
        private const float PushEvery = 11f;   // firestore.rules: one write every 10 s
        private const string CodeLetters = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        private readonly FirestoreClient _db;
        private readonly FirebaseAuthService _auth;
        private float _lastPush = -100f;

        [Serializable] private class Cache {
            public string uid, apelido, codigo;
        }

        private Cache _me;

        public ProfileService(FirestoreClient db, FirebaseAuthService auth) {
            _db = db;
            _auth = auth;
            string json = PlayerPrefs.GetString(CachePref, "");
            if (!string.IsNullOrEmpty(json)) {
                try {
                    _me = JsonUtility.FromJson<Cache>(json);
                } catch (ArgumentException) {
                }
            }
        }

        private bool Mine => _me != null && _me.uid == _auth.UserId;
        public string Nick => Mine ? _me.apelido : null;
        public string Code => Mine ? _me.codigo : null;
        public bool HasNick => !string.IsNullOrEmpty(Nick);

        // the page of this account on the site
        public string SiteLink => SiteUrl + (HasNick ? "#perfil/" + UnityEngine.Networking.UnityWebRequest.EscapeURL(Nick) : "#perfil");

        private void Remember(string nick, string code) {
            _me = new Cache { uid = _auth.UserId, apelido = nick, codigo = code };
            PlayerPrefs.SetString(CachePref, JsonUtility.ToJson(_me));
            PlayerPrefs.Save();
        }

        public void Forget() {
            _me = null;
            PlayerPrefs.DeleteKey(CachePref);
        }

        // ---- what a nickname can be ----

        // "José_1" -> "jose_1", the document's id: nobody takes "jose" if "José" exists. Null if it
        // has anything but letters, digits, _ and -.
        public static string NickKey(string nick) {
            StringBuilder key = new StringBuilder();
            foreach (char raw in (nick ?? "").Trim().ToLowerInvariant()) {
                char c = Plain(raw);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-') key.Append(c);
                else return null;
            }
            return key.ToString();
        }

        private static char Plain(char c) {
            switch (c) {
                case 'á': case 'à': case 'â': case 'ã': case 'ä': return 'a';
                case 'é': case 'è': case 'ê': case 'ë': return 'e';
                case 'í': case 'ì': case 'î': case 'ï': return 'i';
                case 'ó': case 'ò': case 'ô': case 'õ': case 'ö': return 'o';
                case 'ú': case 'ù': case 'û': case 'ü': return 'u';
                case 'ç': return 'c';
                case 'ñ': return 'n';
                default: return c;
            }
        }

        // why this nickname can't be used, or null
        public static string NickProblem(string nick) {
            string key = NickKey(nick);
            if (key == null) return "O apelido só pode ter letras, números, _ e -";
            if (key.Length < MinNickLength) return "O apelido precisa de pelo menos " + MinNickLength + " letras";
            if (key.Length > MaxNickLength) return "O apelido pode ter até " + MaxNickLength + " letras";
            return null;
        }

        // ---- claiming one ----

        // done(error): null when the nickname is free or already this account's
        public IEnumerator CheckFree(string nick, Action<string> done) {
            string key = NickKey(nick);
            string owner = null;
            bool ok = false;
            yield return _db.Get("apelidos/" + key, (success, body) => {
                ok = success;
                owner = FsDoc.Parse(body)?.Text("uid");
            });
            if (!ok) done?.Invoke("Sem conexão com o servidor");
            else if (!string.IsNullOrEmpty(owner) && owner != _auth.UserId) done?.Invoke("Esse apelido já tem dono");
            else done?.Invoke(null);
        }

        // who owns apelidos/{key} (null when nobody). done(ok, uid)
        private IEnumerator OwnerOf(string key, Action<bool, string> done) {
            bool ok = false;
            string owner = null;
            yield return _db.Get("apelidos/" + key, (success, body) => {
                ok = success;
                owner = FsDoc.Parse(body)?.Text("uid");
            });
            done?.Invoke(ok, string.IsNullOrEmpty(owner) ? null : owner);
        }

        // The nickname, the friend code (the first time) and the public profile in one commit: the
        // rules only take a new nickname together with the profile that shows it, and a profile
        // only changes nickname when the same commit frees the old one, so an account holds one
        // nickname and one code (2026-11-04). done(error)
        private IEnumerator Publish(string nick, Action<string> done) {
            nick = nick.Trim();
            string key = NickKey(nick);
            string oldKey = HasNick ? NickKey(Nick) : null;
            bool oldIsMine = false;
            if (!string.IsNullOrEmpty(oldKey) && oldKey != key) {
                bool ok = false;
                string owner = null;
                yield return OwnerOf(oldKey, (o, u) => { ok = o; owner = u; });
                if (!ok) {
                    done?.Invoke("Sem conexão com o servidor");
                    yield break;
                }
                oldIsMine = owner == _auth.UserId;
            }
            for (int attempt = 0; attempt < 5; attempt++) {
                bool ok = false;
                string owner = null;
                yield return OwnerOf(key, (o, u) => { ok = o; owner = u; });
                if (!ok) {
                    done?.Invoke("Sem conexão com o servidor");
                    yield break;
                }
                if (owner != null && owner != _auth.UserId) {
                    done?.Invoke("Esse apelido já tem dono");
                    yield break;
                }
                List<string> writes = new List<string>();
                if (owner == null) {
                    writes.Add(_db.Write("apelidos/" + key, new FirestoreFields()
                        .Set("uid", _auth.UserId).Set("apelido", nick).SetServerTime("criadoEm"), true));
                }
                string code = Code;
                if (string.IsNullOrEmpty(code)) {
                    code = NewCode();
                    writes.Add(_db.Write("codigos/" + code, new FirestoreFields()
                        .Set("uid", _auth.UserId).SetServerTime("criadoEm"), true));
                }
                writes.Add(_db.Write("perfis/" + _auth.UserId, ProfileFields(nick, code)));
                if (oldIsMine) writes.Add(_db.DeleteWrite("apelidos/" + oldKey));

                yield return WaitForTurn();
                long result = 0;
                yield return _db.Commit(writes, c => result = c);
                if (result == 200) {
                    Remember(nick, code);
                    done?.Invoke(null);
                    yield break;
                }
                if (result == 0) {
                    done?.Invoke("Sem conexão com o servidor");
                    yield break;
                }
                // somebody took the nickname or the new code first: the next try looks again. With
                // neither new, nothing raced it: the rules refused the profile itself
                if (owner != null && !string.IsNullOrEmpty(Code)) break;
            }
            done?.Invoke("Não deu para salvar o apelido agora");
        }

        private static string NewCode() {
            StringBuilder code = new StringBuilder("FRENY-");
            for (int i = 0; i < 4; i++) code.Append(CodeLetters[UnityEngine.Random.Range(0, CodeLetters.Length)]);
            return code.ToString();
        }

        // the first nickname of an account (just created, or older than the nicknames): the friend
        // code and the public profile come with it. done(error)
        public IEnumerator Claim(string nick, Action<string> done) {
            yield return Publish(nick, done);
        }

        // the new nickname on the profile, the old one freed, all at once. done(error)
        public IEnumerator Rename(string nick, Action<string> done) {
            yield return Publish(nick, done);
        }

        // ---- the login ----

        // an account's nickname and code from its profile. An account older than the nicknames takes
        // the name it already has, when that one is free; otherwise the Conta page asks for one.
        public IEnumerator AfterLogin(string currentName, Action<string> nickFound) {
            if (_auth.IsAnonymous) yield break;
            bool ok = false;
            FsDoc doc = null;
            yield return _db.Get("perfis/" + _auth.UserId, (success, body) => {
                ok = success;
                doc = FsDoc.Parse(body);
            });
            if (!ok) yield break;
            if (doc != null && !string.IsNullOrEmpty(doc.Text("apelido"))) {
                Remember(doc.Text("apelido"), doc.Text("codigo"));
                nickFound?.Invoke(doc.Text("apelido"));
                yield return Push(false);   // "online há..."
                yield break;
            }
            if (NickProblem(currentName) == null) {
                string error = null;
                yield return Claim(currentName, e => error = e);
                if (error == null) nickFound?.Invoke(currentName);
            }
        }

        // ---- the public profile ----

        // right after a nickname change it waits for the rules' 10 s; the end of a run just skips it
        // (the next run sends it)
        public IEnumerator Push(bool mustGo) {
            if (!_auth.IsSignedIn || _auth.IsAnonymous || !HasNick) yield break;
            if (!mustGo && Time.realtimeSinceStartup < _lastPush + PushEvery) yield break;
            yield return WaitForTurn();
            yield return _db.Patch("perfis/" + _auth.UserId, ProfileFields(Nick, Code ?? ""), null);
        }

        // firestore.rules takes one profile write every 10 s
        private IEnumerator WaitForTurn() {
            float wait = _lastPush + PushEvery - Time.realtimeSinceStartup;
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            _lastPush = Time.realtimeSinceStartup;
        }

        // the whole public profile, as firestore.rules checks it (validPerfil)
        private FirestoreFields ProfileFields(string nick, string code) {
            FirestoreFields fields = new FirestoreFields()
                .Set("apelido", nick)
                .Set("chave", NickKey(nick))
                .Set("codigo", code ?? "")
                .Set("helmet", FrenyLook.WornId(LookSlot.Helmet))
                .Set("cape", FrenyLook.WornId(LookSlot.Cape))
                .Set("color", FrenyLook.WornId(LookSlot.Color))
                .Set("suit", FrenyLook.WornId(LookSlot.Suit))
                .Set("rebirths", (long)Mathf.Clamp(Rebirth.Count, 0, 999))
                .SetServerTime("lastSeen");
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                OnlineServices.SignedBest(level, out long best, out string sig);
                fields.Set("best" + level, best);
                fields.Set("sig" + level, sig ?? "");
            }
            foreach (string key in SummaryKeys) {
                fields.Set(key, PlayerStats.Get(key));
            }
            return fields;
        }

        // the stats the site and the friend page show; firestore.rules lists them too
        public static readonly string[] SummaryKeys = {
            PlayerStats.RunsManual, PlayerStats.RunsAuto, PlayerStats.SecondsManual, PlayerStats.SecondsAuto,
            PlayerStats.LongestRun, PlayerStats.Kills, PlayerStats.Deaths, PlayerStats.BestCombo,
            PlayerStats.KillKey("cyclops"), PlayerStats.KillKey("supreme"), PlayerStats.Nights, PlayerStats.BloodMoons,
            PlayerStats.Chests, PlayerStats.CoinsEarned,
        };

        // this account as a friend's ranking and side-by-side page see it, without reading it back
        public PerfilInfo Own() {
            PerfilInfo perfil = new PerfilInfo {
                uid = _auth.UserId,
                apelido = HasNick ? Nick : "Você",
                codigo = Code,
                helmet = FrenyLook.WornId(LookSlot.Helmet),
                cape = FrenyLook.WornId(LookSlot.Cape),
                color = FrenyLook.WornId(LookSlot.Color),
                suit = FrenyLook.WornId(LookSlot.Suit),
                rebirths = Rebirth.Count,
                lastSeen = DateTime.UtcNow
            };
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                OnlineServices.SignedBest(level, out long best, out string _);
                perfil.numbers["best" + level] = best;
            }
            foreach (string key in SummaryKeys) perfil.numbers[key] = PlayerStats.Get(key);
            return perfil;
        }

        public IEnumerator Read(string uid, Action<PerfilInfo> done) {
            yield return _db.Get("perfis/" + uid, (ok, body) => done?.Invoke(ok ? PerfilInfo.From(FsDoc.Parse(body)) : null));
        }

        // several at once, 30 at a time ("IN" takes up to 30). done(null) if a read failed
        public IEnumerator ReadMany(List<string> uids, Action<List<PerfilInfo>> done) {
            List<PerfilInfo> found = new List<PerfilInfo>();
            bool failed = false;
            for (int start = 0; start < uids.Count; start += 30) {
                StringBuilder values = new StringBuilder();
                for (int i = start; i < Mathf.Min(uids.Count, start + 30); i++) {
                    if (values.Length > 0) values.Append(',');
                    values.Append("{\"referenceValue\":\"").Append(_db.DocRoot).Append("/perfis/").Append(uids[i]).Append("\"}");
                }
                string query = "{\"from\":[{\"collectionId\":\"perfis\"}]," +
                               "\"where\":{\"fieldFilter\":{\"field\":{\"fieldPath\":\"__name__\"},\"op\":\"IN\"," +
                               "\"value\":{\"arrayValue\":{\"values\":[" + values + "]}}}}}";
                yield return _db.RunQuery(query, (ok, body) => {
                    if (!ok) {
                        failed = true;
                        return;
                    }
                    foreach (FsDoc doc in FsDoc.ParseQuery(body)) found.Add(PerfilInfo.From(doc));
                });
            }
            done?.Invoke(failed ? null : found);
        }

        // ---- finding someone ----

        // "FRENY-4F7K", "4f7k" or a nickname -> the account's user id. done(uid, error)
        public IEnumerator Find(string text, Action<string, string> done) {
            text = (text ?? "").Trim();
            string upper = text.ToUpperInvariant();
            bool code = upper.StartsWith("FRENY-", StringComparison.Ordinal);
            string path = code ? "codigos/" + upper : null;
            if (path == null) {
                string problem = NickProblem(text);
                if (problem != null) {
                    done?.Invoke(null, "Digite um código (FRENY-XXXX) ou um apelido");
                    yield break;
                }
                path = "apelidos/" + NickKey(text);
            }
            bool ok = false;
            string uid = null;
            yield return _db.Get(path, (success, body) => {
                ok = success;
                uid = FsDoc.Parse(body)?.Text("uid");
            });
            if (!ok) done?.Invoke(null, "Sem conexão com o servidor");
            else if (string.IsNullOrEmpty(uid)) done?.Invoke(null, code ? "Nenhum jogador com esse código" : "Nenhum jogador com esse apelido");
            else done?.Invoke(uid, null);
        }
    }
}
