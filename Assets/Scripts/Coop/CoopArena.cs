using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Throwaway co-op prototype: what the two players see of each other in the Arena.
// Each one runs his own Freny and sends where he is; the other draws a ghost a little in the past,
// between the last two positions received. The host runs the enemies and sends them too; the
// client's ArenaDirector stays off and it draws puppets, and a puppet hit asks the host for the hit.
//
// Limits of this first version, on purpose: the enemies only chase the host's Freny; the client gets
// hurt by touching a puppet; a grenade or a net only hurts the enemies on the host; score and drops
// are the host's.
public class CoopArena : MonoBehaviour {

    private const float SendInterval = 0.05f;        // 20 messages a second per stream
    private const float RenderDelay = 0.1f;          // how far in the past the other side is drawn
    private const float HitFlashSeconds = 0.12f;
    private const int MaxEnemyBytes = 1200;
    private const float NameTagHeight = 0.9f;
    private static readonly Color GhostTint = new Color(0.55f, 0.8f, 1f);

    private Player player;
    private Collider2D playerCol;
    private Animator playerAnim;
    private ArenaDirector director;
    private FieldInfo enemiesField;
    private bool isHost;
    private float nextSend;
    private float startedAt;
    private float nextLog;
    private int nextShot;
    private int received;
    private int receivedRate;
    private float rateStart;

    private Transform ghost;
    private Animator ghostAnim;
    private SpriteRenderer[] ghostSprites;
    private readonly Track ghostTrack = new Track();
    private int ghostState;
    private bool peerDead;
    private int peerHealth;

    private readonly Dictionary<Enemy, ushort> enemyIds = new Dictionary<Enemy, ushort>();
    private readonly Dictionary<ushort, Enemy> enemiesById = new Dictionary<ushort, Enemy>();
    private ushort nextEnemyId = 1;
    private readonly Dictionary<ushort, CoopPuppet> puppets = new Dictionary<ushort, CoopPuppet>();
    private readonly HashSet<ushort> seen = new HashSet<ushort>();
    private readonly List<ushort> gone = new List<ushort>();
    private GameObject puppetHolder;
    private int hostScore;
    private int remoteHits;
    private int remoteKills;
    private GUIStyle overlay;
    private GUIStyle nameTag;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook() {
        SceneManager.sceneLoaded += (scene, _) => {
            if (scene.name == CoopNet.ArenaScene && CoopNet.Net != null) {
                new GameObject("Coop Arena").AddComponent<CoopArena>();
            }
        };
    }

