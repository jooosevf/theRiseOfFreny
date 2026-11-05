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
// The enemies go after the nearer Freny (CoopWorld); a hit on the client's Freny travels as a message.
// Each player has lives (WatchRevive); the host starts the next run when both are out of them.
// Props, chests and pickups are CoopShared; shots, grenades and portals are drawn on both sides.
// Limits for now: a net doesn't catch a puppet; Ciclope, mines and acid only hurt the host; the other
// player's helmet and looks don't show.
public class CoopArena : MonoBehaviour {

    private const float SendInterval = 0.05f;        // 20 messages a second per stream
    private const float RenderDelay = 0.1f;          // how far in the past the other side is drawn
    private const float HitFlashSeconds = 0.12f;
    private const int MaxEnemyBytes = 1200;
    private const float NameTagHeight = 0.9f;
    private const float RestartDelay = 4f;
    private const float SpawnGap = 2f;               // each one starts this far to his side of the scene's spawn
    private const float ReviveGap = 1.2f;            // and comes back this far from the other
    private const float NameSendSeconds = 3f;
    private const int MaxNameBytes = 32;
    private const byte CyclopsKind = 254;            // it isn't in the director's list: it has a field of its own
    private const float CyclopsBodyLift = 1.6f;      // Cyclops.Init: the art grows from its middle, its feet are at the bottom
    // José, 2026-11-04: each player has lives. A fall costs one and he comes back beside the other; with
    // none left he watches, until the other picks up a life for him. Both out: the run ends.
    private const int StartingLives = 2;
    private const float ReviveSeconds = 3f;
    private const float ReviveSafeSeconds = 2f;
    private static readonly Color GhostTint = new Color(0.55f, 0.8f, 1f);

