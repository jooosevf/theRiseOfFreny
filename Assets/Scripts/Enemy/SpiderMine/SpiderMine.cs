using System.Collections.Generic;
using UnityEngine;

// The Mina-aranha: a mine on six legs that walks (and climbs, like the walkers) up to Freny, beeps
// faster and faster as it closes in, then stops, arms for ArmTime and blows up: ArmedDamage hearts
// and a shove for Freny, a hit for every enemy in the blast too (like the red Golem's GolemMine).
// One hit kills it, and it always goes off when it dies, so the safe way is from afar: shot down
// over there, its blast never reaches Freny (José, 2026-11-01: "melhor acertar de longe"); a sword
// kill up close still catches him, for PoppedDamage. Built at runtime from the Golem prefab
// (EnemyTemplates) and drawn by FrameAnimator, like the Escudeiro.
public class SpiderMine : Enemy {
    public const string TemplateName = "SpiderMine_Enemy";   // the kill log and the API know it by this
    public const float ArmTime = 0.6f;
    public const float Blast = 1.9f;          // a bit smaller than a grenade's (2.5)
    private const int ArmedDamage = 2;        // its own explosion
    private const int PoppedDamage = 1;       // killed before it was ready
    private const int EnemyDamage = 2;
    private const float Knock = 6f;
    private const float ArmReach = 0.15f;     // starts arming this close past its attack distance
    private const float BeepRange = 8f;       // it beeps once Freny is this close
    private const string Sheets = "SpiderMine/SpiderMine ";

    private static readonly Color BodyColor = new Color(0.3f, 0.25f, 0.55f);
    private static readonly Color FireColor = new Color(1f, 0.35f, 0.2f);
    private static Sprite lampSprite;
    private static Sprite[] blastFrames;

    // the runtime template (ArenaDirector makes it), for SpawnAt
    public static GameObject Template { get; private set; }

    // the ones walking around (the autopilot shoots them from afar)
    public static readonly List<SpiderMine> Alive = new List<SpiderMine>();

    private FrameAnimator frames;
    private SpriteRenderer lamp;
    private float armedAt = -1f;      // when it started arming (-1: not yet)
    private bool blown;
    private float nextBeep;
    private float beepPeriod = 1f;

    public bool Arming => armedAt >= 0f && !isDead;

    protected override bool Small => true;            // any hit kills it, and the hard levels speed it up
    protected override float SpeedFactor => 1.4f;
    protected override bool CanStartAttack => false;  // no bite: it arms instead (Update)
    protected override Color DeathColor => BodyColor;
    protected override bool HealsInBloodMoon => false;
    public override bool ShowsHealthBar => false;

    public static GameObject MakeTemplate(GameObject golemPrefab) {
        // feet at y = -1 of the root, like the Escudeiro's; the sheets sit on them (Init)
        Template = EnemyTemplates.Make<SpiderMine>(golemPrefab, TemplateName, new Vector2(0.8f, 0.7f), new Vector2(0f, -0.65f));
        return Template;
    }

    // for testing: a spider mine standing on that floor point, already after Freny
    public static SpiderMine SpawnAt(Vector3 floorPoint) {
        if (Template == null) {
            return null;
        }
        GameObject spawned = Instantiate(Template, floorPoint, Quaternion.identity);
        SpiderMine mine = spawned.GetComponent<SpiderMine>();
        mine.PlaceFeetAt(floorPoint);
        mine.isHit = true;   // provoked: comes for him from anywhere within provokedRadius
        return mine;
    }

    protected override void OnEnable() {
        base.OnEnable();
        Alive.Add(this);
    }

    protected override void OnDisable() {
        base.OnDisable();
        Alive.Remove(this);
    }

