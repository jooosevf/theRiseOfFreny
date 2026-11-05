using UnityEngine;
using UnityEngine.SceneManagement;

// Feeds the Arena's runs to the Estatísticas (AccountStats, every run) and to the Missões do dia
// (DailyMissions, manual runs only: AchievementTracker.Counts, the Conquistas' rule), from the same
// events the Conquistas listen to. Shows the "Missão cumprida" notice (CornerBanner).
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

    // the same small notice as the Conquistas', under the coins (José, 2026-11-05: in the middle of
    // the screen it got in the way). The coins go to the same counter right above it.
    private void Show(DailyMissions.Mission m) {
        MenuArt art = MenuArt.Get;
        CornerBanner.Show(art != null ? MenuArt.Trim(art.coin) : null,
            "Missão cumprida!  <color=#7CFC8A>+" + m.Reward + "</color>\n<size=10>" + m.text + "</size>", "coin");
    }
}
