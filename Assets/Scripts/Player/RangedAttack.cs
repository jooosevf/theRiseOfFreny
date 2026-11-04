using System;
using System.Collections;
using UnityEngine;

public class RangedAttack : MonoBehaviour {

    private Vector3 bulletDirection = Vector3.right;
    private bool hasDirection;
    [SerializeField] private float speed = 15f; // units per second
    private float push = 1f; // how hard it shoves an enemy back (x its HitPush): the revolver's 0, the shotgun's 1

    // Player calls these two right after spawning the bullet
    public void SetDirection(Vector3 direction) {
        bulletDirection = direction;
        hasDirection = true;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction.x < 0 ? -1 : 1);
        transform.localScale = scale;
        // a shot fired up (W) turns the sprite to point along its path
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x)) {
            transform.rotation = Quaternion.Euler(0f, 0f, direction.y > 0 ? 90f : -90f);
            transform.localScale = new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
        }
    }

    // the shotgun shot this pellet belongs to (0 for the revolver)
    public int ShotgunShot { get; set; }
    public bool FromShotgun { get; set; }   // a pellet (the shotgun's damage buff) or a revolver bullet
    public bool Cosmetic { get; set; }      // co-op: the other player's shot, drawn here without damage

    public void SetPush(float amount) {
        push = amount;
    }

    private void Start() {
        if (!hasDirection) {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) {
                SetDirection(player.transform.localScale.x < 0 ? Vector3.left : Vector3.right);
            }
        }
        // the prefab's dynamic body fell asleep after half a second of transform-only motion, and a
        // sleeping body never touches a chest or a vase (they have no moving body of their own)
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body) {
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }
        Destroy(gameObject, 3.0f);
    }

    private void Update() {
        // direction is a unit vector; scaling by deltaTime here keeps the speed the same at any frame rate
        transform.Translate(bulletDirection * (speed * Time.deltaTime), Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        
        // the player is IDamageable too, don't shoot yourself
        if (other.CompareTag("Player")) {
            return;
        }

        IDamageable hit = other.GetComponent<IDamageable>();

        if (hit != null) {
            if (Cosmetic) {
                // nothing: the side that fired it deals the hit
            } else if (hit is Enemy enemy) {
                bool alive = !enemy.IsDead;
                if (alive) Enemy.NextHitPower = WeaponLevels.Roll(FromShotgun ? WeaponLevels.ShotgunPower : WeaponLevels.PistolPower, transform.position);
                if (alive) Enemy.NextHitSource = FromShotgun ? PlayerStats.WeaponShotgun : PlayerStats.WeaponGun;
                enemy.ShotDamage(push, transform.position);
                if (alive && enemy.IsDead && ShotgunShot > 0) {
                    Achievements.ShotKill(ShotgunShot);
                    MultiKill.ShotKill(ShotgunShot);
                }
            } else {
                hit.Damage();
            }
            Fx.Burst(transform.position, new Color(1f, 0.85f, 0.4f), 8, 2f, 0.07f, 0.3f);
            Destroy(gameObject);
        }
    }

}