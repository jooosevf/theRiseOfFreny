using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// One attempt at a campaign stage, on the player (added by CampaignBootstrap). Dying sends Freny
// back to the last checkpoint; falling into a pit costs a heart and puts him back on the last
// solid ground; the exit door finishes the stage and opens the next one.
// The attempt (timer, deaths, checkpoint) is static because a respawn reloads the scene.
public class CampaignRun : MonoBehaviour {

    [SerializeField] private float respawnDelay = 2.5f;
    [SerializeField] private float nextStageDelay = 3f;
    [SerializeField] private float pitMargin = 6f;   // below the lowest ground of the stage
    [SerializeField] private float worldEndDelay = 5f;   // the end of the test dungeon, then the menu

    public static CampaignRun Instance { get; private set; }

    // ---- the attempt, kept across reloads ----
    private static string attemptStage;
    private static float attemptStart;
    private static Vector3? checkpoint;
    private static string arrivingDoor;   // set by a Link door before loading its target scene

    public static int Deaths { get; private set; }

    public string StageId { get; private set; }
    public float Elapsed => (Finished ? finishTime : Time.time) - attemptStart;
    public bool Finished { get; private set; }
    public float PitY => pitY;           // below this Freny loses a heart and enemies are removed
    public bool IsLastStage => CampaignLevels.NextOf(StageId) == null;
    public float SecondsUntilRespawn => deathTime < 0f ? 0f : Mathf.Max(0f, deathTime + respawnDelay - Time.time);
    public float SecondsUntilNextStage => Finished && !IsLastStage ? Mathf.Max(0f, finishTime + nextStageDelay - Time.time) : 0f;

    private Player player;
    private Rigidbody2D rb;
    private Vector3 lastSafe;
    private float pitY = float.NegativeInfinity;
    private float deathTime = -1f;
    private float finishTime;

    private void Awake() {
        Instance = this;
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Start() {
        player = GetComponent<Player>();
        rb = GetComponent<Rigidbody2D>();
        StageId = SceneManager.GetActiveScene().name;

        if (attemptStage != StageId) {
            // a new stage (not a respawn): fresh timer, no checkpoint
            attemptStage = StageId;
            attemptStart = Time.time;
            checkpoint = null;
            Deaths = 0;
        }

        Vector3? worldSpawn = CampaignWorld.Active ? CampaignWorld.TakeSpawn() : null;
        Vector3? spawn = DoorPosition(arrivingDoor) ?? (CampaignWorld.Active ? worldSpawn : checkpoint);
        arrivingDoor = null;
        if (spawn.HasValue) {
            transform.position = spawn.Value;
        }
        lastSafe = transform.position;
        pitY = LowestGround() - pitMargin;
        if (CampaignWorld.Active) {
            StartCoroutine(RestoreCarriedNextFrame());
        }
    }

    // after every Start of this frame, Player's and NetThrower's, which hand out the usual loadout
    private IEnumerator RestoreCarriedNextFrame() {
        yield return null;
        CampaignWorld.RestoreCarried(player);
    }

    private void Update() {
        if (player == null || Finished) {
            return;
        }

        if (player.IsDead) {
            if (deathTime < 0f) {
                deathTime = Time.time;
                Deaths++;
            }
            if (SecondsUntilRespawn <= 0f) {
                if (CampaignWorld.Active) {
                    CampaignWorld.Respawn();
                } else {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
            return;
        }

        if (player.IsGrounded && !player.IsStunned) {
            lastSafe = transform.position;
        }

        if (transform.position.y < pitY) {
            // a pit costs a heart, not the whole stage
            if (player.TryDamage() && !player.IsDead) {
                Sfx.Play("pit_fall");
                transform.position = lastSafe;
                player.ResetFall();
                if (rb) {
                    rb.SetVel(Vector2.zero);
                }
            }
        }
    }

    // ---- called by doors and checkpoints ----

    // true when this is a new checkpoint (not the one already saved)
    public static bool SetCheckpoint(Vector3 position) {
        if (checkpoint.HasValue && Vector3.Distance(checkpoint.Value, position) < 1f) {
            return false;
        }
        checkpoint = position;
        return true;
    }

    public void FinishStage() {
        if (Finished || player == null || player.IsDead) {
            return;
        }
        Finished = true;
        finishTime = Time.time;
        CampaignProgress.Complete(StageId, Elapsed);
        if (CampaignWorld.Active) {
            CampaignWorld.Save(player, transform.position);
            Invoke(nameof(BackToMenu), worldEndDelay);
        }
        attemptStage = null; // replaying the stage starts a fresh attempt

        // Freny stops at the door while the "stage clear" panel is up
        player.enabled = false;
        if (rb) {
            rb.SetVel(Vector2.zero);
        }
        Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.3f), 30, 3f, 0.1f, 0.8f);
        Sfx.Play("stage_clear");

        if (!IsLastStage) {
            Invoke(nameof(LoadNextStage), nextStageDelay);
        }
    }

    private void BackToMenu() {
        GameMenus.LoadMainMenu();
    }

    public void LoadNextStage() {
        string next = CampaignLevels.NextOf(StageId);
        if (next != null) {
            SceneManager.LoadScene(next);
        }
    }

    // a door that leads to another scene and a door in it (side rooms, going back)
    public static void Travel(string scene, string door) {
        arrivingDoor = door;
        if (CampaignWorld.Active && Instance != null) {
            CampaignWorld.CaptureCarried(Instance.player);
        }
        if (scene != attemptStage) {
            checkpoint = null;
        }
        SceneManager.LoadScene(scene);
    }

    public static void LoadStage(string stageId) {
        attemptStage = null;
        arrivingDoor = null;
        SceneManager.LoadScene(stageId);
    }

    // ---- helpers ----

    private static Vector3? DoorPosition(string doorId) {
        if (string.IsNullOrEmpty(doorId)) {
            return null;
        }
        foreach (LevelDoor door in FindCompat.All<LevelDoor>()) {
            if (door.doorId == doorId) {
                return door.transform.position;
            }
        }
        return null;
    }

    // the bottom of the stage's solid tiles, so falling below it means a pit.
    // Uses the tiles themselves: a collider merged into a CompositeCollider2D reports empty bounds.
    private static float LowestGround() {
        float lowest = float.PositiveInfinity;
        foreach (TilemapCollider2D tiles in FindCompat.All<TilemapCollider2D>()) {
            Tilemap map = tiles.GetComponent<Tilemap>();
            if (tiles.enabled && map != null) {
                map.CompressBounds();
                if (map.cellBounds.size.y > 0) {
                    lowest = Mathf.Min(lowest, map.transform.TransformPoint(map.localBounds.min).y);
                }
            }
        }
        return float.IsPositiveInfinity(lowest) ? -100f : lowest;
    }
}
