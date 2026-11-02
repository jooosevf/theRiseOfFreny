using UnityEngine;
using UnityEngine.SceneManagement;

// Sets up "cada corrida diferente" when a scene loads, without touching the scenes or the other
// bootstraps: every Player gets his look (FrenyLookRenderer); an arena run fixes the week's
// mutator (WeeklyMutators), clears the last run's blessing (Blessings) and gets ArenaRunVariety,
// which offers the three blessings and runs their effects. Any other scene has neither.
public static class ArenaVariety {

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // the first scene is already loaded when this runs
        SetUp(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (mode == LoadSceneMode.Single) {
            SetUp(scene);
        }
    }

    private static void SetUp(Scene scene) {
        bool arena = scene.name == GameMenus.ArenaScene;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        bool newRun = playerObj == null || playerObj.GetComponent<ArenaRunVariety>() == null;
        if (newRun) {
            // (the first scene can be set up twice: by sceneLoaded and by Init; the second keeps the run)
            Blessings.Clear();
            WeeklyMutators.BeginRun(arena);
        }
        if (playerObj == null || playerObj.GetComponent<Player>() == null) {
            return;
        }
        if (Features.Looks && playerObj.GetComponent<FrenyLookRenderer>() == null) {
            playerObj.AddComponent<FrenyLookRenderer>();
        }
        if (arena && newRun) {
            playerObj.AddComponent<ArenaRunVariety>();
        }
    }
}
