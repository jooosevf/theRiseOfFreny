using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Arena's three rare power-ups (José, 2026-11-02, after Call of Duty Zombies): now and then an
// enemy Freny kills leaves one, spinning on the floor. They are ItemPickups like the rest; this
// rolls them and does what they do. Added at runtime by ArenaBootstrap, so the scene stays as is.
// - Insta-kill: for InstaKillSeconds every enemy is down to one heart, the ones that arrive too. It
//   never touches the Ciclope, which has to stay strong, and doesn't drop while one is alive.
// - Nuke: every enemy on screen dies at once (the screen is the whole arena), the Ciclope excepted,
//   and nothing spawns for NukeSpawnPause seconds (ArenaDirector.PauseSpawns). They are Freny's kills
//   like any other, one by one in the run's log, but one Nuke pays at most NukeMaxPoints (José,
//   2026-11-02): past that a kill is worth 1 point, the flat kill the ranking API already knows.
// - Ímã: for MagnetSeconds coins, diamonds and the items he can use fly to Freny (Coin, ItemPickup).
// Nothing here goes into the save: a run's power-ups end with it.
public class ArenaPowerUps : MonoBehaviour {

    public const float InstaKillSeconds = 10f;
    public const float MagnetSeconds = 10f;
    public const float NukeSpawnPause = 3f;
    public const int NukeMaxPoints = 2500;

    // per kill by Freny: small on purpose, about one every 55 kills
    private const float InstaKillChance = 0.006f;
    private const float NukeChance = 0.004f;
    private const float MagnetChance = 0.008f;
    public const float DropChance = InstaKillChance + NukeChance + MagnetChance;

    private const float PullSpeed = 18f;     // world units per second
    private const float NukeWave = 0.35f;    // the blast reaches the farthest enemy in this long
    private const float FlashTime = 0.55f;
    private const float EndingSeconds = 3f;  // the HUD icon blinks for its last seconds
    private const float FrameTime = 0.09f;

    private static readonly Color InstaKillColor = new Color(1f, 0.25f, 0.2f, 0.55f);
    private static readonly Color NukeColor = new Color(0.55f, 1f, 0.3f, 0.55f);
    private static readonly Color MagnetColor = new Color(0.7f, 0.45f, 1f, 0.55f);
    private static readonly Color FlashColor = new Color(0.85f, 1f, 0.75f);

    private static ArenaPowerUps instance;
    private static float instaKillUntil = -1f;
    private static float magnetUntil = -1f;
    private static readonly Dictionary<ItemPickup.Kind, Sprite[]> sheets = new Dictionary<ItemPickup.Kind, Sprite[]>();

    private Player hero;
    private ArenaDirector director;
    private bool nuking;
    private float flashUntil = -1f;
    private float nextSpeck;

    public static bool InstaKillOn => Time.time < instaKillUntil;
    private static bool MagnetOn => Time.time < magnetUntil;

    public static bool IsPowerUp(ItemPickup.Kind kind) {
        return kind == ItemPickup.Kind.InstaKill || kind == ItemPickup.Kind.Nuke || kind == ItemPickup.Kind.Magnet;
    }

    public static Color ColorOf(ItemPickup.Kind kind) {
        return kind == ItemPickup.Kind.InstaKill ? InstaKillColor : kind == ItemPickup.Kind.Nuke ? NukeColor : MagnetColor;
    }

    // ---- the art (Tools/ArtGen/power_ups.py) ----

    private static Sprite[] Frames(ItemPickup.Kind kind) {
        if (!sheets.TryGetValue(kind, out Sprite[] frames) || frames == null || frames.Length == 0) {
            string name = kind == ItemPickup.Kind.InstaKill ? "Insta-kill" : kind == ItemPickup.Kind.Nuke ? "Nuke" : "Magnet";
            frames = Resources.LoadAll<Sprite>("PowerUps/" + name);
            System.Array.Sort(frames, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
            sheets[kind] = frames;
        }
        return frames;
    }

    private static int FrameIndex(Sprite sprite) {
        int underscore = sprite.name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int i) ? i : 0;
    }

    // facing front (the Guia and the size of the item on the floor)
    public static Sprite Icon(ItemPickup.Kind kind) {
        Sprite[] frames = Frames(kind);
        return frames.Length > 0 ? frames[0] : null;
    }

