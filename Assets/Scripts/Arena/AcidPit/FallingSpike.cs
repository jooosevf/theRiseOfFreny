using System.Collections.Generic;
using UnityEngine;

// A spike that breaks off a ceiling of the Poço de Ácido over Freny's head (AcidPitArena drops
// one now and then): it hangs there shaking and shedding grit for ShakeSeconds, the warning, then
// falls straight down. It takes a heart from Freny and hurts any enemy it lands on (no points for
// him), and shatters on the floor. The art is the project's unused "Falling spike" sheet.
public class FallingSpike : MonoBehaviour {
    private const float ShakeSeconds = 1.1f;
    private const float Gravity = 28f;
    private const float MaxSpeed = 22f;
    private const float TipBelowCeiling = 0.75f;   // the point of the spike, under where it hangs
    private const int Damage = 1;
    private const float ShatterFps = 12f;

    // hanging or falling now (the autopilot steps from under them)
    public static readonly List<FallingSpike> Active = new List<FallingSpike>();

    private static Sprite[] frames;
    private SpriteRenderer sprite;
    private Vector3 home;
    private float hungAt;
    private float speed;
    private float floorY;
    private bool falling;
    private bool shattered;
    private float shatteredAt;
    private readonly HashSet<Enemy> hit = new HashSet<Enemy>();

    public float X => home.x;
    public float Tip => transform.position.y + 0.5f - TipBelowCeiling;
    public bool Falling => falling && !shattered;
    public bool Dangerous => !shattered;

    public static FallingSpike Hang(Vector3 ceiling) {
        if (frames == null || frames.Length == 0) {
            frames = FrameAnimator.Load("Traps/Falling spike");
        }
        if (frames.Length < 14) {
            return null;
        }
        GameObject go = new GameObject("Falling Spike");
        FallingSpike spike = go.AddComponent<FallingSpike>();
        // the frame is a 1-unit square with the spike in its top part: its middle half a unit down
        spike.home = ceiling + Vector3.down * 0.5f;
        go.transform.position = spike.home;
        spike.sprite = go.AddComponent<SpriteRenderer>();
        spike.sprite.sprite = frames[0];
        spike.sprite.sortingOrder = 48;
        spike.sprite.color = new Color(0.85f, 1f, 0.8f);
        spike.hungAt = Time.time;
        RaycastHit2D below = Physics2D.Raycast(new Vector2(ceiling.x, ceiling.y - 0.9f), Vector2.down, 30f, LayerMask.GetMask("Ground"));
        spike.floorY = below.collider != null ? below.point.y : (ArenaBounds.Active ? ArenaBounds.Bottom - 2f : ceiling.y - 30f);
        Sfx.Play("chest_break", ceiling, 0.5f, 1.7f);
        return spike;
    }

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    private void Update() {
        if (shattered) {
            int frame = 10 + (int)((Time.time - shatteredAt) * ShatterFps);
            if (frame >= frames.Length) {
                Destroy(gameObject);
                return;
            }
            sprite.sprite = frames[frame];
            return;
        }
        if (!falling) {
            // shaking, a little harder towards the end, and grit falling off the ceiling
            float t = (Time.time - hungAt) / ShakeSeconds;
            sprite.sprite = frames[(int)(Time.time * 14f) % 4];
            transform.position = home + Vector3.right * (Mathf.Sin(Time.time * 60f) * Mathf.Lerp(0.02f, 0.07f, t));
            if (Random.value < 0.15f) {
                Fx.Dust(home + Vector3.up * 0.45f, 1);
            }
            if (t >= 1f) {
                falling = true;
                transform.position = home;
                sprite.sprite = frames[0];
                Sfx.Play("chest_break", home, 0.7f, 1.2f);
            }
            return;
        }
        speed = Mathf.Min(MaxSpeed, speed + Gravity * Time.deltaTime);
        transform.position += Vector3.down * (speed * Time.deltaTime);
        HitThings();
        if (Tip <= floorY) {
            transform.position = new Vector3(home.x, floorY + TipBelowCeiling - 0.5f, 0f);
            Shatter();
        }
    }

    private void HitThings() {
        Vector2 tip = new Vector2(home.x, Tip);
        Player freny = FindAnyObjectByType<Player>();
        if (freny != null && !freny.IsDead) {
            Vector3 body = freny.BodyCenter;
            if (Mathf.Abs(tip.x - body.x) < 0.45f && tip.y < body.y + freny.BodyHeight * 0.5f && tip.y > body.y - freny.BodyHeight * 0.5f) {
                if (freny.TryDamage(Damage)) {
                    freny.Knockback(new Vector2(Mathf.Sign(body.x - tip.x + 0.01f) * 3f, 1f), false);
                }
                Shatter();
                return;
            }
        }
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy == null || enemy.IsDead || hit.Contains(enemy)) {
                continue;
            }
            if (enemy.BodyBounds.Contains(new Vector3(tip.x, tip.y, enemy.BodyBounds.center.z))) {
                hit.Add(enemy);
                enemy.HitBy(null, 2);   // not Freny's hit: no points
                Shatter();
                return;
            }
        }
    }

    private void Shatter() {
        if (shattered) {
            return;
        }
        shattered = true;
        shatteredAt = Time.time;
        Fx.Burst(new Vector3(home.x, Tip, 0f), new Color(0.55f, 0.5f, 0.45f), 12, 3f, 0.08f, 0.4f);
        Sfx.Play("chest_break", transform.position, 0.8f, 0.9f);
    }
}
