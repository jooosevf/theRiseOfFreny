using System.Collections.Generic;
using UnityEngine;

// Procedural motion on top of Freny's sprite frames, so the pixel art feels alive without new
// redrawing: lean with speed, squash and stretch on jumps and landings, a forward sword arc (the
// body stays still, only the cut moves) with a hit-stop, dash afterimages, a flip on the double jump, a springy flinch when hurt and
// a body that breaks into tumbling pieces on death (a small ragdoll).
//
// Everything moves a "Rig" transform placed at his feet, between the Player root (physics, facing
// flip) and the animated Sprite child, so the Animator, the colliders and the ground check are
// untouched. Added at runtime by PlayerAnimation, which also forwards the action events.
public class FrenyMotion : MonoBehaviour {

    [Header("Lean")]
    public float leanPerSpeed = 1.6f;      // degrees per unit/s of horizontal speed
    public float maxLean = 10f;
    public float accelLean = 0.9f;         // extra lean while speeding up or braking

    [Header("Squash & stretch")]
    public float airStretch = 0.12f;       // stretch at full vertical speed
    public float takeoffKick = 4.5f;
    public float landingKick = 0.45f;      // per unit/s of fall speed
    public float breathe = 0.018f;

    [Header("Slash")]
    public float windup = 0f;              // delay before the arc appears
    public float swordReach = 1.0f;        // the sword hitbox length, in units ahead of his hand
    public float hitStop = 0.09f;          // the whole game stands still on a hit (the finisher 1.8x), real seconds
    public float hitRecoil = 1.4f;         // he is pushed back a touch by his own hit

    private const float SpringStiff = 320f, SpringDamp = 16f;    // squash: bouncy
    private const float AngleStiff = 220f, AngleDamp = 19f;      // lean: a little overshoot
    private const float SettleStiff = 900f, SettleDamp = 60f;    // standing still: straighten up fast, no wobble

    private Player player;
    private Rigidbody2D rb;
    private Dash dash;
    private Animator anim;
    private SpriteRenderer body;
    private Transform rig;
    private Transform swordBox;
    private BoxCollider2D swordCollider;

    // "Player slash.png": his own sword held straight out (melee frame 3, blade lengthened)
    private static readonly Vector2 Hand = new Vector2(0.34f, -0.5f);        // grip, in the Sprite's space
    private static readonly Vector2 CrouchHand = new Vector2(0.38f, -0.66f); // the same, crouched
    // where the sword sprite's grip goes: his fist in "Player slash", and crouched
    private static readonly Vector2 SlashGrip = new Vector2(0.46f, -0.34f);
    private static readonly Vector2 CrouchGrip = new Vector2(0.36f, -0.68f);
    // the cut: a crescent in front of the blade, swept from above his shoulder to below his hips
    private const float ArcAhead = 0.42f, ArcRadius = 0.5f, ArcLift = 0.1f;
    private const float ArcFrom = 80f, ArcTo = -80f;
    // the great sword's cut and hitbox, "bem maior": 1.8x, 2.2x from the Armeiro's level 2 (WeaponLevels)
    private static float GreatSwordScale => WeaponLevels.GreatSwordScale;
    private const float FinisherScale = 1.35f;   // the combo's third hit: a bigger cut and reach

    // the great sword in hand: bought at the shop, or dropped for a few swings (Player.GreatSwordActive)
    private bool GreatSword => player && player.GreatSwordActive;
    private Vector3 rigBase;
    private float bodyCenter = 0.6f;       // height of his middle above the feet

    private Spring squash, angle, lunge, hop;
    private float lastVx, lastVy;
    private bool wasGrounded = true;
    private bool couldAirJump;
    private bool wasFacingRight = true;
    private float flipStart = -10f;
    private float slashStart = -10f;
    private float shotStart = -10f;
    private bool arcPending;
    private int slashStep = 1;             // the combo hit being drawn (1, 2 or 3)
    private float swingClock = 10f;        // time into the swing; stands still during the hit-stop
    private bool strikePending;            // the wind-up is still on: the cut hasn't started

