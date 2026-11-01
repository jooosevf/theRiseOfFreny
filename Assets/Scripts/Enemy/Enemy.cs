using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base of every enemy: sight, patrol, chase, the wind-up attack, taking hits and dying. Subclasses
// (Golem, Bug, Licker) only pick their colours; Bat and Sorcerer override Update for their own
// movement and attacks. Health and death live here, so every enemy dies the same way.
public abstract class Enemy : MonoBehaviour, IDamageable
{
    // every enemy in the scene, kept up to date by OnEnable/OnDisable, so the autopilot
    // doesn't need FindObjectsOfType (which walks the whole scene) to find them
    public static readonly List<Enemy> Active = new List<Enemy>();

    protected virtual void OnEnable() {
        Active.Add(this);
    }

    protected virtual void OnDisable() {
        Active.Remove(this);
        chasers.Remove(this);
    }

    // in the arena every enemy knows where Freny is, but only ArenaDirector.MaxChasers of them
    // go after him from afar at once; the others patrol until a place frees up. Those that see
    // him up close (their own sight) or were hit always fight.
    private static readonly HashSet<Enemy> chasers = new HashSet<Enemy>();

    // true when it should chase Freny; nearby = he is in its own sight, or it was provoked
    protected bool JoinChase(bool nearby) {
        bool chase = nearby || (ArenaBounds.Active && (chasers.Contains(this) || chasers.Count < ArenaDirector.MaxChasers));
        if (chase) {
            chasers.Add(this);
        } else {
            chasers.Remove(this);
        }
        return chase;
    }

    protected void LeaveChase() {
        chasers.Remove(this);
    }

    public GameObject coinPrefab;

    [SerializeField]
    protected int health;
    [SerializeField]
    protected int speed;
    [SerializeField]
    protected int coins;
    [SerializeField]
    public Transform pointA, pointB;
    [SerializeField]
    public int scoreToAdd;

    public Vector3 currentTarget;
    protected Animator anim;
    protected SpriteRenderer sprite;
    protected bool isDead = false;
    public bool IsDead => isDead;
    public int Health { get; set; }

    protected bool isHit; // provoked: fights back even outside its sight
    protected float distance;
    protected Transform player_t;
    protected Player player;
    public string spawnerUuid;
    // sight: enemies look sideways. They only notice Freny within `radius` horizontally and
    // `sightHeight` vertically, so a Golem on a platform ignores him walking underneath
    public float radius;
    [SerializeField]
    protected float sightHeight = 1.5f;
    [SerializeField]
    protected float rearSightFactor = 0.6f; // behind its back it only notices Freny this fraction of `radius` away
    // gap between the enemy's collider and the player's collider (edge to edge, not centre to centre)
    public float attackDistance = 0.5f;
    [SerializeField]
    protected float attackWindup = 0.35f; // seconds between the Attack animation and the hit
    [SerializeField]
    protected float knockback = 0f; // a heavy hit: shoves Freny this hard and knocks him down (0 = only the light shove)
    // every hit pushes Freny back a little, so two enemies can't keep him pinned between them
    private const float LightKnockback = 4f;
    private bool isPlayerInRange = false;
    private float patrolPositionTolerance = 0.1f;
    public LayerMask ignoreLayer;
    protected Rigidbody2D rb;
    [SerializeField]
    protected float attackCooldown = 1.5f;
    private float nextAttackTime;
    private bool hasAttackTrigger;
    protected Collider2D col;
    private Collider2D playerCol;
    private Coroutine attackRoutine;
    private bool hasMovingParam;
    private bool hasInCombatParam;
    private bool hasHitTrigger;
    [SerializeField]
    protected float patrolPause = 0.8f; // seconds standing at each end of the patrol
    private float patrolPauseUntil;
    [SerializeField]
    protected float provokedRadius = 10f; // how far a hit enemy keeps chasing
    [SerializeField]
    protected float keepChasingFactor = 1.75f; // once chasing, it only gives up when Freny is this many times its sight away
    // ---- enemies hit each other: a swing at Freny also catches whoever stands in front of it, and a
    // hexball hits whatever it crosses. The one hit turns on whoever hit it and they fight
    private Enemy rival;
    private float rivalUntil;
    private const float RivalMemory = 6f;    // a fight with another enemy lasts this long after its last hit
    private static readonly List<Enemy> bystanders = new List<Enemy>();
    private float nextDustTime;
    private float fallVelocity;      // walkers fall through StickToGround (there is no physics gravity on them)
    private static int groundMask = -1;
    protected static int GroundMask => groundMask >= 0 ? groundMask : (groundMask = LayerMask.GetMask("Ground"));
    private static readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private const float FallGravity = 20f;
    private const float MaxFallSpeed = 12f;
    private const float StepUp = 0.35f;      // a floor this little above the feet is a step, anything higher is a wall

    // ---- moving between platforms: walkers jump up to a platform, hop a gap or drop off an edge to
    // reach Freny, then catch their breath; after a swing they rest and back off before closing in again
    [SerializeField]
    protected float maxJumpHeight = 3.2f;    // the highest platform above its feet it can jump onto
    [SerializeField]
    protected float jumpCooldown = 1.2f;
    [SerializeField]
    protected float landPause = 0.45f;       // stands still a moment after landing
    [SerializeField]
    protected float restAfterAttack = 0.5f;  // stands still after a swing...
    [SerializeField]
    protected Vector2 backOffTime = new Vector2(0.6f, 1.2f); // ...then walks away for a moment
    private const float ClimbSight = 4.5f;   // notices Freny this far above or below
    private bool airborne;                   // jumping or dropping (Update moves it, not Movement)
    private bool rising;                     // the upward part of a jump
    private bool fellSinceTakeoff;
    private float airborneSince;
    private Vector2 airVelocity;
    private float nextJumpTime;
    private float restUntil;
    private float backOffUntil;
    private Transform ownPatrolA, ownPatrolB; // the patrol ends after it changed platforms
    private float dropDir;                   // the side of its platform it walks to, to drop down to the target (0: none)
    private float dropEdgeX;
    private float nextDropPlan;
    private const float DropScan = 16f;      // how far along its platform it looks for an edge

    // the collider's shape relative to the root, cached once. Feet and Center are computed from the
    // transform with it, not read from col.bounds: the bounds lag one physics step behind a transform
    // move, so at high frame rates an enemy was already past the floor before its bounds said so
    private Vector2 colOffset;
    private float colHalfWidth = 0.3f;
    private float colHalfHeight = 0.5f;
    private bool geometryCached;

    // ground enemies are moved by their transform and kept on the floor by StickToGround; the Bat flies
    protected virtual bool WalksOnGround => true;
    // the one-hit enemies (Bug, Bat), which the arena's harder levels speed up the most. Any hit kills
    // them, whatever the prefab or the scene says
    protected virtual bool Small => false;
    // how hard Freny's hits push it back (units per second at the start of the push; heavy ones less)
    protected virtual float HitPush => 6f;
    private float pushVelocity;              // > 0 right, < 0 left; fades out over a moment
    private float pushScale = 1f;            // set for one sword hit by Damage(amount, push)
    private const float PushDrag = 40f;
    // arena difficulty on attack rhythm (1 outside the arena)
    protected float DifficultyCooldown => ArenaBounds.Active ? ArenaDifficulty.CooldownFactor * ArenaDirector.CooldownRamp * ScoreCooldown : 1f;

