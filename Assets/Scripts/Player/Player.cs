using UnityEngine;

// Freny. Reads one PlayerInputFrame per frame (keyboard or arena autopilot, picked by FrenyInput)
// and turns it into movement, jumps and attacks. Nothing here knows who is driving.
//
//   Update       what he wants to do this frame: ground check, stun, move, sprint, jump, weapons
//   FixedUpdate  the physics: horizontal speed, fast fall, knockback slide
//
// Animation goes through PlayerAnimation; the Melee Attack clip switches the sword hitbox on and off.
public class Player : MonoBehaviour, IDamageable {

    // ---- scene references ---------------------------------------------------------------------
    public GameObject revolverBulletPrefab;
    public Transform playerRevolverShootPosition;
    public Transform feetPos;          // ground check circle (read by the autopilot too)
    public LayerMask whatIsGround;
    public GameObject gameOver;

    // ---- tuning -------------------------------------------------------------------------------
    // Movement: the same in every scene (arena and campaign), so these are not saved in the scenes.
    // Change them here; each scene used to keep its own copy, and the stages felt different.
    private float _speed = 3.0f;
    private float jumpHeight = 2.6f;      // units; arena platforms are 2 apart
    private float gravityScale = 3f;      // heavier than Unity's default: a snappy jump
    private float fallMultiplier = 1.6f;  // extra gravity on the way down (2 fell so fast the camera lost him)
    private float maxFallSpeed = 12f;     // units/s: about the jump's take-off speed, so the camera keeps up
    private float checkRadius = 0.4f;     // ground check circle at feetPos
    private float coyoteTime = 0.1f;      // may still jump this long after walking off an edge
    private float jumpBuffer = 0.12f;     // a jump pressed this long before landing still happens

    // fall damage: a drop longer than this (from the highest point of the fall) costs a heart,
    // and one more for every FallDamageStep beyond it
    public const float SafeFallHeight = 7f;
    private const float FallDamageStep = 5f;
    private const int FallDamageMax = 3;

    // sprint (Shift): a stamina bar that drains while sprinting and refills from wherever it
    // stopped, so letting go at half keeps that half
    private float runMultiplier = 1.6f;
    private float runDuration = 1.5f;     // a full bar lasts this long...
    private float runCooldown = 2.5f;     // ...and refills from empty in this long
    private const float RunMinStamina = 0.2f; // below this a new sprint doesn't start (no stutter)

    [Header("Combat")]
    public int MaxHealth = 4;
    [SerializeField] private float attackCooldown = 0.5f;  // the sword; the guns have their own
    // the sword is a 3-hit combo: hits 1 and 2 are quick, the third pushes harder and has the long
    // recovery. A press later than ComboWindow after the swing was ready starts over at hit 1.
    private const float ComboGapFactor = 0.5f;      // cooldown after hits 1 and 2 (x attackCooldown)
    private const float FinisherRecovery = 1.3f;    // cooldown after hit 3 (x attackCooldown)
    private const float ComboWindow = 0.4f;
    [SerializeField] private float pistolCooldown = 0.2f;  // between revolver shots
    [SerializeField] private float shotgunCooldown = 0.6f;
    // blinking after a hit: nothing can hurt him meanwhile, so enemies around him can't chain their hits
    private const float InvulnerableTime = 1.2f;
    [SerializeField] private float stunTime = 0.3f;        // crouched and helpless after a knockback
    private const float ShoveTime = 0.2f;                   // no control while a light hit shoves him back
    [SerializeField] private float knockbackDrag = 14f;    // how fast the shove dies out (units/s²)
    [SerializeField] private int shellsPerRun = 6;         // shotgun shells at the start of a run; chests add more
    [SerializeField] private int revolverCapacity = 4;     // shots before the revolver has to be reloaded
    [SerializeField] private float revolverReloadTime = 1.6f;
    [SerializeField] private int revolverReserveStart = 8;  // spare bullets at the start of a run: the gun is no longer infinite
    [SerializeField] private int revolverReserveMax = 16;
    [SerializeField] private int shellsMax = 30;

    [Header("Grenade (E)")]
    [SerializeField] private int grenadesFromUpgrade = 2;  // the shop's grenade upgrade starts each run with these
    [SerializeField] private int grenadesMax = 6;
    [SerializeField] private float grenadeAngle = 55f;     // degrees above the horizontal
    [SerializeField] private float grenadeMinSpeed = 5f;
    [SerializeField] private float grenadeMaxSpeed = 13f;
    public const float GrenadeGravity = 1.5f;

    // ---- state --------------------------------------------------------------------------------
    public int coins;
    public int Health { get; set; }
    public int ShotgunShells { get; private set; }
    public int RevolverBullets { get; private set; }
    public int RevolverCapacity => revolverCapacity;
    public int RevolverReserve { get; private set; }       // spare bullets; reloading takes them from here
    public int Grenades { get; private set; }
    public bool ShotgunOwned { get; private set; }          // bought in the shop, or found in a chest this run
    public bool ShotgunEquipped { get; private set; } = true; // 1 = pistol, 2 = shotgun
    public bool IsRunning { get; private set; }
    public bool ChargingGrenade { get; private set; }
    public float GrenadeCharge { get; private set; }

