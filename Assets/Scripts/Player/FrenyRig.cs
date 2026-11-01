using System.Collections.Generic;
using UnityEngine;

// Freny drawn from parts on a skeleton (José, 2026-11-01): his idle, walk, run, jump and the 3-hit
// sword combo come from ten parts hung on bones and posed in code, instead of the sheets' frames.
// Tools/ArtGen/freny_rig.py draws the parts by hand (helmet, torso, upper arm and forearm of each
// arm, thigh and shin of each leg), each with a round cap painted around its joint that hides
// under the part it hangs from, so a turned limb never uncovers a hole; at rest they are the idle
// frame pixel for pixel. Tools/ArtGen/freny_rig_preview.py draws the same poses in Python, to
// judge them without Unity: keep the two in step.
//
// Pixel art kept: every part is a frame pre-rotated at a whole angle (15 degree steps, at the
// 50x50 art resolution) sitting on the art's 0.04 u pixel grid. Everything else (crouch, dash,
// climb, shots, the hurt and death frames) stays with the Animator and FrenyMotion: the rig only
// takes over the frames it covers and yields otherwise.
//
// It lives under the body sprite, so FrenyMotion's rig (lean, squash, lunge, the double-jump
// flip) still moves it; the combo follows the same wind-up/cut timing and hit-stop as FrenyMotion,
// and the sword is the blade freny_poses.py draws at that angle, in his fist. The body frame is
// made invisible (alpha 0, so the blink, the afterimages and the ragdoll that read it keep
// working). The helmet layers drawn for the frame's head (Rage/Berserker, the cosmetic look) and
// FrenyMotion's sword are hidden while the rig draws: the rig has its own head sheets for them and
// a tint mask over the coat for the look's colour; the cape asks the rig where the helmet is.
//
// Off with the pref "freny.rig.freny" = 0 (or FrenyRig.on = false before the player spawns): the
// game then draws Freny as before (frames + FrenyMotion).
[DefaultExecutionOrder(150)]   // after FrenyMotion (0) and FrenyLookRenderer (100)
public class FrenyRig : MonoBehaviour {
    public const string Pref = "freny.rig.freny";
    public static bool on = true;
    public static int angleStep = 15;
    public static bool snapPositions = true;

    public const float ArtPixel = 0.04f;
    private const string Sheets = "FrenyRig/";
    private const int FrameSize = 108;         // 27x27 art pixels
    private const int Frames = 25;             // -180..180

    public enum Bone { Pelvis, Torso, Neck, ShoulderBack, ElbowBack, ShoulderFront, ElbowFront, HipBack, KneeBack, HipFront, KneeFront }
    private const int Bones = 11;
    private static readonly string[] PartOf = {
        null, "torso", "head", "upperArmBack", "forearmBack", "upperArmFront", "forearmFront", "thighBack", "shinBack", "thighFront", "shinFront",
    };
    // draw order against the body frame: the back arm and leg behind the torso, the front leg and
    // arm over it (the thighs' caps are the hem's own colour, so the front thigh can sit on top)
    private static readonly int[] OrderOf = { 0, 0, 2, -6, -5, 3, 4, -4, -3, 1, 2 };
    private static readonly bool[] HasShirt = { false, true, false, true, true, true, true, false, false, false, false };
    private static readonly Bone[] ParentOf = {
        Bone.Pelvis, Bone.Pelvis, Bone.Torso, Bone.Torso, Bone.ShoulderBack, Bone.Torso, Bone.ShoulderFront,
        Bone.Pelvis, Bone.HipBack, Bone.Pelvis, Bone.HipFront,
    };
    // joints in the Sprite's space (freny_rig.py prints them): the pelvis absolute, the rest
    // relative to their parent joint
    private static readonly Vector2[] Origin = {
        new Vector2(0.02f, -0.62f),    // pelvis: the belt
        Vector2.zero,                  // torso turns about it
        new Vector2(0f, 0.48f),        // neck
        new Vector2(-0.24f, 0.40f),    // shoulder back
        new Vector2(0f, -0.16f),       // elbow back
        new Vector2(0.20f, 0.36f),     // shoulder front
        new Vector2(0f, -0.12f),       // elbow front
        new Vector2(-0.12f, 0f),       // hip back
        new Vector2(-0.04f, -0.20f),   // knee back
        new Vector2(0.12f, 0f),        // hip front
        new Vector2(0f, -0.20f),       // knee front
    };
    private static readonly Vector2 FistOffset = new Vector2(0.02f, -0.14f);   // the fist's centre, from the front elbow
    private static readonly Vector2 CapeOffset = new Vector2(-0.16f, 0.14f);   // the helmet's back-top corner, from the neck