    // José, 2026-10-29: the more points, the shorter every enemy's cooldown: 1x at 0 down to
    // ScoreCooldownHold at the first Ciclope (50 000), held there until a million (it got too hard
    // too fast after the first Ciclope), then down to 0.5x at 1 500 000 and no lower
    public const int ScoreCooldownHoldFrom = 50000;
    public const int ScoreCooldownHoldUntil = 1000000;
    public const int ScoreCooldownFloorAt = 1500000;
    public const float ScoreCooldownHold = 0.85f;
    public const float ScoreCooldownFloor = 0.5f;
    public static float ScoreCooldown {
        get {
            int score = ArenaBounds.Active && UIManager.Instance ? UIManager.Instance.Score : 0;
            if (score <= ScoreCooldownHoldFrom) {
                return Mathf.Lerp(1f, ScoreCooldownHold, (float)score / ScoreCooldownHoldFrom);
            }
            if (score <= ScoreCooldownHoldUntil) {
                return ScoreCooldownHold;
            }
            return Mathf.Lerp(ScoreCooldownHold, ScoreCooldownFloor, (float)(score - ScoreCooldownHoldUntil) / (ScoreCooldownFloorAt - ScoreCooldownHoldUntil));
        }
    }

    // colour of the particles when this enemy is hit or dies
    protected virtual Color DeathColor => new Color(0.55f, 0.5f, 0.45f);

    // prefix of this enemy's sounds in Resources/Sfx: "golem" plays golem_attack, golem_hurt,
    // golem_death, golem_step... (see Sfx); null for none
    protected virtual string SoundName => null;

    // lower for a bigger body (the Ciclope uses the Golem's sounds, deeper)
    protected virtual float SoundPitch => 1f;

    protected void PlaySound(string what) {
        if (SoundName != null) {
            Sfx.Play(SoundName + "_" + what, Center, 1f, SoundPitch);
        }
    }

