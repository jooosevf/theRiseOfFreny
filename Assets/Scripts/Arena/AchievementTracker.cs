using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Watches each Arena run for the Conquistas (Achievements) and shows the "Conquista!" banner when
// one is earned. A run counts until the autopilot drives Freny, the same rule as the ranking.
// Self-bootstrapping, like OnlineServices.
public class AchievementTracker : MonoBehaviour {

    private static AchievementTracker instance;

    private Player player;
    private FrenyInput input;
    private ArenaRunManager run;
    private bool arena;
    private bool assisted;
    private int runKills, runCyclopes, runBackstabs;

    // a manual Arena run is going on (Freny may have just fallen: the own grenade counts)
    public static bool Counts => instance != null && instance.arena && instance.player != null && !instance.assisted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        if (instance != null) return;
        GameObject go = new GameObject("AchievementTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<AchievementTracker>();
    }

    private void Awake() {
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        UIManager.KillScored += OnKillScored;
        Achievements.Unlocked += Show;
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UIManager.KillScored -= OnKillScored;
        Achievements.Unlocked -= Show;
    }

    private void Start() {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        arena = scene.name == GameMenus.ArenaScene;
        player = arena ? FindAnyObjectByType<Player>() : null;
        input = player != null ? player.GetComponent<FrenyInput>() : null;
        run = player != null ? player.GetComponent<ArenaRunManager>() : null;
        assisted = false;
        runKills = runCyclopes = runBackstabs = 0;
        Achievements.EarnedThisRun.Clear();
    }

    private void Update() {
        if (arena && player != null && !player.IsDead) {
            if (input != null && input.IsAutopilotDriving) {
                assisted = true;
            }
            if (run != null && run.RunSeconds >= Achievements.SurviveSeconds) {
                Achievements.Earn(Achievement.Survivor);
            }
            if (run != null && run.RunSeconds >= Achievements.MarathonSeconds) {
                Achievements.Earn(Achievement.Marathon);
            }
        }
        UpdateBanner();
    }

    // every kill that scores, with the combo it was scored at
    private void OnKillScored(Enemy enemy, float combo) {
        if (!Counts || enemy == null) {
            return;
        }
        string name = Online.RunLog.PrefabName(enemy.gameObject.name);
        if (UIManager.Instance != null && UIManager.Instance.Score >= Achievements.MillionScore) {
            Achievements.Earn(Achievement.Million);
        }
        if (++runKills >= Achievements.RunKills) {
            Achievements.Earn(Achievement.Kills500);
        }
        if (name == "Cyclops_Boss") {
            Achievements.Earn(Achievement.Cyclops);
            if (++runCyclopes >= Achievements.CyclopsKills) {
                Achievements.Earn(Achievement.Cyclops3);
            }
            if (enemy.LastHitMark == Enemy.EyeHit) {
                Achievements.Earn(Achievement.Eye, "eye", 1);
            }
        } else if (name == "Sorcerer_Supreme") {
            Achievements.Earn(Achievement.Supreme);
        } else if (name == "Shielded_Enemy" && enemy.LastHitMark == Enemy.BackHit) {
            if (++runBackstabs >= Achievements.BackstabKills) {
                Achievements.Earn(Achievement.Backstab, "back", 1);
            } else {
                Achievements.Moment("back", 1);
            }
        }
        if (Mathf.RoundToInt((combo - 1f) * 10f) >= Achievements.ComboStep) {
            Achievements.Earn(Achievement.Combo50);
        }
    }

    // ---- the banner: small, under the coins at the top right, one achievement at a time ----
    // José, 2026-11-03: the big one in the middle got in the way of the game. Only the icon and the
    // name here; what it was for is on the Conquistas page, whose button says "(nova!)" until opened.

