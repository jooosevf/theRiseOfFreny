using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The rooms of the test dungeon, where the world remembers (design/gdd/estado-do-mundo.md): dead
// enemies stay dead unless the checkpoint goes back to before they died, what Freny carries goes
// with him from room to room, and only a save point writes to disk. The older stages don't use any
// of this. Static like CampaignRun's attempt, because every room is a scene load.
public static class CampaignWorld {

    public const string FirstRoom = "T-1";
    public const string SavedKey = "freny.campaign.world";
    public const int MaxLives = 3;       // the lives Freny can carry and drink when he wants (Q)
    public const int StartLives = 1;
    private const float ToastSeconds = 3f;

    private const string CarriedKey = "carried/known";
    private const string LivesKey = "carried/lives";

    public static WorldState State { get; private set; }
    public static bool Active { get; private set; }

    public static string Toast { get; private set; }
    public static float ToastUntil { get; private set; }

    private static string room;
    private static bool fullHearts;       // coming back from a death or a save: he starts at full life
    private static bool carriedReady;     // false until the new room's Freny got what he carries
    private static Vector3? spawn;
    private static bool depositing;
    private static readonly Dictionary<Enemy, string> placedEnemies = new Dictionary<Enemy, string>();

    public static int Lives {
        get => State != null ? State.Get(LivesKey) : 0;
        set {
            if (State != null) {
                State.Set(LivesKey, value);
            }
        }
    }

    public static bool HasSave => !string.IsNullOrEmpty(PlayerPrefs.GetString(SavedKey, ""));

    // ---- entering and leaving ----

    public static void Enter(bool fresh) {
        State = fresh ? null : WorldState.FromJson(PlayerPrefs.GetString(SavedKey, ""));
        if (State == null) {
            State = new WorldState(FirstRoom);
            PlayerPrefs.DeleteKey(SavedKey);
        }
        GoBack();
    }

    public static void Respawn() {
        State.RestoreCheckpoint();
        GoBack();
    }

    private static void GoBack() {
        fullHearts = true;
        spawn = State.HasCheckpointPosition ? (Vector3)State.CheckpointPosition : (Vector3?)null;
        GameMenus.StartStage(State.CheckpointScene);
    }

    // every scene load that replaces the scene (CampaignBootstrap). Leaving the rooms forgets what
    // the save point didn't write.
    public static void OnSceneChanged(Scene scene) {
        placedEnemies.Clear();
        carriedReady = false;
        room = scene.name;
        Active = Features.CampaignWorld && CampaignLevels.IsWorldRoom(scene.name);
        if (!Active) {
            State = null;
        } else if (State == null) {
            State = new WorldState(scene.name);   // played straight from the scene, in the Editor
        }
    }

    // the room's objects, once CampaignBootstrap has set up the stage
    public static void SetUpRoom(Scene scene) {
        WorldMarkers.Build(scene);
        foreach (Enemy enemy in FindCompat.All<Enemy>()) {
            string key = room + "/enemy/" + enemy.name;
            if (State.Has(key)) {
                enemy.gameObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
            } else {
                placedEnemies[enemy] = key;
            }
        }
        foreach (Coin coin in FindCompat.All<Coin>()) {
            string key = room + "/coin/" + coin.name;
            if (State.Has(key)) {
                coin.gameObject.SetActive(false);
                Object.Destroy(coin.gameObject);
            } else {
                coin.worldKey = key;
            }
        }
    }

    public static string KeyFor(string kind, string id) {
        return room + "/" + kind + "/" + id;
    }

    public static void Mark(string key) {
        if (Active && State != null) {
            State.Set(key);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() {
        Enemy.Died -= OnEnemyDied;
        Enemy.Died += OnEnemyDied;
    }

    private static void OnEnemyDied(Enemy enemy) {
        if (Active && placedEnemies.TryGetValue(enemy, out string key)) {
            State.Set(key);
        }
    }

    // ---- where Freny appears, and what he carries ----

    public static Vector3? TakeSpawn() {
        Vector3? taken = spawn;
        spawn = null;
        return taken;
    }

    public static void RestoreCarried(Player player) {
        if (!State.Has(CarriedKey)) {
            Lives = StartLives;
        } else {
            NetThrower nets = player.GetComponent<NetThrower>();
            if (nets != null) {
                nets.SetNets(State.Get("carried/nets"));
            }
            player.RestoreCarried(
                fullHearts ? player.MaxHealth : State.Get("carried/health", player.MaxHealth),
                State.Get("carried/armor"),
                State.Get("carried/bullets"),
                State.Get("carried/reserve"),
                State.Get("carried/shotgun") == 1,
                State.Get("carried/shells"),
                State.Get("carried/grenades"),
                State.Get("carried/swings"));
        }
        fullHearts = false;
        carriedReady = true;
        player.ShowCoins(FrenyProgress.Coins + State.BagCoins);
        CaptureCarried(player);
    }

    public static void CaptureCarried(Player player) {
        if (!carriedReady || player == null) {
            return;
        }
        State.Set(CarriedKey);
        State.Set("carried/health", player.Health);
        State.Set("carried/armor", player.Armor);
        State.Set("carried/bullets", player.RevolverBullets);
        State.Set("carried/reserve", player.RevolverReserve);
        State.Set("carried/shotgun", player.ShotgunOwned ? 1 : 0);
        State.Set("carried/shells", player.ShotgunShells);
        State.Set("carried/grenades", player.Grenades);
        State.Set("carried/swings", player.GreatSwordSwings);
        NetThrower nets = player.GetComponent<NetThrower>();
        State.Set("carried/nets", nets != null ? nets.Nets : 0);
    }

    // ---- the bag: what was found since the last save point ----

    public static void AddFoundCoins(Player player, int amount) {
        State.BagCoins += amount;
        player.ShowCoins(FrenyProgress.Coins + State.BagCoins);
    }

    // true when the XP went into the bag instead of the account (FrenyProgress.AddXp)
    public static bool TakeXp(int amount) {
        if (!Active || depositing || State == null) {
            return false;
        }
        State.BagXp += amount;
        return true;
    }

    private static void Deposit() {
        int coins = State.BagCoins;
        int xp = State.BagXp;
        State.EmptyBag();
        FrenyProgress.Coins += coins;
        depositing = true;
        FrenyProgress.AddXp(xp);
        depositing = false;
    }

    // ---- checkpoints and save points ----

    // true when it is a new one, for the effect; touching one again takes a new copy all the same
    public static bool TouchCheckpoint(Player player, Vector3 position) {
        if (!carriedReady || player == null || player.IsDead) {
            return false;
        }
        bool isNew = !State.HasCheckpointPosition || State.CheckpointScene != room ||
                     Vector2.Distance(State.CheckpointPosition, position) >= 1f;
        CaptureCarried(player);
        State.MarkCheckpoint(room, position);
        return isNew;
    }

    public static void Save(Player player, Vector3 position) {
        if (!carriedReady || player == null || player.IsDead) {
            return;
        }
        CaptureCarried(player);
        Deposit();
        State.MarkCheckpoint(room, position);
        PlayerPrefs.SetString(SavedKey, State.ToJson());
        SaveSystem.SetLastScene(room);
        SaveSystem.SaveNow();
        player.ShowCoins(FrenyProgress.Coins);
    }

    public static void ShowToast(string text) {
        Toast = text;
        ToastUntil = Time.time + ToastSeconds;
    }
}