    private bool isGrounded;
    private bool wasGrounded;
    private bool landed;                // standing still on the ground since the last fall ended
    private const float LandedSpeed = 1.5f; // falling slower than this on the ground = he has landed
    private bool isJumping;
    private bool stunned;
    private bool isDead;
    private int airJumpsLeft;          // double jump upgrade
    private float lastGroundedTime = -10f;
    private float fallPeakY;            // the highest point since he last stood on the ground
    private float jumpPressedTime = -10f;
    private float jumpStartTime = -10f;
    private float attackReadyAt;
    private float swingCooldown = 0.5f;   // the cooldown the last swing started (the HUD's sword meter)
    private float gunReadyAt;
    private float meleePressedTime = -10f;  // a swing pressed while knocked down happens when he gets up
    private float rangedPressedTime = -10f;
    private const float AttackBuffer = 0.3f;
    private float dropThroughUntil;         // Down + Jump on a pass-through platform
    private Collider2D dropThroughPlatform;
    private float reloadReadyAt = -1f;  // -1: not reloading
    private float invulnerableUntil;
    private float greatSwordGraceUntil;
    private float rageUntil;

    // drops and buffs (see GiveGreatSword, GiveArmor, OnEnemyKilled)
    public const int ArmorMax = 3;
    private const int GreatSwordDropSwings = 6;     // two combos
    private const float GreatSwordGrace = 0.45f;   // the last dropped swing keeps the big hitbox
    // Rage rises with action and drains when nothing happens (fractions of the full meter)
    private const float RagePerKill = 0.12f;
    private const float RagePerHit = 0.04f;
    private const float RagePerHurt = 0.08f;
    private const float RageCalmDelay = 2f;     // seconds without action before it starts draining
    private const float RageDrainPerSecond = 0.05f;
    private float lastRageAction;
    private const float RageSeconds = 20f;
    private const int RageMeleeDamage = 2;
    private float stunnedUntil;
    private float runStamina = 1f;
    private float nextDustTime;

    private float _baseSpeed;
    private float _baseAttackCooldown;
    private int _baseMaxHealth;

    private Rigidbody2D rg2d;
    private Collider2D col2d;
    private FrenyInput _input;
    private PlayerAnimation _playerAnim;
    private Dash _dash;
    private SpriteRenderer _sprite;
    private GameManager _gameManager;