    private const float BannerSeconds = 3.5f;
    private const float BannerHeight = 40f;
    private const float Gap = 18f;                                  // between the coins and the banner
    private static readonly Vector2 Fallback = new Vector2(-28f, -98f);   // no coins on screen
    private readonly Queue<Achievement> waiting = new Queue<Achievement>();
    private Canvas canvas;
    private RectTransform panel;
    private Image icon;
    private Text title;
    private float shownUntil;
    private Vector2 spot;

    private void Show(Achievement a) {
        waiting.Enqueue(a);
    }

    private void UpdateBanner() {
        if (canvas != null && GameMenus.Paused) {
            // not behind the pause menu: it waits and shows its full time after
            canvas.gameObject.SetActive(false);
            if (shownUntil > Time.unscaledTime) shownUntil += Time.unscaledDeltaTime;
            return;
        }
        if (canvas != null && Time.unscaledTime < shownUntil) {
            canvas.gameObject.SetActive(true);
            float t = shownUntil - Time.unscaledTime;
            // slides in from the right edge and back out at the end
            float slide = Mathf.Clamp01(Mathf.Min(BannerSeconds - t, t) * 5f);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(panel.sizeDelta.x + 40f, spot.x, slide), spot.y);
            return;
        }
        if (waiting.Count == 0) {
            if (canvas != null) canvas.gameObject.SetActive(false);
            return;
        }
        Achievement next = waiting.Dequeue();
        Build();
        canvas.gameObject.SetActive(true);
        icon.sprite = Achievements.Icon(next);
        title.text = Achievements.Of(next).name;
        panel.sizeDelta = new Vector2(Mathf.Ceil(title.preferredWidth) + BannerHeight + 22f, BannerHeight);
        spot = UnderCoins();
        shownUntil = Time.unscaledTime + BannerSeconds;
        Sfx.Play("achievement");
    }

    // the top right corner of the banner: right under the HUD's coin and lined up with it, in this
    // canvas' units from the screen's top right corner
    private Vector2 UnderCoins() {
        UIManager ui = UIManager.Instance;
        Text coins = ui != null ? ui.coinCountHUD : null;
        if (coins == null || !coins.isActiveAndEnabled || coins.transform.parent == null) {
            return Fallback;
        }
        Canvas hud = coins.canvas != null ? coins.canvas.rootCanvas : null;
        Camera cam = hud != null && hud.renderMode != RenderMode.ScreenSpaceOverlay ? hud.worldCamera : null;
        // the coin icon beside the number has a rect that fits it, unlike the number's text
        float right = float.MinValue, bottom = float.MaxValue;
        Vector3[] corners = new Vector3[4];
        foreach (Image image in coins.transform.parent.GetComponentsInChildren<Image>()) {
            if (!image.isActiveAndEnabled) continue;
            image.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++) {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                right = Mathf.Max(right, p.x);
                bottom = Mathf.Min(bottom, p.y);
            }
        }
        if (right == float.MinValue) {
            return Fallback;
        }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 at = new Vector2((right - Screen.width) / scale, (bottom - Screen.height) / scale - Gap);
        // something odd (a HUD off the screen): the usual place
        return at.x > 0f || at.x < -400f || at.y > 0f || at.y < -300f ? Fallback : at;
    }

    private void Build() {
        if (canvas != null) {
            return;
        }
        canvas = MenuKit.CreateCanvas("Achievement Banner", 25000, transform);
        Image back = MenuKit.MakeImage("Panel", canvas.transform, new Color(0.08f, 0.05f, 0.11f, 0.88f));
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        Outline edge = back.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.85f, 0.64f, 0.16f, 1f);
        edge.effectDistance = new Vector2(2f, -2f);
        back.raycastTarget = false;

        icon = MenuKit.MakeImage("Icon", panel, Color.white);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        iconRect.sizeDelta = new Vector2(BannerHeight - 8f, BannerHeight - 8f);

        title = MenuKit.MakeText(panel, "", MenuKit.SmallTextSize, AchievementsScreen.Earned, TextAnchor.MiddleLeft);
        title.raycastTarget = false;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform textRect = title.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(BannerHeight + 4f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
    }
}
