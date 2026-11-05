using UnityEngine;
using UnityEngine.SceneManagement;

// Sets up a campaign stage when its scene loads, the same way ArenaBootstrap does for the arena:
// the player gets the input layer, dash, the run (respawn, pits, exit), the temporary HUD and the
// minimap, the VHS overlay is switched on, the old Savepoint sprites become checkpoints and the
// vases, barrels and crates of the tilemaps become breakable. The scenes themselves only need the level.
public static class CampaignBootstrap {

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // the first scene is already loaded when this runs
        CampaignWorld.OnSceneChanged(SceneManager.GetActiveScene());
        SetUp(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (mode == LoadSceneMode.Single) {
            CampaignWorld.OnSceneChanged(scene);
        }
        SetUp(scene);
    }

    private static void SetUp(Scene scene) {
        if (!CampaignLevels.IsStage(scene.name)) {
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null || playerObj.GetComponent<Player>() == null) {
            return;
        }

        GetOrAdd<FrenyInput>(playerObj); // no autopilot in the campaign: the keyboard drives
        GetOrAdd<Dash>(playerObj);
        GetOrAdd<CampaignRun>(playerObj);
        GetOrAdd<CampaignHud>(playerObj);
        GetOrAdd<CampaignMinimap>(playerObj);
        if (ArenaAssets.Instance == null) {
            // the item, grenade and vase sprites live on the arena's director; without them the
            // dropped items were plain discs
            GameObject items = Resources.Load<GameObject>("ItemAssets");
            if (items != null) {
                Object.Instantiate(items).name = items.name;
            }
        }
        VhsOverlay.Enable(Camera.main);
        if (Object.FindAnyObjectByType<GameManager>() == null) {
            // holds the potion bought from the Merchant, as in the arena (buying one threw without it)
            new GameObject("GameManager").AddComponent<GameManager>();
        }

        // ledges, spikes, ladders and props get their behaviour from the tiles, then enemies saved
        // inside the ground are lifted out of it
        StageTerrain.Build(scene);
        StageTerrain.SettleEnemies(scene);
        GetOrAdd<LadderClimber>(playerObj);
        GetOrAdd<LiquidSwimmer>(playerObj);

        // stages generated outside the Editor (1-6) are saved without the merged collider shape
        foreach (CompositeCollider2D composite in FindCompat.All<CompositeCollider2D>()) {
            if (composite.pathCount == 0) {
                composite.GenerateGeometry();
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
                if (child.name.StartsWith("Savepoint") && child.GetComponent<Checkpoint>() == null) {
                    MakeCheckpoint(child.gameObject);
                }
            }
        }

        Breakable.FromTilemaps();
        if (CampaignWorld.Active) {
            CampaignWorld.SetUpRoom(scene);
        }
    }

    private static void MakeCheckpoint(GameObject obj) {
        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1.5f, 2.5f);
        obj.AddComponent<Checkpoint>();
    }

    private static T GetOrAdd<T>(GameObject obj) where T : Component {
        T component = obj.GetComponent<T>();
        return component ? component : obj.AddComponent<T>();
    }
}