    // ---- read by the autopilot and the HUD, so they decide with the same information a person sees
    public bool IsGrounded => isGrounded;
    // off the ground, or just jumped (the feet circle still touches the floor for a moment)
    public bool IsAirborne => !isGrounded || isJumping;
    public bool IsDead => isDead;
    public bool IsStunned => stunned;
    // down held on the ground: he stays put (turning is allowed), lower and shorter
    public bool IsCrouching { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public bool CanAirJump => !isGrounded && airJumpsLeft > 0;
    // the HUD's jump meter: on the ground (or just off an edge), or with the double jump still left
    public bool JumpReady => isGrounded || Time.time - lastGroundedTime <= coyoteTime || CanAirJump;
    public bool AttackReady => Time.time >= attackReadyAt;
    public bool FacingRight => transform.localScale.x > 0;
    public bool RunReady => !IsRunning && runStamina >= RunMinStamina;
    public bool CanThrowGrenade => Grenades > 0;                   // no cooldown: as many as he carries
    public bool UsingShotgun => ShotgunOwned && ShotgunEquipped && ShotgunShells > 0;
    public bool Reloading => reloadReadyAt > 0f;
    public bool RevolverDry => RevolverBullets <= 0 && RevolverReserve <= 0 && !Reloading;
    // the gun can fire: not on its own cooldown and, for the revolver, loaded and not reloading
    public bool GunReady => GunReadyFor(UsingShotgun);
    public bool GunReadyFor(bool shotgun) => Time.time >= gunReadyAt && (shotgun
        ? ShotgunOwned && ShotgunShells > 0
        : !Reloading && RevolverBullets > 0);
    // a dropped potion only heals: at full health it stays on the floor until needed or it expires
    public bool WantsPotion => !isDead && Health < MaxHealth;
    public bool WantsBullets => RevolverReserve < revolverReserveMax;
    public bool WantsShells => ShotgunOwned && ShotgunShells < shellsMax;
    public bool WantsGrenades => Grenades < grenadesMax;
    public bool WantsArmor => !isDead && Armor < ArmorMax;
    // a dropped great sword is no use with the bought one, or with a fresh one still in hand
    public bool WantsGreatSword => !isDead && !FrenyProgress.HasGreatSword && GreatSwordSwings < GreatSwordDropSwings;
    // how full each supply is (0 empty .. 1 full): the drops lean on what he's short of (ItemPickup.Need)
    public float BulletsFill => revolverReserveMax > 0 ? RevolverReserve / (float)revolverReserveMax : 1f;
    public float ShellsFill => shellsMax > 0 ? ShotgunShells / (float)shellsMax : 1f;
    public float GrenadesFill => grenadesMax > 0 ? Grenades / (float)grenadesMax : 1f;

    // ---- drops and buffs: the dropped great sword, armor and Rage
    // the great sword (bought at the shop, or dropped for a few swings): FrenyMotion makes the slash
    // and the hitbox bigger. It stays on briefly after the last dropped swing so that swing is big too.
    public bool GreatSwordActive => FrenyProgress.HasGreatSword || GreatSwordSwings > 0 || Time.time < greatSwordGraceUntil;
    public int GreatSwordSwings { get; private set; }
    // the combo hit of the last swing (1, 2 or 3) and a count of swings, so the sword can hit the
    // same enemy once per swing (MeleeAttack) and FrenyMotion can draw each hit its own way
    public int ComboStep { get; private set; }
    public int SwingCount { get; private set; }
    // a press now would chain the next combo hit (the autopilot uses it to keep the combo going)
    public float AttackReadyIn => Mathf.Max(0f, attackReadyAt - Time.time);
    public bool ComboContinues => ComboStep > 0 && ComboStep < 3 && Time.time - attackReadyAt <= ComboWindow;
    // armor hearts (the Berserker helmet): each one takes a hit before the life hearts
    public int Armor { get; private set; }
    // kills, sword hits and hits taken fill the Rage meter, calm drains it; full, it gives
    // RageSeconds of red helmet and harder sword hits
    public float Rage { get; private set; }
    public bool Raging => Time.time < rageUntil;
    public float RageLeft => Raging ? (rageUntil - Time.time) / RageSeconds : 0f;
    public int MeleeDamage => Raging ? RageMeleeDamage : 1;
    public float ReloadProgress => Reloading ? Mathf.Clamp01(1f - (reloadReadyAt - Time.time) / revolverReloadTime) : 1f;
    // body and jump, for the autopilot's platform map (the same numbers the physics uses)
    public float JumpHeight => jumpHeight;
    public float MaxSpeed => _speed * runMultiplier;

    // slowed down for a moment (wading through the Ciclope's vomit)
    private float slowUntil;
    private float slowFactor = 1f;
    private float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;
    public bool Slowed => Time.time < slowUntil;

    public void Slow(float factor, float seconds) {
        slowFactor = Time.time < slowUntil ? Mathf.Min(slowFactor, factor) : factor;
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
    }
    public float BodyHalfWidth => col2d ? col2d.bounds.extents.x : 0.28f;
    public float BodyHeight => col2d ? col2d.bounds.size.y : 1.3f;
    // the middle of his body (the transform sits near the head), where flyers should aim
    public Vector3 BodyCenter => col2d ? (Vector3)col2d.bounds.center : transform.position;
    public float VerticalSpeed => rg2d ? rg2d.Vel().y : 0f;
    // where the sorcerers aim: his head when he stands. Crouched, a shot from his height passes
    // over him (the crouched collider top is about 0.4 lower), so ducking dodges it
    public Vector3 HeadPoint {
        get {
            if (col2d == null || standSize == Vector2.zero) {
                return transform.position;
            }
            float top = (standOffset.y + standSize.y * 0.5f) * Mathf.Abs(transform.lossyScale.y);
            return new Vector3(col2d.bounds.center.x, transform.position.y + top - 0.12f, 0f);
        }
    }
    // meters for the HUD: 1 = ready / full, 0 = just used / empty
    public float AttackReadiness => swingCooldown > 0f ? 1f - Mathf.Clamp01((attackReadyAt - Time.time) / swingCooldown) : 1f;
    public float RunMeter => runStamina;
    // the GameManager is looked up once in Start (FindObjectOfType walks the whole scene,
    // and the autopilot asks this on every decision)
    public bool HasPotion => _gameManager != null && _gameManager.HasPotion;

    public Vector3 Feet => feetPos ? feetPos.position : transform.position;

    private static PhysicsMaterial2D frictionless;
    private static PhysicsMaterial2D Frictionless =>
        frictionless ? frictionless : (frictionless = new PhysicsMaterial2D("Freny") { friction = 0f, bounciness = 0f });

    // ==== lifecycle ============================================================================

    private void Start() {
        _input = GetComponent<FrenyInput>();
        if (_input == null) {
            // scenes made before the input layer still get keyboard control
            _input = gameObject.AddComponent<FrenyInput>();
        }
        rg2d = GetComponent<Rigidbody2D>();
        if (rg2d != null) {
            rg2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // the big knockbacks can't skip a wall's edge
        }
        col2d = GetComponent<Collider2D>();
        if (col2d is CapsuleCollider2D capsule) {
            standSize = capsule.size;
        } else if (col2d is BoxCollider2D box) {
            standSize = box.size;
        }
        standOffset = col2d ? col2d.offset : Vector2.zero;
        _playerAnim = GetComponent<PlayerAnimation>();
        _dash = GetComponent<Dash>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _gameManager = FindAnyObjectByType<GameManager>();
        if (playerRevolverShootPosition == null) {
            playerRevolverShootPosition = FindShootPosition();
        }
        if (rg2d) {
            rg2d.gravityScale = gravityScale;
        }
        if (col2d) {
            // no friction: he slides down walls instead of sticking to them (only the arena had this)
            col2d.sharedMaterial = Frictionless;
        }

        // Freny's attribute track (FrenyProgress) multiplies the inspector values
        _baseSpeed = _speed;
        _baseAttackCooldown = attackCooldown;
        _baseMaxHealth = MaxHealth;
        ApplyUpgrades();
        FrenyProgress.Changed += ApplyUpgrades;
        Enemy.Killed += OnEnemyKilled;

        Health = MaxHealth;
        if (UIManager.Instance) {
            UIManager.Instance.UpdateLives(Health, false); // the bought hearts too (the scene has 4)
        }
        // the run's loadout: a loaded revolver with spare bullets; the shop's shotgun and grenades
        // add theirs (and again when bought mid-run, see GrantUpgradeLoadout)
        RevolverBullets = revolverCapacity;
        RevolverReserve = revolverReserveStart;
        loadoutReady = true;
        GrantUpgradeLoadout();
        // coins persist between runs and scenes (GDD: persistência)
        coins = FrenyProgress.Coins;
        if (UIManager.Instance) {
            UIManager.Instance.UpdateCoins(coins);
        }
    }

    private void OnDestroy() {
        FrenyProgress.Changed -= ApplyUpgrades;
        Enemy.Killed -= OnEnemyKilled;
    }

    private void Update() {
        if (isDead) {
            return;
        }

        if (_sprite) {
            // blink while invulnerable
            _sprite.enabled = !IsInvulnerable || Mathf.Repeat(Time.time, 0.16f) < 0.08f;
        }
        if (!Raging && Rage > 0f && Time.time - lastRageAction > RageCalmDelay) {
            Rage = Mathf.Max(0f, Rage - RageDrainPerSecond * Time.deltaTime);
        }

        UpdateDropThrough();
        CheckGround();
        if (_input.Current.meleePressed) {
            meleePressedTime = Time.time;
        }
        if (_input.Current.rangedPressed) {
            rangedPressedTime = Time.time;
        }
        if (UpdateStun()) {
            RecoverStamina();
            return; // knocked down: no control until he gets up (a swing pressed now is kept)
        }

        Movement();
        Run();
        Jump();
        SelectWeapon();
        Attack();
        Reload();
        Shoot();
        ThrowGrenade();
        UsePotion();
    }

    // all velocity changes happen here: doing them in Update made the jump depend on the frame rate
    private float nextWallCheck;

    private void FixedUpdate() {
        if (isDead || rg2d == null) {
            return;
        }
        // a big shove or a wrap can leave him inside a wall's tiles, where nothing pushes him out
        if (col2d != null && Time.time >= nextWallCheck) {
            nextWallCheck = Time.time + 0.1f;
            WallRescue.Free(transform, col2d.bounds.center, col2d.bounds.size.y, rg2d);
        }

        Vector2 velocity = rg2d.Vel();
        if (stunned) {
            // the shove carries him and fades out instead of stopping dead
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, knockbackDrag * Time.fixedDeltaTime);
        } else if (_dash == null || !_dash.IsDashing) {
            // while dashing, Dash owns the horizontal velocity
            velocity.x = IsCrouching ? 0f : _input.Current.move * _speed * (IsRunning ? runMultiplier : 1f) * SlowFactor;
        }

        // fixed-height jump with a quick fall: holding the button doesn't change the height
        if (velocity.y < 0f) {
            velocity.y += Physics2D.gravity.y * gravityScale * (fallMultiplier - 1f) * Time.fixedDeltaTime;
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }

        rg2d.SetVel(velocity);
    }