    public override void Init() {
        health = 1;
        speed = 2;
        coins = 2;
        scoreToAdd = 250;
        attackCooldown = 1f;
        provokedRadius = 14f;
        base.Init();
        SpriteRenderer renderer = sprite;
        renderer.transform.localPosition = new Vector3(0f, -1f, 0f);
        renderer.transform.localScale = Vector3.one;
        frames = renderer.GetComponent<FrameAnimator>();
        if (frames == null) {
            frames = renderer.gameObject.AddComponent<FrameAnimator>();
        }
        frames.Setup(this, renderer);
        frames.Add("idle", Sheets + "Idle", 4f);
        frames.Add("walk", Sheets + "Walk", 14f);
        frames.Add("arm", Sheets + "Arm", 4f / ArmTime, false);

        if (lampSprite == null) {
            lampSprite = Resources.Load<Sprite>(Sheets + "Lamp");
        }
        GameObject light = new GameObject("Lamp");
        light.transform.SetParent(renderer.transform, false);
        light.transform.localPosition = new Vector3(0f, 0.46f, 0f);
        lamp = light.AddComponent<SpriteRenderer>();
        lamp.sprite = lampSprite;
        lamp.sortingLayerID = renderer.sortingLayerID;
        lamp.sortingOrder = renderer.sortingOrder + 1;
        if (ItemPickup.UnlitMaterial != null) {
            lamp.sharedMaterial = ItemPickup.UnlitMaterial;   // it glows in the dark corners
        }
        lamp.enabled = false;
    }

    public override void Update() {
        if (isDead) {
            if (!blown) {
                // shot, cut down or caught in another blast: it goes off where it fell
                blown = true;
                Explode(armedAt >= 0f ? ArmedDamage : PoppedDamage);
            }
            return;
        }
        if (Arming) {
            // planted: no more walking, only falling if the floor went away
            UpdatePush();
            StickToGround();
            ClampToArena();
            Blink(0.1f, 1f);
            if (Time.time >= armedAt + ArmTime) {
                Detonate();
            }
            return;
        }
        base.Update();
        if (isDead) {
            return;
        }
        Transform target = Target;
        if (target != null && !Netted && (Rival != null || (player != null && player.Health > 0)) && InAttackReach(ArmReach)) {
            StartArming();
            return;
        }
        // beeps quicker the closer Freny is (1 s far away, 0.18 s next to him)
        float near = player_t != null ? Vector2.Distance(Center, player_t.position) : float.MaxValue;
        if (near <= BeepRange && player != null && player.Health > 0) {
            float closeness = 1f - Mathf.Clamp01((near - 1f) / (BeepRange - 1f));
            Blink(Mathf.Lerp(1f, 0.18f, closeness), closeness);
        } else if (lamp != null) {
            lamp.enabled = Mathf.Repeat(Time.time, 1.6f) < 0.12f;   // a slow idle blink
        }
    }

    private void StartArming() {
        armedAt = Time.time;
        frames.Play("arm", ArmTime + 0.5f);
        Fx.Burst(Center, FireColor, 6, 1.5f, 0.06f, 0.3f);
        Sfx.Play("golem_windup", Center, 0.8f, 1.8f);
    }

    // the lamp on top: on for 40% of each period, with a beep as it lights
    private void Blink(float period, float closeness) {
        if (lamp == null) {
            return;
        }
        if (Mathf.Abs(period - beepPeriod) > 0.05f && Time.time + period < nextBeep) {
            nextBeep = Time.time + period;   // it got closer: don't wait out the slow beep
        }
        beepPeriod = period;
        if (Time.time >= nextBeep) {
            nextBeep = Time.time + period;
            Sfx.Play(Arming ? "ui_select" : "ui_move", Center, 0.35f + 0.45f * closeness, Arming ? 2f : 1.5f + 0.6f * closeness);
        }
        lamp.enabled = nextBeep - Time.time > period * 0.6f;
        lamp.transform.localScale = Vector3.one * (Arming ? 1.4f : 1f);
    }

    // the Nuke takes it whole: no blast of its own to catch Freny with
    public override void Nuked() {
        blown = true;
        base.Nuked();
    }

