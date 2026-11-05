using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Online {
    // Presents from the team (José gives them to alpha testers in the site's admin, Presentes tab):
    // coins and looks, one document each in presentes/{uid}/itens, which only the server writes
    // (freny-online api/admin/presentes.js). While the player is signed in to an account and in a
    // menu, the game reads them, adds them to the save slot being played, deletes them and shows
    // a notice. Lives on the GameMenus object, next to CloudProgressSync, and waits for its pull,
    // so a present never lands in a slot the cloud copy is about to replace.
    // The coins go straight into the save, not through FrenyProgress.Coins: they are not coins
    // earned in play, so the Perfil's counters (and its pace in the Firestore rules) leave them out.
    public class GiftInbox : MonoBehaviour {
        private const string Collection = "presentes";
        private const string AppliedKey = "freny.gifts.applied";   // ids already added on this PC
        private const int AppliedKept = 40;
        private const float CheckEvery = 300f;
        private const float RetryDelay = 120f;
        private const float NoticeSeconds = 7f;

        private CloudProgressSync _sync;
        private string _checkedUser;
        private float _checkAt;
        private bool _busy;

        private Canvas _canvas;
        private Text _notice;
        private RectTransform _panel;
        private float _noticeUntil;

        private void Start() {
            _sync = GetComponent<CloudProgressSync>();
        }

        private void Update() {
            if (_notice != null) {
                _canvas.gameObject.SetActive(Time.unscaledTime < _noticeUntil);
            }
            OnlineServices online = OnlineServices.Instance;
            if (_busy || online == null || online.State != OnlineServices.Status.Online || online.Db == null) {
                return;
            }
            FirebaseAuthService auth = online.Auth;
            if (auth == null || !auth.IsSignedIn || auth.IsAnonymous) {
                return;
            }
            string user = auth.UserId;
            if (_sync == null || _sync.SyncedUser != user || _sync.Settling) {
                return;
            }
            // while a stage runs, Player holds the coins in memory; presents wait for a menu
            if (GameMenus.IsGameplayScene(SceneManager.GetActiveScene().name)) {
                return;
            }
            if (user == _checkedUser && Time.unscaledTime < _checkAt) {
                return;
            }
            StartCoroutine(Check(online, user));
        }

        private IEnumerator Check(OnlineServices online, string user) {
            _busy = true;
            bool ok = false;
            string body = null;
            yield return online.Db.Get(Collection + "/" + user + "/itens", (success, json) => {
                ok = success;
                body = json;
            });
            _checkedUser = user;
            _checkAt = Time.unscaledTime + (ok ? CheckEvery : RetryDelay);
            if (!ok || online.Auth.UserId != user) {
                _busy = false;
                yield break;
            }
            List<FsDoc> gifts = Parse(body);
            List<string> applied = Applied();
            int coins = 0;
            List<string> looks = new List<string>();
            string note = "";
            foreach (FsDoc gift in gifts) {
                if (applied.Contains(gift.Id)) {
                    continue;   // added here before; its delete failed
                }
                coins += (int)Math.Max(0, Math.Min(gift.Int("moedas"), 1000000));
                foreach (string look in gift.Text("visuais").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (!looks.Contains(look)) looks.Add(look);
                }
                if (gift.Text("mensagem").Length > 0) note = gift.Text("mensagem");
                applied.Add(gift.Id);
            }
            List<string> newLooks = Give(coins, looks);
            if (gifts.Count > 0) {
                SetApplied(applied);
            }
            if (coins > 0 || newLooks.Count > 0) {
                SaveSystem.SaveNow();
                CloudProgressSync.NotifyChanged();
                Show(coins, newLooks, note);
                Debug.Log("[Online] Present received: " + coins + " coins, looks " + string.Join(",", newLooks));
            }
            foreach (FsDoc gift in gifts) {
                yield return online.Db.Delete(Collection + "/" + user + "/itens/" + gift.Id, null);
            }
            _busy = false;
        }

        // adds them to the live save keys (SaveSystem.SaveNow then writes the slot); the looks the
        // player has already, or that this version doesn't know, are left out of the notice
        private static List<string> Give(int coins, List<string> looks) {
            if (coins > 0) {
                PlayerPrefs.SetInt(FrenyProgress.CoinsKey, PlayerPrefs.GetInt(FrenyProgress.CoinsKey, 0) + coins);
            }
            List<string> owned = FrenyLook.BoughtIds;
            List<string> added = new List<string>();
            foreach (string id in looks) {
                FrenyLook.Item item = FrenyLook.Find(id);
                if (item == null || owned.Contains(id)) continue;
                owned.Add(id);
                added.Add(item.name);
            }
            if (added.Count > 0) {
                FrenyLook.SetBoughtIds(owned);
            }
            return added;
        }

        // a collection's GET: {"documents": [{"name": ..., "fields": ...}, ...]}, or {} when empty
        private static List<FsDoc> Parse(string json) {
            List<FsDoc> docs = new List<FsDoc>();
            if (string.IsNullOrEmpty(json)) return docs;
            string[] parts = json.Split(new[] { "\"name\"" }, StringSplitOptions.None);
            for (int i = 1; i < parts.Length; i++) {
                FsDoc doc = FsDoc.Parse("{\"name\"" + parts[i]);
                if (doc != null && !string.IsNullOrEmpty(doc.Id)) docs.Add(doc);
            }
            return docs;
        }

        private static List<string> Applied() {
            return new List<string>(PlayerPrefs.GetString(AppliedKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void SetApplied(List<string> ids) {
            if (ids.Count > AppliedKept) ids.RemoveRange(0, ids.Count - AppliedKept);
            PlayerPrefs.SetString(AppliedKey, string.Join(",", ids));
            PlayerPrefs.Save();
        }

        // ---- the notice: a panel at the bottom left of the screen for a few seconds ----

        private void Show(int coins, List<string> looks, string note) {
            List<string> what = new List<string>();
            if (coins > 0) what.Add("+" + coins.ToString("N0", new System.Globalization.CultureInfo("pt-BR")) + " moedas");
            what.AddRange(looks);
            string text = "Presente da equipe: " + string.Join(", ", what);
            if (note.Length > 0) text += "\n\"" + note + "\"";
            if (_canvas == null) Build();
            _notice.text = text;
            // grows with the text (a long message, many looks)
            _panel.sizeDelta = new Vector2(560f, Mathf.Max(76f, _notice.preferredHeight + 24f));
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            _canvas.gameObject.SetActive(true);
        }

        private void Build() {
            _canvas = MenuKit.CreateCanvas("Gift Notice", 26000, transform);
            Image panel = MenuKit.MakeImage("Panel", _canvas.transform, MenuKit.PanelColor);
            panel.raycastTarget = false;
            // bottom left: clear of the title (top) and of the main menu's buttons (right side)
            _panel = panel.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = Vector2.zero;
            _panel.anchoredPosition = new Vector2(16f, 56f);
            _panel.sizeDelta = new Vector2(560f, 76f);
            Outline border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = MenuKit.TitleColor;
            border.effectDistance = new Vector2(2f, -2f);
            _notice = MenuKit.MakeText(panel.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
            _notice.raycastTarget = false;
            MenuKit.Stretch(_notice.rectTransform);
            _notice.rectTransform.offsetMin = new Vector2(12f, 6f);
            _notice.rectTransform.offsetMax = new Vector2(-12f, -6f);
            _notice.horizontalOverflow = HorizontalWrapMode.Wrap;
            _notice.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