    // bone angles in degrees, + = swung forward (the way he faces), 0 = hanging straight down
    public struct Pose {
        public float torso, head, upperBack, foreBack, upperFront, foreFront, thighBack, shinBack, thighFront, shinFront;
        public float this[Bone b] {
            get {
                switch (b) {
                    case Bone.Torso: return torso;
                    case Bone.Neck: return head;
                    case Bone.ShoulderBack: return upperBack;
                    case Bone.ElbowBack: return foreBack;
                    case Bone.ShoulderFront: return upperFront;
                    case Bone.ElbowFront: return foreFront;
                    case Bone.HipBack: return thighBack;
                    case Bone.KneeBack: return shinBack;
                    case Bone.HipFront: return thighFront;
                    case Bone.KneeFront: return shinFront;
                    default: return 0f;
                }
            }
        }
        public static Pose Lerp(Pose a, Pose b, float t) {
            t = Mathf.Clamp01(t);
            return new Pose {
                torso = Mathf.Lerp(a.torso, b.torso, t), head = Mathf.Lerp(a.head, b.head, t),
                upperBack = Mathf.Lerp(a.upperBack, b.upperBack, t), foreBack = Mathf.Lerp(a.foreBack, b.foreBack, t),
                upperFront = Mathf.Lerp(a.upperFront, b.upperFront, t), foreFront = Mathf.Lerp(a.foreFront, b.foreFront, t),
                thighBack = Mathf.Lerp(a.thighBack, b.thighBack, t), shinBack = Mathf.Lerp(a.shinBack, b.shinBack, t),
                thighFront = Mathf.Lerp(a.thighFront, b.thighFront, t), shinFront = Mathf.Lerp(a.shinFront, b.shinFront, t),
            };
        }
    }

    // the combo, FrenyMotion's numbers: arm and blade angles (0 = forward, up is +) from the
    // wind-up to the end of the cut, and how long each takes
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
    private const float WindupPull = 15f;
    private const float HitStop = 0.06f;
    private const float Recover = 0.22f;       // the arm comes back down after a cut
    private const float Stride = 1.1f;         // units walked per step cycle

    private static Sprite[][] sheets;                                  // per bone
    private static Sprite[][] shirtSheets;                             // per bone, the tint mask
    private static Dictionary<string, Sprite[]> heads;                 // per helmet kind
    private static Dictionary<string, Sprite> blades;

    private Player player;
    private Rigidbody2D rb;
    private Dash dash;
    private FrenyMotion motion;
    private SpriteRenderer body;
    private Transform[] bones;
    private Transform fist;
    private Transform[] parts;
    private SpriteRenderer[] partRenderers, shirtRenderers;
    private SpriteRenderer blade;
    private float[] totalAngle;
    private bool drawing;
    private float phase;
    private int slashStep = 1;
    private float slashStart = -10f;
    private float swingClock = 10f;
    private float frozenUntil;
    private float swingArm, swingBlade;
    private FrenyLook.Item lookHelmet, lookColor;   // cached: FrenyLook.Worn reads the prefs
    public bool Drawing => drawing;
    public string Clip { get; private set; } = "idle";

