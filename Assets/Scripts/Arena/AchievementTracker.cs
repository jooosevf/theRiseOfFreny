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

    // ---- the banner: top of the screen, one achievement at a time ----

    private const float BannerSeconds = 4f;
    private const float BannerY = -150f;   // under the score and combo at the top of the HUD
    private readonly Queue<Achievement> waiting = new Queue<Achievement>();
    private Canvas canvas;
    private Image icon;
    private Text title;
    private float shownUntil;

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
            // slides in from above and back up at the end
            float slide = Mathf.Clamp01(Mathf.Min(BannerSeconds - t, t) * 4f);
            ((RectTransform)canvas.transform.GetChild(0)).anchoredPosition = new Vector2(0f, Mathf.Lerp(120f, BannerY, slide));
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
        title.text = "<size=12><color=#FFD84A>CONQUISTA DESBLOQUEADA!</color></size>\n" + Achievements.Of(next).name +
                     "\n<size=10><color=#B399D1>" + Achievements.Of(next).how + "</color></size>";
        shownUntil = Time.unscaledTime + BannerSeconds;
        Sfx.Play("level_up");
    }

    private void Build() {
        if (canvas != null) {
            return;
        }
        canvas = MenuKit.CreateCanvas("Achievement Banner", 25000, transform);
        Image panel = MenuKit.MakeImage("Panel", canvas.transform, MenuKit.PanelColor);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(560f, 96f);
        Outline edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.85f, 0.64f, 0.16f, 1f);
        edge.effectDistance = new Vector2(3f, -3f);
        panel.raycastTarget = false;

        icon = MenuKit.MakeImage("Icon", panel.transform, Color.white);
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(12f, 0f);
        iconRect.sizeDelta = new Vector2(80f, 80f);

        title = MenuKit.MakeText(panel.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleLeft);
        title.supportRichText = true;
        title.raycastTarget = false;
        title.lineSpacing = 1.3f;
        RectTransform textRect = title.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(106f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
    }
}