    public virtual void Init() {
        anim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) {
            player_t = playerObj.transform;
            player = playerObj.GetComponent<Player>();
        }
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CacheGeometry();
        if (rb && WalksOnGround) {
            // a walker's body is kinematic: StickToGround is its gravity and nothing pushes it around
            // (the Golem was a dynamic body on top of that, and the campaign's ChangeGroundArea made it jump)
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.SetVel(Vector2.zero);
        }
        playerCol = player ? player.GetComponent<Collider2D>() : null;
        if (playerCol != null) {
            // in every mode: a kinematic body would shove Freny around, so he walks through enemies
            // and the fight happens with the sword and the guns. Per instance: the project's layer
            // matrix (which Play mode edits would save) is never touched
            foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true)) {
                if (!c.isTrigger) {
                    Physics2D.IgnoreCollision(playerCol, c, true);
                }
            }
        }
        Health = Small ? 1 : health; // the inspector value
        fullHealth = Health;
        EnemyHealthBar.Attach(this);
        hasAttackTrigger = anim && HasParameter(anim, "Attack");
        hasMovingParam = anim && HasParameter(anim, "Moving");
        hasInCombatParam = anim && HasParameter(anim, "InCombat");
        hasHitTrigger = anim && HasParameter(anim, "Hit");
    }

    // the Bat's controller has no parameters, so every animator call checks first
    protected void SetInCombat(bool value) {
        if (hasInCombatParam) {
            anim.SetBool("InCombat", value);
        }
    }

    private void Start() {
        Init();
    }

    public virtual void Update() {
        if (isDead) {
            return;
        }
        if (FellOutOfWorld()) {
            return;
        }

        UpdateRival();
        if (rival != null) {
            // fighting another enemy: it goes after that one, wherever Freny is
            distance = Vector2.Distance(Center, rival.Center);
            LeaveChase(); // busy with that one: its place goes to another
            if (!isPlayerInRange) {
                isPlayerInRange = true;
                SetInCombat(true);
            }
        } else if (player_t && player != null) {
            distance = Vector2.Distance(transform.position, player_t.position);
            float dx = Mathf.Abs(player_t.position.x - transform.position.x);
            float dy = Mathf.Abs(player_t.position.y - transform.position.y);

            // chase when the player enters the (horizontal) sight, or after being hit (even enemies
            // with radius 0 fight back instead of standing still). "Same level" means Freny's body
            // overlaps the enemy's own height band, so a platform below or above is out of sight.
            bool sameLevel = dy <= sightHeight;
            if (col != null && playerCol != null) {
                Bounds mine = col.bounds;
                Bounds theirs = playerCol.bounds;
                sameLevel = theirs.max.y > mine.min.y + 0.2f && theirs.min.y < mine.max.y - 0.2f;
            }
            // sight is shorter behind the back, but not zero: Freny sneaking up still gets noticed
            bool playerBehind = (player_t.position.x > transform.position.x) != FacingRight;
            float sight = radius * Tuning.EnemyAggro.Value * (playerBehind ? rearSightFactor : 1f);
            if (isPlayerInRange) {
                sight *= keepChasingFactor; // already after him: it doesn't let go the moment he steps out of sight
            }
            bool provoked = isHit && distance <= provokedRadius;
            // walkers also go after him on a platform above or below (they jump and drop, see AttackMode)
            bool reachable = sameLevel || (Climbs && dy <= ClimbSight);
            bool nearby = reachable && (dx <= sight || provoked);
            // from afar only walkers that climb come for him (they work their way up or down)
            bool shouldChase = player.Health > 0 && (sameLevel || Climbs || nearby) && JoinChase(nearby);
            if (player.Health <= 0) {
                LeaveChase();
            }

            if (shouldChase && !isPlayerInRange) {
                isPlayerInRange = true;
                SetInCombat(true);
            } else if (!shouldChase && isPlayerInRange) {
                isPlayerInRange = false;
                ResetAttackMode();
            }
        }

        Vector3 before = transform.position;
        if (airborne) {
            MoveInAir();
        } else if (UpdatePush()) {
            if (WalksOnGround) {
                StickToGround();
            }
        } else {
            Movement();
            if (WalksOnGround) {
                StickToGround();
            }
        }
        ClampToArena();
        bool moving = Mathf.Abs(transform.position.x - before.x) > 0.0001f;
        if (hasMovingParam) {
            // Walk/Idle follow real movement, so the walk cycle never plays while standing still
            anim.SetBool("Moving", moving);
        }
        if (moving && !airborne && Time.time >= nextDustTime) {
            Fx.Dust(Feet, 1);
            PlaySound("step");
            nextDustTime = Time.time + 0.2f;
        }
    }

    // Keeps the feet on the floor under the body and lets the enemy fall when there is none. The ray
    // starts at the top of the body, never at the feet: a ray that starts inside a collider reports
    // nothing (the project's "queries start in colliders" is off), so feet that had sunk a little into
    // a thin platform saw no floor at all and the enemy fell through it. Floors between the body's top
    // and the feet that are higher than a step are ignored (a ledge beside the head is not a floor).
    protected void StickToGround() {
        if (col == null) {
            return;
        }
        Vector3 feet = Feet;
        float height = Mathf.Max(0.2f, HalfHeight * 2f);
        float dt = Time.deltaTime;
        float fallStep = fallVelocity * dt;
        float probe = height + Mathf.Max(0.3f, fallStep + 0.1f);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GroundMask);
        // first from just above the feet: a ray reports each collider once, so from a tall
        // enemy's head height it met a step of the same tilemap above the feet and missed the
        // floor under them (the Ciclope sank through the arena floor beside the side steps)
        float floorY = FloorBelow(feet, StepUp + 0.05f, StepUp + 0.05f + Mathf.Max(0.3f, fallStep + 0.1f), filter);
        if (floorY == float.NegativeInfinity) {
            floorY = FloorBelow(feet, height, probe, filter);
        }

        if (floorY > float.NegativeInfinity) {
            float delta = floorY - feet.y; // > 0: sank in or a small step up, < 0: the floor is below
            if (delta >= -0.02f) {
                fallVelocity = 0f;
                if (Mathf.Abs(delta) > 0.002f) {
                    transform.position += Vector3.up * delta;
                }
                return;
            }
            // a floor within reach below: fall onto it, never past it
            fallVelocity = Mathf.Min(fallVelocity + FallGravity * dt, MaxFallSpeed);
            float step = fallVelocity * dt;
            if (step >= -delta) {
                transform.position += Vector3.up * delta;
                fallVelocity = 0f;
            } else {
                transform.position += Vector3.down * step;
            }
            return;
        }

        fallVelocity = Mathf.Min(fallVelocity + FallGravity * dt, MaxFallSpeed);
        transform.position += Vector3.down * (fallVelocity * dt);
    }

    // the highest ground top at most StepUp above the feet, on a ray cast down from lift above them
    private float FloorBelow(Vector3 feet, float lift, float probe, ContactFilter2D filter) {
        int count = Physics2D.Raycast(new Vector2(feet.x, feet.y + lift), Vector2.down, filter, groundHits, probe);
        float floorY = float.NegativeInfinity;
        for (int i = 0; i < count; i++) {
            float y = groundHits[i].point.y;
            if (y <= feet.y + StepUp && y > floorY) {
                floorY = y;
            }
        }
        return floorY;
    }

    // is there floor to step on just ahead (within one unit below the feet)? Ground enemies stop
    // or turn at platform edges instead of walking off into a hole
    protected bool GroundAhead(float direction) {
        if (col == null) {
            return true;
        }
        Vector3 feet = Feet;
        float ahead = HalfWidth + 0.15f;
        Vector2 from = new Vector2(feet.x + direction * ahead, feet.y + 0.3f);
        return Physics2D.Raycast(from, Vector2.down, 1.3f, GroundMask).collider != null;
    }

    // a wall or spikes right ahead. Enemies move by their transform, so nothing physical stops them:
    // without this they walked through walls and out of the stage, and into spikes
    protected bool Blocked(float direction) {
        if (col == null) {
            return false;
        }
        Vector3 feet = Feet;
        float x = feet.x + direction * (HalfWidth + 0.15f);
        return WallAhead(direction) || Hazard.At(new Vector2(x, feet.y + 0.1f));
    }

    // solid ground (not a one-way ledge) beside the body, above step height
    protected bool WallAhead(float direction) {
        if (col == null) {
            return false;
        }
        Vector3 feet = Feet;
        float reach = HalfWidth + 0.12f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GroundMask);
        float top = feet.y + Mathf.Max(StepUp + 0.05f, HalfHeight * 2f - 0.1f);
        for (float y = feet.y + StepUp + 0.05f; y <= top + 0.001f; y += Mathf.Max(0.2f, (top - feet.y - StepUp) * 0.5f)) {
            int count = Physics2D.Raycast(new Vector2(feet.x, y), new Vector2(direction, 0f), filter, groundHits, reach);
            for (int i = 0; i < count; i++) {
                if (!groundHits[i].collider.usedByEffector) {
                    return true;
                }
            }
        }
        return false;
    }

    // puts the feet (the collider's bottom edge) on `floorPoint`. Spawners call this right after
    // Instantiate, before Start: the root sits above the feet by the collider's offset, so placing the
    // root on the floor left the Bug's feet 0.46 inside it
    public void PlaceFeetAt(Vector2 floorPoint) {
        if (!geometryCached) {
            CacheGeometry();
        }
        Vector3 feet = Feet;
        transform.position += new Vector3(floorPoint.x - feet.x, floorPoint.y + 0.01f - feet.y, 0f);
        fallVelocity = 0f;
    }

    private void CacheGeometry() {
        if (col == null) {
            col = GetComponent<Collider2D>();
        }
        geometryCached = true;
        if (col is BoxCollider2D box) {
            colOffset = box.offset;
            colHalfWidth = box.size.x * 0.5f;
            colHalfHeight = box.size.y * 0.5f;
        } else if (col is CapsuleCollider2D capsule) {
            colOffset = capsule.offset;
            colHalfWidth = capsule.size.x * 0.5f;
            colHalfHeight = capsule.size.y * 0.5f;
        } else if (col is CircleCollider2D circle) {
            colOffset = circle.offset;
            colHalfWidth = circle.radius;
            colHalfHeight = circle.radius;
        } else if (col != null) {
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Abs(scale.x) > 0.001f ? scale.x : 1f;
            float sy = Mathf.Abs(scale.y) > 0.001f ? scale.y : 1f;
            Bounds bounds = col.bounds;
            colOffset = new Vector2((bounds.center.x - transform.position.x) / sx, (bounds.center.y - transform.position.y) / sy);
            colHalfWidth = bounds.extents.x / Mathf.Abs(sx);
            colHalfHeight = bounds.extents.y / Mathf.Abs(sy);
        } else {
            colOffset = Vector2.zero;
            colHalfWidth = 0.3f;
            colHalfHeight = 0.5f;
        }
    }

    // an enemy that fell out of the level (a hole, a bad spawn) is removed quietly, so it stops
    // counting as alive: below the arena screen, or below a stage's pit line (the one Freny has)
    protected bool FellOutOfWorld() {
        float killY = ArenaBounds.Active ? ArenaBounds.Bottom - 3f
            : CampaignRun.Instance ? CampaignRun.Instance.PitY : float.NegativeInfinity;
        if (transform.position.y >= killY) {
            return false;
        }
        Despawn();
        return true;
    }

    // gone without a fight: no XP, coins or combo
    public void Despawn() {
        isDead = true;
        if (attackRoutine != null) {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        ReleaseSpawnerSlot();
        Destroy(gameObject);
    }

    // in the arena the screen is the world: nobody walks out of view (no-op in the campaign)
    private float nextWallCheck;

    protected void ClampToArena() {
        if (!ArenaBounds.Active) {
            return;
        }
        // shoved into a wall's tiles (by a knockback or another enemy): out to the nearest free spot
        if (col != null && Time.time >= nextWallCheck) {
            nextWallCheck = Time.time + 0.2f;
            WallRescue.Free(transform, col.bounds.center, col.bounds.size.y, GetComponent<Rigidbody2D>());
        }
        Vector3 pos = transform.position;
        float clamped = ArenaBounds.ClampX(pos.x, 0.5f);
        if (clamped != pos.x) {
            pos.x = clamped;
            transform.position = pos;
        }
    }

    // the patrol ends, kept inside the arena so an end beyond the screen edge doesn't keep the
    // enemy walking into the wall forever
    private void PatrolRange(out float min, out float max) {
        min = Mathf.Min(pointA.position.x, pointB.position.x);
        max = Mathf.Max(pointA.position.x, pointB.position.x);
        if (ArenaBounds.Active) {
            min = ArenaBounds.ClampX(min, 0.6f);
            max = ArenaBounds.ClampX(max, 0.6f);
        }
    }

    // sprites face left by default, so scale.x = -1 means looking right
    protected bool FacingRight => transform.localScale.x < 0f;

    // collider centre in world space, from the transform (the facing flip is in scale.x)
    private Vector2 ColliderCenter {
        get {
            if (!geometryCached) {
                CacheGeometry();
            }
            Vector3 scale = transform.lossyScale;
            return (Vector2)transform.position + new Vector2(colOffset.x * scale.x, colOffset.y * scale.y);
        }
    }
    public float HalfWidth => colHalfWidth * Mathf.Abs(transform.lossyScale.x);
    public float HalfHeight => colHalfHeight * Mathf.Abs(transform.lossyScale.y);
    public Vector3 Feet {
        get {
            Vector2 center = ColliderCenter;
            return new Vector3(center.x, center.y - HalfHeight, 0f);
        }
    }
    public Vector3 Center => ColliderCenter;
    // winding up or landing a hit right now (the autopilot steps out of the way)
    public bool IsAttacking => attackRoutine != null;

    public virtual void Movement() {
        if (isPlayerInRange) {
            AttackMode();
        } else {
            Patrol();
        }
    }

    private void Patrol() {
        if (pointA == null || pointB == null || Time.time < patrolPauseUntil) {
            return;
        }

        // only x matters: the patrol points sit at the spawner's height, not on the floor
        PatrolRange(out float min, out float max);
        float goalX = Mathf.Clamp(currentTarget.x, min, max);
        if (Mathf.Abs(transform.position.x - goalX) <= patrolPositionTolerance) {
            ChangeDirection(); // reached this end: pause, then head for the other one
            return;
        }

        float dir = goalX > transform.position.x ? 1f : -1f;
        transform.localScale = new Vector3(-dir, 1, 1);
        if (!GroundAhead(dir) || Blocked(dir)) {
            ChangeDirection(); // the platform ends here, or a wall or spikes
            return;
        }
        Vector2 goal = new Vector2(goalX, transform.position.y);
        transform.position = Vector2.MoveTowards(transform.position, goal, MoveSpeed * Time.deltaTime);
    }

    // a little faster or slower than its whole-number speed
    protected virtual float SpeedFactor => 1f;
    protected float MoveSpeed => speed * SpeedFactor * Tuning.EnemySpeed.Value * (ArenaBounds.Active ? ArenaDifficulty.SpeedFactor(Small) * ArenaDirector.SpeedRamp * (1f + BloodSpeed * BloodMoon) : 1f);

    // ---- the Lua de Sangue (ArenaDayNight.Blood): faster, +1 heart a hit, heals, red eyes ----
    public static float BloodMoon => ArenaBounds.Active ? ArenaDayNight.Blood : 0f;
    public static int BloodDamage => BloodMoon > 0.5f ? 1 : 0;
    private const float BloodSpeed = 0.25f;
    private const float BloodHealEvery = 3f;   // +1 life this often, up to its full life (not bosses)
    private static readonly Color BloodRed = new Color(1f, 0.12f, 0.1f, 0.85f);
    private static Sprite eyeDot;
    private SpriteRenderer bloodEye;
    private int fullHealth;
    private float nextBloodHeal;
    private float nextTrail;
    private Vector3 lastTrailAt;

    protected virtual bool HealsInBloodMoon => true;

    // in LateUpdate: the Bat and the Sorcerer have their own Update
    protected virtual void LateUpdate() {
        UpdateBloodMoon();
    }

    private void UpdateBloodMoon() {
        float blood = BloodMoon;
        if (blood <= 0.5f || isDead || sprite == null) {
            if (bloodEye != null) {
                bloodEye.enabled = false;
            }
            return;
        }
        if (HealsInBloodMoon && Time.time >= nextBloodHeal) {
            nextBloodHeal = Time.time + BloodHealEvery;
            if (Health < fullHealth) {
                Health++;
                DamageNumbers.Show(Center + Vector3.up * (HalfHeight + 0.2f), "+1", DamageNumbers.Healed);
            }
        }
        if (bloodEye == null) {
            GameObject eye = new GameObject("Blood Eye");
            eye.transform.SetParent(transform, false);
            bloodEye = eye.AddComponent<SpriteRenderer>();
            bloodEye.sprite = EyeDot();
            bloodEye.color = BloodRed;
            bloodEye.sortingLayerID = sprite.sortingLayerID;
            bloodEye.sortingOrder = sprite.sortingOrder + 1;
            if (ItemPickup.UnlitMaterial != null) {
                bloodEye.sharedMaterial = ItemPickup.UnlitMaterial;
            }
        }
        bloodEye.enabled = sprite.enabled;
        Bounds b = sprite.bounds;
        // the eyes: high on the face, a little forward
        Vector3 at = b.center + new Vector3((FacingRight ? 1f : -1f) * b.extents.x * 0.35f, b.extents.y * 0.45f, 0f);
        bloodEye.transform.position = at;
        bloodEye.transform.localScale = Vector3.one * Mathf.Clamp(b.extents.y * 0.35f, 0.12f, 0.5f);
        bloodEye.color = new Color(BloodRed.r, BloodRed.g, BloodRed.b, BloodRed.a * (0.75f + 0.25f * Mathf.Sin(Time.time * 8f)));
        // a faint red trail behind it as it moves
        if (Time.time >= nextTrail && (at - lastTrailAt).sqrMagnitude > 0.01f) {
            nextTrail = Time.time + 0.05f;
            Fx.Burst(at, new Color(1f, 0.15f, 0.1f, 0.5f), 1, 0.2f, 0.07f, 0.45f);
        }
        lastTrailAt = at;
    }

    // a soft round dot (1 unit wide at scale 1)
    private static Sprite EyeDot() {
        if (eyeDot == null) {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1.2f - d * 1.2f)));
                }
            }
            texture.Apply();
            eyeDot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return eyeDot;
    }

    public void ChangeDirection() {
        if (pointA == null || pointB == null) {
            return;
        }
        currentTarget = currentTarget.x == pointB.position.x ? pointA.position : pointB.position;
        patrolPauseUntil = Time.time + patrolPause;
    }

    private void AttackMode() {
        // mid-swing: hold position and facing until the hit resolves
        if (attackRoutine != null) {
            return;
        }
        if (Time.time < restUntil) {
            FaceTarget(); // catching its breath after a swing or a landing
            return;
        }
        if (Time.time < backOffUntil) {
            BackOff();
            return;
        }

        FaceTarget();

        if (InAttackReach(LungeDistance)) {
            // close enough: wind up, then hit; the cooldown starts after the hit, so the rhythm is steady
            if (Time.time >= nextAttackTime && CanStartAttack) {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
            return;
        }

        // inside the radius but too far to attack: walk towards the target. Walkers follow it to
        // other platforms; the others never leave their patrol area
        float targetX = ArenaBounds.ClampX(Target.position.x, 0.5f);
        if (pointA && pointB && !Climbs) {
            PatrolRange(out float min, out float max);
            targetX = Mathf.Clamp(targetX, min, max);
        }
        targetX = ChaseX(targetX);
        float step = targetX - transform.position.x;
        float dir = step >= 0f ? 1f : -1f;
        float herFeet = rival != null ? rival.Feet.y : playerCol ? playerCol.bounds.min.y : player_t.position.y - 1f;
        float myFeet = Feet.y;
        bool canJump = Climbs && Time.time >= nextJumpTime;
        if (canJump && herFeet > myFeet + 0.6f && TryJumpUp(dir)) {
            return; // he is on a platform above
        }
        if (Climbs && herFeet < myFeet - 0.6f) {
            // he is on a floor below: head for an edge it can walk off, not for the spot over his head
            if (PlanDrop(Target.position.x, herFeet, out float dropX)) {
                targetX = ArenaBounds.ClampX(dropX, 0.5f);
                step = targetX - transform.position.x;
                dir = step >= 0f ? 1f : -1f;
            }
        } else {
            dropDir = 0f;
            nextDropPlan = 0f;
        }
        if (Mathf.Abs(step) < 0.01f) {
            return;
        }
        if (Blocked(dir)) {
            if (canJump && WallAhead(dir)) {
                TryJumpUp(dir); // over the wall if it is low enough
            }
            return; // never into a wall or onto spikes
        }
        if (!GroundAhead(dir)) {
            if (canJump && herFeet < myFeet - 0.6f && FloorBelowEdge(Feet.x + dir * (HalfWidth + 0.15f), dir, out _)) {
                StartAirborne(new Vector2(dir * MoveSpeed, 0f), false); // he is below: step off the edge
            } else if (canJump) {
                TryJumpAcross(dir); // a gap at this height
            }
            return; // otherwise hold the edge and keep facing him
        }
        // it faces the way it walks: heading for an edge to drop from, or clamped back into its
        // patrol range, it walked backwards facing Freny ("moonwalk", José 2026-10-29)
        transform.localScale = new Vector3(-dir, 1, 1);
        Vector2 target = new Vector2(targetX, transform.position.y);
        transform.position = Vector2.MoveTowards(transform.position, target, MoveSpeed * Time.deltaTime);
    }

    // true when the colliders are within attackDistance of touching and roughly at the same height.
    // Falls back to centre distance when a collider is missing (e.g. the player died and lost its collider).
    protected bool InAttackReach(float extra = 0f) {
        if (col == null || (rival == null && playerCol == null)) {
            return distance <= attackDistance + extra + 1f;
        }

        Bounds mine = col.bounds;
        Bounds theirs = rival != null ? rival.BodyBounds : playerCol.bounds;
        float gapX = Mathf.Max(0f, Mathf.Max(theirs.min.x - mine.max.x, mine.min.x - theirs.max.x));
        bool sameHeight = theirs.max.y > mine.min.y && theirs.min.y < mine.max.y;
        return sameHeight && gapX <= attackDistance + extra;
    }

    // where it walks to while chasing (the Ciclope leans towards the middle of the arena)
    protected virtual float ChaseX(float targetX) => targetX;

    // false while it must not start a punch (the Ciclope resting after a blow)
    protected virtual bool CanStartAttack => true;

    // the horizontal gap between its collider and its target's (0 when they overlap)
    protected float TargetGap() {
        if (col == null || (rival == null && playerCol == null)) {
            return Mathf.Max(0f, distance - 1f);
        }
        Bounds mine = col.bounds;
        Bounds theirs = rival != null ? rival.BodyBounds : playerCol.bounds;
        return Mathf.Max(0f, Mathf.Max(theirs.min.x - mine.max.x, mine.min.x - theirs.max.x));
    }

    // a dash forward at the end of the wind-up (the red Golem): it starts its punch from this much
    // farther away and closes the gap as it swings
    protected virtual float LungeDistance => 0f;
    private const float LungePart = 0.35f;   // the last part of the wind-up is the dash

    private IEnumerator Lunge(float seconds) {
        float dir = FacingRight ? 1f : -1f;
        float speed = LungeDistance / Mathf.Max(0.05f, seconds);
        float end = Time.time + seconds;
        Fx.Dust(Feet, 6);
        while (Time.time < end && !isDead) {
            // stops at Freny (in reach), at a wall, spikes or the platform's edge
            float gap = TargetGap() - attackDistance * 0.5f;
            if (gap <= 0f || Blocked(dir) || !GroundAhead(dir)) {
                yield break;
            }
            // never past Freny, even on a long frame (it went through him at 15 fps)
            transform.position += Vector3.right * (dir * Mathf.Min(speed * Time.deltaTime, gap));
            yield return null;
        }
    }

    private IEnumerator AttackRoutine() {
        if (hasAttackTrigger) {
            anim.SetTrigger("Attack");
        }
        PlaySound("windup");
        float windup = attackWindup * Tuning.EnemyWindup.Value;
        if (LungeDistance > 0f) {
            // charge the punch, then dash in with it
            yield return new WaitForSeconds(windup * (1f - LungePart));
            yield return Lunge(windup * LungePart);
        } else {
            yield return new WaitForSeconds(windup);
        }
        if (!isDead) {
            PlaySound("attack");
        }

        // the target can still step or dash out of the swing
        if (!isDead) {
            Enemy foe = rival;
            if (foe != null) {
                if (!foe.isDead && InAttackReach()) {
                    foe.HitBy(this, HitDamage);
                }
            } else if (player != null && player.Health > 0 && InAttackReach()) {
                HitPlayer();
            }
            HitBystanders(foe);
        }
        nextAttackTime = Time.time + attackCooldown * Tuning.EnemyCooldown.Value * DifficultyCooldown;
        // not frantic: rest a moment, then step back before closing in again
        restUntil = Time.time + restAfterAttack * DifficultyCooldown;
        backOffUntil = restUntil + Random.Range(backOffTime.x, backOffTime.y) * DifficultyCooldown;
        attackRoutine = null;
    }

    // walks away from Freny for a moment (stops at the platform's edge)
    private void BackOff() {
        float dir = Target.position.x > transform.position.x ? -1f : 1f;
        transform.localScale = new Vector3(-dir, 1, 1);
        if (!GroundAhead(dir) || Blocked(dir)) {
            return;
        }
        Vector2 target = (Vector2)transform.position + new Vector2(dir, 0f);
        transform.position = Vector2.MoveTowards(transform.position, target, MoveSpeed * 0.7f * Time.deltaTime);
    }

    // ---- jumping and dropping between platforms --------------------------------------------------

    // ground enemies that chase from platform to platform (the Sorcerer stands, the Bat flies)
    protected virtual bool Climbs => WalksOnGround && !(this is Sorcerer);

    // a platform above, ahead of it (or right over it): jump onto it
    private bool TryJumpUp(float dir) {
        Vector3 feet = Feet;
        float[] offsets = { 1.2f, 2f, 0.6f, 2.8f };
        for (int side = 0; side < 2; side++) {
            float d = side == 0 ? dir : -dir;
            foreach (float off in offsets) {
                float x = feet.x + d * off;
                if (ArenaBounds.Active && ArenaBounds.ClampX(x, 0.6f) != x) {
                    continue;
                }
                Vector2 from = new Vector2(x, feet.y + maxJumpHeight + 0.2f);
                RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, maxJumpHeight - 0.3f, GroundMask);
                if (hit.collider == null || hit.point.y < feet.y + 0.6f) {
                    continue;
                }
                // room to stand on it
                if (Physics2D.Raycast(hit.point + Vector2.up * 0.05f, Vector2.up, HalfHeight * 2f + 0.1f, GroundMask).collider != null) {
                    continue;
                }
                JumpTo(new Vector2(x, hit.point.y), hit.point.y - feet.y + 0.5f);
                return true;
            }
        }
        return false;
    }

    // a gap at the same height: hop to the floor on the other side
    private bool TryJumpAcross(float dir) {
        Vector3 feet = Feet;
        for (float off = 1.4f; off <= 3.2f; off += 0.6f) {
            float x = feet.x + dir * off;
            if (ArenaBounds.Active && ArenaBounds.ClampX(x, 0.6f) != x) {
                return false;
            }
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, feet.y + 1f), Vector2.down, 2f, GroundMask);
            if (hit.collider != null && Mathf.Abs(hit.point.y - feet.y) < 1f) {
                JumpTo(new Vector2(x + dir * 0.3f, hit.point.y), Mathf.Max(0.8f, hit.point.y - feet.y + 0.6f));
                return true;
            }
        }
        return false;
    }

    // He is on a floor below. Walking towards his x only gets it off its platform when he is beyond
    // the edge: with him right underneath it just followed him along the top (the Licker on Hard).
    // Enemies never drop through a platform the way Freny does with S+Space, so they go round: this
    // looks for the edges of its own platform it can walk off onto a floor (not a pit or spikes) and
    // picks the side with the shortest way to him. Returns the x to walk to, a little past that edge.
    private bool PlanDrop(float herX, float herFeet, out float goalX) {
        if (Time.time >= nextDropPlan) {
            nextDropPlan = Time.time + 0.4f;
            float feetX = Feet.x;
            float bestDir = 0f, bestEdge = 0f, bestCost = float.PositiveInfinity;
            float keepEdge = 0f, keepCost = float.PositiveInfinity;
            for (int side = 0; side < 2; side++) {
                float d = side == 0 ? -1f : 1f;
                if (!FindDropEdge(d, out float edgeX, out Vector2 landing)) {
                    continue;
                }
                float cost = Mathf.Abs(edgeX - feetX) + Mathf.Abs(landing.x - herX) + Mathf.Abs(landing.y - herFeet);
                if (d == dropDir) {
                    keepCost = cost;
                    keepEdge = edgeX;
                }
                if (cost < bestCost) {
                    bestCost = cost;
                    bestDir = d;
                    bestEdge = edgeX;
                }
            }
            // keeps the side it chose unless the other is clearly shorter, so Freny pacing underneath
            // doesn't turn it back and forth
            if (keepCost <= bestCost + 3f) {
                bestDir = dropDir;
                bestEdge = keepEdge;
            }
            dropDir = bestDir;
            dropEdgeX = bestEdge;
        }
        goalX = dropEdgeX + dropDir * 1f;
        return dropDir != 0f;
    }

    // walks the platform in direction `d` (as GroundAhead sees it) up to the first spot with no floor
    // ahead; a wall, spikes or the arena's side first means there is no way down on that side
    private bool FindDropEdge(float d, out float edgeX, out Vector2 landing) {
        edgeX = 0f;
        landing = Vector2.zero;
        if (col == null) {
            return false;
        }
        Vector3 feet = Feet;
        float ahead = HalfWidth + 0.15f;
        float wall = DropScan + ahead;
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(GroundMask);
        int count = Physics2D.Raycast(new Vector2(feet.x, feet.y + StepUp + 0.05f), new Vector2(d, 0f), filter, groundHits, wall);
        for (int i = 0; i < count; i++) {
            if (!groundHits[i].collider.usedByEffector) {
                wall = Mathf.Min(wall, groundHits[i].distance);
            }
        }
        for (float off = 0f; off <= DropScan && off + ahead < wall; off += 0.25f) {
            float x = feet.x + d * off;
            if (ArenaBounds.Active && !ArenaBounds.Inside(x, 0.5f)) {
                return false;
            }
            float probeX = x + d * ahead;
            if (Hazard.At(new Vector2(probeX, feet.y + 0.1f))) {
                return false;
            }
            if (Physics2D.Raycast(new Vector2(probeX, feet.y + 0.3f), Vector2.down, 1.3f, GroundMask).collider != null) {
                continue;
            }
            edgeX = x;
            return FloorBelowEdge(probeX, d, out landing);
        }
        return false;
    }

    // the floor it lands on after walking off the edge at `edgeX` (it drifts on a little while falling):
    // none when it is a pit, spikes, or below the level
    private bool FloorBelowEdge(float edgeX, float d, out Vector2 landing) {
        landing = Vector2.zero;
        Vector3 feet = Feet;
        float x = edgeX + d * (HalfWidth + 0.4f);
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, feet.y - 0.1f), Vector2.down, 30f, GroundMask);
        if (hit.collider == null || hit.point.y > feet.y - 0.6f) {
            return false;
        }
        float killY = ArenaBounds.Active ? ArenaBounds.Bottom - 3f
            : CampaignRun.Instance ? CampaignRun.Instance.PitY : float.NegativeInfinity;
        if (hit.point.y < killY || Hazard.At(hit.point + Vector2.up * 0.1f)) {
            return false;
        }
        landing = hit.point;
        return true;
    }

    // a ballistic hop whose apex is `apex` above the feet, landing at `landing`
    private void JumpTo(Vector2 landing, float apex) {
        Vector3 feet = Feet;
        float up = Mathf.Sqrt(2f * FallGravity * apex);
        float down = Mathf.Max(0.05f, apex - (landing.y - feet.y));
        float time = up / FallGravity + Mathf.Sqrt(2f * down / FallGravity);
        float vx = (landing.x - feet.x) / time;
        transform.localScale = new Vector3(vx > 0f ? -1 : 1, 1, 1);
        StartAirborne(new Vector2(vx, up), true);
        Fx.Dust(feet, 3);
    }

    private void StartAirborne(Vector2 velocity, bool jump) {
        airborne = true;
        rising = jump;
        fellSinceTakeoff = false;
        airborneSince = Time.time;
        airVelocity = velocity;
        fallVelocity = 0f;
    }

    // Update calls this instead of Movement while it jumps or drops
    private void MoveInAir() {
        float dt = Time.deltaTime;
        if (rising) {
            if (airVelocity.x != 0f && WallAhead(Mathf.Sign(airVelocity.x))) {
                airVelocity.x = 0f;
            }
            transform.position += new Vector3(airVelocity.x, airVelocity.y, 0f) * dt;
            airVelocity.y -= FallGravity * dt;
            if (airVelocity.y <= 0f) {
                rising = false;
                fellSinceTakeoff = true;
            }
            return;
        }
        // falling (or walking off the edge): drift sideways, StickToGround is the gravity
        if (airVelocity.x != 0f && WallAhead(Mathf.Sign(airVelocity.x))) {
            airVelocity.x = 0f; // slide down the wall instead of into it
        }
        transform.position += new Vector3(airVelocity.x, 0f, 0f) * dt;
        StickToGround();
        if (fallVelocity > 0f) {
            fellSinceTakeoff = true;
        } else if (fellSinceTakeoff || Time.time - airborneSince > 1.5f) {
            Land();
        }
    }

    private void Land() {
        airborne = false;
        airVelocity = Vector2.zero;
        restUntil = Mathf.Max(restUntil, Time.time + landPause);
        nextJumpTime = Time.time + jumpCooldown;
        Fx.Dust(Feet, 3);
        PlaySound("step");
        // a new platform: patrol around here from now on
        if (ownPatrolA == null) {
            ownPatrolA = new GameObject(name + " PatrolA").transform;
            ownPatrolB = new GameObject(name + " PatrolB").transform;
        }
        Vector3 feet = Feet;
        ownPatrolA.position = new Vector3(feet.x - 3f, feet.y, 0f);
        ownPatrolB.position = new Vector3(feet.x + 3f, feet.y, 0f);
        pointA = ownPatrolA;
        pointB = ownPatrolB;
        currentTarget = (Random.value < 0.5f ? ownPatrolA : ownPatrolB).position;
    }

    private void OnDestroy() {
        if (ownPatrolA) {
            Destroy(ownPatrolA.gameObject);
        }
        if (ownPatrolB) {
            Destroy(ownPatrolB.gameObject);
        }
    }

    // hearts one hit takes (the Golem hits for 2)
    protected virtual int HitDamage => 1;

    // damages Freny and shoves him away from the enemy: a little for most, hard for heavy hitters
    // (those knock him down for a moment)
    // how much of the knockback goes upwards (the Ciclope throws Freny high as well as far)
    protected virtual float KnockbackLift => 0.5f;

    protected void HitPlayer() {
        if (!player.TryDamage(HitDamage + BloodDamage)) {
            return;
        }
        float dir = Mathf.Sign(player_t.position.x - transform.position.x);
        if (dir == 0f) {
            dir = FacingRight ? 1f : -1f;
        }
        if (knockback > LightKnockback) {
            player.Knockback(new Vector2(dir * knockback, knockback * KnockbackLift));
        } else {
            player.Knockback(new Vector2(dir * LightKnockback, LightKnockback * 0.4f), false);
        }
    }

    private void FaceTarget() {
        transform.localScale = new Vector3(Target.position.x > transform.position.x ? -1 : 1, 1, 1);
    }

    // ==== fighting other enemies ===============================================================

    // whom it goes after: the enemy it is fighting, otherwise Freny
    protected Transform Target => rival != null ? rival.transform : player_t;
    // the enemy it is fighting right now (null: none, it is after Freny)
    protected Enemy Rival => rival;
    // the collider's box in world space, from the transform (see Feet)
    public Bounds BodyBounds => new Bounds(Center, new Vector3(HalfWidth * 2f, HalfHeight * 2f, 1f));

    // the fight ends when the rival dies, gets too far or nobody has hit anybody for a while
    protected void UpdateRival() {
        if (rival == null) {
            return;
        }
        if (rival.isDead || Time.time >= rivalUntil || Vector2.Distance(Center, rival.Center) > provokedRadius) {
            rival = null;
            isPlayerInRange = false;
            ResetAttackMode();
        }
    }

    // hit by another enemy, by accident or in a fight: it loses the hearts and turns on whoever hit it
    public void HitBy(Enemy attacker, int amount) {
        if (isDead || attacker == this) {
            return;
        }
        if (attacker != null && !attacker.isDead) {
            if (rival != attacker) {
                restUntil = backOffUntil = 0f; // no breather: it goes straight for the one that hit it
            }
            rival = attacker;
            rivalUntil = Time.time + RivalMemory;
            isPlayerInRange = true;
            SetInCombat(true);
        }
        Health -= Mathf.Max(1, amount);
        TakeHit(false);
        if (Health < 1) {
            Die(false);
        }
    }

    // a swing hits every other enemy standing in front of it, not only the one it aimed at
    private void HitBystanders(Enemy aimedAt) {
        if (col == null) {
            return;
        }
        float dir = FacingRight ? 1f : -1f;
        Vector2 center = Center;
        float reachX = center.x + dir * (HalfWidth + attackDistance + 0.2f);
        Bounds swing = new Bounds();
        swing.SetMinMax(new Vector3(Mathf.Min(center.x, reachX), center.y - HalfHeight, -1f),
            new Vector3(Mathf.Max(center.x, reachX), center.y + HalfHeight, 1f));
        bystanders.Clear();
        bystanders.AddRange(Active);
        foreach (Enemy other in bystanders) {
            if (other == null || other == this || other == aimedAt || other.isDead) {
                continue;
            }
            if (swing.Intersects(other.BodyBounds)) {
                other.HitBy(this, HitDamage);
            }
        }
        bystanders.Clear();
    }

    private void ResetAttackMode() {
        isHit = false;
        SetInCombat(false);
        // go back to patrolling towards the nearest end
        if (pointA && pointB) {
            currentTarget = Mathf.Abs(transform.position.x - pointA.position.x) < Mathf.Abs(transform.position.x - pointB.position.x)
                ? pointA.position : pointB.position;
        }
    }

    // ==== taking hits ==========================================================================

    // any enemy killed (Freny's Rage meter counts them)
    public static event System.Action<Enemy> Killed;
    // any enemy that died, whoever killed it (the arena holds a dead sorcerer's place for a while)
    public static event System.Action<Enemy> Died;

    // when it was last hit (FrameAnimator shows the hit frames for a moment after)
    public float LastHitTime { get; private set; } = -10f;
    // the small bar over its head (EnemyHealthBar); a boss draws its own
    public virtual bool ShowsHealthBar => true;

    // how much of a hit from Freny at that point gets through: 1 for most, less on the Escudeiro's
    // shield and more on its back. shot: a bullet or shotgun pellet rather than the sword
    protected virtual float HitScale(Vector3 from, bool shot) {
        return 1f;
    }

    // what was special about the last hit from Freny, for the Conquistas: BackHit (round the
    // Escudeiro's shield) or EyeHit (the Ciclope's eye); null for any other hit and for blasts
    public const string BackHit = "back";
    public const string EyeHit = "eye";
    public string LastHitMark { get; protected set; }

    // hits scaled below a point add up here until they make one
    private float wear;
    // where the current hit from Freny landed, when the weapon knows (the Ciclope's eye)
    protected Vector3? HitPoint { get; private set; }

    // a sword hit or a shot that knows where it landed
    public void Damage(int amount, float push, Vector3 at) {
        HitPoint = at;
        Damage(amount, push);
        HitPoint = null;
    }

    public void ShotDamage(float push, Vector3 at) {
        HitPoint = at;
        ShotDamage(push);
        HitPoint = null;
    }

    private void ScaledHit(int amount, bool shot) {
        LastHitMark = null;
        float scale = player_t != null ? HitScale(player != null ? player.Feet : player_t.position, shot) : 1f;
        wear += Mathf.Max(1, amount) * scale;
        int whole = Mathf.FloorToInt(wear + 0.001f);
        if (whole < 1) {
            TakeHit(true);   // it flinches, but holds
            return;
        }
        wear = Mathf.Max(0f, wear - whole);
        TakeDamage(whole);
    }

    // a blast catches it whole: no shield in the way
    public void BlastDamage(int amount) {
        if (isDead) {
            return;
        }
        LastHitMark = null;
        TakeDamage(Mathf.Max(1, amount));
    }

    // a harder hit from Freny (the sword during Rage, the combo)
    public void Damage(int amount) {
        if (isDead) {
            return;
        }
        ScaledHit(amount, false);
    }

    // a bullet or pellet from Freny
    public void ShotDamage(float push) {
        if (isDead) {
            return;
        }
        pushScale = push;
        ScaledHit(1, true);
        pushScale = 1f;
    }

    // a sword hit: push scales how far it is shoved back (the combo's third hit pushes harder)
    public void Damage(int amount, float push) {
        pushScale = push;
        Damage(amount);
        pushScale = 1f;
    }

    // one point of damage from Freny's sword, a bullet or a grenade
    public virtual void Damage() {
        if (isDead) {
            return;
        }
        ScaledHit(1, false);
    }

    private void TakeDamage(int amount) {
        if (isDead) {
            return;
        }
        rival = null; // Freny hit it: he is the one it goes after now
        Health -= amount;
        DamageNumbers.Show(Center + Vector3.up * (HalfHeight + 0.2f), "-" + amount, DamageNumbers.Hit);
        TakeHit(true);
        if (Health < 1) {
            Die(true);
        } else {
            PushedBack();
        }
    }

    // a hit from Freny shoves it a little away from him (walkers only, and never through a wall,
    // into spikes or off a ledge); it doesn't walk while the push lasts
    private void PushedBack() {
        if (!WalksOnGround || player_t == null || airborne) {
            return;
        }
        float dir = Mathf.Sign(transform.position.x - player_t.position.x);
        if (dir == 0f) {
            dir = FacingRight ? -1f : 1f;
        }
        pushVelocity = dir * HitPush * pushScale;
    }

    protected bool BeingPushed => pushVelocity != 0f;

    // moves it along the push; true while it is still being pushed (Update skips Movement)
    protected bool UpdatePush() {
        if (pushVelocity == 0f) {
            return false;
        }
        float dir = Mathf.Sign(pushVelocity);
        if (Blocked(dir) || !GroundAhead(dir)) {
            pushVelocity = 0f;
            return false;
        }
        transform.position += Vector3.right * (pushVelocity * Time.deltaTime);
        pushVelocity = Mathf.MoveTowards(pushVelocity, 0f, PushDrag * Time.deltaTime);
        return true;
    }

    // flinch, fight back, and bleed a few particles
    protected void TakeHit(bool byPlayer) {
        LastHitTime = Time.time;
        if (byPlayer) {
            isHit = true;
        }
        if (hasHitTrigger) {
            anim.SetTrigger("Hit");
        }
        SetInCombat(true);
        Fx.Burst(Center, DeathColor, 6, 2.5f, 0.08f, 0.4f);
        if (Health > 0) {
            PlaySound("hurt"); // the last hit plays the death instead
        }
    }

    // the sprite disappears in a burst of particles instead of playing the "pieces" animation,
    // and the body stops colliding and moving at once (the pieces used to keep walking).
    // Killed by another enemy, it still drops its coins but gives Freny no XP, score or Rage
    protected void Die(bool byPlayer) {
        isDead = true;
        Died?.Invoke(this);
        LeaveChase();
        if (attackRoutine != null) {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        Fx.Burst(Center, DeathColor, 28, 3.5f, 0.12f, 0.8f);
        Disintegrate();
        Fx.Dust(Feet, 6);
        PlaySound("death");
        PlaySound("scream"); // bosses have a second layer
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>()) {
            renderer.enabled = false;
        }
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) {
            c.enabled = false;
        }
        if (rb) {
            rb.simulated = false;
        }

        if (byPlayer) {
            Killed?.Invoke(this);
            FrenyProgress.AddXp(Mathf.Max(scoreToAdd, 10));
            if (UIManager.Instance) {
                UIManager.Instance.HandleComboManager(this);
            }
        }
        ReleaseSpawnerSlot();
        DropCoin();
        if (ArenaBounds.Active) {
            ItemPickup.RollEnemyDrop(Center); // now and then a potion or ammo too; in the stages only coins
        }
        Destroy(gameObject, 1f);
    }

    // a big body crumbles all over, not just in a puff at its middle (José, 2026-10-30: the
    // Ciclope just vanished): specks from all over its sprite, drifting up and fading
    private void Disintegrate() {
        if (sprite == null) {
            return;
        }
        Bounds b = sprite.bounds;
        int specks = Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.y * 30f), 0, 260);
        if (specks < 20) {
            return;   // small ones: the puff is enough
        }
        for (int i = 0; i < specks; i++) {
            Vector3 at = new Vector3(Random.Range(b.min.x, b.max.x) * 0.8f + b.center.x * 0.2f, Random.Range(b.min.y, b.max.y), 0f);
            Color c = Color.Lerp(DeathColor, Color.white, Random.Range(0f, 0.35f));
            Fx.Rise(at, c, Random.Range(0.08f, 0.16f), Random.Range(0.8f, 1.6f));
        }
    }

    private void DropCoin() {
        if (coins <= 0 || coinPrefab == null) {
            return;
        }
        GameObject dropped = Instantiate(coinPrefab, transform.position, Quaternion.identity);
        Coin coin = dropped.GetComponent<Coin>();
        if (coin) {
            coin.coins = coins;
            coin.Toss(new Vector2(Random.Range(-0.6f, 0.6f), 3.5f)); // a small hop, then falls to the floor (bats die mid-air)
        }
    }

    // the spawner that made this enemy may spawn another one
    private void ReleaseSpawnerSlot() {
        if (string.IsNullOrEmpty(spawnerUuid)) {
            return;
        }
        foreach (GameObject spawnerObj in GameObject.FindGameObjectsWithTag("Spawner")) {
            EnemySpawner spawner = spawnerObj.GetComponent<EnemySpawner>();
            if (spawner != null && spawner.uuid == spawnerUuid) {
                spawner.countEnemies--;
            }
        }
    }

    private static bool HasParameter(Animator animator, string paramName) {
        foreach (AnimatorControllerParameter param in animator.parameters) {
            if (param.name == paramName) {
                return true;
            }
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.collider.gameObject.layer) & ignoreLayer) != 0)
        {
            Physics2D.IgnoreCollision(collision.collider, GetComponent<Collider2D>());
        }

    }
}
