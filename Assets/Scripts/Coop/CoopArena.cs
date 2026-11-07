using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// What the players in a room see of each other in the Arena.
// Each one runs his own Freny and sends where he is; the others draw a ghost a little in the past,
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
    private const float SpawnGap = 2f;               // how far apart they start, around the scene's spawn
    private const float ReviveGap = 1.2f;            // and how far from the other one comes back
    private const int MaxSnapshotEnemies = 50;
    private const byte CyclopsKind = 254;            // it isn't in the director's list: it has a field of its own
    private const float CyclopsBodyLift = 1.6f;      // Cyclops.Init: the art grows from its middle, its feet are at the bottom
    // José, 2026-11-04: each player has lives. A fall costs one and he comes back beside the other; with
    // none left he watches, until another picks up a life for him. All out: the run ends.
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
    private ArenaDayNight dayNight;
    private ArenaDayNight DayNight => dayNight != null ? dayNight : (dayNight = FindAnyObjectByType<ArenaDayNight>());
    private bool isHost;
    private float nextSend;
    private float startedAt;
    private float nextLog;
    private int nextShot;
    private int received;
    private int receivedRate;
    private float rateStart;

    // one per other player in the room: his body on this screen
    private class Peer {
        public ulong Id;
        public Transform Ghost;
        public Animator Anim;
        public FrenyMotion Motion;
        public readonly Track Track = new Track();
        public int State;
        public bool Dead;
        public bool Shielded;   // picking a blessing: the enemies leave him alone
        public int Lives = StartingLives;
        public byte ReviveIn;
        public CoopBody Body;
        public bool Out => Dead && Lives == 0 && ReviveIn == 0;
    }
    private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
    private readonly List<ulong> left = new List<ulong>();
    private GameObject ghostSource;

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
    private int peerSlashes;
    private int bossAttacks;
    private static readonly int MeleeState = Animator.StringToHash("Base Layer.Melee Attack");
    private static bool testSpawned;
    private static bool testGift;
    private static bool testBoss;
    private static bool testGrenade;
    private static bool testKey;
    private static bool testChest;
    private int peerSpells;
    private int killsCredited;
    private float bothDownAt = -1f;
    private float reviveAt = -1f;
    private bool fallHandled;
    private int lives = StartingLives;
    private float nextTestDeath;
    private bool leaveOpen;
    private const float FloorProbe = 3f;
    private string localName;
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
        CoopNet.LifeGift += OnLifeGift;
        CoopWorld.Active = true;
        CoopWorld.DifficultyFactor = CoopNet.Difficulty;
        CoopWorld.Bodies.Clear();
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
        CoopNet.LifeGift -= OnLifeGift;
        CoopWorld.Active = false;
        CoopWorld.GainLife = null;
        CoopWorld.DifficultyFactor = 1;
        CoopWorld.BossesShown = 0;
        CoopWorld.Bodies.Clear();
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
        ghostSource = playerObj;
        localName = CoopScreens.PlayerName;
        int players = Mathf.Max(1, CoopNet.Roster.Count);
        float slot = CoopNet.Slot(CoopNet.LocalId) - (players - 1) * 0.5f;
        PlaceAt(player.transform.position + Vector3.right * (slot * SpawnGap));
        Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} nasceu em {player.transform.position}");
        FieldInfo chestField = typeof(ArenaDirector).GetField("chestPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        CoopShared.Begin(director != null ? (GameObject)chestField.GetValue(director) : null);
        // the staff casts the sorcerers' ball, which it learns from a sorcerer coming in: the client
        // never has a real one, and the host needs it to draw the others' spells before one comes
        MageStaff.Remember(HexballPrefab());
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

    // a copy of Freny with only his sprites and animator left
    private Peer MakeGhost(ulong id) {
        GameObject source = ghostSource;
        Peer peer = new Peer { Id = id };
        GameObject copy = Instantiate(source, puppetHolder.transform);
        CoopStrip.ToVisuals(copy, keepMotion: true);
        // the source's own Head and Sword layers: FrenyMotion makes this copy's when it starts
        foreach (SpriteRenderer layer in copy.GetComponentsInChildren<SpriteRenderer>(true)) {
            if (layer.name == "Head" || layer.name == "Sword") {
                Destroy(layer.gameObject);
            }
        }
        peer.Motion = copy.GetComponent<FrenyMotion>();
        copy.name = "Coop Ghost " + id;
        copy.tag = "Untagged";
        copy.transform.SetParent(null);
        peer.Ghost = copy.transform;
        peer.Anim = copy.GetComponentInChildren<Animator>();
        if (peer.Anim != null) {
            peer.Anim.fireEvents = false;   // the clips call the sword's hitbox, which the ghost doesn't have
        }
        foreach (SpriteRenderer sprite in copy.GetComponentsInChildren<SpriteRenderer>(true)) {
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
            peer.Body = new CoopBody { Owner = id, Transform = copy.transform, Collider = box };
            CoopWorld.Bodies.Add(peer.Body);
        }
        copy.SetActive(false);
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
        peers[id] = peer;
        return peer;
    }

    private void Update() {
        // Esc: a room has no pause, only a way out
        if (Input.GetKeyDown(KeyCode.Escape)) {
            ShowLeave(!leaveOpen);
        }
        float now = Time.unscaledTime;
        // before sending: in the frame he comes back the others must not read "dead, no lives, not
        // coming back" (they would think he is out and give him their life)
        WatchRevive(now);
        CoopWorld.Lives = lives;
        if (now >= nextSend && player != null) {
            nextSend = now + SendInterval;
            SendPlayer();
            if (isHost) {
                SendEnemies();
            }
        }
        DrawGhosts(now);
        DrawPuppets(now);
        WatchRunEnd(now);
        if (now - rateStart >= 1f) {
            receivedRate = received;
            received = 0;
            rateStart = now;
        }
        if (now >= nextLog) {
            nextLog = now + 5f;
            Debug.Log($"[Coop] {(isHost ? "host" : "cliente")} RTT {CoopNet.RttSummary()} | parceiros {peers.Count}, dificuldade {CoopWorld.DifficultyFactor}x, inimigos {(isHost ? Enemy.Active.Count : puppets.Count)}, vidas {lives} | {receivedRate} msg/s recebidas | {CoopNet.BytesPerSecond} B/s enviados | {puppets.Count} bonecos | golpes do cliente {remoteHits}, mortes {remoteKills} | golpes recebidos {hurtsTaken}, feitiços {hexesSeen} | visando o outro {TargetingPeer()} | {CoopShared.Counts()} | ganhou {CoopShared.CoinsGranted} moedas, {CoopShared.ItemsGranted} itens, quebrou {CoopShared.PropsBrokenByAsk} a pedido | dos outros: {peerShots} tiros, {peerGrenades} granadas, {peerPortals} portais, {peerSlashes} espadadas | escopeta {(player != null && player.ShotgunOwned)} | power-ups {ArenaPowerUps.InstaKillOn} | ataques do host copiados {bossAttacks}, Ciclopes {CoopWorld.BossesShown}, chaves {BossKey.Carried}, baús dourados {GoldenChest.Active.Count}, bênçãos {Blessings.Held.Count}{(ArenaRunVariety.PickerOpen ? " (escolhendo)" : "")}, cajado {(player != null && MageStaff.Held(player))}, feitiços dos outros {peerSpells} | mortes creditadas {killsCredited}, fúria {(player != null ? player.Rage : 0f):0.00}");
        }
        TestRunner(now);
    }

    // ---- Freny's state: each one sends his, and the host passes it on
    private void SendPlayer() {
        using FastBufferWriter writer = new FastBufferWriter(40, Allocator.Temp);
        writer.WriteValueSafe(CoopNet.LocalId);
        Vector2 position = player.transform.position;
        writer.WriteValueSafe(position);
        writer.WriteValueSafe((sbyte)(player.FacingRight ? 1 : -1));
        AnimatorStateInfo state = playerAnim != null ? playerAnim.GetCurrentAnimatorStateInfo(0) : default;
        writer.WriteValueSafe(state.fullPathHash);
        writer.WriteValueSafe(state.normalizedTime);
        writer.WriteValueSafe(CoopAnim.Bools(playerAnim));
        writer.WriteValueSafe((byte)((player.IsDead ? 1 : 0) | (CoopWorld.LocalShielded ? 2 : 0)));   // 2: picking a blessing
        writer.WriteValueSafe((byte)Mathf.Max(0, player.Health));
        writer.WriteValueSafe((byte)Mathf.CeilToInt(ReviveLeft));
        writer.WriteValueSafe((byte)Mathf.Clamp(lives, 0, 255));
        CoopNet.Send("coop.p", writer, NetworkDelivery.UnreliableSequenced);
    }

    private void OnPlayerState(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out ulong owner);
        reader.ReadValueSafe(out Vector2 position);
        reader.ReadValueSafe(out sbyte facing);
        reader.ReadValueSafe(out int state);
        reader.ReadValueSafe(out float time);
        reader.ReadValueSafe(out byte bools);
        reader.ReadValueSafe(out byte dead);
        reader.ReadValueSafe(out byte health);
        reader.ReadValueSafe(out byte reviveIn);
        reader.ReadValueSafe(out byte peerLives);
        received++;
        if (owner == CoopNet.LocalId || ghostSource == null) {
            return;
        }
        if (!peers.TryGetValue(owner, out Peer peer)) {
            peer = MakeGhost(owner);
        }
        peer.Track.Add(Time.unscaledTime, position);
        peer.Dead = (dead & 1) != 0;
        peer.Shielded = (dead & 2) != 0;
        peer.ReviveIn = reviveIn;
        peer.Lives = peerLives;
        if (peer.Body != null) {
            peer.Body.Health = health;
        }
        Vector3 scale = peer.Ghost.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        peer.Ghost.localScale = scale;
        CoopAnim.SetBools(peer.Anim, bools);   // or the state machine leaves the state for lack of them
        if (peer.Anim != null && state != 0 && state != peer.State && peer.Anim.isActiveAndEnabled) {
            peer.State = state;
            peer.Anim.Play(state, 0, Mathf.Repeat(time, 1f));
        }
    }

    private void DrawGhosts(float now) {
        left.Clear();
        foreach (Peer peer in peers.Values) {
            // someone who left the room: his Freny goes too
            if (CoopNet.Roster.Count > 0 && !CoopNet.InRoster(peer.Id)) {
                left.Add(peer.Id);
                continue;
            }
            bool visible = !peer.Dead;
            if (peer.Ghost.gameObject.activeSelf != visible) {
                peer.Ghost.gameObject.SetActive(visible);
                peer.State = 0;
            }
            peer.Ghost.position = peer.Track.At(now - RenderDelay);
            if (peer.Body != null) {
                peer.Body.Alive = visible && !peer.Shielded;
            }
        }
        foreach (ulong id in left) {
            Peer peer = peers[id];
            if (peer.Body != null) {
                CoopWorld.Bodies.Remove(peer.Body);
            }
            Destroy(peer.Ghost.gameObject);
            peers.Remove(id);
        }
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
                reader.ReadValueSafe(out byte shotgun);
                reader.ReadValueSafe(out ulong shooter);
                if (player != null) {
                    player.DrawPeerShot(position, direction, shotgun != 0);
                    peerShots++;
                }
                // his copy draws the gun he fired: the Ranged frames alone show the pistol
                if (peers.TryGetValue(shooter, out Peer gunner) && gunner.Motion != null) {
                    gunner.Motion.Shot(shotgun != 0);
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
            case CoopWorld.Fx.Slash:
                reader.ReadValueSafe(out ulong slasher);
                reader.ReadValueSafe(out byte step);
                if (peers.TryGetValue(slasher, out Peer peer) && peer.Ghost.gameObject.activeSelf) {
                    if (peer.Anim != null && peer.Anim.isActiveAndEnabled) {
                        peer.Anim.Play(MeleeState, 0, 0f);
                        peer.State = MeleeState;
                    }
                    if (peer.Motion != null) {
                        peer.Motion.Slash(step);
                    }
                    peerSlashes++;
                    if (peerSlashes % 3 == 1 && !string.IsNullOrEmpty(CoopArgs.ShotDir)) {
                        StartCoroutine(SlashShot());   // test binaries: a picture in the middle of a cut
                    }
                }
                break;
            // the host's attacks: a copy here, which only hurts this side's Freny
            case CoopWorld.Fx.Wave:
                reader.ReadValueSafe(out float dir);
                if (dir > 0f) {   // the smash sends two, one each way: the dust and the shake once
                    Fx.Dust(position, 24);
                    Sfx.Play("grenade_explode", position, 0.8f, 0.6f);
                    ScreenKick.Kick(0.35f, 0.5f);
                }
                Shockwave.Launch(position, dir);
                bossAttacks++;
                break;
            case CoopWorld.Fx.Flood:
                reader.ReadValueSafe(out float xMin);
                reader.ReadValueSafe(out float xMax);
                reader.ReadValueSafe(out float seconds);
                AcidPuddle.Flood(position.x, position.y, xMin, xMax, seconds);
                bossAttacks++;
                break;
            case CoopWorld.Fx.BeamCharge:
                reader.ReadValueSafe(out float charge);
                StartCoroutine(BeamCharge(position, charge));
                break;
            case CoopWorld.Fx.Beam:
                reader.ReadValueSafe(out Vector2 at);
                reader.ReadValueSafe(out float beamSeconds);
                BloodBeam.Fire(position, at, beamSeconds, player, null);
                Sfx.Play("grenade_explode", position, 0.9f, 0.4f);
                bossAttacks++;
                break;
            case CoopWorld.Fx.Net:
                reader.ReadValueSafe(out Vector2 netVelocity);
                reader.ReadValueSafe(out float netFacing);
                reader.ReadValueSafe(out float netFeet);
                NetProjectile.LaunchCopy(position, netVelocity, netFacing, netFeet);
                break;
            case CoopWorld.Fx.NetOpen:
                if (isHost) {
                    NetProjectile.CatchAt(position, false);   // the copy here already lies on the floor
                }
                break;
            case CoopWorld.Fx.Kill:
                // the host: one of this player's hits killed an enemy there (Fúria, Vampiro)
                if (player != null) {
                    player.CreditKill();
                    ArenaRunVariety run = player.GetComponent<ArenaRunVariety>();
                    if (run != null) {
                        run.CreditKill();
                    }
                    killsCredited++;
                    MultiKill.CreditedKill();   // the DOUBLE/TRIPLE KILL banners
                }
                break;
            case CoopWorld.Fx.Spider:
                reader.ReadValueSafe(out byte spiderDamage);
                SpiderMine.BlastCopy(position, spiderDamage);
                bossAttacks++;
                break;
            case CoopWorld.Fx.Spell:
                reader.ReadValueSafe(out Vector2 spellDir);
                MageStaff.CastCopy(position, spellDir);
                peerSpells++;
                break;
            case CoopWorld.Fx.Mine:
                reader.ReadValueSafe(out Vector2 mineVelocity);
                reader.ReadValueSafe(out float gravity);
                reader.ReadValueSafe(out float size);
                reader.ReadValueSafe(out float mineFuse);
                GolemMine.ThrowCopy(position, mineVelocity, gravity, size, mineFuse);
                bossAttacks++;
                break;
        }
    }

    // the Ciclope's eye charging before the beam (Cyclops.Beam), so the warning shows here too
    private IEnumerator BeamCharge(Vector3 eye, float seconds) {
        SpriteRenderer glow = BloodBeam.Orb("Eye Glow", null);
        const float beam = 0.8f;
        for (float t = 0f; t < seconds + beam && glow != null; t += Time.deltaTime) {
            float u = Mathf.Clamp01(t / seconds);
            glow.transform.position = eye;
            glow.transform.localScale = Vector3.one * (1.3f * (t < seconds ? 0.3f + 0.7f * u : 1.3f) * (1f + Mathf.Sin(Time.time * 40f) * 0.08f));
            yield return null;
        }
        if (glow != null) {
            Destroy(glow.gameObject);
        }
    }

    private IEnumerator GrenadeResult(int before) {
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log($"[Coop] teste: granada do outro, vida+armadura {before} -> {player.Health + player.Armor}");
    }

    private IEnumerator SlashShot() {
        yield return new WaitForSecondsRealtime(0.08f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(CoopArgs.ShotDir, $"slash_{(isHost ? "host" : "client")}_{peerSlashes}.png"));
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
    private bool AllPeersOut {
        get {
            foreach (Peer peer in peers.Values) {
                if (!peer.Out) {
                    return false;
                }
            }
            return true;
        }
    }

    private Peer FirstStanding() {
        foreach (Peer peer in peers.Values) {
            if (!peer.Dead && peer.Ghost.gameObject.activeSelf) {
                return peer;
            }
        }
        return null;
    }

    private Peer FirstOut() {
        foreach (Peer peer in peers.Values) {
            if (peer.Out) {
                return peer;
            }
        }
        return null;
    }

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
            // beside someone who is up; if nobody is, where he fell
            Peer standing = FirstStanding();
            player.Revive(standing != null ? standing.Ghost.position : player.transform.position, player.MaxHealth, ReviveSafeSeconds);
            if (standing != null) {
                PlaceAt(standing.Ghost.position + Vector3.right * (isHost ? -ReviveGap : ReviveGap));
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
        Peer outOfLives = FirstOut();
        if (outOfLives != null) {
            CoopNet.SendGift(outOfLives.Id);
            Debug.Log("[Coop] vida entregue a um parceiro que estava fora");
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

    // the host decides when the run is over, so everyone restarts together
    private void WatchRunEnd(float now) {
        if (!isHost || player == null) {
            return;
        }
        if (!LocalOut || !AllPeersOut) {
            bothDownAt = -1f;
            return;
        }
        if (bothDownAt < 0f) {
            bothDownAt = now;
            Debug.Log($"[Coop] todos caíram, {(UIManager.Instance ? UIManager.Instance.Score : 0)} pontos");
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
        reader.ReadValueSafe(out ulong homingAt);
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
            if (homingTurn > 0f && homingAt == CoopNet.LocalId && player != null) {
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
        writer.WriteValueSafe(DayNight != null ? DayNight.Elapsed : -1f);
        writer.WriteValueSafe((ushort)Mathf.Min(count, MaxSnapshotEnemies));
        int written = 0;
        foreach (Enemy enemy in alive) {
            if (enemy == null || enemy.IsDead || written >= MaxSnapshotEnemies) {
                continue;
            }
            written++;
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
            // the Ciclope's life is more than a byte: its share of the whole goes, for the bar
            int health = enemy is Cyclops cyclops ? Mathf.CeilToInt(cyclops.HealthFraction * 255f) : enemy.Health;
            writer.WriteValueSafe((byte)Mathf.Clamp(health, 0, 255));
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
        reader.ReadValueSafe(out float clock);
        if (clock >= 0f && DayNight != null) {
            DayNight.MatchClock(clock);   // and one sky: the Lua de Sangue comes at the same time
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
            // SpiderMine.Init: its art stands on the feet, a unit under the root; without this the
            // client drew it a unit up, walking on the air over the platforms
            body.transform.localScale = Vector3.one;
            body.transform.localPosition = new Vector3(0f, -1f, 0f);
        }
        copy.tag = "Untagged";
        CoopPuppet puppet = copy.AddComponent<CoopPuppet>();
        puppet.Init(this, id);
        if (kind == CyclopsKind) {
            int slot = 0;
            foreach (CoopPuppet other in puppets.Values) {
                if (other != null && other.IsBoss) {
                    slot++;
                }
            }
            puppet.MakeBossBar(slot);
        }
        copy.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
        copy.SetActive(true);
        return puppet;
    }

    private void DrawPuppets(float now) {
        int bosses = 0;
        foreach (CoopPuppet puppet in puppets.Values) {
            if (puppet != null) {
                puppet.Draw(now - RenderDelay);
                if (puppet.IsBoss) {
                    bosses++;
                }
            }
        }
        CoopWorld.BossesShown = bosses;   // the torches and the Ciclope's music (ArenaBossLoot.BossOut)
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
            CoopWorld.RemoteHit = true;   // a kill now is the client's: his Fúria fills, not the host's
            try {
                enemy.Damage(amount);
            } finally {
                CoopWorld.RemoteHit = false;
            }
            remoteHits++;
            if (enemy.IsDead) {
                remoteKills++;
                using FastBufferWriter credit = new FastBufferWriter(16, Allocator.Temp);
                credit.WriteValueSafe((byte)CoopWorld.Fx.Kill);
                credit.WriteValueSafe((Vector2)enemy.transform.position);
                CoopNet.SendTo(sender, "coop.fx", credit, NetworkDelivery.ReliableSequenced);
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
            reviveStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, fontSize = unit * 2, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            reviveStyle.normal.textColor = MenuKit.TitleColor;
        }
        foreach (Peer peer in peers.Values) {
            if (peer.Ghost.gameObject.activeSelf) {
                DrawName(peer.Ghost.position, CoopNet.NameOf(peer.Id) ?? "Parceiro", GhostTint);
            }
        }
        if (player != null && !player.IsDead) {
            DrawName(player.transform.position, localName, MenuKit.TextColor);
        }
        float scale = Screen.height / 720f;
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        if (!isHost) {
            text.Append($"Ping {CoopNet.LastRtt:0} ms\n");
        }
        foreach (Peer peer in peers.Values) {
            string state = peer.Out ? "fora" : peer.Dead && peer.ReviveIn > 0 ? $"volta em {peer.ReviveIn} s" : $"{peer.Lives} vidas";
            text.Append($"{CoopNet.NameOf(peer.Id) ?? "Parceiro"}: {state}\n");
        }
        Rect panel = new Rect(Screen.width - 330f * scale, 72f * scale, 320f * scale, (text.ToString().Split('\n').Length * 20f + 8f) * scale);
        GUI.Box(panel, text.ToString().TrimEnd(), overlay);
        if (player != null && player.IsDead) {
            string wait = reviveAt >= 0f ? $"Você volta em {Mathf.CeilToInt(ReviveLeft)} s"
                : AllPeersOut ? "Todos caíram"
                : "Sem vidas\nUma vida pega por um parceiro traz você de volta";
            DrawCentered(wait);
        }
    }

    // the fallen player's message: the first line big, the rest smaller, wrapped inside the screen
    // (one long line at the big size ran off both edges), with a dark edge to read over the arena
    private void DrawCentered(string text) {
        string[] parts = text.Split('\n');
        float width = Screen.width * 0.86f;
        float x = (Screen.width - width) / 2f;
        float y = Screen.height * 0.36f;
        int big = reviveStyle.fontSize;
        for (int i = 0; i < parts.Length; i++) {
            reviveStyle.fontSize = i == 0 ? big : Mathf.Max(9, Mathf.RoundToInt(big * 0.55f));
            float height = reviveStyle.CalcHeight(new GUIContent(parts[i]), width) + 8f;
            Rect r = new Rect(x, y, width, height);
            float o = Mathf.Max(1f, Screen.height / 540f);
            reviveStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), parts[i], reviveStyle);
            GUI.Label(new Rect(r.x - o, r.y + o, r.width, r.height), parts[i], reviveStyle);
            reviveStyle.normal.textColor = i == 0 ? MenuKit.TitleColor : MenuKit.TextColor;
            GUI.Label(r, parts[i], reviveStyle);
            y += height + 6f;
        }
        reviveStyle.fontSize = big;
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
        Transform ghost = FirstStanding()?.Ghost;
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
            ItemPickup.Spawn(ItemPickup.Kind.Shells, 3, ghost.position + Vector3.left + Vector3.up, Vector2.zero);
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
        // test only: the Ciclope's key, a staff and then the golden chest on the client's Freny
        if (CoopArgs.Has("-coopLootTest") && isHost && ghost != null) {
            if (!testKey && elapsed >= 12f) {
                testKey = true;
                Vector3 at = ghost.position + Vector3.up * 0.5f;
                CoopShared.KeySpawned(BossKey.Spawn(at, Vector2.zero), at, Vector2.zero);
                MageStaff.Drop(ghost.position + Vector3.up);
                Debug.Log("[Coop] teste: chave e cajado ao lado do cliente");
            } else if (testKey && !testChest && elapsed >= 16f) {
                testChest = true;
                RaycastHit2D floor = Physics2D.Raycast(ghost.position, Vector2.down, 4f, player.whatIsGround);
                Vector3 at = floor.collider != null ? (Vector3)floor.point : ghost.position;
                CoopShared.GoldenChestSpawned(GoldenChest.Spawn(at, null), at);
                Debug.Log($"[Coop] teste: baú dourado ao lado do cliente em {at}");
            }
        }
        // test only: another player's grenade going off beside this Freny
        if (CoopArgs.Has("-coopAutopilot") && !isHost && !testGrenade && elapsed >= 15f && player != null && !player.IsDead) {
            testGrenade = true;
            int before = player.Health + player.Armor;
            player.DrawPeerGrenade(player.transform.position, Vector2.zero, 0.3f);
            StartCoroutine(GrenadeResult(before));
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

    // the host's Ciclope: the boss bar at the bottom, from its share of life (0..255)
    public bool IsBoss { get; private set; }
    private GameObject bar;
    private UnityEngine.UI.Slider barLife;
    private UnityEngine.UI.Slider barTrail;

    public void MakeBossBar(int slot) {
        IsBoss = true;
        health = 255;
        bar = Cyclops.BuildBar(slot, slot > 0, out barLife, out barTrail);
    }

    private void Update() {
        if (bar == null) {
            return;
        }
        bar.SetActive(!GameMenus.Paused);
        float value = Mathf.Clamp01(health / 255f);
        if (barLife != null) {
            barLife.value = value;
        }
        if (barTrail != null) {
            barTrail.value = barTrail.value > value ? Mathf.MoveTowards(barTrail.value, value, 0.4f * Time.deltaTime) : value;
        }
    }

    private void OnDestroy() {
        if (bar != null) {
            Destroy(bar);
        }
    }

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
    public static void ToVisuals(GameObject root, bool keepColliders = false, bool keepMotion = false) {
        for (int pass = 0; pass < 3; pass++) {   // a component required by another goes on a later pass
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) {
                if (component is Transform || component is SpriteRenderer || component is Animator || component is SpriteSkin) {
                    continue;
                }
                if (keepColliders && (component is Collider2D || component is Rigidbody2D)) {
                    continue;
                }
                if (keepMotion && component is FrenyMotion) {
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