    private Player player;
    private Collider2D playerCol;
    private Animator playerAnim;
    private ArenaDirector director;
    private FieldInfo enemiesField;
    private FieldInfo cyclopsField;
    private ComboManager combo;
    private ComboManager Combo => combo != null ? combo : (combo = FindAnyObjectByType<ComboManager>());
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
    private int hurtsTaken;
    private int hexesSeen;
    private int peerShots;
    private int peerGrenades;
    private int peerPortals;
    private static bool testSpawned;
    private static bool testGift;
    private static bool testBoss;
    private int ghostFrames;
    private int ghostFramesInState;
    private int ghostStates;
    private float bothDownAt = -1f;
    private float reviveAt = -1f;
    private bool fallHandled;
    private int lives = StartingLives;
    private byte peerLives = StartingLives;
    private byte peerReviveIn;
    private float nextTestDeath;
    private bool leaveOpen;
    private const float FloorProbe = 3f;
    private string localName;
    private string peerName;
    private float nextNameSend;
    private Canvas leaveCanvas;
    private static bool testDied;
    private static float firstStart = -1f;
    private GUIStyle overlay;
    private GUIStyle nameTag;
    private GUIStyle reviveStyle;

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
        cyclopsField = typeof(ArenaDirector).GetField("cyclopsTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (!isHost) {
            if (director != null) {
                director.enabled = false;
                // the Escudeiro, the Mina-aranha and the Ciclope are built at runtime: the same list
                // and the same templates as the host, without the director running
                typeof(ArenaDirector).GetMethod("AddRuntimeEnemies", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
            }
            foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include)) {
                spawner.enabled = false;
            }
        }
        CoopNet.PlayerState += OnPlayerState;
        CoopNet.EnemyState += OnEnemyState;
        CoopNet.EnemyHit += OnEnemyHit;
        CoopNet.Hurt += OnHurt;
        CoopNet.HexCast += OnHexCast;
        CoopNet.World += OnWorld;
        CoopNet.Effect += OnEffect;
        CoopNet.PeerName += OnPeerName;
        CoopWorld.Active = true;
        CoopWorld.GainLife = OnLifeGained;
        GameCursor.ShowWhile(this, () => leaveOpen);
        puppetHolder = new GameObject("Coop Puppets (inactive)");
        puppetHolder.SetActive(false);
    }

    private void OnDestroy() {
        CoopNet.PlayerState -= OnPlayerState;
        CoopNet.EnemyState -= OnEnemyState;
        CoopNet.EnemyHit -= OnEnemyHit;
        CoopNet.Hurt -= OnHurt;
        CoopNet.HexCast -= OnHexCast;
        CoopNet.World -= OnWorld;
        CoopNet.Effect -= OnEffect;
        CoopNet.PeerName -= OnPeerName;
        CoopWorld.Active = false;
        CoopWorld.GainLife = null;
        CoopWorld.PeerAlive = false;
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
        CoopWorld.LocalBody = playerCol;
        MakeGhost(playerObj);
        localName = CoopScreens.PlayerName;
        PlaceAt(player.transform.position + Vector3.right * (isHost ? -SpawnGap : SpawnGap));
        Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} nasceu em {player.transform.position}");
        FieldInfo chestField = typeof(ArenaDirector).GetField("chestPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        CoopShared.Begin(director != null ? (GameObject)chestField.GetValue(director) : null);
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

    // moves him there if there is floor under it; if not, he stays where he is
    private void PlaceAt(Vector3 point) {
        if (Physics2D.Raycast(point, Vector2.down, FloorProbe, player.whatIsGround).collider == null) {
            return;
        }
        player.transform.position = point;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) {
            body.position = point;
        }
    }

    private void OnPeerName(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out FixedString64Bytes name);
        peerName = name.ToString();
    }

    private void SendName() {
        using FastBufferWriter writer = new FastBufferWriter(MaxNameBytes * 3, Allocator.Temp);
        writer.WriteValueSafe(new FixedString64Bytes(localName ?? ""));
        CoopNet.Send("coop.name", writer, NetworkDelivery.ReliableSequenced);
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
            // these layers show this player's own look, not the other's
            if (sprite.name.StartsWith("Look ")) {
                sprite.enabled = false;
            }
        }
        if (isHost && playerCol != null) {
            // what the enemies reach for: a trigger, so it doesn't push anything or stop a bullet
            Bounds bounds = playerCol.bounds;
            BoxCollider2D box = copy.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = bounds.size;
            box.offset = bounds.center - source.transform.position;
            CoopWorld.PeerBody = box;
            CoopWorld.PeerTransform = copy.transform;
        }
        copy.SetActive(false);
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
    }

    private void Update() {
        // Esc: a room has no pause, only a way out
        if (Input.GetKeyDown(KeyCode.Escape)) {
            ShowLeave(!leaveOpen);
        }
        float now = Time.unscaledTime;
        if (now >= nextNameSend && localName != null) {
            nextNameSend = now + NameSendSeconds;
            SendName();
        }
        if (now >= nextSend && player != null) {
            nextSend = now + SendInterval;
            SendPlayer();
            if (isHost) {
                SendEnemies();
            }
        }
        DrawGhost(now);
        DrawPuppets(now);
        WatchRevive(now);
        WatchRunEnd(now);
        if (now - rateStart >= 1f) {
            receivedRate = received;
            received = 0;
            rateStart = now;
        }
        if (ghostAnim != null && ghostAnim.isActiveAndEnabled) {
            ghostFrames++;
            if (ghostAnim.GetCurrentAnimatorStateInfo(0).fullPathHash == ghostState) {
                ghostFramesInState++;
            }
        }
        if (now >= nextLog) {
            nextLog = now + 5f;
            Debug.Log($"[Coop] parceiro: animação igual em {(ghostFrames > 0 ? 100 * ghostFramesInState / ghostFrames : 0)}% dos quadros, Moving {(ghostAnim != null && ghostAnim.isActiveAndEnabled && ghostAnim.GetBool("Moving"))}, estados vistos {ghostStates}");
            Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} RTT {CoopNet.RttSummary()} | transporte {CoopNet.TransportRtt} ms | {receivedRate} msg/s recebidas | {CoopNet.BytesPerSecond} B/s enviados | {puppets.Count} bonecos | golpes do cliente {remoteHits}, mortes {remoteKills} | golpes recebidos {hurtsTaken}, feitiços {hexesSeen} | visando o outro {TargetingPeer()} | {CoopShared.Counts()} | ganhou {CoopShared.CoinsGranted} moedas, {CoopShared.ItemsGranted} itens, quebrou {CoopShared.PropsBrokenByAsk} a pedido | do outro: {peerShots} tiros, {peerGrenades} granadas, {peerPortals} portais | power-ups {ArenaPowerUps.InstaKillOn}");
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
        writer.WriteValueSafe(CoopAnim.Bools(playerAnim));
        writer.WriteValueSafe((byte)(player.IsDead ? 1 : 0));
        writer.WriteValueSafe((byte)Mathf.Max(0, player.Health));
        writer.WriteValueSafe((byte)Mathf.CeilToInt(ReviveLeft));
        writer.WriteValueSafe((byte)Mathf.Clamp(lives, 0, 255));
        CoopNet.Send("coop.p", writer, NetworkDelivery.UnreliableSequenced);
    }

    private void OnPlayerState(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out Vector2 position);
        reader.ReadValueSafe(out sbyte facing);
        reader.ReadValueSafe(out int state);
        reader.ReadValueSafe(out float time);
        reader.ReadValueSafe(out byte bools);
        reader.ReadValueSafe(out byte dead);
        reader.ReadValueSafe(out byte health);
        reader.ReadValueSafe(out peerReviveIn);
        reader.ReadValueSafe(out peerLives);
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
        CoopAnim.SetBools(ghostAnim, bools);   // or the state machine leaves the state for lack of them
        if (ghostAnim != null && state != 0 && state != ghostState && ghostAnim.isActiveAndEnabled) {
            ghostState = state;
            ghostStates++;
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
        CoopWorld.PeerAlive = visible;
        CoopWorld.PeerHealth = peerHealth;
    }

    private void OnWorld(ulong sender, FastBufferReader reader) {
        CoopShared.OnWorld(sender, reader, player);
    }

    private void OnEffect(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out byte raw);
        reader.ReadValueSafe(out Vector2 position);
        switch ((CoopWorld.Fx)raw) {
            case CoopWorld.Fx.Shot:
                reader.ReadValueSafe(out Vector2 direction);
                if (player != null) {
                    player.DrawPeerShot(position, direction);
                    peerShots++;
                }
                break;
            case CoopWorld.Fx.Grenade:
                reader.ReadValueSafe(out Vector2 velocity);
                reader.ReadValueSafe(out float fuse);
                if (player != null) {
                    player.DrawPeerGrenade(position, velocity, fuse);
                    peerGrenades++;
                }
                break;
            case CoopWorld.Fx.Portal:
                reader.ReadValueSafe(out float duration);
                reader.ReadValueSafe(out Color color);
                Fx.Portal(position, duration, color);
                peerPortals++;
                break;
            case CoopWorld.Fx.LifeGift:
                OnLifeGift();
                break;
        }
    }

    private float ReviveLeft => reviveAt < 0f ? 0f : Mathf.Max(0f, reviveAt - Time.unscaledTime);

    // Esc: the same kind of page as the pause menu, but the game goes on behind it
    private void ShowLeave(bool show) {
        leaveOpen = show;
        if (leaveCanvas == null) {
            if (!show) {
                return;
            }
            leaveCanvas = MenuKit.CreateCanvas("Coop Leave", 30000, transform);
            MenuStack stack = leaveCanvas.gameObject.AddComponent<MenuStack>();
            MenuPage page = new MenuPage(stack.transform, "Sala", 420f, new Vector2(0.5f, 0.5f));
            page.Button("Continuar", () => ShowLeave(false));
            page.Button("Sair da sala", CoopNet.Leave);
            stack.Push(page);
        }
        leaveCanvas.gameObject.SetActive(show);
    }

    private bool LocalOut => player != null && player.IsDead && lives <= 0 && reviveAt < 0f;
    private bool PeerOut => !CoopNet.Connected || (peerDead && peerLives == 0 && peerReviveIn == 0);

    private void WatchRevive(float now) {
        if (player == null) {
            return;
        }
        if (!player.IsDead) {
            reviveAt = -1f;
            fallHandled = false;
            return;
        }
        if (!fallHandled) {
            fallHandled = true;
            SpendLife(now);
            Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} caiu, vidas {lives}, {(reviveAt >= 0f ? "volta logo" : "fora")}");
        }
        if (reviveAt >= 0f && now >= reviveAt) {
            // beside the other if he is up; if not, where he fell
            bool besidePeer = !peerDead && CoopNet.Connected && ghost != null && ghost.gameObject.activeSelf;
            player.Revive(besidePeer ? ghost.position : player.transform.position, player.MaxHealth, ReviveSafeSeconds);
            if (besidePeer) {
                PlaceAt(ghost.position + Vector3.right * (isHost ? -ReviveGap : ReviveGap));
            }
            reviveAt = -1f;
            fallHandled = false;
            Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} voltou, vidas {lives}");
        }
    }

    private void SpendLife(float now) {
        if (lives > 0) {
            lives--;
            reviveAt = now + ReviveSeconds;
        }
    }

    // this player picked up a life (ItemPickup)
    private bool OnLifeGained() {
        if (PeerOut && CoopNet.Connected) {
            CoopWorld.GiveLifeToPeer();
            Debug.Log("[Coop] vida entregue ao parceiro, que estava fora");
            return true;
        }
        lives++;
        Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} pegou uma vida, vidas {lives}");
        return true;
    }

    private void OnLifeGift() {
        lives++;
        if (player != null && player.IsDead && reviveAt < 0f) {
            SpendLife(Time.unscaledTime);
        }
        Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} ganhou a vida do parceiro");
    }

    // the host decides when the run is over, so both sides restart together
    private void WatchRunEnd(float now) {
        if (!isHost || player == null) {
            return;
        }
        if (!LocalOut || !PeerOut) {
            bothDownAt = -1f;
            return;
        }
        if (bothDownAt < 0f) {
            bothDownAt = now;
            Debug.Log($"[Coop] os dois caíram, {(UIManager.Instance ? UIManager.Instance.Score : 0)} pontos");
        } else if (now - bothDownAt >= RestartDelay) {
            bothDownAt = float.MaxValue;
            CoopNet.RestartRun();
        }
    }

    // ---- what the host tells the client's Freny
    private void OnHurt(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out byte amount);
        reader.ReadValueSafe(out Vector2 push);
        reader.ReadValueSafe(out byte heavy);
        reader.ReadValueSafe(out FixedString32Bytes source);
        if (player == null) {
            return;
        }
        Player.NextDamageSource = source.ToString();
        if (player.TryDamage(amount)) {
            hurtsTaken++;
            player.Knockback(push, heavy != 0);
        }
    }

    private void OnHexCast(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out Vector2 position);
        reader.ReadValueSafe(out Vector2 direction);
        reader.ReadValueSafe(out float speed);
        reader.ReadValueSafe(out byte damage);
        reader.ReadValueSafe(out float push);
        reader.ReadValueSafe(out float homingTurn);
        GameObject prefab = HexballPrefab();
        if (prefab == null) {
            return;
        }
        HexballEffect effect = Instantiate(prefab, position, Quaternion.identity).GetComponent<HexballEffect>();
        if (effect != null) {
            effect.speedMultiplier = speed;
            effect.damage = damage;
            effect.knockback = push;
            effect.SetDirection(direction);
            if (homingTurn > 0f && player != null) {
                effect.homingTarget = player;
                effect.homingTurn = homingTurn;
            }
        }
        hexesSeen++;
    }

    private GameObject HexballPrefab() {
        ArenaDirector.Entry[] entries = Entries();
        if (entries != null) {
            foreach (ArenaDirector.Entry entry in entries) {
                Sorcerer sorcerer = entry.prefab != null ? entry.prefab.GetComponent<Sorcerer>() : null;
                if (sorcerer != null && sorcerer.hexballEffectPrefab != null) {
                    return sorcerer.hexballEffectPrefab;
                }
            }
        }
        return null;
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
        ComboManager comboNow = Combo;
        writer.WriteValueSafe(comboNow != null ? comboNow.GetCombo() : 1f);
        writer.WriteValueSafe(comboNow != null ? comboNow.TimeLeft : 0f);
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
            writer.WriteValueSafe(CoopAnim.Bools(anim));
            writer.WriteValueSafe(anim == null ? CoopAnim.FrameHash(sprite) : 0);
            writer.WriteValueSafe((byte)Mathf.Clamp(enemy.Health, 0, 255));
        }
        CoopNet.Send("coop.e", writer, NetworkDelivery.UnreliableSequenced);
    }

    // the prefab's place in the director's list, which starts the same on both sides; an enemy the
    // director only builds at runtime (Escudeiro, Mina-aranha, Ciclope) has none and shows as a Golem
    private byte KindOf(Enemy enemy) {
        if (enemy is Cyclops) {
            return CyclopsKind;
        }
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
        if (UIManager.Instance && UIManager.Instance.Score != hostScore) {
            UIManager.Instance.UpdateScore(hostScore - UIManager.Instance.Score, true);   // one score for the room
        }
        reader.ReadValueSafe(out float comboValue);
        reader.ReadValueSafe(out float comboLeft);
        if (Combo != null) {
            Combo.ShowFromHost(comboValue, comboLeft);   // and one combo
        }
        reader.ReadValueSafe(out ushort count);
        float now = Time.unscaledTime;
        seen.Clear();
        for (int i = 0; i < count; i++) {
            reader.ReadValueSafe(out ushort id);
            reader.ReadValueSafe(out byte kind);
            reader.ReadValueSafe(out Vector2 position);
            reader.ReadValueSafe(out sbyte facing);
            reader.ReadValueSafe(out int state);
            reader.ReadValueSafe(out byte bools);
            reader.ReadValueSafe(out int frame);
            reader.ReadValueSafe(out byte health);
            seen.Add(id);
            if (!puppets.TryGetValue(id, out CoopPuppet puppet)) {
                puppet = MakePuppet(id, kind, position);
                if (puppet == null) {
                    continue;
                }
                puppets[id] = puppet;
            }
            puppet.Receive(now, position, facing, state, bools, frame, health);
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
        GameObject prefab = kind == CyclopsKind && cyclopsField != null && director != null ? (GameObject)cyclopsField.GetValue(director) : null;
        if (entries != null) {
            if (prefab == null && kind < entries.Length) {
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
        // what these two set on their sprite in Init, which a puppet never runs
        SpriteRenderer body = copy.GetComponentInChildren<SpriteRenderer>(true);
        if (kind == CyclopsKind && body != null) {
            body.transform.localScale = Vector3.one * Cyclops.Size;
            body.transform.localPosition = new Vector3(0f, CyclopsBodyLift * (Cyclops.Size - 1f), 0f);
        } else if (prefab.GetComponent<SpiderMine>() != null && body != null) {
            body.transform.localScale = Vector3.one;
        }
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
    public void RequestHit(ushort id, int amount) {
        using FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe((byte)Mathf.Clamp(amount, 1, 255));
        CoopNet.Send("coop.h", writer, NetworkDelivery.ReliableSequenced);
    }

    private void OnEnemyHit(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out ushort id);
        reader.ReadValueSafe(out byte amount);
        if (enemiesById.TryGetValue(id, out Enemy enemy) && enemy != null && !enemy.IsDead) {
            enemy.Damage(amount);
            remoteHits++;
            if (enemy.IsDead) {
                remoteKills++;
            }
        }
    }

    private int TargetingPeer() {
        int count = 0;
        if (isHost) {
            foreach (Enemy enemy in Enemy.Active) {
                if (enemy != null && enemy.AimsAtPeer) {
                    count++;
                }
            }
        }
        return count;
    }

    public static float FlashSeconds => HitFlashSeconds;

    // ---- the numbers on screen: this is what the test is for
    private void OnGUI() {
        if (overlay == null) {
            int unit = Mathf.Max(9, Mathf.RoundToInt(Screen.height / 60f));
            overlay = new GUIStyle(GUI.skin.box) { font = MenuKit.Font, fontSize = unit, alignment = TextAnchor.UpperLeft, wordWrap = true };
            overlay.normal.textColor = MenuKit.TextColor;
            nameTag = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, fontSize = unit, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            reviveStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, fontSize = unit * 2, alignment = TextAnchor.MiddleCenter };
            reviveStyle.normal.textColor = MenuKit.TitleColor;
        }
        if (ghost != null && ghost.gameObject.activeSelf) {
            DrawName(ghost.position, string.IsNullOrEmpty(peerName) ? "Parceiro" : peerName, GhostTint);
        }
        if (player != null && !player.IsDead) {
            DrawName(player.transform.position, localName, MenuKit.TextColor);
        }
        float scale = Screen.height / 720f;
        Rect panel = new Rect(Screen.width - 330f * scale, 72f * scale, 320f * scale, 76f * scale);
        string text = $"Ping {CoopNet.LastRtt:0} ms\n" +
                      $"Suas vidas: {lives}\n" +
                      $"Vidas do parceiro: {peerLives}" +
                      (peerDead && CoopNet.Connected ? (peerReviveIn > 0 ? $" (volta em {peerReviveIn} s)" : " (fora)") : "");
        GUI.Box(panel, text, overlay);
        if (player != null && player.IsDead) {
            string wait = reviveAt >= 0f ? $"Você volta em {Mathf.CeilToInt(ReviveLeft)} s"
                : PeerOut ? "Os dois caíram"
                : "Sem vidas. Uma vida pega pelo parceiro traz você de volta";
            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f), wait, reviveStyle);
        }
    }

    private void DrawName(Vector3 feetOrHead, string name, Color color) {
        Camera cam = Camera.main;
        if (cam == null || string.IsNullOrEmpty(name)) {
            return;
        }
        Vector3 point = cam.WorldToScreenPoint(feetOrHead + Vector3.up * NameTagHeight);
        Rect rect = new Rect(point.x - 150f, Screen.height - point.y - 14f, 300f, 28f);
        nameTag.normal.textColor = Color.black;   // readable over the bright sky too
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), name, nameTag);
        nameTag.normal.textColor = color;
        GUI.Label(rect, name, nameTag);
    }

    // ---- only for the test binaries, driven from the command line
    private void TestRunner(float now) {
        string shots = CoopArgs.ShotDir;
        if (firstStart < 0f) {
            firstStart = now;
        }
        float elapsed = now - firstStart;
        // test only: a Sorcerer next to the client's Freny, and an Insta-kill for him to pick up
        if (CoopArgs.Has("-coopSpawnTest") && isHost && !testSpawned && elapsed >= 12f && ghost != null) {
            testSpawned = true;
            GameObject prefab = null;
            foreach (ArenaDirector.Entry entry in Entries()) {
                if (entry.prefab != null && entry.prefab.GetComponent<Sorcerer>() != null) {
                    prefab = entry.prefab;
                }
            }
            if (prefab != null) {
                Instantiate(prefab, ghost.position + new Vector3(4f, 0.5f, 0f), Quaternion.identity);
            }
            ItemPickup.Spawn(ItemPickup.Kind.InstaKill, 1, ghost.position + Vector3.up, Vector2.zero);
            ItemPickup.Spawn(ItemPickup.Kind.Life, 1, ghost.position + Vector3.right + Vector3.up, Vector2.zero);
            Debug.Log($"[Coop] teste: feiticeiro {(prefab != null ? "criado" : "não existe na lista")} e Insta-kill ao lado do cliente");
        }
        if (CoopArgs.Has("-coopSpawnTest") && isHost && !testGift && elapsed >= 30f && ghost != null) {
            testGift = true;
            ItemPickup.Spawn(ItemPickup.Kind.Life, 1, ghost.position + Vector3.up, Vector2.zero);
            Debug.Log($"[Coop] teste: segunda vida ao lado do cliente, host {(LocalOut ? "fora" : "de pé")}");
        }
        if (CoopArgs.Has("-coopBossTest") && isHost && !testBoss && elapsed >= 10f && director != null) {
            testBoss = true;
            director.StartCoroutine("SpawnBoss");
            Debug.Log("[Coop] teste: Ciclope chamado");
        }
        float dieEvery = CoopArgs.DieEvery;
        if (dieEvery > 0f && player != null && !player.IsDead && elapsed >= nextTestDeath && elapsed >= dieEvery) {
            nextTestDeath = elapsed + dieEvery;
            player.TryDamage(99, false);
        }
        float dieAt = CoopArgs.DieAt;
        if (dieAt > 0f && !testDied && elapsed >= dieAt && player != null) {
            testDied = true;
            Debug.Log($"[Coop] teste: {(isHost ? "host" : "cliente")} cai agora");
            player.TryDamage(99, false);
        }
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
    private const float JumpDistance = 4f;   // farther than this between two messages: he crossed the wrap, not the map
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
                if ((points[newer] - points[older]).sqrMagnitude > JumpDistance * JumpDistance) {
                    return points[newer];   // it never flies across the screen
                }
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
    private SpriteRenderer body;
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
        body = GetComponentInChildren<SpriteRenderer>(true);
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

    public void Receive(float time, Vector2 position, sbyte facing, int state, byte bools, int frame, byte newHealth) {
        track.Add(time, position);
        if (newHealth < health) {
            flashUntil = Time.unscaledTime + CoopArena.FlashSeconds;
        }
        health = newHealth;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        CoopAnim.SetBools(anim, bools);
        if (anim != null && state != 0 && state != animState && anim.isActiveAndEnabled) {
            animState = state;
            anim.Play(state, 0, 0f);
        }
        if (anim == null && frame != 0 && body != null && CoopAnim.Frame(frame) is Sprite sprite) {
            body.sprite = sprite;   // the ones drawn by FrameAnimator have no Animator to follow
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
    }

    public void Damage() {
        DamageBy(1);
    }

    public void DamageBy(int amount) {
        flashUntil = Time.unscaledTime + CoopArena.FlashSeconds;
        arena.RequestHit(id, amount);
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
                if (component is Transform || component is SpriteRenderer || component is Animator || component is SpriteSkin) {
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

// What an enemy's or Freny's Animator is doing, in a form that fits a message. Both sides have the same
// controllers, so the Nth bool is the same bool, and a sprite's name is the same frame.
public static class CoopAnim {
    private const int MaxBools = 8;
    private static readonly string[] FrameFolders = { "Cyclops", "Shielded", "SpiderMine" };

    private static readonly Dictionary<RuntimeAnimatorController, int[]> boolsByController = new Dictionary<RuntimeAnimatorController, int[]>();
    private static readonly Dictionary<Sprite, int> hashes = new Dictionary<Sprite, int>();
    private static Dictionary<int, Sprite> frames;

    private static int[] BoolsOf(Animator animator) {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null) {
            return System.Array.Empty<int>();
        }
        if (!boolsByController.TryGetValue(controller, out int[] bools)) {
            List<int> found = new List<int>();
            foreach (AnimatorControllerParameter parameter in animator.parameters) {
                if (parameter.type == AnimatorControllerParameterType.Bool && found.Count < MaxBools) {
                    found.Add(parameter.nameHash);
                }
            }
            bools = found.ToArray();
            boolsByController[controller] = bools;
        }
        return bools;
    }

    public static byte Bools(Animator animator) {
        if (animator == null || !animator.isActiveAndEnabled) {
            return 0;
        }
        int[] bools = BoolsOf(animator);
        int mask = 0;
        for (int i = 0; i < bools.Length; i++) {
            if (animator.GetBool(bools[i])) {
                mask |= 1 << i;
            }
        }
        return (byte)mask;
    }

    public static void SetBools(Animator animator, byte mask) {
        if (animator == null || !animator.isActiveAndEnabled) {
            return;
        }
        int[] bools = BoolsOf(animator);
        for (int i = 0; i < bools.Length; i++) {
            animator.SetBool(bools[i], (mask & (1 << i)) != 0);
        }
    }

    public static int FrameHash(SpriteRenderer renderer) {
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite == null) {
            return 0;
        }
        if (!hashes.TryGetValue(sprite, out int hash)) {
            hash = Animator.StringToHash(sprite.name);
            hashes[sprite] = hash;
        }
        return hash;
    }

    public static Sprite Frame(int hash) {
        if (frames == null) {
            frames = new Dictionary<int, Sprite>();
            foreach (string folder in FrameFolders) {
                foreach (Sprite sprite in Resources.LoadAll<Sprite>(folder)) {
                    frames[Animator.StringToHash(sprite.name)] = sprite;
                }
            }
        }
        return frames.TryGetValue(hash, out Sprite found) ? found : null;
    }
}