    private bool loadoutReady;
    private bool shotgunGranted;
    private bool grenadesGranted;

    private void GrantUpgradeLoadout() {
        if (!loadoutReady) {
            return;
        }
        if (FrenyProgress.HasShotgun && !shotgunGranted) {
            shotgunGranted = true;
            ShotgunOwned = true;
            ShotgunShells = Mathf.Max(ShotgunShells, shellsPerRun);
        }
        if (FrenyProgress.HasGrenade && !grenadesGranted) {
            grenadesGranted = true;
            Grenades = Mathf.Min(grenadesMax, Grenades + grenadesFromUpgrade);
        }
    }

    // applies in the arena, by hand and in the campaign
    public void ApplyUpgrades() {
        GrantUpgradeLoadout();
        int oldMax = MaxHealth;
        MaxHealth = _baseMaxHealth + FrenyProgress.BonusMaxHealth;
        _speed = _baseSpeed * FrenyProgress.SpeedMultiplier;
        attackCooldown = _baseAttackCooldown * FrenyProgress.AttackCooldownMultiplier;

        // the bought hearts stay in the row for good, empty when lost
        if (UIManager.Instance) {
            UIManager.Instance.EnsureBars(MaxHealth);
        }
        // a bought heart arrives full
        if (Health > 0 && MaxHealth > oldMax) {
            Health += MaxHealth - oldMax;
            if (UIManager.Instance) {
                UIManager.Instance.UpdateLives(Health, false);
            }
        }
    }

    // ==== movement =============================================================================

    private void CheckGround() {
        // dropping through a platform: the feet circle still overlaps it, but he is falling
        isGrounded = dropThroughPlatform == null && Physics2D.OverlapCircle(Feet, checkRadius, whatIsGround);
        if (isGrounded) {
            // the feet circle still touches the ground for a moment after take-off
            bool justTookOff = Time.time - jumpStartTime <= 0.15f;
            if (!wasGrounded && !justTookOff) {
                Fx.Dust(Feet, 5); // landed
                Sfx.Play("freny_land", Feet);
            }
            // the fall only ends once he has really stopped on the ground: the wide feet circle
            // grazes the ledges he falls past for several frames, and each graze used to restart
            // the fall there, so a long drop through the arena never hurt (José, 2026-10-29)
            bool stopped = rg2d == null || rg2d.Vel().y > -LandedSpeed;
            if (stopped) {
                if (!landed && !justTookOff) {
                    FallDamage(fallPeakY - transform.position.y);
                }
                landed = true;
                fallPeakY = transform.position.y;
            } else {
                landed = false;
                fallPeakY = Mathf.Max(fallPeakY, transform.position.y);
            }
            if (!justTookOff) {
                isJumping = false;
            }
            airJumpsLeft = FrenyProgress.HasDoubleJump ? 1 : 0;
            lastGroundedTime = Time.time;
        } else {
            landed = false;
            fallPeakY = Mathf.Max(fallPeakY, transform.position.y);
        }
        wasGrounded = isGrounded;
    }

