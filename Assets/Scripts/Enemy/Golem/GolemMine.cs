using System.Collections.Generic;
using UnityEngine;

// The red Golem's mine (the Mine sprites, also the Ciclope's bomb): thrown in an arc like Freny's
// grenade, it sticks where it lands and blows up with the grenade's blast when its fuse runs out,
// Fuse seconds after the throw (José, 2026-10-29: it used to go off the moment it touched Freny).
// A sword hit or a shot sets it off early.
// Enemies caught in the blast are hurt too, blamed on the Golem (no XP for Freny).
public class GolemMine : MonoBehaviour, IDamageable {
    // 2 s, down to 1.5 s as the arena gets harder
    public static float Fuse => Mathf.Lerp(2f, 1.5f, ArenaDirector.CurrentDifficulty);
    private const float WarnTime = 0.6f;   // its eye blinks fast in the last moments
    public const float Gravity = 1.5f;

    private static Sprite[] idleFrames, nearFrames;
    private static readonly Color BlastColor = new Color(0.75f, 0.35f, 1f);

    private Enemy thrower;
    private SpriteRenderer sprite;
    private Rigidbody2D body;
    private float explodeAt;
    private bool landed;
    private bool exploded;
    private float blast = Grenade.BlastRadius;
    private const float BigBlast = 1.5f;

    public int Health { get; set; } = 1;

    // the mines flying or lying around (the autopilot keeps away from them)
    public static readonly List<GolemMine> Active = new List<GolemMine>();
    public bool Landed => landed;
    public float Blast => blast;

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    // thrown at a point on a fixed angle, with the speed that lands it there (as
    // Player.GrenadeChargeFor); a heavier mine (gravityScale) flies faster and flatter
    public static void ThrowAt(Enemy thrower, Vector3 from, Vector2 to, float angleDegrees, float gravityScale, float maxSpeed, float size = 1f) {
        float dx = to.x - from.x, dy = to.y - from.y;
        float rad = angleDegrees * Mathf.Deg2Rad;
        float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
        float x = Mathf.Abs(dx);
        float denom = 2f * Mathf.Cos(rad) * Mathf.Cos(rad) * (x * Mathf.Tan(rad) - dy);
        float speed = denom > 0.01f ? Mathf.Min(maxSpeed, Mathf.Sqrt(g * x * x / denom)) : maxSpeed;
        Vector2 velocity = new Vector2(Mathf.Sign(dx) * Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        Throw(thrower, from, velocity, gravityScale, size);
    }

    // size: the Ciclope's bomb is 4 times the red Golem's mine, and its blast reaches BigBlast farther
    public static void Throw(Enemy thrower, Vector3 from, Vector2 velocity, float gravityScale = Gravity, float size = 1f) {
        LoadFrames();
        GameObject mine = new GameObject("Golem Mine");
        mine.transform.position = from;
        mine.transform.localScale = Vector3.one * (0.8f * size);
        SpriteRenderer renderer = mine.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 50;
        Rigidbody2D body = mine.AddComponent<Rigidbody2D>();
        body.gravityScale = gravityScale;
        body.SetVel(velocity);
        CircleCollider2D trigger = mine.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.3f;
        GolemMine m = mine.AddComponent<GolemMine>();
        m.thrower = thrower;
        m.sprite = renderer;
        m.body = body;
        m.explodeAt = Time.time + Fuse;
        m.blast = Grenade.BlastRadius * (size > 1f ? BigBlast : 1f);
        Sfx.Play("grenade_throw", from);
    }

    private static void LoadFrames() {
        if (idleFrames == null) {
            idleFrames = FrameAnimator.Load("Mine/Mine idle");
            nearFrames = FrameAnimator.Load("Mine/Mine near");
        }
    }

    private void Update() {
        bool warning = explodeAt - Time.time < WarnTime;
        Sprite[] frames = warning ? nearFrames : idleFrames;
        if (frames != null && frames.Length > 0) {
            float fps = warning ? 14f : 8f;
            sprite.sprite = frames[(int)(Time.time * fps) % frames.Length];
        }
        if (Time.time >= explodeAt) {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (exploded) {
            return;
        }
        if (other.GetComponentInParent<Player>() != null) {
            return;   // no blast on touch: the fuse decides
        }
        if (!landed && !other.isTrigger && other.GetComponentInParent<Enemy>() == null && body.Vel().y <= 0f) {
            Land();
        }
    }

    // sticks to the floor (or wall) it hit; the fuse keeps burning
    private void Land() {
        landed = true;
        body.SetVel(Vector2.zero);
        body.bodyType = RigidbodyType2D.Kinematic;
        Sfx.Play("ui_select", transform.position);
    }

    // Freny's sword or bullet
    public void Damage() {
        Explode();
    }

    private void Explode() {
        if (exploded) {
            return;
        }
        exploded = true;

        Vector3 at = transform.position;
        Fx.Burst(at, new Color(1f, 0.95f, 0.9f), 24, 3f, 0.4f, 0.15f);
        Fx.Burst(at, BlastColor, 90, blast * 3.5f, 0.22f, 0.45f);
        Fx.Burst(at, new Color(1f, 0.3f, 0.6f), 50, blast * 2.2f, 0.18f, 0.6f);
        Sfx.Play("grenade_explode", at);
        Fx.Burst(at, new Color(0.3f, 0.3f, 0.3f, 0.8f), 36, blast * 1.2f, 0.32f, 1.2f);

        Collider2D[] caught = Physics2D.OverlapCircleAll(at, blast);
        HashSet<Enemy> hit = new HashSet<Enemy>();
        Player freny = null;
        foreach (Collider2D col in caught) {
            if (freny == null && col.GetComponentInParent<Player>() is Player p) {
                freny = p;
            }
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy == thrower || enemy.IsDead || !hit.Add(enemy)) {
                continue;
            }
            if (thrower != null && !thrower.IsDead) {
                enemy.HitBy(thrower, Grenade.EnemyDamage);
            }
        }
        if (freny != null && !freny.IsDead && freny.TryDamage(Grenade.SelfDamage)) {
            float side = Mathf.Sign(freny.transform.position.x - at.x);
            if (side == 0f) {
                side = freny.FacingRight ? -1f : 1f;
            }
            freny.Knockback(new Vector2(side * Grenade.SelfKnockback, Grenade.SelfKnockback * 0.6f));
        }
        foreach (Collider2D col in caught) {
            if (col.GetComponent<ArenaChest>() is ArenaChest chest) {
                chest.Damage();
            } else if (col.GetComponent<ArenaVase>() is ArenaVase vase) {
                vase.Damage();
            }
        }
        Destroy(gameObject);
    }
}
