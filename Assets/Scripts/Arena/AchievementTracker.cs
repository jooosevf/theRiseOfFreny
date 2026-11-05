using UnityEngine;
using UnityEngine.SceneManagement;

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

    // the small notice under the coins. Only the icon and the name there; what it was for is on the
    // Conquistas page, whose button says "(nova!)" until opened (José, 2026-11-03).
    private void Show(Achievement a) {
        CornerBanner.Show(Achievements.Icon(a), Achievements.Of(a).name, "achievement");
    }
}
