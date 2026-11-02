using UnityEngine;
using UnityEngine.SceneManagement;

// Turns the arena scene into idle mode without editing the scene file: when it loads, the player
// gets the input layer, the autopilot and the temporary HUD. Components already placed by hand
// in the scene are kept, so this can be removed once ZE Arena is set up in the Editor.
public static class ArenaBootstrap {

    private const string ArenaSceneName = "ZE Arena";
    private const float PropRegrowSeconds = 90f;   // a broken vase, crate or barrel comes back after this

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // the first scene is already loaded when this runs
        SetUp(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        SetUp(scene);
    }

    private static void SetUp(Scene scene) {
        if (scene.name != ArenaSceneName) {
            ArenaBounds.Disable();
            return;
        }
        ArenaBounds.Enable(Camera.main); // the screen is the whole arena

        // the campaign's invisible posts at platform edges turned (and made jump) the enemies walking
        // into them and blocked Freny; arena enemies turn at edges by themselves
        foreach (ChangeGroundArea area in FindCompat.All<ChangeGroundArea>(true)) {
            if (area.gameObject.scene == scene) {
                area.gameObject.SetActive(false);
            }
        }
        // (enemies are kinematic bodies and would shove Freny: each one ignores his collider in
        // Enemy.Init, here and in the campaign, so he walks through them)

        // the vases, crates and barrels painted on the foreground tilemaps break like the campaign's
        Breakable.FromTilemaps(PropRegrowSeconds);

        if (!System.Array.Exists(FindCompat.All<ArenaDayNight>(), d => d.gameObject.scene == scene)) {
            GameObject dayNight = new GameObject("Day Night");
            SceneManager.MoveGameObjectToScene(dayNight, scene);
            dayNight.AddComponent<ArenaDayNight>();
        }
        // the Ciclope's key, golden chest, music and red torches
        if (!System.Array.Exists(FindCompat.All<ArenaBossLoot>(), l => l.gameObject.scene == scene)) {
            GameObject loot = new GameObject("Boss Loot");
            SceneManager.MoveGameObjectToScene(loot, scene);
            loot.AddComponent<ArenaBossLoot>();
        }

        // the rare drops: Insta-kill, Nuke and Ímã
        if (!System.Array.Exists(FindCompat.All<ArenaPowerUps>(), p => p.gameObject.scene == scene)) {
            GameObject powerUps = new GameObject("Power Ups");
            SceneManager.MoveGameObjectToScene(powerUps, scene);
            powerUps.AddComponent<ArenaPowerUps>();
        }

        // Double, Triple, Quadra and Penta Kill
        if (!System.Array.Exists(FindCompat.All<MultiKill>(), m => m.gameObject.scene == scene)) {
            GameObject multiKill = new GameObject("Multi Kill");
            SceneManager.MoveGameObjectToScene(multiKill, scene);
            multiKill.AddComponent<MultiKill>();
        }

        // the Poço de Ácido is this same scene, turned toxic at runtime (ArenaVariant, AcidPitArena)
        if (ArenaVariant.Current == ArenaKind.AcidPit && AcidPitArena.Instance == null) {
            GameObject pit = new GameObject("Acid Pit");
            SceneManager.MoveGameObjectToScene(pit, scene);
            pit.AddComponent<AcidPitArena>();
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null || playerObj.GetComponent<Player>() == null) {
            return;
        }

        FrenyInput input = GetOrAdd<FrenyInput>(playerObj);
        GetOrAdd<Dash>(playerObj); // the arena Player never had the Dash component, so nobody could dash
        GetOrAdd<ArenaWrap>(playerObj);
        GetOrAdd<ArenaAutopilot>(playerObj);
        GetOrAdd<ArenaRunManager>(playerObj);
        GetOrAdd<ArenaHud>(playerObj);
        GetOrAdd<CombatHud>(playerObj); // weapons and cooldowns, same as in the campaign
        input.FindAutopilot();
        // before the first Update, so a manual run never has the autopilot drive a single frame
        input.SetAutoEnabled(!ArenaDifficulty.Manual);
    }

    private static T GetOrAdd<T>(GameObject obj) where T : Component {
        T component = obj.GetComponent<T>();
        return component ? component : obj.AddComponent<T>();
    }
}