    // ---- runs right after the scene loads, before any of its Start methods
    private void Awake() {
        isHost = CoopNet.IsHost;
        director = FindAnyObjectByType<ArenaDirector>();
        enemiesField = typeof(ArenaDirector).GetField("enemies", BindingFlags.NonPublic | BindingFlags.Instance);
        if (!isHost) {
            if (director != null) {
                director.enabled = false;
            }
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include)) {
                spawner.enabled = false;
            }
        }
        CoopNet.PlayerState += OnPlayerState;
        CoopNet.EnemyState += OnEnemyState;
        CoopNet.EnemyHit += OnEnemyHit;
        puppetHolder = new GameObject("Coop Puppets (inactive)");
        puppetHolder.SetActive(false);
    }

    private void OnDestroy() {
        CoopNet.PlayerState -= OnPlayerState;
        CoopNet.EnemyState -= OnEnemyState;
        CoopNet.EnemyHit -= OnEnemyHit;
    }

    private IEnumerator Start() {
        yield return null;   // ArenaBootstrap and the Player's Start have run
        startedAt = Time.unscaledTime;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) {
            Debug.LogError("[Coop] a Arena não tem Player");
            yield break;
        }
        player = playerObj.GetComponent<Player>();
        playerCol = playerObj.GetComponent<Collider2D>();
        playerAnim = playerObj.GetComponentInChildren<Animator>();
        MakeGhost(playerObj);
        if (!isHost) {
            foreach (Enemy enemy in new List<Enemy>(Enemy.Active)) {
                Destroy(enemy.gameObject);
            }
            if (CoopArgs.Autopilot) {
                ArenaAutopilot autopilot = playerObj.GetComponent<ArenaAutopilot>();
                if (autopilot != null) {
                    autopilot.enabled = false;
                }
                playerObj.AddComponent<CoopTestPilot>();
                FrenyInput input = playerObj.GetComponent<FrenyInput>();
                input.FindAutopilot();
                input.SetAutoEnabled(true);
            }
        }
    }

    // a copy of Freny with only his sprites and animator left
    private void MakeGhost(GameObject source) {
        GameObject copy = Instantiate(source, puppetHolder.transform);
        CoopStrip.ToVisuals(copy);
        copy.name = "Coop Ghost";
        copy.tag = "Untagged";
        copy.transform.SetParent(null);
        ghost = copy.transform;
        ghostAnim = copy.GetComponentInChildren<Animator>();
        if (ghostAnim != null) {
            ghostAnim.fireEvents = false;   // the clips call the sword's hitbox, which the ghost doesn't have
        }
        ghostSprites = copy.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sprite in ghostSprites) {
            sprite.color = GhostTint;
        }
        copy.SetActive(false);
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
    }

    private void Update() {
        float now = Time.unscaledTime;
        if (now >= nextSend && player != null) {
            nextSend = now + SendInterval;
            SendPlayer();
            if (isHost) {
                SendEnemies();
            }
        }
        DrawGhost(now);
        DrawPuppets(now);
        if (now - rateStart >= 1f) {
            receivedRate = received;
            received = 0;
            rateStart = now;
        }
        if (now >= nextLog) {
            nextLog = now + 5f;
            Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} RTT {CoopNet.RttSummary()} | transporte {CoopNet.TransportRtt} ms | {receivedRate} msg/s recebidas | {CoopNet.BytesPerSecond} B/s enviados | {puppets.Count} bonecos | golpes do cliente {remoteHits}, mortes {remoteKills}");
        }
        TestRunner(now);
    }

    // ---- Freny's state, both ways
    private void SendPlayer() {
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        Vector2 position = player.transform.position;
        writer.WriteValueSafe(position);
        writer.WriteValueSafe((sbyte)(player.FacingRight ? 1 : -1));
        AnimatorStateInfo state = playerAnim != null ? playerAnim.GetCurrentAnimatorStateInfo(0) : default;
        writer.WriteValueSafe(state.fullPathHash);
        writer.WriteValueSafe(state.normalizedTime);
        writer.WriteValueSafe((byte)(player.IsDead ? 1 : 0));
        writer.WriteValueSafe((byte)Mathf.Max(0, player.Health));
        CoopNet.Send("coop.p", writer, NetworkDelivery.UnreliableSequenced);
    }

    private void OnPlayerState(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out Vector2 position);
        reader.ReadValueSafe(out sbyte facing);
        reader.ReadValueSafe(out int state);
        reader.ReadValueSafe(out float time);
        reader.ReadValueSafe(out byte dead);
        reader.ReadValueSafe(out byte health);
        received++;
        ghostTrack.Add(Time.unscaledTime, position);
        peerDead = dead != 0;
        peerHealth = health;
        if (ghost == null) {
            return;
        }
        Vector3 scale = ghost.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        ghost.localScale = scale;
        if (ghostAnim != null && state != 0 && state != ghostState && ghostAnim.isActiveAndEnabled) {
            ghostState = state;
            ghostAnim.Play(state, 0, Mathf.Repeat(time, 1f));
        }
    }

    private void DrawGhost(float now) {
        if (ghost == null || !ghostTrack.HasData) {
            return;
        }
        bool visible = CoopNet.Connected && !peerDead;
        if (ghost.gameObject.activeSelf != visible) {
            ghost.gameObject.SetActive(visible);
            ghostState = 0;
        }
        ghost.position = ghostTrack.At(now - RenderDelay);
    }

    // ---- the enemies: host to client
    private void SendEnemies() {
        using FastBufferWriter writer = new FastBufferWriter(MaxEnemyBytes, Allocator.Temp);
        int score = UIManager.Instance ? UIManager.Instance.Score : 0;
        List<Enemy> alive = Enemy.Active;
        int count = 0;
        foreach (Enemy enemy in alive) {
            if (enemy != null && !enemy.IsDead) {
                count++;
            }
        }
        writer.WriteValueSafe(score);
        writer.WriteValueSafe((ushort)count);
        foreach (Enemy enemy in alive) {
            if (enemy == null || enemy.IsDead) {
                continue;
            }
            if (!enemyIds.TryGetValue(enemy, out ushort id)) {
                id = nextEnemyId++;
                enemyIds[enemy] = id;
                enemiesById[id] = enemy;
            }
            Animator anim = enemy.GetComponentInChildren<Animator>();
            AnimatorStateInfo state = anim != null && anim.isActiveAndEnabled ? anim.GetCurrentAnimatorStateInfo(0) : default;
            SpriteRenderer sprite = enemy.GetComponentInChildren<SpriteRenderer>();
            sbyte facing = (sbyte)((enemy.transform.localScale.x < 0f ? -1 : 1) * (sprite != null && sprite.flipX ? -1 : 1));
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(KindOf(enemy));
            writer.WriteValueSafe((Vector2)enemy.transform.position);
            writer.WriteValueSafe(facing);
            writer.WriteValueSafe(state.fullPathHash);
            writer.WriteValueSafe((byte)Mathf.Clamp(enemy.Health, 0, 255));
        }
        CoopNet.Send("coop.e", writer, NetworkDelivery.UnreliableSequenced);
    }

    // the prefab's place in the director's list, which starts the same on both sides; an enemy the
    // director only builds at runtime (Escudeiro, Mina-aranha, Ciclope) has none and shows as a Golem
    private byte KindOf(Enemy enemy) {
        ArenaDirector.Entry[] entries = Entries();
        if (entries != null) {
            string name = enemy.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].prefab != null && entries[i].prefab.name == name) {
                    return (byte)i;
                }
            }
        }
        return byte.MaxValue;
    }

    private ArenaDirector.Entry[] Entries() {
        return director != null ? (ArenaDirector.Entry[])enemiesField.GetValue(director) : null;
    }

    private void OnEnemyState(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out hostScore);
        reader.ReadValueSafe(out ushort count);
        float now = Time.unscaledTime;
        seen.Clear();
        for (int i = 0; i < count; i++) {
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out sbyte facing);
            reader.ReadValueSafe(out int state);
            reader.ReadValueSafe(out byte health);
            seen.Add(id);
            if (!puppets.TryGetValue(id, out CoopPuppet puppet)) {
                puppet = MakePuppet(id, kind, position);
                if (puppet == null) {
                    continue;
                }
                puppets[id] = puppet;
            }
            puppet.Receive(now, position, facing, state, health);
        }
        gone.Clear();
        foreach (KeyValuePair<ushort, CoopPuppet> pair in puppets) {
            if (!seen.Contains(pair.Key)) {
                gone.Add(pair.Key);
            }
        }
        foreach (ushort id in gone) {
            if (puppets[id] != null) {
                Fx.Burst(puppets[id].transform.position, new Color(0.7f, 0.1f, 0.1f), 10, 2f, 0.08f, 0.35f);
                Destroy(puppets[id].gameObject);
            }
            puppets.Remove(id);
        }
    }

    private CoopPuppet MakePuppet(ushort id, byte kind, Vector2 position) {
        ArenaDirector.Entry[] entries = Entries();
        GameObject prefab = null;
        if (entries != null) {
            if (kind < entries.Length) {
                prefab = entries[kind].prefab;
            }
            for (int i = 0; prefab == null && i < entries.Length; i++) {
                if (entries[i].prefab != null && entries[i].prefab.GetComponent<Golem>() != null) {
                    prefab = entries[i].prefab;
                }
            }
        }
        if (prefab == null) {
            return null;
        }
        GameObject copy = Instantiate(prefab, position, Quaternion.identity, puppetHolder.transform);
        CoopStrip.ToVisuals(copy, keepColliders: true);
        copy.tag = "Untagged";
        CoopPuppet puppet = copy.AddComponent<CoopPuppet>();
        puppet.Init(this, id);
        copy.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
        copy.SetActive(true);
        return puppet;
    }

    private void DrawPuppets(float now) {
        foreach (CoopPuppet puppet in puppets.Values) {
            if (puppet != null) {
                puppet.Draw(now - RenderDelay);
            }
        }
    }

    // a puppet was hit on this side: only the host can hurt the real one
    public void RequestHit(ushort id) {
        using FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp);
        writer.WriteValueSafe(id);
        CoopNet.Send("coop.h", writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnEnemyHit(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out ushort id);
        if (enemiesById.TryGetValue(id, out Enemy enemy) && enemy != null && !enemy.IsDead) {
            enemy.Damage(1);
            remoteHits++;
            if (enemy.IsDead) {
                remoteKills++;
            }
        }
    }

    // called by a puppet overlapping Freny (the prototype's stand-in for enemy attacks on the client)
    public void TouchedBy(Collider2D body) {
        if (player != null && playerCol != null && !player.IsDead && !player.IsInvulnerable
            && body.bounds.Intersects(playerCol.bounds)) {
            player.TryDamage(1);
        }
    }

    public static float FlashSeconds => HitFlashSeconds;

    // ---- the numbers on screen: this is what the test is for
    private void OnGUI() {
        if (overlay == null) {
            overlay = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true };
            overlay.normal.textColor = new Color(1f, 0.95f, 0.75f);
            nameTag = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameTag.normal.textColor = GhostTint;
        }
        if (ghost != null && ghost.gameObject.activeSelf && Camera.main != null) {
            Vector3 point = Camera.main.WorldToScreenPoint(ghost.position + Vector3.up * NameTagHeight);
            GUI.Label(new Rect(point.x - 50f, Screen.height - point.y - 12f, 100f, 24f), isHost ? "Jogador 2" : "Jogador 1", nameTag);
        }
        string text = (isHost ? $"HOST | código {CoopNet.JoinCode}\n" : "CLIENTE\n") +
                      $"Ping: {CoopNet.RttSummary()}\n" +
                      $"Transporte: {CoopNet.TransportRtt} ms | Recebendo {receivedRate} msg/s | Enviando {CoopNet.BytesPerSecond} B/s\n" +
                      $"Outro Freny: {(CoopNet.Connected ? (peerDead ? "caído" : $"{peerHealth} corações") : "desconectado")}" +
                      (isHost ? "" : $" | Pontos do host: {hostScore}");
        GUI.Box(new Rect(Screen.width - 340f, 72f, 330f, 118f), text, overlay);
    }

    // ---- only for the test binaries, driven from the command line
    private void TestRunner(float now) {
        string shots = CoopArgs.ShotDir;
        float elapsed = now - startedAt;
        if (!string.IsNullOrEmpty(shots) && elapsed >= 5f + nextShot * CoopArgs.ShotEvery) {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, $"{(isHost ? "host" : "client")}_{nextShot:00}.png"));
            nextShot++;
        }
        float quitAfter = CoopArgs.QuitAfter;
        bool capped = CoopArgs.Autopilot && hostScoreOrOwn() >= 4500;
        if ((quitAfter > 0f && elapsed >= quitAfter) || capped) {
            Debug.Log($"[Coop] FIM {(isHost ? "host" : "cliente")} após {elapsed:0} s: {CoopNet.RttSummary()}");
            Application.Quit();
        }
    }

    private int hostScoreOrOwn() {
        return isHost ? (UIManager.Instance ? UIManager.Instance.Score : 0) : hostScore;
    }
}