    // its own fuse ran out: the full blast, and nobody gets the kill
    private void Detonate() {
        blown = true;
        coins = 0;
        Explode(ArmedDamage);
        Health = 0;
        Die(false);
    }

    private void Explode(int frenyDamage) {
        if (lamp != null) {
            lamp.enabled = false;
        }
        CoopWorld.SpiderBlast(Center, frenyDamage);   // co-op host: the others blow up their own copy
        Blow(Center, frenyDamage, this, false);
    }

    // co-op client: the host's spider mine went off; this side's Freny can be caught in it, the
    // enemies and the crates are the host's
    public static void BlastCopy(Vector3 at, int frenyDamage) {
        Blow(at, frenyDamage, null, true);
    }

    private static void Blow(Vector3 at, int frenyDamage, SpiderMine self, bool copy) {
        Fx.Burst(at, new Color(1f, 0.95f, 0.85f), 20, 3f, 0.35f, 0.15f);
        Fx.Burst(at, FireColor, 70, Blast * 3.5f, 0.2f, 0.45f);
        Fx.Burst(at, BodyColor, 40, Blast * 2.2f, 0.16f, 0.6f);
        Fx.Burst(at, new Color(0.3f, 0.3f, 0.3f, 0.8f), 26, Blast * 1.2f, 0.3f, 1.1f);
        Sfx.Play("grenade_explode", at, 0.9f, 1.15f);
        ShowBlast(at);
        ScreenKick.Kick(0.12f, 0.2f);

        Collider2D[] caught = Physics2D.OverlapCircleAll(at, Blast);
        HashSet<Enemy> hit = new HashSet<Enemy>();
        Player freny = null;
        foreach (Collider2D col in caught) {
            if (freny == null && col.GetComponentInParent<Player>() is Player p) {
                freny = p;
            }
            Enemy enemy = copy ? null : col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy == self || enemy.IsDead || !hit.Add(enemy)) {
                continue;
            }
            enemy.HitBy(null, EnemyDamage);   // other spider mines go off in turn
        }
        Player.NextDamageSource = PlayerStats.CauseMine;
        if (freny != null && !freny.IsDead && freny.TryDamage(frenyDamage)) {
            float side = Mathf.Sign(freny.transform.position.x - at.x);
            if (side == 0f) {
                side = freny.FacingRight ? -1f : 1f;
            }
            freny.Knockback(new Vector2(side * Knock, Knock * 0.6f));
        }
        foreach (Collider2D col in caught) {
            if (copy) {
                break;
            }
            if (col.GetComponent<ArenaChest>() is ArenaChest chest) {
                chest.Damage();
            } else if (col.GetComponent<ArenaVase>() is ArenaVase vase) {
                vase.Damage();
            }
        }
    }

    // the Mine's explosion frames, played once where it blew up
    private static void ShowBlast(Vector3 at) {
        if (blastFrames == null) {
            blastFrames = FrameAnimator.Load("Mine/Mine explode");
        }
        if (blastFrames.Length == 0) {
            return;
        }
        GameObject obj = new GameObject("Spider Mine Blast");
        obj.transform.position = at;
        obj.transform.localScale = Vector3.one * 1.6f;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 60;
        if (ItemPickup.UnlitMaterial != null) {
            renderer.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        obj.AddComponent<Flipbook>().Play(blastFrames, 16f);
    }

    // plays a row of frames once, then goes away
    private class Flipbook : MonoBehaviour {
        private Sprite[] frames;
        private float fps;
        private float start;
        private SpriteRenderer renderer;

        public void Play(Sprite[] sprites, float framesPerSecond) {
            frames = sprites;
            fps = framesPerSecond;
            start = Time.time;
            renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
        }

        private void Update() {
            int frame = (int)((Time.time - start) * fps);
            if (frame >= frames.Length) {
                Destroy(gameObject);
                return;
            }
            renderer.sprite = frames[frame];
        }
    }
}
