using System.Collections.Generic;
using UnityEngine;

// Thrown by Freny (E, or the autopilot): flies in an arc, bounces and rolls, and goes off when its
// fuse runs out (5 s from the pin, Player.GrenadeFuse; José, 2026-11-01: no more bursting on impact),
// damaging every enemy around it, and Freny too when he is caught in the blast. Built at runtime by
// Player.SpawnGrenade, so no prefab is needed. The seconds left show over it.
public class Grenade : MonoBehaviour {

    public const float BlastRadius = 2.5f;
    public const int EnemyDamage = 3;      // one grenade kills any enemy but the red Golem (6 HP)
    public const int SelfDamage = 2;       // what Freny takes in his own blast (armor first)
    public const float SelfKnockback = 7f;
    public const float ImpactFuse = 1f;    // with Features.GrenadeFuse off: at most this long in the air

    public float fuse = Player.GrenadeFuse;
    // the Armeiro's grenade levels (WeaponLevels) widen the blast and raise the damage
    public float radius = BlastRadius;
    public int damage = EnemyDamage;

    // the grenades on the floor, for the autopilot to keep away from
    public static readonly List<Grenade> Live = new List<Grenade>();
    public float SecondsLeft => Mathf.Max(0f, explodeAt - Time.time);

    private static PhysicsMaterial2D bouncy;
    public static PhysicsMaterial2D Bouncy =>
        bouncy ? bouncy : (bouncy = new PhysicsMaterial2D("Grenade") { friction = 0.8f, bounciness = 0.35f });

    private float explodeAt;
    private bool exploded;
    private SpriteRenderer sprite;
    private static GUIStyle countStyle;

    private static readonly Color BlastColor = new Color(1f, 0.6f, 0.2f);

    private void Start() {
        explodeAt = Time.time + fuse;
        sprite = GetComponent<SpriteRenderer>();
        Live.Add(this);
    }

    private void OnDestroy() {
        Live.Remove(this);
    }

    // after the first bounce it brakes, so it settles near where it landed instead of rolling away
    private bool landed;

    private void OnCollisionEnter2D(Collision2D collision) {
        if (!Features.GrenadeFuse) {
            Explode();
            return;
        }
        if (landed || !TryGetComponent(out Rigidbody2D body)) {
            return;
        }
        landed = true;
        Vector2 v = body.Vel();
        body.SetVel(new Vector2(v.x * 0.5f, v.y));
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = 3f;
        body.angularDamping = 10f;
#else
        body.drag = 3f;
        body.angularDrag = 10f;
#endif
    }

    private void Update() {
        if (Time.time >= explodeAt) {
            Explode();
            return;
        }
        // it blinks red, faster as the end comes
        if (sprite != null) {
            float rate = SecondsLeft < 1.5f ? 0.15f : 0.5f;
            sprite.color = Mathf.Repeat(Time.time, rate) < rate * 0.5f ? new Color(1f, 0.45f, 0.4f) : Color.white;
        }
    }

    private void OnGUI() {
        Camera cam = Camera.main;
        if (exploded || cam == null || GameMenus.Paused) {
            return;
        }
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.45f);
        if (screen.z < 0f) {
            return;
        }
        if (countStyle == null) {
            countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        countStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(Screen.height / 60f));   // small, it is a lot of grenades sometimes
        Rect rect = new Rect(screen.x - 40f, Screen.height - screen.y - 20f, 80f, 40f);
        string text = Mathf.CeilToInt(SecondsLeft).ToString();
        countStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, countStyle);
        countStyle.normal.textColor = SecondsLeft < 1.5f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        GUI.Label(rect, text, countStyle);
    }

    private void Explode() {
        if (exploded) {
            return;
        }
        exploded = true;

        Vector3 at = transform.position;
        // a big fireball: a white flash, the fire filling the blast radius, then smoke
        Fx.Burst(at, new Color(1f, 0.95f, 0.8f), 24, 3f, 0.4f, 0.15f);
        Fx.Burst(at, BlastColor, 90, radius * 3.5f, 0.22f, 0.45f);
        Fx.Burst(at, new Color(1f, 0.3f, 0.1f), 50, radius * 2.2f, 0.18f, 0.6f);
        Sfx.Play("grenade_explode", at);
        Fx.Burst(at, new Color(0.3f, 0.3f, 0.3f, 0.8f), 36, radius * 1.2f, 0.32f, 1.2f);

        Collider2D[] caught = Physics2D.OverlapCircleAll(at, radius);
        HashSet<Enemy> hit = new HashSet<Enemy>();
        Player freny = null;
        foreach (Collider2D col in caught) {
            if (freny == null && col.GetComponentInParent<Player>() is Player p) {
                freny = p;
            }
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead || !hit.Add(enemy)) {
                continue;
            }
            enemy.BlastDamage(damage);   // round the Escudeiro's shield too
        }
        int kills = 0;
        foreach (Enemy enemy in hit) {
            if (enemy.IsDead) kills++;
        }
        Achievements.GrenadeBlast(kills);
        // Freny caught in his own blast: hurt and thrown away from it
        if (freny != null && !freny.IsDead && freny.TryDamage(SelfDamage)) {
            float side = Mathf.Sign(freny.transform.position.x - at.x);
            if (side == 0f) {
                side = freny.FacingRight ? -1f : 1f;
            }
            freny.Knockback(new Vector2(side * SelfKnockback, SelfKnockback * 0.6f));
            if (freny.IsDead) {
                Achievements.OwnGrenade();
            }
        }
        // chests, vases and the stages' props break too
        foreach (Collider2D col in caught) {
            if (col.GetComponent<ArenaChest>() is ArenaChest chest) {
                chest.Damage();
            } else if (col.GetComponent<ArenaVase>() is ArenaVase vase) {
                vase.Damage();
            } else if (col.GetComponent<Breakable>() is Breakable prop) {
                prop.Damage();
            }
        }
        Destroy(gameObject);
    }
}