    // The 3-hit combo, standing or in the air: the arm ("Freny swing_<deg>" poses, every 15 degrees)
    // and the sword ("Freny blade_<deg>") go from a wind-up to the end of the cut. Angles are in
    // degrees, 0 = straight ahead, up is +. Tools/ArtGen/freny_poses.py draws the poses and prints
    // SwingGrips, the fist of each pose in the Sprite's space.
    //   1  a flat cut forward from above the shoulder, with a step
    //   2  a rising cut back up from below the hip, with a hop
    //   3  the sword taken back over his head and brought down hard, with a lunge and a shake
    private struct Swing {
        public float armFrom, bladeFrom, armTo, bladeTo, windup, cut;
        public Swing(float armFrom, float bladeFrom, float armTo, float bladeTo, float windup, float cut) {
            this.armFrom = armFrom; this.bladeFrom = bladeFrom; this.armTo = armTo; this.bladeTo = bladeTo;
            this.windup = windup; this.cut = cut;
        }
    }
    private static readonly Swing[] Swings = {
        new Swing(45f, 105f, -15f, -15f, 0.035f, 0.065f),
        new Swing(-45f, -60f, 75f, 105f, 0.03f, 0.06f),
        new Swing(90f, 150f, -45f, -75f, 0.06f, 0.07f),
    };
    private const float WindupPull = 15f;  // the sword keeps drawing back a little during the wind-up
    // the crescent of each hit (SlashArc), degrees like the blade's: kept in front of him, where
    // the hitbox is, instead of following the blade all the way from behind his head
    private static readonly float[] CutFrom = { 85f, -55f, 130f };
    private static readonly float[] CutTo = { -40f, 80f, -70f };
    private const int SwingMin = -60, SwingMax = 90, BladeMin = -90, BladeMax = 180, AngleStep = 15;
    private static readonly Vector2[] SwingGrips = {
        new Vector2(0.32f, -0.52f), new Vector2(0.40f, -0.48f), new Vector2(0.44f, -0.40f), new Vector2(0.44f, -0.36f),
        new Vector2(0.48f, -0.28f), new Vector2(0.44f, -0.20f), new Vector2(0.44f, -0.16f), new Vector2(0.40f, -0.08f),
        new Vector2(0.32f, -0.04f), new Vector2(0.28f, -0.04f), new Vector2(0.20f, 0.00f),
    };
    private static readonly Vector2 Shoulder = new Vector2(0.2f, -0.28f);  // the arm's root, in the Sprite's space
    private const float ArmAndBlade = 0.86f, ArmAndGreatBlade = 1.2f;      // shoulder to the tip
    private float swingArm, swingBlade;                                    // this frame's angles
    private float lastCutFrame = -10f;
    private const float LowerTime = 0.07f;                                 // the sword lowered, then put away
    private Dictionary<string, Sprite> blades;

    // the sword hurts only once the cut starts, not during the wind-up (MeleeAttack asks)
    // (for the overhead finisher, once the blade has come halfway down in front of him: a hit any
    // earlier froze him in the hit-stop with the sword still behind his head)
    public bool SwordLive => !enabled || rig == null || (player && player.IsCrouching)
        || swingClock >= Swings[slashStep - 1].windup + Swings[slashStep - 1].cut * (slashStep == 3 ? 0.5f : 0f);
    private float frozenUntil;
    private float nextGhost;
    private bool dead;

    // the helmet is its own layer over the body frames: Rage and Berserker (armor) repaint it.
    // Tools/ArtGen/freny_heads.py cuts one head per body frame, named after that frame.
    private SpriteRenderer head;
    private Dictionary<string, Sprite> rageHeads, berserkerHeads, rageBerserkerHeads, brokenHeads;
    public SpriteRenderer HeadRenderer => head;

    // poses the Animator has no state for, drawn over its frame: crouching (and slashing or
    // shooting crouched) and shooting in the air. Tools/ArtGen/freny_poses.py builds them.
    private Dictionary<string, Sprite> poses;

    // the sword he swings is the item itself (Items 100x100_1, or _3 for the long sword), held
    // in his fist while the cut lasts; the slash frames have an empty fist
    private SpriteRenderer sword;
    private Sprite normalSword, longSword;

