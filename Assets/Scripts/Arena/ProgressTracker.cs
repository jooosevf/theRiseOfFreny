using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feeds the Arena's runs to the Estatísticas (AccountStats, every run) and to the Missões do dia
// (DailyMissions, manual runs only: AchievementTracker.Counts, the Conquistas' rule), from the same
// events the Conquistas listen to. Shows the "Missão cumprida!" banner, under the Conquistas' one.
// Self-bootstrapping, like AchievementTracker.
public class ProgressTracker : MonoBehaviour {

    private static ProgressTracker instance;

    private Player player;
    private ArenaRunManager run;
    private bool arena;
    private bool ended;
    private bool healed;          // this run: Freny healed (a potion), for "sem se curar"
    private int comboTenths;      // this run's best combo on screen, in tenths
    private float nextTick;

    private const float TickSeconds = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        if (instance != null) return;
        GameObject go = new GameObject("ProgressTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<ProgressTracker>();
    }

    private void Awake() {
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        UIManager.KillScored += OnKillScored;
        Enemy.MarkedHit += OnMarkedHit;
        Player.Vital += OnVital;
        DailyMissions.Completed += Show;
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UIManager.KillScored -= OnKillScored;
        Enemy.MarkedHit -= OnMarkedHit;
        Player.Vital -= OnVital;
        DailyMissions.Completed -= Show;
    }

    private void Start() {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        arena = scene.name == GameMenus.ArenaScene;
        player = arena ? FindAnyObjectByType<Player>() : null;
        run = player != null ? player.GetComponent<ArenaRunManager>() : null;
        ended = false;
        healed = false;
        comboTenths = 0;
    }

    private bool InRun => arena && player != null && !ended;

    private void Update() {
        if (InRun) {
            if (player.IsDead) {
                ended = true;
                int score = UIManager.Instance != null ? UIManager.Instance.Score : 0;
                AccountStats.RunEnded(ArenaDifficulty.Current, score, run != null ? run.RunSeconds : 0f, AchievementTracker.Counts);
            } else if (AchievementTracker.Counts && Time.unscaledTime >= nextTick) {
                nextTick = Time.unscaledTime + TickSeconds;
                int score = UIManager.Instance != null ? UIManager.Instance.Score : 0;
                DailyMissions.RunTick(score, run != null ? run.RunSeconds : 0f, comboTenths, healed);
            }
        }
        UpdateBanner();
    }

    // every kill that scores, with the combo it was scored at
    private void OnKillScored(Enemy enemy, float combo) {
        if (!InRun || enemy == null) {
            return;
        }
        string prefab = Online.RunLog.PrefabName(enemy.gameObject.name);
        // the kill at 1.4x shows 1.5x after it (Achievements.ComboStep)
        int shown = Mathf.RoundToInt(combo * 10f) + 1;
        comboTenths = Mathf.Max(comboTenths, shown);
        AccountStats.Kill(prefab, shown);
        if (AchievementTracker.Counts) {
            DailyMissions.Kill(prefab, enemy.LastHitWeapon, enemy.LastHitMark);
            DailyMissions.RunTick(UIManager.Instance != null ? UIManager.Instance.Score : 0,
                run != null ? run.RunSeconds : 0f, comboTenths, healed);
        }
    }

    private void OnMarkedHit(Enemy enemy, string mark) {
        if (!InRun || mark != Enemy.EyeHit) {
            return;
        }
        AccountStats.EyeHit();
        if (AchievementTracker.Counts) {
            DailyMissions.EyeHit();
        }
    }

    private void OnVital(string kind, int count) {
        if (InRun && kind == "heal") {
            healed = true;
            AccountStats.Healed();
        }
    }

    // ---- the banner: under the Conquistas' one, one mission at a time ----

    private const float BannerSeconds = 4f;
    private const float BannerY = -256f;
    private readonly Queue<DailyMissions.Mission> waiting = new Queue<DailyMissions.Mission>();
    private Canvas canvas;
    private Image icon;
    private Text title;
    private float shownUntil;

    private void Show(DailyMissions.Mission m) {
        waiting.Enqueue(m);
    }

    private void UpdateBanner() {
        if (canvas != null && GameMenus.Paused) {
            canvas.gameObject.SetActive(false);
            if (shownUntil > Time.unscaledTime) shownUntil += Time.unscaledDeltaTime;
            return;
        }
        if (canvas != null && Time.unscaledTime < shownUntil) {
            canvas.gameObject.SetActive(true);
            float t = shownUntil - Time.unscaledTime;
            float slide = Mathf.Clamp01(Mathf.Min(BannerSeconds - t, t) * 4f);
            ((RectTransform)canvas.transform.GetChild(0)).anchoredPosition = new Vector2(0f, Mathf.Lerp(120f, BannerY, slide));
            return;
        }
        if (waiting.Count == 0) {
            if (canvas != null) canvas.gameObject.SetActive(false);
            return;
        }
        DailyMissions.Mission next = waiting.Dequeue();
        Build();
        canvas.gameObject.SetActive(true);
        title.text = "<size=12><color=#7CFC8A>MISSÃO DO DIA CUMPRIDA!</color></size>\n" + next.text +
                     "\n<size=10><color=#FFD84A>+" + next.Reward + " moedas</color></size>";
        shownUntil = Time.unscaledTime + BannerSeconds;
        Sfx.Play("coin");
    }

    private void Build() {
        if (canvas != null) {
            return;
        }
        canvas = MenuKit.CreateCanvas("Mission Banner", 24990, transform);
        Image panel = MenuKit.MakeImage("Panel", canvas.transform, MenuKit.PanelColor);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(520f, 84f);
        Outline edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.3f, 0.75f, 0.35f, 1f);
        edge.effectDistance = new Vector2(3f, -3f);
        panel.raycastTarget = false;

        icon = MenuKit.MakeImage("Icon", panel.transform, Color.white);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        MenuArt art = MenuArt.Get;
        icon.sprite = art != null ? MenuArt.Trim(art.coin) : null;
        icon.enabled = icon.sprite != null;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        iconRect.sizeDelta = new Vector2(56f, 56f);

        title = MenuKit.MakeText(panel.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleLeft);
        title.supportRichText = true;
        title.raycastTarget = false;
        title.lineSpacing = 1.3f;
        RectTransform textRect = title.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(86f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
    }
}
