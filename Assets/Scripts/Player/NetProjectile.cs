using System.Collections.Generic;
using UnityEngine;

// A thrown Rede (NetThrower): a bundle of rope flying in an arc that opens on the first enemy it
// touches, or on the floor on its way down, and catches every enemy within Radius (NetTrap).
// Built at runtime like the grenade.
public class NetProjectile : MonoBehaviour {

    public const float Radius = 1.6f;
    private const float MaxLife = 4f;
    private static readonly Color RopeColor = new Color(0.85f, 0.7f, 0.45f);

    private Rigidbody2D body;
    private bool opened;
    private float dieAt;
    private float throwerFeet;   // one-way platforms above this are flown through both ways

    public static void Launch(Vector3 from, Vector2 velocity, float facing, float feetY) {
        GameObject net = new GameObject("Net");
        net.transform.position = from;
        net.transform.localScale = Vector3.one * 0.8f;
        SpriteRenderer renderer = net.AddComponent<SpriteRenderer>();
        renderer.sprite = NetThrower.Icon;
        renderer.sortingOrder = 50;
        Rigidbody2D body = net.AddComponent<Rigidbody2D>();
        body.gravityScale = NetThrower.Gravity;
        body.SetVel(velocity);
        body.angularVelocity = -facing * 300f;
        CircleCollider2D trigger = net.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.3f;
        NetProjectile projectile = net.AddComponent<NetProjectile>();
        projectile.body = body;
        projectile.throwerFeet = feetY;
        projectile.dieAt = Time.time + MaxLife;
        Fx.Spray(from, velocity, RopeColor, 4, 2f, 0.4f);
        Sfx.Play("grenade_throw", from, 0.8f, 1.3f);
    }

    private void Update() {
        if (!opened && (Time.time >= dieAt || (ArenaBounds.Active && transform.position.y < ArenaBounds.Bottom - 2f))) {
            Destroy(gameObject);   // thrown off the map
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (opened || other.GetComponentInParent<Player>() != null) {
            return;
        }
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null && !enemy.IsDead) {
            Open();
        } else if (enemy == null && !other.isTrigger && body != null && body.Vel().y <= 0f) {
            // thrown from under a one-way platform the arc came down on top of it and missed the
            // enemy on Freny's floor: only his own floor or lower stops the net (an enemy up there
            // still opens it, above)
            if (other.usedByEffector && other.bounds.max.y > throwerFeet + 0.2f) {
                return;
            }
            Open();
        }
    }

    private void Open() {
        opened = true;
        Vector3 at = transform.position;
        Fx.Burst(at, RopeColor, 18, 3f, 0.08f, 0.4f);
        Sfx.Play("weapon_switch", at, 1f, 0.7f);
        HashSet<Enemy> caught = new HashSet<Enemy>();
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy == null || enemy.IsDead) {
                continue;
            }
            // anyone whose body is within the net's reach, not only the one it touched
            Bounds b = enemy.BodyBounds;
            Vector2 closest = b.ClosestPoint(at);
            if (Vector2.Distance(closest, at) <= Radius && caught.Add(enemy)) {
                NetTrap.Catch(enemy);
            }
        }
        if (caught.Count == 0) {
            NetTrap.DropEmpty(at);   // a miss: the net lies there a moment
        }
        Destroy(gameObject);
    }
}