    private void FallDamage(float drop) {
        if (drop <= SafeFallHeight) {
            return;
        }
        int hearts = Mathf.Min(FallDamageMax, 1 + Mathf.FloorToInt((drop - SafeFallHeight) / FallDamageStep));
        Fx.Dust(Feet, 12);
        TryDamage(hearts, false); // the armor doesn't soften a fall
    }

    // after a teleport (pit, checkpoint): the fall starts again from here
    public void ResetFall() {
        fallPeakY = transform.position.y;
    }

    // true while he is still down
    private bool UpdateStun() {
        if (!stunned) {
            return false;
        }
        if (Time.time < stunnedUntil) {
            _playerAnim.Move(false);
            return true;
        }
        stunned = false;
        _playerAnim.Stunned(false);
        return false;
    }

    private void Movement() {
        float move = _input.Current.move;
        SetCrouching(_input.Current.crouchHeld && isGrounded && (_dash == null || !_dash.IsDashing));
        _playerAnim.Move(move != 0f && !IsCrouching);
        if (move == 0f) {
            return;
        }

        Face(move > 0f);
        if (IsCrouching) {
            return;
        }
        // dust kicked up while running
        if (isGrounded && Time.time >= nextDustTime) {
            Fx.Dust(Feet, 2);
            Sfx.Play("freny_step", Feet);
            nextDustTime = Time.time + 0.15f;
        }
    }

    private const float CrouchHeight = 0.7f;  // of the standing collider, keeping its bottom
    private Vector2 standSize, standOffset;

    private void SetCrouching(bool crouching) {
        if (crouching == IsCrouching) {
            return;
        }
        IsCrouching = crouching;
        if (standSize == Vector2.zero) {
            return;
        }
        Vector2 size = crouching ? new Vector2(standSize.x, standSize.y * CrouchHeight) : standSize;
        Vector2 offset = standOffset - new Vector2(0f, (standSize.y - size.y) * 0.5f);
        if (col2d is CapsuleCollider2D capsule) {
            capsule.size = size;
        } else if (col2d is BoxCollider2D box) {
            box.size = size;
        }
        col2d.offset = offset;
    }

    private void Face(bool right) {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
        transform.localScale = scale;
    }

    // Shift: a sprint that drains the stamina bar; it refills (from where it stopped) when he lets go
    private void Run() {
        PlayerInputFrame input = _input.Current;
        bool wantsRun = input.runHeld && input.move != 0f;
        if (IsRunning) {
            runStamina = Mathf.Max(0f, runStamina - Time.deltaTime / runDuration);
            if (!wantsRun || runStamina <= 0f) {
                EndRun();
            }
        } else {
            RecoverStamina();
            if (wantsRun && RunReady && isGrounded) {
                // in the air the same key is the dash (see Dash)
                IsRunning = true;
                Fx.Dust(Feet, 4);
            }
        }
    }

    private void RecoverStamina() {
        if (!IsRunning) {
            runStamina = Mathf.Min(1f, runStamina + Time.deltaTime / runCooldown);
        }
    }

    private void EndRun() {
        IsRunning = false;
    }

    // TowerFall style: fixed height, with coyote time and a jump buffer so it feels responsive
    private void Jump() {
        if (_input.Current.jumpPressed && _input.Current.crouchHeld && TryDropThrough()) {
            return;
        }
        if (_input.Current.jumpPressed) {
            jumpPressedTime = Time.time;
        }
        bool wantsJump = Time.time - jumpPressedTime <= jumpBuffer;
        bool groundJump = !isJumping && (isGrounded || Time.time - lastGroundedTime <= coyoteTime);
        bool airJump = _input.Current.jumpPressed && CanAirJump; // the buffer only applies on the ground

        if (wantsJump && (groundJump || airJump)) {
            jumpPressedTime = -10f;
            if (groundJump) {
                Fx.Dust(Feet, 4);
                Sfx.Play("freny_jump", Feet);
            } else {
                airJumpsLeft--;
                Fx.Burst(Feet, Color.white, 8, 1.5f, 0.07f, 0.3f);
                Sfx.Play("freny_jump_air", Feet);
            }
            isJumping = true;
            jumpStartTime = Time.time;
            // keep the horizontal speed; setting Vector2.up * force zeroed it for one physics step
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * gravityScale * jumpHeight);
            rg2d.SetVel(new Vector2(rg2d.Vel().x, jumpVelocity));
        }

