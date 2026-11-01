using System.Collections;
using UnityEngine;

namespace Online {
    // The Conquistas online: achievements/{uid} holds one field per achievement the ranking API
    // signed (its key -> sha256("<key>|uid|ach|<id>")), plus "played" once the player finished a
    // manual run the API accepted. Anyone may count them (the site's Conquistas tab shows the share
    // of players with each); firestore.rules only takes a field with the right signature.
    public class AchievementService {
        private const string Collection = "achievements";

        private readonly FirestoreClient _db;
        private readonly ScoreApi _api;
        private readonly FirebaseAuthService _auth;

        public AchievementService(FirestoreClient db, ScoreApi api, FirebaseAuthService auth) {
            _db = db;
            _api = api;
            _auth = auth;
        }

        // after a login: the ones this account already has online come back on this device too
        public IEnumerator Load() {
            if (!_auth.IsSignedIn) yield break;
            yield return _db.Get(Collection + "/" + _auth.UserId, (ok, body) => {
                if (!ok || string.IsNullOrEmpty(body)) return;
                foreach (Achievements.Info info in Achievements.All) {
                    if (body.Contains("\"" + info.key + "\":")) Achievements.MarkConfirmed(info.key);
                }
                if (body.Contains("\"played\":")) Achievements.MarkConfirmed("played");
                PlayerTitles.ReadOwn(body);
                PlayerPrefs.Save();
            });
        }

        // the end of a manual run: the API checks the log and signs what it backs up
        public IEnumerator Claim(RunLog log, string token, string board, long score) {
            ScoreApi.Earned[] earned = null;
            string refused = null;
            yield return _api.ClaimAchievements(log.ToJson(token, board, score), (e, error) => {
                earned = e;
                refused = error;
            });
            if (earned == null) {
                Debug.LogWarning("[Online] Achievements not confirmed: " + refused);
                yield break;
            }
            if (earned.Length == 0) yield break;
            FirestoreFields fields = new FirestoreFields();
            foreach (ScoreApi.Earned one in earned) {
                fields.Set(one.id, one.sig);
            }
            fields.SetServerTime("updatedAt");
            bool written = false;
            yield return _db.Patch(Collection + "/" + _auth.UserId, fields, ok => written = ok);
            if (!written) yield break;
            foreach (ScoreApi.Earned one in earned) {
                Achievements.MarkConfirmed(one.id);
            }
            PlayerPrefs.Save();
        }
    }
}
