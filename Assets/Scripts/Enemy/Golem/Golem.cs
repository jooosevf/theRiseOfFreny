using UnityEngine;

// Heavy melee enemy: slow but stubborn (sight 7 in the prefab, and it keeps after Freny), and its
// punch takes two hearts and shoves him back (knockback in the prefab). Everything else is the Enemy base.
public class Golem : Enemy {
    // the red Golem (Golem_Red): a tank. Its health, coins and score are in the prefab and its red
    // frames come from SpriteSkin; here it only shrugs off Freny's pushes and bleeds red sparks
    [SerializeField] private bool armored = false;
    public bool Armored => armored;
    public const int MaxRed = 3;
    public static int RedAlive {
        get {
            int count = 0;
            foreach (Enemy enemy in Active) {
                if (enemy is Golem golem && golem.armored && !golem.IsDead) {
                    count++;
                }
            }
            return count;
        }
    }

    protected override int HitDamage => 2;
    protected override float HitPush => armored ? 1f : 3f; // heavy: Freny's hits barely move it
    protected override Color DeathColor => armored ? new Color(0.8f, 0.25f, 0.2f) : new Color(0.6f, 0.55f, 0.5f);
    protected override string SoundName => "golem";
    // the red Golem charges its punch and dashes forward with it (José, 2026-10-29: Freny is too
    // quick for a punch that stays put)
    protected override float LungeDistance => armored ? 2.2f : 0f;

    // the red Golem also lobs a mine (GolemMine) at Freny when he keeps his distance
    private const float ThrowCooldown = 6f;
    private const float FirstThrowDelay = 3f;
    private const float ThrowMinDistance = 3f;
    private const float ThrowMaxDistance = 10f;
    private const float ThrowAngle = 55f;
    private const float ThrowMaxSpeed = 12f;
    private float nextThrow;
    // José, 2026-10-30: two or three of them threw at once and the mines stacked up: after any red
    // Golem throws, none throws again for SharedThrowGap seconds
    private const float SharedThrowGap = 3f;
    private static float nextAnyThrow;

    public override void Init() {
        base.Init();
        nextThrow = Time.time + FirstThrowDelay;
        if (armored) {
            attackWindup = 0.6f;   // a longer charge, the dash comes in its last third
        }
    }

    public override void Update() {
        base.Update();
        if (armored) {
            TryThrowMine();
        }
    }

    private void TryThrowMine() {
        if (isDead || player == null || player.Health <= 0 || IsAttacking || Time.time < nextThrow || Time.time < nextAnyThrow) {
            return;
        }
        Vector3 from = Center + Vector3.up * (HalfHeight * 0.6f);
        Vector2 to = player.transform.position;
        float dx = to.x - from.x, dy = to.y - from.y;
        if (Mathf.Abs(dx) < ThrowMinDistance || Mathf.Abs(dx) > ThrowMaxDistance || Mathf.Abs(dy) > 5f) {
            return;
        }
        nextThrow = Time.time + ThrowCooldown * ScoreCooldown;
        nextAnyThrow = Time.time + SharedThrowGap;
        // the speed that lands it on Freny on the fixed angle (as Player.GrenadeChargeFor)
        float rad = ThrowAngle * Mathf.Deg2Rad;
        float g = Mathf.Abs(Physics2D.gravity.y) * GolemMine.Gravity;
        float x = Mathf.Abs(dx);
        float denom = 2f * Mathf.Cos(rad) * Mathf.Cos(rad) * (x * Mathf.Tan(rad) - dy);
        float speed = denom > 0.01f ? Mathf.Min(ThrowMaxSpeed, Mathf.Sqrt(g * x * x / denom)) : ThrowMaxSpeed;
        Vector2 velocity = new Vector2(Mathf.Sign(dx) * Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        GolemMine.Throw(this, from, velocity);
    }
}