    // PlayerAnimation adds it: nothing happens unless the flag is on
    public static FrenyRig Attach(GameObject playerObject) {
        bool enabled = on && PlayerPrefs.GetInt(Pref, 1) > 0;
        return enabled && playerObject.GetComponent<FrenyRig>() == null ? playerObject.AddComponent<FrenyRig>() : null;
    }

    private static Sprite[] Slice(string sheet) {
        Texture2D tex = Resources.Load<Texture2D>(Sheets + sheet);
        if (tex == null) {
            return null;
        }
        Sprite[] frames = new Sprite[Frames];
        for (int f = 0; f < Frames; f++) {
            Rect r = new Rect(f * FrameSize, 0f, FrameSize, FrameSize);
            frames[f] = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            frames[f].name = sheet + "_" + f;
        }
        return frames;
    }

    private static void LoadSheets() {
        if (sheets != null) {
            return;
        }
        sheets = new Sprite[Bones][];
        shirtSheets = new Sprite[Bones][];
        for (int i = 1; i < Bones; i++) {
            sheets[i] = Slice(PartOf[i]);
            shirtSheets[i] = HasShirt[i] ? Slice(PartOf[i] + "_shirt") : null;
        }
        heads = new Dictionary<string, Sprite[]>();
        foreach (string kind in new[] { "rage", "berserker", "rage_berserker", "gold", "ice", "viking", "shadow", "crown" }) {
            Sprite[] frames = Slice("head_" + kind);
            if (frames != null) {
                heads[kind] = frames;
            }
        }
        blades = new Dictionary<string, Sprite>();
        foreach (Sprite s in Resources.LoadAll<Sprite>("FrenyPoses/blades")) {
            blades[s.name] = s;
        }
    }

    private void Start() {
        player = GetComponent<Player>();
        rb = GetComponent<Rigidbody2D>();
        dash = GetComponent<Dash>();
        motion = GetComponent<FrenyMotion>();
        Animator anim = GetComponentInChildren<Animator>();
        body = anim ? anim.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.transform == transform) {
            enabled = false;
            return;
        }
        LoadSheets();
        bones = new Transform[Bones];
        totalAngle = new float[Bones];
        for (int i = 0; i < Bones; i++) {
            Transform b = new GameObject("bone " + (Bone)i).transform;
            b.SetParent(i == 0 ? body.transform : bones[(int)ParentOf[i]], false);
            b.localPosition = Origin[i];
            bones[i] = b;
        }
        fist = new GameObject("bone Fist").transform;
        fist.SetParent(bones[(int)Bone.ElbowFront], false);
        fist.localPosition = FistOffset;

