using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Online {
    [Serializable]
    public class LeaderboardEntry {
        public string userId;
        public string name;
        public long score;
        public int rebirths;   // Renascer: the star after the name (Rebirth.RankingName)
    }

    // One board per arena difficulty ("leaderboard_hard", "leaderboard_freny", "_medium", "_easy"), with
    // one document per player keyed by user id holding that player's best score there.
    // firestore.rules only lets a score go up, and only with the signature the ranking API (ScoreApi)
    // gives a run it checked. The old mixed "leaderboard" is no longer used.
    public class LeaderboardService {
        public static string Collection(ArenaLevel level) => "leaderboard_" + level.ToString().ToLowerInvariant();

        // the autopilot's own board per difficulty, "auto_hard" etc. (José, 2026-11-01): runs started in
        // Automático go here, never on the manual board above. No daily board for them.
        public static string AutoCollection(ArenaLevel level) => "auto_" + level.ToString().ToLowerInvariant();

        public static string Collection(ArenaLevel level, bool auto) => auto ? AutoCollection(level) : Collection(level);

        // today's board, daily_<level>_<yyyymmdd> by Brasília time (UTC-3), what the site's Diário
        // tab reads. The API says which day a run counts for (ScoreApi.FinishRun); this is the guess
        // for reading it.
        public static string DailyCollection(ArenaLevel level) =>
            "daily_" + level.ToString().ToLowerInvariant() + "_" + DateTime.UtcNow.AddHours(-3).ToString("yyyyMMdd");

        private readonly FirestoreClient _db;
        private readonly FirebaseAuthService _auth;

        public LeaderboardService(FirestoreClient db, FirebaseAuthService auth) {
            _db = db;
            _auth = auth;
        }

        // sig: what ScoreApi.FinishRun returned for this score
        public IEnumerator SubmitBest(ArenaLevel level, string playerName, long score, string sig, Action<bool> done) {
            yield return SubmitBest(Collection(level), playerName, score, sig, done);
        }

        // collection: an all-time or a daily board
        public IEnumerator SubmitBest(string collection, string playerName, long score, string sig, Action<bool> done) {
            FirestoreFields fields = new FirestoreFields()
                .Set("name", playerName)
                .Set("score", score)
                .Set("sig", sig)
                .SetServerTime("updatedAt");
            AddRebirths(fields);
            yield return _db.Patch(collection + "/" + _auth.UserId, fields, done);
        }

        // The player's own score on a board, 0 when they have none. done(null) on a network error.
        public IEnumerator GetOwn(ArenaLevel level, Action<long?> done) {
            yield return GetOwn(Collection(level), done);
        }

        public IEnumerator GetOwn(string collection, Action<long?> done) {
            yield return _db.Get(collection + "/" + _auth.UserId, (ok, body) => {
                if (!ok) {
                    done?.Invoke(null);
                    return;
                }
                LeaderboardDocument doc = string.IsNullOrEmpty(body) ? null : JsonUtility.FromJson<LeaderboardDocument>(body);
                done?.Invoke(doc?.fields?.score?.Value ?? 0);
            });
        }

        // the score with its signature, what the public profile shows. done(false, ...) on a network error
        public IEnumerator GetOwnSigned(ArenaLevel level, Action<bool, long, string> done) {
            yield return _db.Get(Collection(level) + "/" + _auth.UserId, (ok, body) => {
                FsDoc doc = ok ? FsDoc.Parse(body) : null;
                done?.Invoke(ok, doc?.Int("score") ?? 0, doc?.Text("sig"));
            });
        }

        // Renames the player's entry without touching the score.
        public IEnumerator Rename(ArenaLevel level, string playerName, Action<bool> done) {
            yield return Rename(Collection(level), playerName, done);
        }

        public IEnumerator Rename(string collection, string playerName, Action<bool> done) {
            FirestoreFields fields = new FirestoreFields().Set("name", playerName).SetServerTime("updatedAt");
            AddRebirths(fields);   // a new star shows on the boards with the next rename too
            yield return _db.Patch(collection + "/" + _auth.UserId, fields, done);
        }

        // Renascer's count, for the star after the name (firestore.rules: validEntry). Only once
        // there is one, so players who never rebirth write what they always did
        private static void AddRebirths(FirestoreFields fields) {
            if (Rebirth.Count > 0) {
                fields.Set("rebirths", (long)Mathf.Min(Rebirth.Count, 999));
            }
        }

        public IEnumerator GetTop(ArenaLevel level, int limit, Action<List<LeaderboardEntry>> done) {
            yield return GetTop(Collection(level), limit, done);
        }

        public IEnumerator GetTop(string collection, int limit, Action<List<LeaderboardEntry>> done) {
            string query = "{\"from\":[{\"collectionId\":\"" + collection + "\"}]," +
                           "\"orderBy\":[{\"field\":{\"fieldPath\":\"score\"},\"direction\":\"DESCENDING\"}]," +
                           "\"limit\":" + limit + "}";

            yield return _db.RunQuery(query, (ok, body) => {
                List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
                if (ok && !string.IsNullOrEmpty(body)) {
                    // runQuery returns a bare array, which JsonUtility cannot read directly.
                    QueryResponse res = JsonUtility.FromJson<QueryResponse>("{\"items\":" + body + "}");
                    foreach (QueryItem item in res.items) {
                        if (item.document == null || string.IsNullOrEmpty(item.document.name)) continue;
                        entries.Add(new LeaderboardEntry {
                            userId = item.document.name.Substring(item.document.name.LastIndexOf('/') + 1),
                            name = item.document.fields.name.stringValue,
                            score = item.document.fields.score.Value,
                            rebirths = item.document.fields.rebirths != null ? (int)Mathf.Clamp(item.document.fields.rebirths.Value, 0, 999) : 0
                        });
                    }
                }
                done?.Invoke(ok ? entries : null);
            });
        }

        [Serializable] private class QueryResponse { public QueryItem[] items; }
        [Serializable] private class QueryItem { public LeaderboardDocument document; }
        [Serializable] private class LeaderboardDocument {
            public string name;
            public LeaderboardFields fields;
        }
        [Serializable] private class LeaderboardFields {
            public FsString name;
            public FsInteger score;
            public FsInteger rebirths;
        }
    }
}
