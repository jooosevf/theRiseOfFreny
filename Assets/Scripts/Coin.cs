using System.Collections.Generic;
using UnityEngine;

// A coin lying in the arena: Freny collects it by touching it. Enemies drop one where they die;
// a broken chest tosses a handful that fly out in a small arc and fall to rest just above the floor.
public class Coin : MonoBehaviour {

    public int coins = 1;
    public GameObject coinCount;

    private const float TossGravity = -18f;
    private static int groundMask = -1;
    private static int GroundMask => groundMask >= 0 ? groundMask : (groundMask = LayerMask.GetMask("Ground"));

    private const float BobHeight = 0.06f;  // bobs up from its resting spot, never into the floor
    private const float RestLift = 0.2f;    // pivot above the floor
    private const float MaxFallSpeed = 14f;
    private const float Lifetime = 29f;   // dropped coins: 25 s steady, then blink for the last 4 (like the items)
    private const float BlinkFor = 4f;
    private float dieAt = -1f;            // < 0: a coin placed in a level, it stays
    private SpriteRenderer sprite;

    private bool tossing;
    private bool hovering;
    private float hoverY;
    private float bobPhase;
    private Vector2 velocity;
    private Rigidbody2D rb; // the prefab has one (no gravity) for the pickup trigger; the arc moves it directly
    private bool pulled;    // on its way to Freny (the Ímã, ArenaPowerUps)

    // coins lying around, so the autopilot can go and collect them
    public static readonly List<Coin> Active = new List<Coin>();

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    // flies along a small arc and stops on the first floor it meets (no Rigidbody, so it can't
    // push anything or be pushed)
    public void Toss(Vector2 initialVelocity) {
        velocity = initialVelocity;
        tossing = true;
        rb = GetComponent<Rigidbody2D>();
        Expire();
        CoopShared.CoinSpawned(this, initialVelocity);
    }

    // a dropped coin (enemy or chest) disappears after a while, blinking first
    public void Expire() {
        dieAt = Time.time + Lifetime;
        sprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update() {
        if (dieAt >= 0f) {
            if (Time.time >= dieAt) {
                Destroy(gameObject);
                return;
            }
            if (sprite) {
                sprite.enabled = dieAt - Time.time > BlinkFor || Mathf.Repeat(Time.time, 0.3f) < 0.18f;
            }
        }
        if (ArenaPowerUps.PullTarget(out Vector3 pullTo)) {
            pulled = true;
            tossing = hovering = false;
            Place(ArenaPowerUps.PulledFrom(transform.position, pullTo));
            return;
        }
        if (pulled) {
            // the magnet ran out with it in the air: it falls from there
            pulled = false;
            tossing = true;
            velocity = Vector2.zero;
        }
        if (hovering) {
            // rests just above the floor, bobbing a little, easy to see and grab
            Vector3 p = transform.position;
            p.y = hoverY + (Mathf.Sin(Time.time * 3f + bobPhase) + 1f) * 0.5f * BobHeight;
            Place(p);
            return;
        }
        if (!tossing) {
            return;
        }

        velocity.y = Mathf.Max(velocity.y + TossGravity * Time.deltaTime, -MaxFallSpeed);
        Vector3 pos = transform.position;
        Vector3 next = pos + (Vector3)(velocity * Time.deltaTime);

        if (velocity.y < 0f) {
            float drop = pos.y - next.y;
            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.down, drop + RestLift, GroundMask);
            if (hit.collider != null) {
                next.y = hit.point.y + RestLift;
                tossing = false;
                hovering = true;
                hoverY = next.y;
                bobPhase = Random.Range(0f, Mathf.PI * 2f);
            }
        }
        if (next.y < -50f || (ArenaBounds.Active && next.y < ArenaBounds.Bottom - 3f)) {
            Destroy(gameObject); // fell out of the world
            return;
        }
        Place(next);
    }

    private void Place(Vector3 position) {
        if (rb) {
            rb.position = position;
        } else {
            transform.position = position;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) {
            return;
        }
        Player player = other.GetComponent<Player>();
        if (player != null) {
            if (CoopShared.AskToPick(this)) {
                return;
            }
            CoopShared.Picked(this);
            player.AddCoins(RunModifiers.ScaleCoins(coins));   // rebirths, "Chuva de moedas"
            Sfx.Play("coin", transform.position);
            Destroy(gameObject);
        }
    }
}