// The two positions around the moment being drawn; a position older than the buffer holds still.
public class Track {
    private const int Size = 12;
    private readonly float[] times = new float[Size];
    private readonly Vector2[] points = new Vector2[Size];
    private int count;
    private int head;

    public bool HasData => count > 0;

    public void Add(float time, Vector2 point) {
        times[head] = time;
        points[head] = point;
        head = (head + 1) % Size;
        count = Mathf.Min(count + 1, Size);
    }

    public Vector2 At(float time) {
        int newest = (head - 1 + Size) % Size;
        Vector2 result = points[newest];
        if (time >= times[newest]) {
            return result;
        }
        for (int i = 1; i < count; i++) {
            int older = (newest - i + Size) % Size;
            if (times[older] <= time) {
                int newer = (older + 1) % Size;
                float span = times[newer] - times[older];
                float t = span > 0f ? (time - times[older]) / span : 1f;
                return Vector2.Lerp(points[older], points[newer], t);
            }
        }
        return points[(newest - count + 1 + Size) % Size];
    }
}

// An enemy drawn on the client. IDamageable, so Freny's sword and bullets hit it as usual.
public class CoopPuppet : MonoBehaviour, IDamageable {

    private CoopArena arena;
    private ushort id;
    private readonly Track track = new Track();
    private Animator anim;
    private SpriteRenderer[] sprites;
    private Collider2D body;
    private int animState;
    private float flashUntil;
    private int health = 1;

