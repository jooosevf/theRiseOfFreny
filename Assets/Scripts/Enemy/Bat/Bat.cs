using UnityEngine;

// Flying enemy: hovers near its spawn point and, when Freny comes into sight, swoops at him.
// Next to him it stops, rears back and flashes for a moment (the wind-up), then lunges and bites;
// he can strike it or step away during the wind-up. After a bite it pulls back before coming in
// again. It ignores platforms, so it reaches him wherever he is.
public class Bat : Enemy {

    [SerializeField] private float hoverAmplitude = 0.4f;
    [SerializeField] private float hoverFrequency = 2.5f;
    [SerializeField] private float biteDistance = 0.9f;   // horizontal, when at body height
    [SerializeField] private float biteCooldown = 1.4f;
    [SerializeField] private float sideOffset = 0.7f;     // comes in beside Freny, not down onto his head
    [SerializeField] private float biteWindup = 0.45f;    // seconds it hangs back, flashing, before the bite
    [SerializeField] private float rearBack = 0.45f;      // how far it pulls away from him during the wind-up
    [SerializeField] private float retreatTime = 0.5f;    // seconds it backs off after a bite
    private static readonly Color WindupTint = new Color(1f, 0.45f, 0.45f);
    private float side = 1f;

    private Vector3 home;
    private float nextBiteTime;
    private float phase;
    private float windupEnd = -1f;  // >= 0 while winding up
    private float retreatUntil;
    private int healthAtWindup;

    protected override Color DeathColor => new Color(0.35f, 0.3f, 0.4f);
    protected override string SoundName => "bat";
    protected override bool WalksOnGround => false; // flies: no floor, no falling
    protected override bool Small => true;
    protected override float SwarmTopSpeed => 4.5f;   // still under Freny's run (4.8)

    public override void Init() {
        base.Init();
        home = transform.position;
        phase = Random.Range(0f, Mathf.PI * 2f);
        if (rb) {
            rb.gravityScale = 0f;
        }
    }

    public override void Update() {
        if (isDead || player == null || player_t == null || FellOutOfWorld()) {
            EndWindup();
            return;
        }

        // an enemy that hit it comes first, then Freny
        UpdateRival();
        Enemy foe = Rival;
        distance = Vector2.Distance(transform.position, player_t.position);
        // in the arena it comes for him from anywhere while there is room among the chasers
        bool nearby = distance <= radius * Tuning.EnemyAggro.Value || isHit;
        bool chasing = foe != null || (player.Health > 0 && JoinChase(nearby));
        if (foe != null || player.Health <= 0) {
            LeaveChase();
        }
        SetInCombat(chasing);

        // a smaller bob while chasing, so it stays where the sword and the bullets reach
        float bob = Mathf.Sin(Time.time * hoverFrequency + phase) * hoverAmplitude * (chasing ? 0.3f : 1f);
        Vector3 body = foe != null ? (Vector3)foe.Center : player.BodyCenter;
        Vector3 centerOffset = (Vector3)Center - transform.position; // its own body vs its pivot
        Vector3 goal;
        if (chasing) {
            // approach from the side it is already on, its body level with the middle of him
            // (aiming at player_t put it at head height, over the sword and the shots)
            if (Mathf.Abs(transform.position.x - body.x) > sideOffset * 1.5f) {
                side = Mathf.Sign(transform.position.x - body.x);
            }
            goal = body + new Vector3(side * sideOffset, 0f, 0f) - centerOffset;
            if (windupEnd >= 0f) {
                // rear back, a little up and away from him, so the bite is readable
                goal += new Vector3(side * rearBack, rearBack * 0.5f, 0f);
            } else if (Time.time < retreatUntil) {
                goal += new Vector3(side * 1.6f, 0.8f, 0f);
            }
        } else {
            goal = home;
        }
        goal.y += bob;
        float speedNow = MoveSpeed * (chasing ? 1f : 0.5f);
        if (windupEnd >= 0f) {
            speedNow *= 0.5f;
        }

        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, goal, speedNow * Time.deltaTime);
        ClampToArena();

        float dx = transform.position.x - before.x;
        if (windupEnd >= 0f) {
            dx = body.x - transform.position.x; // keeps facing him while it rears back
        }
        if (Mathf.Abs(dx) > 0.0001f) {
            // sprites face left by default, so scale.x = -1 means looking right
            transform.localScale = new Vector3(dx > 0 ? -1 : 1, 1, 1);
        }

        float dxToPlayer = Mathf.Abs(Center.x - body.x);
        float dyToPlayer = Mathf.Abs(Center.y - body.y);
        if (!chasing) {
            EndWindup();
            return;
        }
        if (windupEnd < 0f) {
            if (dxToPlayer <= biteDistance && dyToPlayer <= 0.8f && Time.time >= nextBiteTime && Time.time >= retreatUntil && !Netted) {
                windupEnd = Time.time + biteWindup * Tuning.EnemyWindup.Value;
                healthAtWindup = Health;
                PlaySound("windup");
            }
            return;
        }

        // flashing while it winds up
        if (sprite) {
            sprite.color = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? WindupTint : Color.white;
        }
        if (Health < healthAtWindup) {
            // struck during the wind-up: the bite is cancelled
            EndWindup();
            nextBiteTime = Time.time + biteCooldown * 0.5f * Tuning.EnemyCooldown.Value * DifficultyCooldown;
            return;
        }
        if (Time.time < windupEnd) {
            return;
        }

        // the lunge: he can still have moved out of reach (it rears back, so the reach is longer)
        EndWindup();
        PlaySound("attack");
        Vector3 lunge = new Vector3(-side * rearBack, -rearBack * 0.5f, 0f);
        transform.position += lunge;
        ClampToArena();
        dxToPlayer = Mathf.Abs(Center.x - body.x);
        dyToPlayer = Mathf.Abs(Center.y - body.y);
        if (dxToPlayer <= biteDistance + 0.3f && dyToPlayer <= 1f) {
            if (foe != null) {
                foe.HitBy(this, HitDamage);
            } else {
                HitPlayer();
            }
        }
        nextBiteTime = Time.time + biteCooldown * Tuning.EnemyCooldown.Value * DifficultyCooldown;
        retreatUntil = Time.time + retreatTime * DifficultyCooldown;
    }

    private void EndWindup() {
        windupEnd = -1f;
        if (sprite) {
            sprite.color = Color.white;
        }
    }

}
