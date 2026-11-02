using UnityEngine;
using UnityEngine.SceneManagement;

// Watches each Arena run for the Perfil (PlayerStats): kills by enemy and weapon, the time with and
// without the autopilot, how Freny fell, the nights he outlived. Nothing goes online during the run;
// when it ends, OnlineServices writes it once. Self-bootstrapping, like AchievementTracker.
public class StatsTracker : MonoBehaviour {

    private static StatsTracker instance;

    private Player player;
    private ArenaRunManager manager;
    private bool arena;
    private bool started;        // saw Freny alive in this scene
    private bool ended;
    private bool startedAuto;    // Automático in the menu
    private bool assisted;       // or the autopilot played at some point: not a manual run
    private ArenaLevel level;
    private int comboTenths;
    private float secondsManual, secondsAuto;

    private bool Running => arena && started && !ended && player != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        if (instance != null) return;
        GameObject go = new GameObject("StatsTracker");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<StatsTracker>();
    }

    private void Awake() {
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        UIManager.KillScored += OnKillScored;
        ArenaDayNight.NightSurvived += OnNightSurvived;
        ArenaDayNight.BloodMoonSurvived += OnBloodMoonSurvived;
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UIManager.KillScored -= OnKillScored;
        ArenaDayNight.NightSurvived -= OnNightSurvived;
        ArenaDayNight.BloodMoonSurvived -= OnBloodMoonSurvived;
    }

    private void Start() {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (mode != LoadSceneMode.Single) return;
        arena = scene.name == GameMenus.ArenaScene;
        player = arena ? FindAnyObjectByType<Player>() : null;
        manager = player != null ? player.GetComponent<ArenaRunManager>() : null;
        started = false;
        ended = false;
        assisted = false;
        startedAuto = arena && !ArenaDifficulty.Manual;
        level = ArenaDifficulty.Current;
        comboTenths = 0;
        secondsManual = 0f;
        secondsAuto = 0f;
    }

    // leaving the arena in the middle of a run (the menu, "Nova corrida") ends it too
    private void OnSceneUnloaded(Scene scene) {
        if (Running) Finish(false);
        PlayerStats.SaveLocal();   // coins spent in the shops, too
    }

    private void OnApplicationQuit() {
        if (Running) Finish(false);
        PlayerStats.SaveLocal();
    }

    private void OnApplicationPause(bool pausing) {
        if (pausing) PlayerStats.SaveLocal();
    }

    private void Update() {
        if (!arena || player == null || ended) return;
        if (player.IsDead) {
            if (started) Finish(true);
            return;
        }
        started = true;
        if (GameMenus.Paused) return;
        if (FrenyInput.AutopilotDrivingNow) {
            assisted = true;
            secondsAuto += Time.unscaledDeltaTime;
        } else {
            secondsManual += Time.unscaledDeltaTime;
        }
    }

    private void Finish(bool died) {
        ended = true;
        bool auto = startedAuto || assisted;
        PlayerStats.Add(auto ? PlayerStats.RunsAuto : PlayerStats.RunsManual);
        PlayerStats.Add(PlayerStats.RunsKey(level));
        PlayerStats.Add(PlayerStats.SecondsManual, Mathf.RoundToInt(secondsManual));
        PlayerStats.Add(PlayerStats.SecondsAuto, Mathf.RoundToInt(secondsAuto));
        PlayerStats.Peak(PlayerStats.LongestRun, Mathf.RoundToInt(manager != null ? manager.RunSeconds : secondsManual + secondsAuto));
        int score = UIManager.Instance != null ? Mathf.Max(0, UIManager.Instance.Score) : 0;
        if (score > 0) PlayerStats.Peak(PlayerStats.BestKey(level, auto), score);
        if (died) {
            PlayerStats.Add(PlayerStats.Deaths);
            PlayerStats.Add(PlayerStats.DeathKey(PlayerStats.CauseOf(player != null ? player.LastDamageSource : null)));
        }
        PlayerStats.SaveLocal();
        Online.OnlineServices.Instance?.OnStatsRunEnded();
    }

    private void OnKillScored(Enemy enemy, float combo) {
        if (!Running || enemy == null) return;
        PlayerStats.Add(PlayerStats.Kills);
        if (FrenyInput.AutopilotDrivingNow) PlayerStats.Add(PlayerStats.KillsAuto);
        string key = PlayerStats.EnemyKey(Online.RunLog.PrefabName(enemy.gameObject.name));
        PlayerStats.Add(PlayerStats.KillKey(key ?? "other"));
        PlayerStats.Add(PlayerStats.WeaponKey(string.IsNullOrEmpty(enemy.LastHitSource) ? PlayerStats.WeaponOther : enemy.LastHitSource));
        // the kill at 1.4x shows 1.5x after it (ProgressTracker does the same)
        int shown = Mathf.RoundToInt(combo * 10f) + 1;
        if (shown > comboTenths) {
            comboTenths = shown;
            PlayerStats.Peak(PlayerStats.BestCombo, shown);
        }
    }

    private void OnNightSurvived() {
        if (Running) PlayerStats.Add(PlayerStats.Nights);
    }

    private void OnBloodMoonSurvived() {
        if (Running) PlayerStats.Add(PlayerStats.BloodMoons);
    }

    // ArenaChest and GoldenChest, whoever opened them
    public static void ChestOpened() {
        if (instance != null && instance.Running) PlayerStats.Add(PlayerStats.Chests);
    }
}