    public int Health { get => health; set => health = value; }

    public void Init(CoopArena owner, ushort enemyId) {
        arena = owner;
        id = enemyId;
        anim = GetComponentInChildren<Animator>();
        if (anim != null) {
            anim.fireEvents = false;
        }
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        body = GetComponent<Collider2D>();
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true)) {
            col.isTrigger = true;
            if (col.gameObject != gameObject && col.GetComponent<IDamageable>() == null) {
                col.gameObject.AddComponent<CoopPuppetPart>().Owner = this;
            }
        }
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
    }

    public void Receive(float time, Vector2 position, sbyte facing, int state, byte newHealth) {
        track.Add(time, position);
        if (newHealth < health) {
            flashUntil = Time.unscaledTime + CoopArena.FlashSeconds;
        }
        health = newHealth;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        if (anim != null && state != 0 && state != animState && anim.isActiveAndEnabled) {
            animState = state;
            anim.Play(state, 0, 0f);
        }
    }

    public void Draw(float time) {
        if (track.HasData) {
            transform.position = track.At(time);
        }
        bool flash = Time.unscaledTime < flashUntil;
        foreach (SpriteRenderer sprite in sprites) {
            if (sprite != null) {
                sprite.color = flash ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
        }
        if (body != null) {
            arena.TouchedBy(body);
        }
    }

    public void Damage() {
        flashUntil = Time.unscaledTime + CoopArena.FlashSeconds;
        arena.RequestHit(id);
    }
}

// A child collider of a puppet (some enemies keep their body lower in the hierarchy).
public class CoopPuppetPart : MonoBehaviour, IDamageable {
    public CoopPuppet Owner;
    public int Health { get => Owner.Health; set => Owner.Health = value; }
    public void Damage() => Owner.Damage();
}

public static class CoopStrip {
    // removes everything except what draws it; the object must be inactive, so nothing has woken up
    public static void ToVisuals(GameObject root, bool keepColliders = false) {
        for (int pass = 0; pass < 3; pass++) {   // a component required by another goes on a later pass
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) {
                if (component is Transform || component is SpriteRenderer || component is Animator) {
                    continue;
                }
                if (keepColliders && (component is Collider2D || component is Rigidbody2D)) {
                    continue;
                }
                Object.DestroyImmediate(component);
            }
        }
    }
}