    public static Sprite FrameAt(ItemPickup.Kind kind, float time) {
        Sprite[] frames = Frames(kind);
        return frames.Length > 0 ? frames[(int)(time / FrameTime) % frames.Length] : null;
    }

    // ---- the run ----

    private void Awake() {
        instance = this;
        instaKillUntil = magnetUntil = -1f;   // a new run (the scene reloads for each one)
    }

    private void OnEnable() {
        Enemy.Killed += OnEnemyKilled;
    }

    private void OnDisable() {
        Enemy.Killed -= OnEnemyKilled;
    }

    private void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
        instaKillUntil = magnetUntil = -1f;
    }

    private void Start() {
        director = FindAnyObjectByType<ArenaDirector>();
    }

    private Player Hero {
        get {
            if (hero == null) {
                GameObject heroObj = GameObject.FindGameObjectWithTag("Player");
                hero = heroObj ? heroObj.GetComponent<Player>() : null;
            }
            return hero;
        }
    }

    private void OnEnemyKilled(Enemy enemy) {
        if (nuking || enemy is Cyclops || OneOnTheFloor()) {
            return;   // a Nuke can't chain into another one, and the boss has its own loot
        }
        float roll = Random.value;
        ItemPickup.Kind kind;
        if (roll < MagnetChance) {
            kind = ItemPickup.Kind.Magnet;
        } else if (roll < MagnetChance + NukeChance) {
            kind = ItemPickup.Kind.Nuke;
        } else if (roll < DropChance && !Cyclops.AnyFighting) {
            kind = ItemPickup.Kind.InstaKill;
        } else {
            return;
        }
        if (!Switched(kind)) {
            return;
        }
        ItemPickup.Spawn(kind, 1, enemy.Center, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(4f, 5.5f)));
    }

    // one switched off (Features) just doesn't drop: the others keep their chances
    private static bool Switched(ItemPickup.Kind kind) {
        return kind == ItemPickup.Kind.Magnet ? Features.Magnet : kind == ItemPickup.Kind.Nuke ? Features.Nuke : Features.InstaKill;
    }

    private static bool OneOnTheFloor() {
        foreach (ItemPickup item in ItemPickup.Active) {
            if (item != null && IsPowerUp(item.kind)) {
                return true;
            }
        }
        return false;
    }

    // Freny touched one (ItemPickup.Apply). Picking up a second one restarts its time
    public static bool Take(ItemPickup.Kind kind) {
        if (instance == null) {
            return false;
        }
        instance.Activate(kind);
        return true;
    }

    private void Activate(ItemPickup.Kind kind) {
        Player player = Hero;
        Vector3 over = player != null ? player.HeadPoint + Vector3.up * 0.6f : transform.position;
        Color solid = ColorOf(kind);
        solid.a = 1f;
        switch (kind) {
            case ItemPickup.Kind.InstaKill:
                instaKillUntil = Time.time + InstaKillSeconds;
                ArenaBossLoot.Say("Insta-kill!", over, solid, 2f);
                Sfx.Play("hexball_launch", 1f, 0.6f);
                foreach (Enemy enemy in Enemy.Active) {
                    if (enemy && !enemy.IsDead && !(enemy is Cyclops)) {
                        Fx.Burst(enemy.Center, solid, 10, 2f, 0.08f, 0.5f);
                    }
                }
                break;
            case ItemPickup.Kind.Nuke:
                ArenaBossLoot.Say("Nuke!", over, solid, 2f);
                StartCoroutine(Nuke());
                break;
            case ItemPickup.Kind.Magnet:
                magnetUntil = Time.time + MagnetSeconds;
                ArenaBossLoot.Say("Ímã!", over, solid, 2f);
                break;
        }
    }

    private IEnumerator Nuke() {
        if (director != null) {
            director.PauseSpawns(NukeSpawnPause);
        }
        flashUntil = Time.time + FlashTime;
        ScreenKick.Kick(0.35f, 0.5f);
        Sfx.Play("grenade_explode", 1f, 0.5f);

        // outward from Freny, the nearest first: a blast, and not 24 death sounds in one frame
        Vector3 from = Hero != null ? Hero.Feet : transform.position;
        List<Enemy> targets = new List<Enemy>();
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead && !(enemy is Cyclops)) {
                targets.Add(enemy);
            }
        }
        targets.Sort((a, b) => Vector2.Distance(a.Center, from).CompareTo(Vector2.Distance(b.Center, from)));
        float farthest = targets.Count > 0 ? Mathf.Max(0.01f, Vector2.Distance(targets[targets.Count - 1].Center, from)) : 1f;
        float start = Time.time;
        // every kill pays at least 1 point, so what can still be shared out is the cap minus those
        int spare = NukeMaxPoints - targets.Count;
        // (nuking stays on until the last one: a kill during the wave rolls no power-up)
        nuking = true;
        foreach (Enemy enemy in targets) {
            if (enemy == null) {
                continue;
            }
            float at = start + NukeWave * Vector2.Distance(enemy.Center, from) / farthest;
            while (Time.time < at) {
                yield return null;
            }
            if (enemy != null) {
                int extra = UIManager.Instance ? UIManager.Instance.PointsFor(enemy) - 1 : 0;
                if (extra > spare) {
                    enemy.OnePointKill = true;
                    extra = 0;
                }
                spare -= extra;
                enemy.Nuked();
            }
        }
        nuking = false;
    }

    private void Update() {
        if (InstaKillOn) {
            foreach (Enemy enemy in Enemy.Active) {
                if (enemy && !(enemy is Cyclops)) {
                    enemy.DropToOneHeart();
                }
            }
        }
        // specks rise from him while something is running, in its colour
        if ((InstaKillOn || MagnetOn) && Hero != null && Time.time >= nextSpeck) {
            nextSpeck = Time.time + 0.12f;
            Color speck = ColorOf(InstaKillOn && (!MagnetOn || Random.value < 0.5f) ? ItemPickup.Kind.InstaKill : ItemPickup.Kind.Magnet);
            speck.a = 1f;
            Fx.Rise(Hero.Feet + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.1f, 1.3f), 0f), speck, Random.Range(0.06f, 0.11f), 0.6f);
        }
    }

    // ---- the Ímã (Coin and ItemPickup ask each frame) ----

    public static bool PullTarget(out Vector3 target) {
        target = Vector3.zero;
        if (!MagnetOn || instance == null || instance.Hero == null || instance.Hero.IsDead) {
            return false;
        }
        target = instance.Hero.BodyCenter;
        return true;
    }

    public static Vector3 PulledFrom(Vector3 position, Vector3 target) {
        return Vector3.MoveTowards(position, target, PullSpeed * Time.deltaTime);
    }

    // ---- on screen ----

    // after the weapons row (CombatHud): what is running, with its seconds; returns where it ends
    public static float DrawHudIcons(float x, float y, float size, float scale, GUIStyle label) {
        x = DrawTimer(ItemPickup.Kind.InstaKill, instaKillUntil, x, y, size, scale, label);
        return DrawTimer(ItemPickup.Kind.Magnet, magnetUntil, x, y, size, scale, label);
    }

    private static float DrawTimer(ItemPickup.Kind kind, float until, float x, float y, float size, float scale, GUIStyle label) {
        float left = until - Time.time;
        Sprite sprite = Icon(kind);
        if (left <= 0f || sprite == null) {
            return x;
        }
        bool show = left > EndingSeconds || Mathf.Repeat(Time.time, 0.3f) < 0.18f;
        if (show) {
            Rect tr = sprite.textureRect;
            Texture tex = sprite.texture;
            Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(new Rect(x, y, size, size), tex, uv);
        }
        if (label != null) {
            string text = Mathf.CeilToInt(left).ToString();
            Rect r = new Rect(x + size * 0.5f - 14f * scale, y + size + 2f * scale, 60f * scale, 26f * scale);
            Color old = label.normal.textColor;
            label.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, label);
            label.normal.textColor = old;
            GUI.Label(r, text, label);
        }
        return x + size + 10f * scale;
    }

    // the Nuke's flash, over everything
    private void OnGUI() {
        float left = flashUntil - Time.time;
        if (left <= 0f || Event.current.type != EventType.Repaint) {
            return;
        }
        Color old = GUI.color;
        Color c = FlashColor;
        c.a = 0.85f * left / FlashTime;
        GUI.color = c;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
