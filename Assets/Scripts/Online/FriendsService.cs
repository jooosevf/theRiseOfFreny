using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Online {
    // The friends (José, 2026-11-02), for accounts:
    //   pedidos/{para}_{de}  a message from one player to another: tipo "pedido" (a request, waits for
    //                        accept or decline), "aceito" (the other side accepted: this side adds them
    //                        too) or "removido" (the other side removed this player)
    //   amigos/{uid}         {lista:[uids]}, up to 50. firestore.rules only lets a uid in when it sent
    //                        this player a request or already lists this player
    // Nothing polls: the messages are read once at login and when the Amigos page opens.
    public class FriendsService {
        public const int MaxFriends = 50;
        private const float FreshSeconds = 60f;   // a list shown again within this is not read again

        public class Pedido {
            public string de;
            public string apelido;
            public string tipo;
        }

        private readonly FirestoreClient _db;
        private readonly FirebaseAuthService _auth;
        private readonly ProfileService _profiles;
        private readonly List<string> _lista = new List<string>();
        private readonly List<PerfilInfo> _perfis = new List<PerfilInfo>();
        private bool _listRead;
        private float _perfisAt = -1000f;

        // requests waiting for an answer, as of the last Sync (the Perfil button's count)
        public readonly List<Pedido> Incoming = new List<Pedido>();

        public FriendsService(FirestoreClient db, FirebaseAuthService auth, ProfileService profiles) {
            _db = db;
            _auth = auth;
            _profiles = profiles;
        }

        public IReadOnlyList<string> Lista => _lista;
        public IReadOnlyList<PerfilInfo> Perfis => _perfis;

        private bool CanUse => _auth.IsSignedIn && !_auth.IsAnonymous;

        public void Forget() {
            _lista.Clear();
            _perfis.Clear();
            Incoming.Clear();
            _listRead = false;
            _perfisAt = -1000f;
        }

        // ---- reading ----

        private IEnumerator ReadList() {
            if (_listRead || !CanUse) yield break;
            yield return _db.Get("amigos/" + _auth.UserId, (ok, body) => {
                if (!ok) return;
                _listRead = true;
                _lista.Clear();
                FsDoc doc = FsDoc.Parse(body);
                if (doc != null && doc.Lists.TryGetValue("lista", out List<string> uids)) _lista.AddRange(uids);
            });
        }

        // what the others sent: requests stay for the player to answer, the rest is applied here
        public IEnumerator Sync() {
            if (!CanUse) yield break;
            string query = "{\"from\":[{\"collectionId\":\"pedidos\"}]," +
                           "\"where\":{\"fieldFilter\":{\"field\":{\"fieldPath\":\"para\"},\"op\":\"EQUAL\"," +
                           "\"value\":{\"stringValue\":\"" + _auth.UserId + "\"}}},\"limit\":100}";
            List<FsDoc> docs = null;
            yield return _db.RunQuery(query, (ok, body) => {
                if (ok) docs = FsDoc.ParseQuery(body);
            });
            if (docs == null) yield break;
            Incoming.Clear();
            foreach (FsDoc doc in docs) {
                Pedido pedido = new Pedido { de = doc.Text("de"), apelido = doc.Text("apelido"), tipo = doc.Text("tipo") };
                if (pedido.tipo == "aceito") {
                    yield return ReadList();
                    bool added = _lista.Contains(pedido.de);
                    if (!added && _lista.Count < MaxFriends) yield return WriteList(pedido.de, null, ok => added = ok);
                    if (added) yield return DeletePedido(_auth.UserId, pedido.de);
                } else if (pedido.tipo == "removido") {
                    yield return ReadList();
                    bool gone = !_lista.Contains(pedido.de);
                    if (!gone) yield return WriteList(null, pedido.de, ok => gone = ok);
                    if (gone) yield return DeletePedido(_auth.UserId, pedido.de);
                } else {
                    Incoming.Add(pedido);
                }
            }
        }

        // the friends' public profiles, sorted by when they last played. Only when the page or the
        // tab opens; a second look within a minute uses what it has.
        public IEnumerator Refresh() {
            if (!CanUse) yield break;
            yield return ReadList();
            if (Time.realtimeSinceStartup - _perfisAt < FreshSeconds) yield break;
            if (_lista.Count == 0) {
                _perfis.Clear();
                _perfisAt = Time.realtimeSinceStartup;
                yield break;
            }
            List<PerfilInfo> found = null;
            yield return _profiles.ReadMany(new List<string>(_lista), perfis => found = perfis);
            if (found == null) yield break;
            found.Sort((a, b) => (b.lastSeen ?? DateTime.MinValue).CompareTo(a.lastSeen ?? DateTime.MinValue));
            _perfis.Clear();
            _perfis.AddRange(found);
            _perfisAt = Time.realtimeSinceStartup;
        }

        private void Stale() {
            _perfisAt = -1000f;
        }

        // ---- writing ----

        private IEnumerator WriteList(string add, string remove, Action<bool> done) {
            List<string> next = new List<string>(_lista);
            if (add != null) next.Add(add);
            if (remove != null) next.Remove(remove);
            FirestoreFields fields = new FirestoreFields()
                .Set("lista", next)
                .Set("novo", add ?? "")
                .SetServerTime("updatedAt");
            bool ok = false;
            yield return _db.Patch("amigos/" + _auth.UserId, fields, written => ok = written);
            if (ok) {
                _lista.Clear();
                _lista.AddRange(next);
                Stale();
            }
            done?.Invoke(ok);
        }

        private IEnumerator SendPedido(string to, string tipo, Action<long> done) {
            FirestoreFields fields = new FirestoreFields()
                .Set("de", _auth.UserId)
                .Set("para", to)
                .Set("tipo", tipo)
                .Set("apelido", _profiles.Nick ?? "")
                .SetServerTime("criadoEm");
            yield return _db.Create("pedidos/" + to + "_" + _auth.UserId, fields, done);
        }

        private IEnumerator DeletePedido(string to, string from) {
            yield return _db.Delete("pedidos/" + to + "_" + from, null);
        }

        // by code or nickname. done(error, or null when it went)
        public IEnumerator Add(string text, Action<string> done) {
            if (!CanUse) {
                done?.Invoke("Crie uma conta para ter amigos");
                yield break;
            }
            string uid = null;
            string error = null;
            yield return _profiles.Find(text, (found, e) => {
                uid = found;
                error = e;
            });
            if (error != null) {
                done?.Invoke(error);
                yield break;
            }
            if (uid == _auth.UserId) {
                done?.Invoke("Esse é você!");
                yield break;
            }
            yield return ReadList();
            if (_lista.Contains(uid)) {
                done?.Invoke("Vocês já são amigos");
                yield break;
            }
            if (_lista.Count >= MaxFriends) {
                done?.Invoke("Sua lista está cheia (" + MaxFriends + " amigos)");
                yield break;
            }
            // that player already asked: this is the answer
            Pedido theirs = Incoming.Find(p => p.de == uid);
            if (theirs != null) {
                yield return Accept(theirs, done);
                yield break;
            }
            long code = 0;
            yield return SendPedido(uid, "pedido", c => code = c);
            done?.Invoke(code == 200 ? null : code == 0 ? "Sem conexão com o servidor" : "O pedido já foi enviado");
        }

        public IEnumerator Accept(Pedido pedido, Action<string> done) {
            yield return ReadList();
            if (_lista.Count >= MaxFriends) {
                done?.Invoke("Sua lista está cheia (" + MaxFriends + " amigos)");
                yield break;
            }
            bool added = _lista.Contains(pedido.de);
            if (!added) yield return WriteList(pedido.de, null, ok => added = ok);
            if (!added) {
                done?.Invoke("Não deu para aceitar agora");
                yield break;
            }
            // tell the other side; a request of this player's to them, if any, is replaced by the answer
            yield return DeletePedido(pedido.de, _auth.UserId);
            yield return SendPedido(pedido.de, "aceito", null);
            yield return DeletePedido(_auth.UserId, pedido.de);
            Incoming.Remove(pedido);
            done?.Invoke(null);
        }

        public IEnumerator Decline(Pedido pedido, Action<string> done) {
            bool ok = false;
            yield return _db.Delete("pedidos/" + _auth.UserId + "_" + pedido.de, written => ok = written);
            if (ok) Incoming.Remove(pedido);
            done?.Invoke(ok ? null : "Sem conexão com o servidor");
        }

        public IEnumerator Remove(string uid, Action<string> done) {
            yield return ReadList();
            bool ok = false;
            yield return WriteList(null, uid, written => ok = written);
            if (!ok) {
                done?.Invoke("Sem conexão com o servidor");
                yield break;
            }
            _perfis.RemoveAll(p => p.uid == uid);
            yield return DeletePedido(uid, _auth.UserId);
            yield return SendPedido(uid, "removido", null);
            done?.Invoke(null);
        }
    }
}