    private void Start() {
        player = GetComponent<Player>();
        rb = GetComponent<Rigidbody2D>();
        dash = GetComponent<Dash>();
        anim = GetComponentInChildren<Animator>();
        body = anim ? anim.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.transform == transform) {
            enabled = false; // nothing to move separately from the physics body
            return;
        }
        BuildRig();
        BuildHead();
        foreach (Transform child in GetComponentsInChildren<Transform>(true)) {
            if (child.name == "Sword_Hit_Box") {
                swordBox = child;
                swordCollider = child.GetComponent<BoxCollider2D>();
            }
        }
        MeleeAttack.FrenyHit += OnSwordHit;
        wasFacingRight = transform.localScale.x > 0f;
    }

    private void OnDestroy() {
        MeleeAttack.FrenyHit -= OnSwordHit;
    }

    private void BuildHead() {
        rageHeads = LoadHeads("FrenyHeads/rage");
        berserkerHeads = LoadHeads("FrenyHeads/berserker");
        rageBerserkerHeads = LoadHeads("FrenyHeads/rage_berserker");
        brokenHeads = LoadHeads("FrenyHeads/berserker_broken");
        poses = LoadHeads("FrenyPoses/poses");
        head = new GameObject("Head").AddComponent<SpriteRenderer>();
        head.transform.SetParent(body.transform, false);
        head.sharedMaterial = body.sharedMaterial;
        head.sortingLayerID = body.sortingLayerID;
        head.sortingOrder = body.sortingOrder + 2;
        head.enabled = false;

        Dictionary<string, Sprite> swords = LoadHeads("FrenyPoses/swords");
        swords.TryGetValue("Freny sword", out normalSword);
        swords.TryGetValue("Freny great sword", out longSword);
        blades = LoadHeads("FrenyPoses/blades");
        sword = new GameObject("Sword").AddComponent<SpriteRenderer>();
        sword.transform.SetParent(body.transform, false);
        sword.sharedMaterial = body.sharedMaterial;
        sword.sortingLayerID = body.sortingLayerID;
        sword.sortingOrder = body.sortingOrder + 1;
        sword.enabled = false;
    }

    private static Dictionary<string, Sprite> LoadHeads(string path) {
        Dictionary<string, Sprite> heads = new Dictionary<string, Sprite>();
        foreach (Sprite s in Resources.LoadAll<Sprite>(path)) {
            heads[s.name] = s;
        }
        return heads;
    }

    // Rage turns the Berserker helmet red, so Rage with armor looks unlike either alone; the grey
    // helmet is the body frame itself. The cosmetic Berserker helmet (Loadout.WearsCosmeticHelmet)
    // shows with a snapped horn while there is no armor, whole again once the armor is on.
    private void UpdateHead() {
        Dictionary<string, Sprite> set = player == null ? null
            : player.Raging && player.Armor > 0 ? rageBerserkerHeads
            : player.Raging ? rageHeads
            : player.Armor > 0 ? berserkerHeads
            : Loadout.WearsCosmeticHelmet ? brokenHeads
            : null;
        Sprite frame = body.sprite;
        if (set == null || frame == null || !set.TryGetValue(frame.name, out Sprite helmet)) {
            head.enabled = false; // no buff, or a frame without a helmet (the white hit flash)
            return;
        }
        head.sprite = helmet;
        head.flipX = body.flipX;
        head.enabled = body.enabled; // blinks with him
        // Rage: the red helmet glows
        head.color = player.Raging ? Color.Lerp(Color.white, new Color(1f, 0.6f, 0.55f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f)) : Color.white;
    }

    // the rig pivots at the feet, so squashing and leaning keep him standing on the floor
    private void BuildRig() {
        Transform sprite = body.transform;
        Vector3 feet = player && player.feetPos ? player.feetPos.position : body.bounds.min;
        rig = new GameObject("Rig").transform;
        rig.SetParent(sprite.parent, false);
        rig.SetSiblingIndex(sprite.GetSiblingIndex());
        rig.position = new Vector3(sprite.position.x, feet.y, sprite.position.z);
        rigBase = rig.localPosition;
        sprite.SetParent(rig, true);
        bodyCenter = Mathf.Max(0.3f, body.bounds.center.y - feet.y);

        // the ground check must not bob with the squash
        if (player && player.feetPos && player.feetPos.IsChildOf(sprite)) {
            player.feetPos.SetParent(transform, true);
        }
    }

    // ==== events from PlayerAnimation ========================================================

    // the 3-hit combo: 1 cuts down, 2 cuts back up, 3 is a wider, brighter cut with a step forward
    public void Slash(int step = 1) {
        slashStart = Time.time;
        arcPending = true;
        slashStep = Mathf.Clamp(step, 1, 3);
        swingClock = 0f;
        strikePending = true;
        if (slashStep == 3) {
            squash.v += 2.5f;   // rising onto his toes for the big one
        }
    }

    // the moment the cut leaves the wind-up: the body throws itself into it
    private void Strike() {
        switch (slashStep) {
            case 1:
                lunge.v += 3f;
                break;
            case 2:
                hop.v += 3.2f;
                squash.v += 2f;
                break;
            default:
                lunge.v += 5.5f;
                squash.v -= 3.5f;
                ScreenKick.Kick(0.07f, 0.16f);
                if (player && player.IsGrounded) {
                    Vector3 feet = player.feetPos ? player.feetPos.position : transform.position;
                    Fx.Dust(feet + Vector3.right * (0.4f * Mathf.Sign(transform.localScale.x)), 6);
                }
                break;
        }
    }

    public void Shot() {
        shotStart = Time.time;
    }

    public void DashStart() {
        squash.v -= 4f;
        angle.v -= 160f;
    }

    public void Hurt() {
        angle.v += 420f;          // knocked back
        squash.v -= 5f;
        lunge.v -= 1.5f;
    }

    public void Die() {
        if (dead || rig == null) {
            return;
        }
        dead = true;
        head.enabled = false;
        sword.enabled = false;
        Ragdoll.Spawn(body, player ? player.whatIsGround : (LayerMask)~0, rb ? rb.Vel() : Vector2.zero);
    }

    private void OnSwordHit(Vector2 point) {
        if (dead || Time.time - slashStart > 0.6f) {
            return;
        }
        // the whole game stands still (HitStop), then he shakes for a couple of frames
        HitStop.Freeze(slashStep == 3 ? hitStop * 1.8f : hitStop);
        frozenUntil = Time.time + 0.03f;
        if (anim) {
            anim.speed = 0f;
        }
        squash.v += 2f;
        lunge.v -= hitRecoil;                  // the blade bites and he is thrown back a little
        ScreenKick.Kick(slashStep == 3 ? 0.07f : 0.035f, slashStep == 3 ? 0.16f : 0.08f);
        Fx.Burst(point, Color.white, 4, 1.5f, 0.1f, 0.12f);
        Fx.Spray(point, new Vector2(transform.localScale.x, 0.3f), new Color(1f, 0.95f, 0.8f), 9, 6f, 0.7f);
    }

    // ==== per frame ==========================================================================

    private void LateUpdate() {
        if (rig == null || dead) {
            return;
        }
        FrenyLook.Dress(body);   // first of all: the afterimages copy this frame
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) {
            return;
        }
        if (anim && anim.speed == 0f && Time.time >= frozenUntil) {
            anim.speed = 1f;
        }
        if (Time.time >= frozenUntil) {
            swingClock += dt;
        }

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 vel = rb ? rb.Vel() : Vector2.zero;
        float vx = vel.x * facing;          // in his own space: + is forward
        float vy = vel.y;
        bool grounded = player == null || player.IsGrounded;
        bool dashing = dash && dash.IsDashing;
        float sinceSlash = Time.time - slashStart;
        bool crouching = player && player.IsCrouching;
        bool climbing = OnLadder();
        Vector2 hand = crouching ? CrouchHand : Hand;

        // ---- events read from the body's motion
        if (wasGrounded && !grounded && vy > 2f) {
            squash.v += takeoffKick;        // take-off stretch
        }
        if (!wasGrounded && grounded && lastVy < -2f) {
            squash.v -= landingKick * -lastVy;   // landing squash, harder from higher
        }
        bool canAirJump = player && player.CanAirJump;
        if (couldAirJump && !canAirJump && !grounded && vy > 2f) {
            flipStart = Time.time;          // the double jump was spent: a quick front flip
            squash.v += takeoffKick * 0.6f;
        }
        couldAirJump = canAirJump;
        bool facingRight = facing > 0f;
        if (facingRight != wasFacingRight && grounded) {
            squash.v -= 2.5f;               // turning around
            angle.x = -angle.x;
        }

        // ---- targets
        float squashTarget = 0f;
        float angleTarget = -Mathf.Clamp(vx * leanPerSpeed, -maxLean, maxLean);
        float lungeTarget = 0f;
        if (grounded) {
            // lean into speeding up only: a braking lean read as him slowly straightening himself out
            float accel = (Mathf.Abs(vx) - Mathf.Abs(lastVx)) / dt;
            angleTarget -= Mathf.Sign(vx) * Mathf.Clamp(accel, 0f, 40f) * accelLean * 0.05f;
            if (Mathf.Abs(vx) < 0.1f) {
                squashTarget = Mathf.Sin(Time.time * 2.4f) * breathe; // breathing
            }
        } else {
            squashTarget = Mathf.Clamp(Mathf.Abs(vy) / 12f, 0f, 1f) * airStretch;
            angleTarget *= 0.5f;
        }
        if (dashing) {
            squashTarget = -0.14f;
            angleTarget = -14f;
            if (Time.time >= nextGhost) {
                nextGhost = Time.time + 0.035f;
                Afterimage.Spawn(body, new Color(0.55f, 0.85f, 1f, 0.55f));
            }
        }
        if (player && player.IsStunned) {
            angleTarget = 8f;
        }
        // the body stays planted while he swings, only leaning into the big one, and while he
        // climbs (the climbing frames do the moving)
        Swing swing = Swings[slashStep - 1];
        if ((sinceSlash < 0.3f && grounded) || climbing) {
            angleTarget = 0f;
            squashTarget = 0f;
            if (slashStep == 3 && !climbing && !crouching) {
                angleTarget = swingClock < swing.windup ? 7f : swingClock < swing.windup + 0.15f ? -9f : 0f;
            }
        }
        if (crouching) {
            // crouched, the arm can't swing: the old crescent in front of the blade
            if (arcPending && sinceSlash >= windup) {
                arcPending = false;
                float scale = GreatSword ? GreatSwordScale : 1f;
                SlashArc.Spawn(this, transform, body, hand + new Vector2(ArcAhead * scale, ArcLift), ArcRadius * scale,
                    ArcFrom, ArcTo, GreatSword, false, SlashArc.DefaultLife, 1f);
            }
            strikePending = false;
        } else if (strikePending && swingClock >= swing.windup) {
            strikePending = false;
            Strike();
            if (arcPending) {
                // the smear follows the tip of the blade, around his shoulder
                arcPending = false;
                float radius = (GreatSword ? ArmAndGreatBlade : ArmAndBlade) * (slashStep == 3 ? 1.4f : 1.25f);
                SlashArc.Spawn(this, body.transform, body, Shoulder, radius, CutFrom[slashStep - 1], CutTo[slashStep - 1], GreatSword,
                    slashStep == 3, slashStep == 3 ? 0.22f : SlashArc.DefaultLife, slashStep == 3 ? 1.05f : 1f);
            }
        }
        UpdateSwingAngles(swing);

        // ---- springs (frozen during the hit-stop)
        if (Time.time >= frozenUntil) {
            squash.Step(squashTarget, SpringStiff, SpringDamp, dt);
            bool settling = grounded && Mathf.Abs(vx) < 0.1f && Mathf.Abs(angleTarget) < 0.5f;
            angle.Step(angleTarget, settling ? SettleStiff : AngleStiff, settling ? SettleDamp : AngleDamp, dt);
            lunge.Step(lungeTarget, SpringStiff, 22f, dt);
            hop.Step(0f, SpringStiff, 20f, dt);
        }
        squash.x = Mathf.Clamp(squash.x, -0.35f, 0.35f);
        angle.x = Mathf.Clamp(angle.x, -35f, 35f);

        // ---- apply
        float flip = 0f;
        float sinceFlip = Time.time - flipStart;
        if (sinceFlip < 0.32f) {
            float t = sinceFlip / 0.32f;
            flip = -360f * (1f - (1f - t) * (1f - t)); // ease-out, forward
        }
        Vector3 shake = Time.time < frozenUntil ? (Vector3)(Random.insideUnitCircle * 0.03f) : Vector3.zero;
        Quaternion flipRot = Quaternion.Euler(0f, 0f, flip);
        Vector3 pivot = new Vector3(0f, bodyCenter, 0f);
        Vector3 flipOffset = pivot - flipRot * pivot;  // the flip turns around his middle, not his feet

        rig.localRotation = Quaternion.Euler(0f, 0f, angle.x + flip);
        rig.localScale = new Vector3(1f - squash.x * 0.8f, 1f + squash.x, 1f);
        rig.localPosition = rigBase + new Vector3(lunge.x, Mathf.Max(0f, hop.x), 0f) + flipOffset + shake;

        // The sword hitbox covers the whole cut: from his hand forward by the reach, from above the
        // shoulder to below the hip. The Melee Attack clip still keys the old box (and switches it
        // on), so its shape is set here, after the Animator, every frame; the great sword reaches 1.8x.
        if (swordBox) {
            float reach = swordReach * (GreatSword ? GreatSwordScale : 1f) * (slashStep == 3 ? FinisherScale : 1f);
            swordBox.localPosition = hand;
            swordBox.localRotation = Quaternion.identity;
            swordBox.localScale = Vector3.one;
            if (swordCollider) {
                // the overhead finisher comes down from above his head: it covers higher up
                bool overhead = slashStep == 3 && !crouching;
                swordCollider.offset = new Vector2(reach * 0.5f, overhead ? 0.4f : 0.2f);
                swordCollider.size = new Vector2(reach, overhead ? 1.45f : 1.05f);
            }
        }

        // the Animator's slash frames are the cue: while they play he is swinging
        // and between two hits of a combo the arm and blade stay where the last cut ended, so the
        // combo reads as one move instead of the sword vanishing between hits
        bool free = !crouching && !climbing && !dashing && Time.time - shotStart > 0.3f && player && !player.IsStunned;
        bool cutFrame = body.sprite && body.sprite.name.StartsWith("Player slash_")
            || (free && player.ComboContinues);
        if (cutFrame) {
            lastCutFrame = Time.time;
        } else if (free && Time.time - lastCutFrame < LowerTime) {
            // a moment with the arm and sword lowered before the sword is put away
            cutFrame = true;
            swingArm = SwingMin;
            swingBlade = BladeMin;
        }
        UpdatePose(crouching, climbing, grounded, sinceSlash, vy, dt, cutFrame);
        UpdateSword(crouching, sinceSlash, cutFrame);
        UpdateHead();

        lastVx = vx;
        lastVy = vy;
        wasGrounded = grounded;
        wasFacingRight = facingRight;
    }

    private void UpdatePose(bool crouching, bool climbing, bool grounded, float sinceSlash, float vy, float dt, bool cutFrame) {
        float sinceShot = Time.time - shotStart;
        string pose = null;
        if (climbing) {
            // hand over hand: the next frame every ClimbStride climbed, held while he stays put
            climbed += Mathf.Abs(vy) * dt;
            pose = "Freny climb_" + (int)(climbed / ClimbStride) % 2;
        } else if (crouching) {
            pose = sinceSlash >= CrouchCut && sinceShot < 0.3f ? "Freny crouch ranged_" + Mathf.Min(2, (int)(sinceShot * 10f))
                : "Freny crouch_0";
        } else if (cutFrame) {
            pose = "Freny swing_" + Snap(swingArm, SwingMin, SwingMax);
        } else if (!grounded && sinceShot < 0.33f) {
            pose = "Freny air ranged_" + Mathf.Min(4, (int)(sinceShot * 15f));
        }
        if (pose != null && poses.TryGetValue(pose, out Sprite sprite)) {
            body.sprite = sprite;
            FrenyLook.Dress(body);
        }
    }

    private const float CrouchCut = 0.25f;  // how long a crouched cut holds the sword out

    // the sword in his fist while he cuts: standing or in the air, the blade drawn at the swing's
    // angle; crouched, the icon exactly as drawn, raised forward at 45 degrees
    private void UpdateSword(bool crouching, float sinceSlash, bool cutFrame) {
        bool cutting = crouching ? sinceSlash < CrouchCut : cutFrame;
        Sprite held = GreatSword ? longSword : normalSword;
        sword.enabled = cutting && body.enabled && held != null;
        if (!sword.enabled) {
            return;
        }
        sword.sortingOrder = body.sortingOrder + 1;
        int arm = Snap(swingArm, SwingMin, SwingMax);
        int bladeAngle = Snap(swingBlade, BladeMin, BladeMax);
        if (!crouching && blades != null
                && blades.TryGetValue((GreatSword ? "Freny great blade_" : "Freny blade_") + bladeAngle, out Sprite drawn)) {
            // swinging: the sword drawn at this moment's angle, in the fist of this moment's arm
            Vector2 fist = SwingGrips[(arm - SwingMin) / AngleStep];
            if (body.flipX) {
                fist.x = -fist.x;
            }
            sword.sprite = drawn;
            sword.flipX = body.flipX;
            sword.transform.localPosition = fist;
            if (bladeAngle > 120) {
                sword.sortingOrder = body.sortingOrder - 1; // taken back over his head: behind him
            }
            return;
        }
        Vector2 grip = crouching ? CrouchGrip : SlashGrip;
        if (body.flipX) {
            grip.x = -grip.x;
        }
        sword.sprite = held;
        sword.flipX = body.flipX;
        sword.transform.localPosition = grip;
    }

    // this frame's arm and sword angles: held in the wind-up (drawing back a little), then the cut
    // is one frame, arm and blade jump to where the cut ends and stay there while the slash frames
    // last. The old eased sweep through every angle in between read as a rubber sword; the
    // crescent (SlashArc) is what shows the cut now, like a nail swing in Hollow Knight.
    private void UpdateSwingAngles(Swing swing) {
        float pull = Mathf.Sign(swing.bladeFrom - swing.bladeTo) * WindupPull;
        if (swingClock < swing.windup) {
            float t = swing.windup > 0f ? swingClock / swing.windup : 1f;
            swingArm = swing.armFrom;
            swingBlade = swing.bladeFrom + pull * t;
            return;
        }
        swingArm = swing.armTo;
        swingBlade = swing.bladeTo;
    }

    // an angle to the nearest drawn one (every AngleStep degrees), kept within what was drawn
    private static int Snap(float degrees, int min, int max) {
        return Mathf.Clamp(Mathf.RoundToInt(degrees / AngleStep) * AngleStep, min, max);
    }

    // LadderClimber (campaign) holds him on a ladder; it is looked up by name so this file doesn't
    // depend on the campaign scripts, and again now and then since it can be added after Start
    private const float ClimbStride = 0.35f;
    private System.Func<bool> climbingProbe;
    private float climbed;
    private float nextClimberLookup;

    private bool OnLadder() {
        if (climbingProbe == null && Time.time >= nextClimberLookup) {
            nextClimberLookup = Time.time + 1f;
            Component climber = GetComponent("LadderClimber");
            var property = climber ? climber.GetType().GetProperty("Climbing") : null;
            if (property != null && property.PropertyType == typeof(bool)) {
                climbingProbe = (System.Func<bool>)System.Delegate.CreateDelegate(typeof(System.Func<bool>), climber, property.GetGetMethod());
            }
        }
        return climbingProbe != null && climbingProbe();
    }

    private struct Spring {
        public float x, v;

        public void Step(float target, float stiffness, float damping, float dt) {
            v += (stiffness * (target - x) - damping * v) * dt;
            x += v * dt;
        }
    }

    // ==== effects ============================================================================

    private static Material vertexColor;

    // Sprites/Default tints by vertex colour and is always in the build (see Fx)
    private static Material VertexColorMaterial {
        get {
            if (vertexColor == null) {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) {
                    vertexColor = new Material(shader);
                }
            }
            return vertexColor;
        }
    }

    // The cut itself, drawn the way Hollow Knight draws a nail swing: a wide, hard-edged crescent
    // that is all there at once (a two-frame sweep, so the direction reads), holds for a few
    // frames and then wipes away from its tail while it thins out. No soft smear: the band has a
    // white outer edge, a pale inside and a thin dark rim, so it stays crisp on any background.
    private class SlashArc : MonoBehaviour {
        private const int Segments = 22;
        public const float DefaultLife = 0.16f;
        private const float Sweep = 0.025f;      // the head reaches the end of the cut
        private const float Hold = 0.05f;         // then the whole crescent stands
        private const float Rim = 0.035f;         // the dark rim, in units

        private Mesh mesh;
        private Vector3[] verts;
        private Color[] colors;
        private float from, to, radius, squashY, life, thickness;
        private float age;            // stands still during Freny's hit-stop, like his blade
        private FrenyMotion motion;
        private Color tint, rim;

        // life: how long it lasts in all; thickness scales how wide the band is (1 = half the radius)
        public static void Spawn(FrenyMotion motion, Transform owner, SpriteRenderer body, Vector2 center, float radius, float from, float to,
                bool greatSword, bool finisher, float life, float thickness) {
            GameObject go = new GameObject("SlashArc");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = new Vector3(center.x, center.y, -0.01f);
            SlashArc arc = go.AddComponent<SlashArc>();
            arc.radius = radius;
            arc.from = from;
            arc.to = to;
            arc.squashY = 1f;
            arc.life = life;
            arc.thickness = thickness;
            arc.motion = motion;
            arc.tint = greatSword ? new Color(0.7f, 0.88f, 1f) : new Color(1f, 0.96f, 0.85f);
            arc.rim = greatSword ? new Color(0.25f, 0.45f, 0.75f, 0.7f) : new Color(0.45f, 0.3f, 0.2f, 0.7f);
            if (finisher) {
                arc.tint = Color.Lerp(arc.tint, new Color(1f, 0.82f, 0.45f), 0.6f); // warmer
                arc.rim = new Color(0.6f, 0.3f, 0.1f, 0.8f);
            }
            arc.age = Sweep * 0.7f;        // most of it is there on the very frame of the cut
            arc.Build(body);
        }

        // four rings across the band: dark rim, white edge, pale inside, dark rim
        private const int Rings = 4;

        private void Build(SpriteRenderer body) {
            mesh = new Mesh();
            verts = new Vector3[(Segments + 1) * Rings];
            colors = new Color[verts.Length];
            int[] tris = new int[Segments * (Rings - 1) * 6];
            int t = 0;
            for (int i = 0; i < Segments; i++) {
                for (int r = 0; r < Rings - 1; r++) {
                    int v = i * Rings + r;
                    tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + Rings;
                    tris[t++] = v + 1; tris[t++] = v + Rings + 1; tris[t++] = v + Rings;
                }
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = VertexColorMaterial;
            mr.sortingLayerID = body.sortingLayerID;
            mr.sortingOrder = body.sortingOrder + 3; // over the sword and the head
            Draw();
        }

        private void Update() {
            if (motion == null || Time.time >= motion.frozenUntil) {
                age += Time.deltaTime;
            }
            Draw();
        }

        private void Draw() {
            if (age >= life) {
                Destroy(gameObject);
                return;
            }
            float head = EaseOut(Mathf.Clamp01(age / Sweep));                        // 0..1 along the cut
            float gone = Mathf.Clamp01((age - Sweep - Hold) / (life - Sweep - Hold)); // the wipe, 0..1
            float tail = gone * gone * 0.9f;                                          // eats the arc from its start
            float thin = 1f - gone * gone;                                            // the band narrows as it goes
            float alpha = 1f - gone * gone * gone;
            float bandMax = Mathf.Min(0.5f * thickness, 0.85f) * radius;
            Color white = new Color(1f, 1f, 1f, alpha);
            Color inside = tint; inside.a = alpha;
            Color edge = rim; edge.a *= alpha;
            for (int i = 0; i <= Segments; i++) {
                float k = i / (float)Segments;                     // 0 = tail, 1 = head
                float along = Mathf.Lerp(tail, head, k);
                float a = Mathf.Lerp(from, to, along) * Mathf.Deg2Rad;
                // pointed at both ends, widest past the middle (the blade's tip does the work)
                float shape = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI)), 0.6f);   // Sin(PI) is a hair negative: Pow gave NaN
                float width = Mathf.Max(0.01f, bandMax * shape * thin);
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * squashY, 0f);
                int v = i * Rings;
                verts[v] = dir * (radius + Rim);
                verts[v + 1] = dir * radius;
                verts[v + 2] = dir * (radius - width);
                verts[v + 3] = dir * (radius - width - Rim);
                colors[v] = edge;
                colors[v + 1] = white;
                colors[v + 2] = inside;
                colors[v + 3] = edge;
            }
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private void OnDestroy() {
            if (mesh) {
                Destroy(mesh);
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }

    // a fading copy of the current frame left behind while dashing
    private class Afterimage : MonoBehaviour {
        private SpriteRenderer sr;
        private Color color;
        private float born;

        public static void Spawn(SpriteRenderer body, Color color) {
            GameObject go = new GameObject("Afterimage");
            Transform t = body.transform;
            go.transform.SetPositionAndRotation(t.position, t.rotation);
            go.transform.localScale = t.lossyScale;
            Afterimage img = go.AddComponent<Afterimage>();
            img.sr = go.AddComponent<SpriteRenderer>();
            img.sr.sprite = body.sprite;
            img.sr.flipX = body.flipX;
            img.sr.sharedMaterial = body.sharedMaterial;
            img.sr.sortingLayerID = body.sortingLayerID;
            img.sr.sortingOrder = body.sortingOrder - 1;
            img.color = color;
            img.sr.color = color;
            img.born = Time.time;
        }

        private void Update() {
            float t = (Time.time - born) / 0.22f;
            if (t >= 1f) {
                Destroy(gameObject);
                return;
            }
            Color c = color;
            c.a *= 1f - t;
            sr.color = c;
        }
    }

    // On death the current frame is cut into head, torso and legs that fly apart, bounce on the
    // floor and fade. Moved by hand against the ground layers only, so the pieces never push
    // enemies or pickups around.
    private class Ragdoll : MonoBehaviour {
        // slices of the 200x200 cell, bottom-up, as fractions of its height (the art fills the lower 62%)
        private static readonly float[] Cuts = { 0f, 0.22f, 0.47f, 0.66f };

        private Vector2 velocity;
        private float spin;
        private float halfHeight;
        private LayerMask ground;
        private SpriteRenderer sr;
        private float born;
        private int bounces;

        public static void Spawn(SpriteRenderer body, LayerMask ground, Vector2 inherited) {
            Sprite sprite = body.sprite;
            if (sprite == null) {
                return;
            }
            Rect rect;
            try {
                rect = sprite.packed ? sprite.textureRect : sprite.rect;
            } catch (System.Exception) {
                return; // tightly packed in an atlas: the plain particle burst is enough
            }
            Transform t = body.transform;
            float facing = Mathf.Sign(t.lossyScale.x) * (body.flipX ? -1f : 1f);
            float ppu = sprite.pixelsPerUnit;
            float x0 = rect.x + rect.width * 0.2f;
            float w = rect.width * 0.6f;
            List<Ragdoll> pieces = new List<Ragdoll>();
            for (int i = 0; i < Cuts.Length - 1; i++) {
                float y0 = rect.y + rect.height * Cuts[i];
                float h = rect.height * (Cuts[i + 1] - Cuts[i]);
                Sprite part = Sprite.Create(sprite.texture, new Rect(x0, y0, w, h), new Vector2(0.5f, 0.5f), ppu);
                // where this slice sits on his body, in the sprite's local space
                Vector2 local = new Vector2(x0 - rect.x + w * 0.5f, y0 - rect.y + h * 0.5f) - sprite.pivot;
                GameObject go = new GameObject("FrenyPiece");
                go.transform.SetPositionAndRotation(t.TransformPoint(local / ppu), t.rotation);
                go.transform.localScale = new Vector3(Mathf.Abs(t.lossyScale.x) * facing, t.lossyScale.y, 1f);
                Ragdoll piece = go.AddComponent<Ragdoll>();
                piece.sr = go.AddComponent<SpriteRenderer>();
                piece.sr.sprite = part;
                piece.sr.sharedMaterial = body.sharedMaterial;
                piece.sr.sortingLayerID = body.sortingLayerID;
                piece.sr.sortingOrder = body.sortingOrder;
                piece.ground = ground;
                piece.halfHeight = h / ppu * 0.5f;
                piece.born = Time.time;
                // the head flies highest and spins most; everything is thrown backwards
                float lift = 3f + i * 2.2f;
                piece.velocity = inherited * 0.4f + new Vector2(-facing * Random.Range(1f, 2.5f + i), lift + Random.Range(0f, 1.5f));
                piece.spin = Random.Range(180f, 420f + i * 200f) * facing;
                pieces.Add(piece);
            }
        }

        private void Update() {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float age = Time.time - born;
            if (age > 3.5f) {
                Destroy(gameObject);
                return;
            }
            if (age > 2.5f) {
                Color c = sr.color;
                c.a = 1f - (age - 2.5f);
                sr.color = c;
            }

            velocity.y -= 28f * dt;
            Vector2 pos = transform.position;
            Vector2 step = velocity * dt;

            // walls
            RaycastHit2D side = Physics2D.Raycast(pos, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + 0.08f, ground);
            if (side.collider && Mathf.Abs(step.x) > 0f) {
                velocity.x = -velocity.x * 0.4f;
                step.x = 0f;
            }
            // floor
            if (velocity.y < 0f) {
                RaycastHit2D down = Physics2D.Raycast(pos, Vector2.down, -step.y + halfHeight, ground);
                if (down.collider) {
                    pos.y = down.point.y + halfHeight;
                    step.y = 0f;
                    bounces++;
                    velocity.y = bounces < 3 ? -velocity.y * 0.35f : 0f;
                    velocity.x *= 0.55f;
                    spin *= 0.45f;
                    if (bounces == 1) {
                        Fx.Dust(down.point, 2);
                    }
                }
            }
            transform.position = pos + step;
            transform.Rotate(0f, 0f, spin * dt);
        }

        private void OnDestroy() {
            if (sr && sr.sprite) {
                Destroy(sr.sprite);
            }
        }
    }
}
