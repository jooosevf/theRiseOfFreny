using UnityEngine;

// The Ciclope's smash: a wave of dust that runs along the floor. It hurts Freny (2 hearts and a
// big knockback) when it passes under his feet while he is on or near the floor: jump over it.
public class Shockwave : MonoBehaviour {
    public const float Speed = 9f;
    private const float Range = 10f;
    private const float Reach = 0.55f;    // half its width
    private const float Height = 0.8f;    // feet higher than this above the floor are safe
    private const int Damage = 2;
    private const float Knockback = 24f;   // as big as the punch's (José, 2026-10-29)

    private float dir;
    private float travelled;
    private float floorY;
    private float nextDust;
    private static Player freny;

    // the waves running now (the autopilot jumps over them)
    public static readonly System.Collections.Generic.List<Shockwave> Active = new System.Collections.Generic.List<Shockwave>();
    public float Direction => dir;
    public float FloorY => floorY;

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    public static void Launch(Vector3 feet, float dir) {
        Shockwave wave = new GameObject("Shockwave").AddComponent<Shockwave>();
        wave.transform.position = feet;
        wave.dir = dir;
        wave.floorY = feet.y;
    }

    private void Update() {
        float step = Speed * Time.deltaTime;
        transform.position += Vector3.right * (dir * step);
        travelled += step;
        if (Time.time >= nextDust) {
            nextDust = Time.time + 0.04f;
            Fx.Dust(transform.position, 3);
        }
        if (freny == null) {
            freny = FindAnyObjectByType<Player>();
        }
        if (freny != null && !freny.IsDead) {
            Vector3 feet = freny.Feet;
            if (Mathf.Abs(feet.x - transform.position.x) < Reach && feet.y >= floorY - 0.3f && feet.y <= floorY + Height
                && Hurt(freny)) {
                freny.Knockback(new Vector2(dir * Knockback, Knockback * 0.6f));
                Destroy(gameObject);
                return;
            }
        }
        if (travelled >= Range || (ArenaBounds.Active && ArenaBounds.ClampX(transform.position.x, 0f) != transform.position.x)) {
            Destroy(gameObject);
        }
    }

    private static bool Hurt(Player freny) {
        Player.NextDamageSource = PlayerStats.CauseShockwave;
        return freny.TryDamage(Damage);
    }
}
