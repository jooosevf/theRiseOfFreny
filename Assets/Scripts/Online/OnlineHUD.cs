using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Online {
    // The online bits of the arena, in the menus' style: Tab opens the ranking over the paused
    // game (GameMenus.ShowRanking, pages from OnlineScreens), a ranked run opens it on its own
    // when it ends, and the run's result is kept for the arena's end-of-run banner. The main
    // menu has its own Ranking and Conta online buttons.
    public class OnlineHUD : MonoBehaviour {
        private const KeyCode ToggleKey = KeyCode.Tab;
        private const float AutoOpenDelay = 1.5f;

        public static OnlineHUD Instance { get; private set; }

        private OnlineServices _online;
        private float _openAt = -1f;
        private Text _hint;

        // e.g. "Novo recorde no Difícil: 1234!", until the next scene loads
        public string LastResult { get; private set; }

        private void Awake() {
            Instance = this;
        }

        private void Start() {
            _online = GetComponent<OnlineServices>();
            _online.RunReported += OnRunReported;
            _online.RankingRefused += OnRankingRefused;
            _online.RecordChanged += OnRecordChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Canvas canvas = MenuKit.CreateCanvas("Online Hint", 50, transform);
            _hint = MenuKit.MakeText(canvas.transform, "[Tab] Ranking", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.LowerRight);
            _hint.raycastTarget = false;
            _hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = _hint.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-12f, 24f);   // over the version label
            rect.sizeDelta = new Vector2(240f, 20f);
        }

        private void OnDestroy() {
            if (_online != null) {
                _online.RunReported -= OnRunReported;
                _online.RankingRefused -= OnRankingRefused;
                _online.RecordChanged -= OnRecordChanged;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            LastResult = null;
            _openAt = -1f;
        }

        private static bool InArena => SceneManager.GetActiveScene().name == GameMenus.ArenaScene;

        private void Update() {
            bool arena = InArena && _online.State != OnlineServices.Status.Disabled;
            _hint.enabled = arena && !GameMenus.Paused;
            if (!arena || GameMenus.Instance == null) {
                return;
            }
            if (Input.GetKeyDown(ToggleKey) && !GameMenus.Paused) {
                GameMenus.Instance.ShowRanking(false);
            }
            if (_openAt > 0 && Time.unscaledTime >= _openAt) {
                _openAt = -1f;
                if (!GameMenus.Paused) {
                    GameMenus.Instance.ShowRanking(true);
                }
            }
        }

        private void OnRunReported(long score, bool newBest, string unranked) {
            if (_online.Record != OnlineServices.RecordStatus.None) {
                // the banner's NOVO RECORDE box takes it from here; the board opens once it is saved
                Sfx.Play("new_record");
                LastResult = null;
                return;
            }
            if (unranked != null) {
                // automatic runs restart on their own; the board stays closed
                LastResult = score + " pontos · " + unranked;
                return;
            }
            ArenaLevel level = ArenaDifficulty.Current;
            LastResult = score + " pontos (recorde no " + ArenaDifficulty.Label(level) + ": " + _online.BestFor(level) + ")";
            _openAt = Time.unscaledTime + AutoOpenDelay;
        }

        private void OnRecordChanged() {
            if (_online.Record == OnlineServices.RecordStatus.Saved && InArena) _openAt = Time.unscaledTime + AutoOpenDelay;
        }

        private void OnRankingRefused(long score, string reason) {
            if (_online.Record != OnlineServices.RecordStatus.None) return;   // the NOVO RECORDE box says it
            LastResult = score + " pontos · o servidor não aceitou no ranking (" + reason + ")";
        }
    }
}
