using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexballEffect : MonoBehaviour
{
    [SerializeField] private float speed = 3f;
    private Vector3 direction = Vector3.right;
    // set by the Sorcerer when it casts: hearts taken, the shove on Freny, and the arena's speed-up
    [NonSerialized] public int damage = 2;
    [NonSerialized] public float knockback = 5f;
    [NonSerialized] public float speedMultiplier = 1f;
    // who cast it: it flies through its caster and hurts any other enemy it crosses
    [NonSerialized] public Enemy caster;
    // a homing hexball (high scores) steers towards this one's head, a limited turn per second,
    // so a sidestep, a jump or ducking still beats it
    [NonSerialized] public Player homingTarget;
    [NonSerialized] public float homingTurn;
    private const float HomingTime = 2.5f; // then it flies straight
    private float bornAt;

    // it flies until it hits something; once it is far from Freny and still moving away it is gone
    private const float LoseDistance = 25f;
    private const float MaxLifetime = 30f;
    private const float EnemyHitRadius = 0.15f;
    // by day the purple ball got lost against the bright sky and walls (José, 2026-10-29): a dark
    // copy of it, a bit bigger, sits behind it, fades in with the daylight and pulses (the ball and
    // its hitbox keep their size)
    private static readonly Color RimColor = new Color(0.1f, 0f, 0.16f);
    private const float RimScale = 1.5f;
    private const float RimAlpha = 0.9f;
    private const float DayPulse = 0.15f;   // rim size swing by full day
    private const float PulseSpeed = 12f;
    private SpriteRenderer ball, rim;
    private static Transform playerT;

    public Vector3 Direction => direction;
    public float Speed => speed * speedMultiplier;

    // live hexballs, read by the autopilot to dodge (instead of FindObjectsOfType)
    public static readonly List<HexballEffect> Active = new List<HexballEffect>();

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void SetDirection(Vector3 newDirection)
    {
        direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : Vector3.right;
        // point the sprite along its path (works for any angle, not only left/right)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Start()
    {
        bornAt = Time.time;
        Destroy(gameObject, MaxLifetime);
        ball = GetComponent<SpriteRenderer>();
        if (ball != null) {
            GameObject rimObj = new GameObject("Day Rim");
            rimObj.transform.SetParent(transform, false);
            rimObj.transform.localScale = Vector3.one * RimScale;
            rim = rimObj.AddComponent<SpriteRenderer>();
            rim.sharedMaterial = ball.sharedMaterial;
            rim.sortingLayerID = ball.sortingLayerID;
            rim.sortingOrder = ball.sortingOrder - 1;
            UpdateRim();
        }
    }

    // the animator changes the ball's frame, so the rim copies it every frame
    private void LateUpdate()
    {
        UpdateRim();
    }

    private void UpdateRim()
    {
        if (rim == null) {
            return;
        }
        float day = ArenaDayNight.Daylight;
        rim.enabled = day > 0.01f;
        if (!rim.enabled) {
            return;
        }
        rim.sprite = ball.sprite;
        rim.flipX = ball.flipX;
        rim.flipY = ball.flipY;
        rim.color = new Color(RimColor.r, RimColor.g, RimColor.b, RimAlpha * day);
        float pulse = 1f + DayPulse * day * (0.5f + 0.5f * Mathf.Sin((Time.time - bornAt) * PulseSpeed));
        rim.transform.localScale = Vector3.one * (RimScale * pulse);
    }

    private void Update()
    {
        Steer();
        transform.Translate(direction * (Speed * Time.deltaTime), Space.World);
        if (HitEnemy()) {
            return;
        }

        if (playerT == null) {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            playerT = obj ? obj.transform : null;
        }
        if (playerT != null) {
            Vector2 away = transform.position - playerT.position;
            if (away.sqrMagnitude > LoseDistance * LoseDistance && Vector2.Dot(away, direction) > 0f) {
                Destroy(gameObject);
            }
        }
    }

    private void Steer()
    {
        if (homingTarget == null || homingTarget.IsDead || Time.time - bornAt > HomingTime) {
            return;
        }
        Vector3 to = homingTarget.HeadPoint - transform.position;
        if (to.sqrMagnitude < 0.01f || Vector2.Dot(to, direction) <= 0f) {
            homingTarget = null; // went past him: no turning around
            return;
        }
        float current = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float wanted = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        float angle = Mathf.MoveTowardsAngle(current, wanted, homingTurn * Time.deltaTime) * Mathf.Deg2Rad;
        SetDirection(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
    }

    // any enemy but its caster that it crosses takes the hit (box test from the transform, so it
    // doesn't depend on which bodies get trigger messages from each other)
    private bool HitEnemy()
    {
        Vector2 p = transform.position;
        for (int i = Enemy.Active.Count - 1; i >= 0; i--) {
            Enemy enemy = Enemy.Active[i];
            // the Ciclope's helpers' spells pass through it (José, 2026-10-29)
            if (enemy == null || enemy == caster || enemy.IsDead || enemy is Cyclops) {
                continue;
            }
            Vector2 c = enemy.Center;
            if (Mathf.Abs(p.x - c.x) <= enemy.HalfWidth + EnemyHitRadius && Mathf.Abs(p.y - c.y) <= enemy.HalfHeight + EnemyHitRadius) {
                enemy.HitBy(caster, damage);
                Fx.Burst(transform.position, new Color(0.7f, 0.4f, 1f), 12, 2.5f, 0.1f, 0.4f);
                Sfx.Play("hexball_impact", transform.position);
                Destroy(gameObject);
                return true;
            }
        }
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();

            if (player != null)
            {
                if (player.TryDamage(damage + Enemy.BloodDamage) && knockback > 0f)
                {
                    float side = direction.x >= 0f ? 1f : -1f;
                    player.Knockback(new Vector2(side * knockback, knockback * 0.5f));
                }
                Fx.Burst(transform.position, new Color(0.7f, 0.4f, 1f), 12, 2.5f, 0.1f, 0.4f);
                Sfx.Play("hexball_impact", transform.position);
                Destroy(this.gameObject);
            }
        }
    }
}