        parts = new Transform[Bones];
        partRenderers = new SpriteRenderer[Bones];
        shirtRenderers = new SpriteRenderer[Bones];
        for (int i = 1; i < Bones; i++) {
            partRenderers[i] = Layer("part " + PartOf[i], OrderOf[i], 0f);
            parts[i] = partRenderers[i].transform;
            if (HasShirt[i]) {
                // the mask sits on its part: same order, a hair closer to the camera
                shirtRenderers[i] = Layer("part " + PartOf[i] + " shirt", OrderOf[i], -0.001f);
                shirtRenderers[i].transform.SetParent(parts[i], false);
            }
        }
        blade = Layer("part blade", 5, 0f);
        MeleeAttack.FrenyHit += OnSwordHit;
        RefreshLook();
        FrenyLook.Changed += RefreshLook;
    }

    private void RefreshLook() {
        lookHelmet = FrenyLook.Worn(LookSlot.Helmet);
        lookColor = FrenyLook.Worn(LookSlot.Color);
    }

    private SpriteRenderer Layer(string name, int order, float z) {
        SpriteRenderer sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(body.transform, false);
        sr.transform.localPosition = new Vector3(0f, 0f, z);
        sr.sharedMaterial = body.sharedMaterial;
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + order;
        sr.enabled = false;
        return sr;
    }

    private void OnDestroy() {
        MeleeAttack.FrenyHit -= OnSwordHit;
        FrenyLook.Changed -= RefreshLook;
        if (body != null && drawing) {
            body.color = Color.white;
        }
    }

    private void OnDisable() {
        if (drawing) {
            Show(false);
        }
    }

    // ==== events ================================================================================

    // PlayerAnimation.Melee: hit `step` of the combo starts now (FrenyMotion gets the same call)
    public void Slash(int step) {
        slashStep = Mathf.Clamp(step, 1, 3);
        slashStart = Time.time;
        swingClock = 0f;
    }

    private void OnSwordHit(Vector2 point) {
        if (Time.time - slashStart > 0.6f) {
            return;
        }
        frozenUntil = Time.time + (slashStep == 3 ? HitStop * 1.8f : HitStop);
    }

    // where the cape hangs from while the rig draws: the helmet's back-top corner, in the body
    // sprite's space (FrenyLookRenderer asks; false when the frames are showing)
    public bool CapeAnchor(out Vector2 anchor) {
        anchor = Vector2.zero;
        if (!drawing || bones == null) {
            return false;
        }
        Transform neck = bones[(int)Bone.Neck];
        float mirror = body.flipX ? -1f : 1f;
        Vector2 offset = new Vector2(CapeOffset.x * mirror, CapeOffset.y);
        Vector3 world = neck.TransformPoint(offset);
        anchor = body.transform.InverseTransformPoint(world);
        return true;
    }

    // ==== per frame =============================================================================

    private void LateUpdate() {
        if (body == null || player == null) {
            return;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (Time.time >= frozenUntil) {
            swingClock += dt;
        }
        string frame = body.sprite ? body.sprite.name : "";
        bool covered = frame.StartsWith("Player idle") || frame.StartsWith("Player walk") || frame.StartsWith("Player run")
            || frame.StartsWith("Player jump_") || frame.StartsWith("Player melee") || frame.StartsWith("Player slash")
            || frame.StartsWith("Freny swing");
        bool take = covered && !player.IsDead && !player.IsStunned && !player.IsCrouching && !(dash && dash.IsDashing);
        if (take != drawing) {
            Show(take);
        }
        if (!drawing) {
            return;
        }
        body.color = new Color(1f, 1f, 1f, 0f);   // every frame: nothing else writes it
        HideFrameLayers();

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 vel = rb ? rb.Vel() : Vector2.zero;
        float vx = vel.x * facing;
        bool grounded = player.IsGrounded;
        Pose pose;
        Swing swing = Swings[slashStep - 1];
        bool swinging = Time.time - slashStart < swing.windup + swing.cut + Recover + 0.3f || player.ComboContinues;
        if (!grounded) {
            Clip = "jump";
            pose = JumpPose(vel.y);
        } else if (Mathf.Abs(vx) > 0.3f) {
            bool running = player.IsRunning;
            Clip = running ? "run" : "walk";
            phase += Mathf.Abs(vx) * dt / Stride;
            pose = WalkPose(phase, running);
        } else {
            Clip = "idle";
            phase = 0f;
            pose = new Pose();
        }
        if (swinging) {
            Clip = "slash" + slashStep;
            SwingArm(ref pose);
        }
        Apply(pose, swinging);
    }

    private void Show(bool show) {
        drawing = show;
        for (int i = 1; i < Bones; i++) {
            partRenderers[i].enabled = show && body.enabled;
            if (shirtRenderers[i]) {
                shirtRenderers[i].enabled = false;
            }
        }
        if (!show) {
            blade.enabled = false;
            body.color = Color.white;
        }
    }

    // the layers drawn for the body frame: FrenyMotion's buff helmet and sword, and the look's
    // helmet and shirt mask (the rig draws its own)
    private void HideFrameLayers() {
        if (motion != null) {
            if (motion.HeadRenderer != null) {
                motion.HeadRenderer.enabled = false;
            }
            if (motion.SwordRenderer != null) {
                motion.SwordRenderer.enabled = false;
            }
        }
        foreach (Transform child in body.transform) {
            if (child.name == "Look Helmet" || child.name == "Look Shirt") {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr) {
                    sr.enabled = false;
                }
            }
        }
    }

    // ==== poses (the same as freny_rig_preview.py) ==============================================

    // the walk: legs against the arms, the knee bends while the leg swings through and
    // straightens to land, the elbow bends as the arm comes forward; running, everything wider and
    // the torso leans in (FrenyMotion's lean adds to it)
    private static Pose WalkPose(float phase, bool run) {
        float s = Mathf.Sin(phase * Mathf.PI * 2f);
        float c = Mathf.Cos(phase * Mathf.PI * 2f);
        float leg = run ? 48f : 32f, knee = run ? 55f : 35f, arm = run ? 45f : 24f, elbow = run ? 65f : 20f;
        float pump = run ? 25f : 0f;
        return new Pose {
            torso = run ? 14f : 0f, head = run ? -14f : 0f,
            thighFront = leg * s, shinFront = -knee * Mathf.Max(0f, c),
            thighBack = -leg * s, shinBack = -knee * Mathf.Max(0f, -c),
            upperFront = -arm * s, foreFront = elbow * Mathf.Max(0f, -s) + pump,
            upperBack = arm * s, foreBack = elbow * Mathf.Max(0f, s) + pump,
        };
    }

    // the jump: the front knee tucked and the arms back on the rise, legs spread and the hands up
    // as he falls
    private static readonly Pose Rise = new Pose {
        torso = 6f, head = 4f, thighFront = 50f, shinFront = -65f, thighBack = -20f, shinBack = -25f,
        upperFront = -35f, foreFront = -25f, upperBack = -50f, foreBack = -20f,
    };
    private static readonly Pose Fall = new Pose {
        torso = -4f, head = -10f, thighFront = 25f, shinFront = -25f, thighBack = -25f, shinBack = -10f,
        upperFront = 70f, foreFront = 40f, upperBack = -60f, foreBack = -30f,
    };
    private static Pose JumpPose(float vy) {
        return Pose.Lerp(Fall, Rise, (vy + 3f) / 7f);
    }

    // the combo on the sword arm: FrenyMotion's angles (0 = forward, up +) become the shoulder's
    // (0 = down); the elbow is cocked during the wind-up and straightens through the cut; after
    // the cut the arm is held, then comes down unless the next hit is coming
    private void SwingArm(ref Pose pose) {
        Swing swing = Swings[slashStep - 1];
        float pull = Mathf.Sign(swing.bladeFrom - swing.bladeTo) * WindupPull;
        float elbow;
        if (swingClock < swing.windup) {
            float t = swing.windup > 0f ? swingClock / swing.windup : 1f;
            swingArm = swing.armFrom;
            swingBlade = swing.bladeFrom + pull * t;
            elbow = -60f;
        } else {
            float k = Mathf.Clamp01((swingClock - swing.windup) / swing.cut);
            k = 1f - (1f - k) * (1f - k) * (1f - k);
            swingArm = Mathf.Lerp(swing.armFrom, swing.armTo, k);
            swingBlade = Mathf.Lerp(swing.bladeFrom + pull, swing.bladeTo, k);
            elbow = Mathf.Lerp(-60f, 0f, k);
        }
        float done = swingClock - swing.windup - swing.cut;
        float back = player.ComboContinues ? 0f : Mathf.Clamp01((done - 0.12f) / Recover);   // held, then lowered
        pose.upperFront = Mathf.Lerp(swingArm + 90f, pose.upperFront, back);
        pose.foreFront = Mathf.Lerp(elbow, pose.foreFront, back);
        // the other arm counters, and the body winds with the big one
        pose.upperBack = Mathf.Lerp(-swingArm * 0.4f, pose.upperBack, back);
        pose.torso += slashStep == 3 ? (swingClock < swing.windup ? -8f : 10f) * (1f - back) : 0f;
    }

    // ==== drawing ===============================================================================

    private void Apply(Pose pose, bool swinging) {
        float mirror = body.flipX ? -1f : 1f;
        for (int i = 1; i < Bones; i++) {
            float angle = pose[(Bone)i] * mirror;
            bones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            totalAngle[i] = totalAngle[(int)ParentOf[i]] + angle;
        }
        Transform space = body.transform;
        bool visible = body.enabled;
        Sprite[] head = HeadSheet(out Color headTint);
        Color tint = ShirtTint();
        for (int i = 1; i < Bones; i++) {
            Vector3 local = Snap(space.InverseTransformPoint(bones[i].position));
            int frame = Mathf.Clamp(Mathf.RoundToInt((totalAngle[i] * mirror + 180f) / angleStep), 0, Frames - 1);
            Sprite[] frames = i == (int)Bone.Neck && head != null ? head : sheets[i];
            SpriteRenderer sr = partRenderers[i];
            sr.sprite = frames != null ? frames[frame] : null;
            sr.flipX = body.flipX;
            sr.enabled = visible;
            sr.color = i == (int)Bone.Neck ? headTint : Color.white;
            parts[i].localPosition = local;
            parts[i].localRotation = Quaternion.identity;
            SpriteRenderer mask = shirtRenderers[i];
            if (mask) {
                bool tinted = visible && tint.a > 0f && shirtSheets[i] != null;
                mask.enabled = tinted;
                if (tinted) {
                    mask.sprite = shirtSheets[i][frame];
                    mask.flipX = body.flipX;
                    mask.color = tint;
                }
            }
        }
        // the sword: the blade freny_poses.py drew at this moment's angle, in the fist
        float sinceSlash = Time.time - slashStart;
        bool cutting = swinging && sinceSlash < 0.6f + Recover || (swinging && player.ComboContinues);
        string key = (player.GreatSwordActive ? "Freny great blade_" : "Freny blade_") + SnapAngle(swingBlade, -90, 180);
        if (cutting && blades != null && blades.TryGetValue(key, out Sprite drawn)) {
            blade.sprite = drawn;
            blade.flipX = body.flipX;
            blade.transform.localPosition = Snap(space.InverseTransformPoint(fist.position));
            blade.sortingOrder = body.sortingOrder + (swingBlade > 120f ? -7 : 5);   // over his head: behind him
            blade.enabled = visible;
        } else {
            blade.enabled = false;
        }
    }

    // Rage and the Berserker armor show their own helmets (they matter in a fight), else the look's
    // cosmetic one; Rage pulses red like FrenyMotion's head
    private Sprite[] HeadSheet(out Color tint) {
        tint = Color.white;
        string kind = null;
        if (player.Raging && player.Armor > 0) {
            kind = "rage_berserker";
        } else if (player.Raging) {
            kind = "rage";
        } else if (player.Armor > 0) {
            kind = "berserker";
        } else {
            kind = lookHelmet != null ? lookHelmet.art : null;
        }
        if (player.Raging) {
            tint = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.55f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
        }
        return kind != null && heads.TryGetValue(kind, out Sprite[] frames) ? frames : null;
    }

    private Color ShirtTint() {
        return lookColor != null ? lookColor.color : new Color(0f, 0f, 0f, 0f);
    }

    private static Vector3 Snap(Vector3 local) {
        return snapPositions
            ? new Vector3(Mathf.Round(local.x / ArtPixel) * ArtPixel, Mathf.Round(local.y / ArtPixel) * ArtPixel, 0f)
            : new Vector3(local.x, local.y, 0f);
    }

    private static int SnapAngle(float degrees, int min, int max) {
        return Mathf.Clamp(Mathf.RoundToInt(degrees / angleStep) * angleStep, min, max);
    }
}