        _playerAnim.Jump(!isGrounded || isJumping);
    }

    // Down + Jump on a pass-through platform (one with a PlatformEffector2D): he drops to the
    // one below. Solid floors ignore it and he just jumps.
    private bool TryDropThrough() {
        if (!isGrounded || col2d == null) {
            return false;
        }
        Collider2D floor = Physics2D.OverlapCircle(Feet, checkRadius, whatIsGround);
        if (floor == null || !floor.usedByEffector || floor.GetComponent<PlatformEffector2D>() == null) {
            return false;
        }
        EndDropThrough();
        dropThroughPlatform = floor;
        dropThroughUntil = Time.time + 0.35f;
        Physics2D.IgnoreCollision(col2d, floor, true);
        jumpPressedTime = -10f;
        isGrounded = false;
        lastGroundedTime = -10f;  // no coyote jump back up
        rg2d.SetVel(new Vector2(rg2d.Vel().x, -2f));
        return true;
    }

    private void UpdateDropThrough() {
        if (dropThroughPlatform != null && Time.time >= dropThroughUntil) {
            EndDropThrough();
        }
    }

    private void EndDropThrough() {
        if (dropThroughPlatform != null && col2d != null) {
            Physics2D.IgnoreCollision(col2d, dropThroughPlatform, false);
        }
        dropThroughPlatform = null;
    }

    // a hit from a heavy enemy shoves Freny back and knocks him down for stunTime; a light one
    // (knockDown false) only pushes him away, still on his feet, for a moment
    public void Knockback(Vector2 velocity, bool knockDown = true) {
        if (isDead || rg2d == null) {
            return;
        }
        if (_dash) {
            _dash.Cancel();
        }
        if (IsRunning) {
            EndRun();
        }
        ChargingGrenade = false;
        GrenadeCharge = 0f;

        rg2d.SetVel(velocity);
        stunned = true;
        if (!knockDown) {
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + ShoveTime);
            return;
        }
        stunnedUntil = Time.time + stunTime;
        _playerAnim.Stunned(true);
        Sfx.Play("freny_stun", transform.position);
    }

    // ==== weapons ==============================================================================

    // 1 = pistol, 2 = shotgun (when owned and loaded)
    private void SelectWeapon() {
        if (_input.Current.selectPistol && ShotgunEquipped) {
            ShotgunEquipped = false;
            Sfx.Play("weapon_switch", transform.position);
        } else if (_input.Current.selectShotgun && ShotgunOwned && !ShotgunEquipped) {
            ShotgunEquipped = true;
            Sfx.Play("weapon_switch", transform.position);
        }
    }

    // the sword and the gun both work mid-air and crouched
    private void Attack() {
        if (Time.time - meleePressedTime > AttackBuffer || !AttackReady) {
            return;
        }
        meleePressedTime = -10f;
        ComboStep = ComboContinues ? ComboStep + 1 : 1;
        SwingCount++;
        _playerAnim.Melee(ComboStep);
        Sfx.Play("sword_combo" + ComboStep, transform.position);
        swingCooldown = attackCooldown * (ComboStep == 3 ? FinisherRecovery : ComboGapFactor);
        attackReadyAt = Time.time + swingCooldown;
        if (GreatSwordSwings > 0 && !FrenyProgress.HasGreatSword) {
            GreatSwordSwings--;
            greatSwordGraceUntil = Time.time + GreatSwordGrace;
        }
    }

    // the revolver holds revolverCapacity shots; empty, it reloads by itself over revolverReloadTime
    // with bullets from the reserve. With no reserve left it stays empty until he picks some up.
    private void Reload() {
        if (Reloading) {
            if (Time.time >= reloadReadyAt) {
                reloadReadyAt = -1f;
                int take = Mathf.Min(revolverCapacity, RevolverReserve);
                RevolverBullets = take;
                RevolverReserve -= take;
                Fx.Spray(transform.position + Vector3.up * 0.6f, Vector3.up, new Color(1f, 0.9f, 0.5f), 4, 1.5f, 0.3f);
                Sfx.Play("pistol_reload", transform.position);
            }
        } else if (RevolverBullets <= 0 && RevolverReserve > 0) {
            reloadReadyAt = Time.time + revolverReloadTime;
            Sfx.Play("pistol_empty", transform.position);
        }
    }

    // up held (W / arrow up): he fires straight up, from above his head
    public bool AimingUp => _input != null && _input.Current.aimUpHeld && !IsCrouching;

    private void Shoot() {
        bool pressed = Time.time - rangedPressedTime <= AttackBuffer;
        if (!pressed || !GunReady) {
            return;
        }
        rangedPressedTime = -10f;

        _playerAnim.Ranged();
        Vector3 muzzle = playerRevolverShootPosition ? playerRevolverShootPosition.position : transform.position;
        Vector3 forward = FacingRight ? Vector3.right : Vector3.left;
        if (AimingUp) {
            muzzle = new Vector3(transform.position.x, col2d ? col2d.bounds.max.y + 0.1f : transform.position.y + 0.8f, 0f);
            forward = Vector3.up;
        } else if (IsCrouching) {
            muzzle += Vector3.down * CrouchMuzzleDrop;
        }
        Fx.Spray(muzzle, forward, new Color(1f, 0.9f, 0.5f), 6, 5f, 0.3f);

        // the shotgun fires three bullets in a fan, one shell per shot; only its pellets shove enemies back
        bool shotgun = UsingShotgun;
        Sfx.Play(shotgun ? "shotgun_shot" : "pistol_shot", muzzle);
        if (shotgun) {
            ShotgunShells--;
            int shot = Achievements.NextShot();   // the three pellets' kills add up for "Doze é Demais"
            FireBullet(muzzle, Quaternion.Euler(0f, 0f, 10f) * forward, 1f, shot);
            FireBullet(muzzle, forward, 1f, shot);
            FireBullet(muzzle, Quaternion.Euler(0f, 0f, -10f) * forward, 1f, shot);
        } else {
            RevolverBullets--;
            FireBullet(muzzle, forward, RevolverPush);
        }
        gunReadyAt = Time.time + (shotgun ? shotgunCooldown : pistolCooldown) * FrenyProgress.AttackCooldownMultiplier;
    }

    private const float CrouchMuzzleDrop = 0.32f; // the crouched gun sits 8 art pixels lower

    private const float RevolverPush = 0f; // the revolver only hurts: no shove

    private void FireBullet(Vector3 from, Vector3 direction, float push = 1f, int shotgunShot = 0) {
        GameObject bullet = Instantiate(revolverBulletPrefab, from, Quaternion.identity);
        RangedAttack shot = bullet.GetComponent<RangedAttack>();
        if (shot) {
            shot.SetDirection(direction);
            shot.SetPush(push);
            shot.ShotgunShot = shotgunShot;
        }
    }

    public void AddShells(int amount) {
        if (ShotgunOwned) {
            ShotgunShells = Mathf.Min(shellsMax, ShotgunShells + amount);
        }
    }

    // ---- pickups (ItemPickup): each returns false when he has no use for it, so it stays on the floor

    public bool AddBullets(int amount) {
        if (!WantsBullets) {
            return false;
        }
        RevolverReserve = Mathf.Min(revolverReserveMax, RevolverReserve + amount);
        return true;
    }

    public bool AddGrenades(int amount) {
        if (!WantsGrenades) {
            return false;
        }
        Grenades = Mathf.Min(grenadesMax, Grenades + amount);
        return true;
    }

    // a shotgun found in a chest: his for the rest of the run; one he already has just gets the shells
    public bool GiveShotgun(int shells) {
        if (ShotgunOwned) {
            if (!WantsShells) {
                return false;
            }
            AddShells(shells);
            return true;
        }
        ShotgunOwned = true;
        ShotgunEquipped = true;
        ShotgunShells = Mathf.Min(shellsMax, shells);
        return true;
    }

    public bool CollectPotion(int healPoints) {
        if (isDead) {
            return false;
        }
        if (Health >= MaxHealth) {
            return false;
        }
        Heal(Mathf.Max(1, healPoints));
        return true;
    }

    // Worms style: hold the grenade button (E) to charge, release to lob the grenade forward in an arc whose range
    // depends on the charge. The autopilot sends the charge it wants directly.
    private void ThrowGrenade() {
        PlayerInputFrame input = _input.Current;
        ChargingGrenade = CanThrowGrenade && input.grenadeHeld;
        GrenadeCharge = ChargingGrenade ? input.grenadeCharge : 0f;

        if (!input.grenadePressed || !CanThrowGrenade) {
            return;
        }
        Grenades--;
        ChargingGrenade = false;

        Vector3 from = transform.position + Vector3.up * 0.6f;
        float facing = FacingRight ? 1f : -1f;
        float speed = Mathf.Lerp(grenadeMinSpeed, grenadeMaxSpeed, Mathf.Clamp01(input.grenadeCharge));
        float rad = grenadeAngle * Mathf.Deg2Rad;
        Vector2 velocity = new Vector2(facing * Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

        // built at runtime, so no prefab is needed
        GameObject grenade = new GameObject("Grenade");
        grenade.transform.position = from;
        grenade.transform.localScale = Vector3.one * 0.35f;
        SpriteRenderer renderer = grenade.AddComponent<SpriteRenderer>();
        renderer.sprite = ArenaAssets.Instance ? ArenaAssets.Instance.grenadeIcon : null;
        renderer.sortingOrder = 50;
        Rigidbody2D body = grenade.AddComponent<Rigidbody2D>();
        body.gravityScale = GrenadeGravity;
        body.SetVel(velocity);
        body.angularVelocity = -facing * 360f;
        CircleCollider2D trigger = grenade.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.4f;
        grenade.AddComponent<Grenade>();
        Fx.Spray(from, velocity, new Color(1f, 0.9f, 0.6f), 4, 2f, 0.4f);
        Sfx.Play("grenade_throw", from);
    }

    // the charge (0..1) that lands a grenade `dx` away horizontally and `dy` higher, on the
    // fixed throw angle; -1 when it is out of range
    public float GrenadeChargeFor(float dx, float dy) {
        float rad = grenadeAngle * Mathf.Deg2Rad;
        float g = Mathf.Abs(Physics2D.gravity.y) * GrenadeGravity;
        float x = Mathf.Abs(dx);
        float denom = 2f * Mathf.Cos(rad) * Mathf.Cos(rad) * (x * Mathf.Tan(rad) - dy);
        if (denom <= 0.01f) {
            return -1f;
        }
        float speed = Mathf.Sqrt(g * x * x / denom);
        if (speed > grenadeMaxSpeed + 1f) {
            return -1f;
        }
        return Mathf.Clamp01(Mathf.InverseLerp(grenadeMinSpeed, grenadeMaxSpeed, speed));
    }

    // ==== health ===============================================================================

    public void Damage() {
        TryDamage();
    }

    // false when the hit was ignored (dead or still blinking from the last one)
    public bool TryDamage() {
        return TryDamage(1);
    }

    // a heavier hit (the Golem, the Sorcerer's hexball): armor takes the points first, then life.
    // A fall skips the armor (armorAbsorbs false).
    public bool TryDamage(int amount) {
        return TryDamage(amount, true);
    }

    public bool TryDamage(int amount, bool armorAbsorbs) {
        if (isDead || IsInvulnerable) {
            return false;
        }
        invulnerableUntil = Time.time + InvulnerableTime;
        AddRage(RagePerHurt);
        int toArmor = armorAbsorbs ? Mathf.Min(Armor, Mathf.Max(1, amount)) : 0;
        int toLife = Mathf.Max(1, amount) - toArmor;
        Armor -= toArmor;
        if (toLife <= 0) {
            // the armor takes the hit: he blinks as usual but keeps his life
            _playerAnim.Hit();
            Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.75f, 0.78f, 0.85f), 12, 2.5f, 0.08f, 0.4f);
            Sfx.Play("sword_hit", transform.position);
            return true;
        }
        int before = Health;
        Health = Mathf.Max(Health - toLife, 0);
        DamageNumbers.Show(HeadPoint + Vector3.up * 0.4f, "-" + (before - Health), DamageNumbers.Hurt);
        if (UIManager.Instance) {
            UIManager.Instance.UpdateLives(Health, true);
        }
        _playerAnim.Hit();
        Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.9f, 0.2f, 0.2f), 10, 2.5f, 0.08f, 0.4f);
        Sfx.Play("freny_hurt", transform.position);

        if (Health < 1) {
            Die();
        }
        return true;
    }

    private void Die() {
        isDead = true;
        _playerAnim.Death();
        // Freny bursts into particles instead of the "pieces" death animation
        Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.9f, 0.3f, 0.3f), 36, 3.5f, 0.12f, 0.9f);
        Sfx.Play("freny_death", transform.position);
        Sfx.Play("game_over");
        if (_sprite) {
            _sprite.enabled = false;
        }
        if (gameOver) {
            gameOver.SetActive(true);
        }
        // the body stops moving and colliding at once; Dash and the enemies check for a missing rb/collider
        Destroy(rg2d);
        Destroy(col2d);
    }

    // drinks the potion bought at the shop (Q, or the autopilot when it has the upgrade)
    private void UsePotion() {
        if (!_input.Current.potionPressed || Health >= MaxHealth || !HasPotion) {
            return;
        }
        _gameManager.HasPotion = false;
        Heal(1);
        Sfx.Play("potion", transform.position);
    }

    public void PickUpItem(HealthPotion healthPotion) {
        if (Health >= MaxHealth) {
            return;
        }
        Heal(Mathf.Max(healthPotion.healPoints, 1));
        Sfx.Play("potion", transform.position);
        Destroy(healthPotion.gameObject);
    }

    private void Heal(int amount) {
        int before = Health;
        Health = Mathf.Min(Health + amount, MaxHealth);
        if (Health > before) {
            DamageNumbers.Show(HeadPoint + Vector3.up * 0.4f, "+" + (Health - before), DamageNumbers.Healed);
        }
        if (UIManager.Instance) {
            UIManager.Instance.UpdateLives(Health, false);
        }
    }

    // a great sword picked up from the floor: bigger slashes for a few swings
    public bool GiveGreatSword() {
        if (!WantsGreatSword) {
            return false;
        }
        GreatSwordSwings = GreatSwordDropSwings;
        return true;
    }

    // the Berserker helmet: full armor
    public bool GiveArmor() {
        if (!WantsArmor) {
            return false;
        }
        Armor = ArmorMax;
        return true;
    }

    private void OnEnemyKilled(Enemy enemy) {
        AddRage(RagePerKill);
    }

    // a sword hit that landed (MeleeAttack)
    public void OnMeleeHit() {
        AddRage(RagePerHit);
    }

    private void AddRage(float amount) {
        if (isDead || Raging) {
            return;
        }
        lastRageAction = Time.time;
        Rage = Mathf.Min(1f, Rage + amount);
        if (Rage >= 1f) {
            Rage = 0f;
            rageUntil = Time.time + RageSeconds;
            Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(1f, 0.25f, 0.2f), 30, 3.5f, 0.12f, 0.7f);
            Sfx.Play("level_up", transform.position);
        }
    }

    public void AddCoins(int amount) {
        coins = Mathf.Max(0, coins + amount);
        FrenyProgress.Coins = coins;
        if (UIManager.Instance) {
            UIManager.Instance.UpdateCoins(coins);
        }
    }

    // ==== helpers ==============================================================================

    private Transform FindShootPosition() {
        foreach (Transform child in GetComponentsInChildren<Transform>()) {
            if (child.CompareTag("RevolverShootPosition")) {
                return child;
            }
        }
        GameObject tagged = GameObject.FindGameObjectWithTag("RevolverShootPosition");
        return tagged ? tagged.transform : transform;
    }

    private Transform FindChild(string name) {
        foreach (Transform child in GetComponentsInChildren<Transform>(true)) {
            if (child.name == name) {
                return child;
            }
        }
        return null;
    }
}
