using System.Collections.Generic;
using UnityEngine;

// Thrown by Freny (G, or the autopilot): flies in an arc, then explodes and damages every enemy
// around it, and Freny too when he is caught in the blast. Built at runtime by
// Player.ThrowGrenade, so no prefab is needed.
public class Grenade : MonoBehaviour {

    public const float BlastRadius = 2.5f;
    public const int EnemyDamage = 3;      // one grenade kills any enemy but the red Golem (6 HP)
    public const int SelfDamage = 2;       // what Freny takes in his own blast (armor first)
    public const float SelfKnockback = 7f;

    public float fuse = 1.0f;
    // the Armeiro's grenade levels (WeaponLevels) widen the blast and raise the damage
    public float radius = BlastRadius;
    public int damage = EnemyDamage;

    private float explodeAt;
    private bool exploded;

    private static readonly Color BlastColor = new Color(1f, 0.6f, 0.2f);

    private void Start() {
        explodeAt = Time.time + fuse;
    }

    private void Update() {
        if (Time.time >= explodeAt) {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player") || other.GetComponentInParent<Player>() != null) {
            return;
        }
        // goes off early when it touches an enemy, or the ground/a wall on the way down (on the
        // way up it passes through the one-way platforms)
        if (other.GetComponentInParent<Enemy>() != null) {
            Explode();
        } else if (!other.isTrigger && TryGetComponent(out Rigidbody2D body) && body.Vel().y <= 0f) {
            Explode();
        }
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
