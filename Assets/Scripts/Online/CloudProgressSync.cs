using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Online {
    // Keeps the three save slots (SaveSystem) in the cloud, in "progress/{userId}", so progress
    // follows the account to another PC, the phone or the browser. When the player signs in,
    // each slot keeps the newer copy (a slot with progress always beats an empty one); after
    // that, saves are uploaded at most every UploadDelay seconds. Lives on the GameMenus object.
    // The local copy is judged by its stamp from when it last matched the cloud (SaveSystem.SyncedTicks):
    // its live stamp is "now" after any scene load, which made an old copy on this PC beat newer
    // progress in the cloud and then overwrite it there. When the cloud wins for the slot being
    // played, it is put into play at the next scene load (the next run), before the Player
    // reads his coins, and nothing is uploaded until then.
    public class CloudProgressSync : MonoBehaviour {
        private const string Collection = "progress";
        private const float UploadDelay = 20f;
        private const float RetryDelay = 300f;

        // a slot came down from the cloud (the main menu redraws its summaries)
        public static event Action Pulled;

        private string _syncedUser;

        // the account whose slots already came down from the cloud (null until then), and whether a
        // pull or a slot waiting for the next scene is still under way: GiftInbox waits for both, so
        // a present goes into the slot that stays
        public string SyncedUser => _syncedUser;
        public bool Settling => _busy || _pendingActive != null;

        // the save changed from outside a run (a present): the main menu redraws its summaries
        public static void NotifyChanged() {
            Pulled?.Invoke();
        }
        private bool _busy;
        private bool _dirty;
        private float _uploadAt;
        private float _pullAt;
        private string _pendingUser;
        private SaveData _pendingActive;

        private void OnEnable() {
            SaveSystem.Saved += OnSaved;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable() {
            SaveSystem.Saved -= OnSaved;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // after GameMenus' own save on load, before the scene's Start (the Player reads the coins there)
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (_pendingActive == null || mode != LoadSceneMode.Single) {
                return;
            }
            SaveSystem.Replace(SaveSystem.ActiveSlot, _pendingActive, true);
            SaveSystem.MarkSynced(SaveSystem.ActiveSlot);
            _pendingActive = null;
            _syncedUser = _pendingUser;
            _pendingUser = null;
            Debug.Log("[Online] Active save slot updated from the cloud");
            Pulled?.Invoke();
        }

        private void OnSaved(int slot) {
            if (!_dirty) {
                _dirty = true;
                _uploadAt = Mathf.Max(_uploadAt, Time.unscaledTime + UploadDelay);
            }
        }

        private void Update() {
            OnlineServices online = OnlineServices.Instance;
            if (_busy || _pendingActive != null || online == null || online.State != OnlineServices.Status.Online || online.Db == null) {
                return;
            }
            string user = online.Auth.UserId;
            if (string.IsNullOrEmpty(user)) {
                return;
            }
            if (user != _syncedUser) {
                if (Time.unscaledTime >= _pullAt) {
                    StartCoroutine(Pull(online, user));
                }
            } else if (_dirty && Time.unscaledTime >= _uploadAt) {
                StartCoroutine(Push(online, user));
            }
        }

        private IEnumerator Pull(OnlineServices online, string user) {
            _busy = true;
            bool ok = false;
            string body = null;
            yield return online.Db.Get(Collection + "/" + user, (success, json) => {
                ok = success;
                body = json;
            });
            _busy = false;
            if (!ok) {
                // e.g. the Firestore rules for "progress" are not published yet. Nothing is
                // uploaded before a pull worked, so an old local copy can't overwrite the cloud.
                _pullAt = Time.unscaledTime + RetryDelay;
                yield break;
            }
            List<string> cloud = ReadSlots(body);
            bool pulled = false;
            bool localAhead = false;
            // while a stage runs, Player holds coins in memory: the live copy is only replaced in menus
            bool canApplyLive = !GameMenus.IsGameplayScene(SceneManager.GetActiveScene().name);
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++) {
                SaveData local = SaveSystem.Load(slot);
                SaveData remote = slot < cloud.Count && !string.IsNullOrEmpty(cloud[slot])
                    ? JsonUtility.FromJson<SaveData>(cloud[slot]) : null;
                long localTicks = SaveSystem.SyncedTicks(slot);
                if (Newer(remote, local, localTicks)) {
                    if (slot == SaveSystem.ActiveSlot && !canApplyLive) {
                        _pendingActive = remote;
                        continue;
                    }
                    SaveSystem.Replace(slot, remote, canApplyLive);
                    SaveSystem.MarkSynced(slot);
                    pulled = true;
                } else if (local != null && local.HasProgress) {
                    localAhead = true;
                }
            }
            if (_pendingActive != null) {
                _pendingUser = user;
            } else {
                _syncedUser = user;
            }
            if (localAhead) {
                _dirty = true;
                _uploadAt = Time.unscaledTime;
            }
            if (pulled) {
                Debug.Log("[Online] Save slots updated from the cloud");
                Pulled?.Invoke();
            }
        }

        private IEnumerator Push(OnlineServices online, string user) {
            _busy = true;
            _dirty = false;
            SlotsDocument bundle = new SlotsDocument();
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++) {
                bundle.slots.Add(SaveSystem.LoadJson(slot));
            }
            FirestoreFields fields = new FirestoreFields()
                .Set("slots", JsonUtility.ToJson(bundle))
                .SetTimestamp("updatedAt", DateTime.UtcNow);
            bool ok = false;
            yield return online.Db.Patch(Collection + "/" + user, fields, success => ok = success);
            if (!ok) {
                _dirty = true;
                _uploadAt = Time.unscaledTime + RetryDelay;
            } else {
                for (int slot = 0; slot < SaveSystem.SlotCount; slot++) {
                    if (bundle.slots[slot] == SaveSystem.LoadJson(slot)) {
                        SaveSystem.MarkSynced(slot);   // unless it was saved again meanwhile
                    }
                }
            }
            _busy = false;
        }

        // the cloud copy wins when it has progress and the local one has none, or the cloud's
        // was saved after the local one as it was when the game started (localTicks)
        private static bool Newer(SaveData remote, SaveData local, long localTicks) {
            if (remote == null || !remote.HasProgress) {
                return false;
            }
            if (local == null || !local.HasProgress) {
                return true;
            }
            return remote.savedAtTicks > localTicks;
        }

        private static List<string> ReadSlots(string body) {
            if (string.IsNullOrEmpty(body)) {
                return new List<string>();
            }
            ProgressDocument doc = JsonUtility.FromJson<ProgressDocument>(body);
            string json = doc?.fields?.slots?.stringValue;
            if (string.IsNullOrEmpty(json)) {
                return new List<string>();
            }
            return JsonUtility.FromJson<SlotsDocument>(json)?.slots ?? new List<string>();
        }

        [Serializable] private class SlotsDocument { public List<string> slots = new List<string>(); }
        [Serializable] private class ProgressDocument { public ProgressFields fields; }
        [Serializable] private class ProgressFields { public FsString slots; }
    }
}
