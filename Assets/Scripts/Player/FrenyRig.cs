using System.Collections.Generic;
using UnityEngine;

// Skeletal-animation prototype on Freny (José, 2026-11-01), the GolemRig idea on the hero: off by
// default. When on, his idle, walk, jump and the 3-hit combo are drawn from eight parts (helmet,
// torso, upper arm and forearm of each arm, two legs; cut from the idle frame by
// Tools/ArtGen/freny_rig.py) hung on bones and posed in code, with the sword from the blade
// sheets freny_poses.py draws. Pixel art kept the Golem's way: every part is a frame pre-rotated at
// a whole angle (15 degree steps, at the 50x50 art resolution) sitting on the art's 0.04 u pixel
// grid. Everything else (crouch, dash, climb, shots, the hurt and death frames) stays with the
// Animator and FrenyMotion: the rig only takes over the frames it covers and yields otherwise.
//
// It lives under the body sprite, so FrenyMotion's rig (lean, squash, lunge, the double-jump
// flip) still moves it; the combo follows the same wind-up/cut timing and hit-stop as FrenyMotion.
// The body frame is made invisible (alpha 0, so the blink, the afterimages and the ragdoll that
// read it keep working) and the helmet layers over it (Rage/Berserker, the cosmetic look) are
// hidden while the rig draws: they are drawn for the frame's head, not the rig's.
[DefaultExecutionOrder(150)]   // after FrenyMotion (0) and FrenyLookRenderer (100)
public class FrenyRig : MonoBehaviour {
    public static bool on = false;             // tests set it; PlayerPrefs int "freny.rig.freny" 1 too
    public static int angleStep = 15;
    public static bool snapPositions = true;

    public const float ArtPixel = 0.04f;
    private const string Sheets = "FrenyRig/";
    private const int FrameSize = 108;         // 27x27 art pixels
    private const int Frames = 25;             // -180..180

    public enum Bone { Pelvis, Torso, Neck, ShoulderBack, ElbowBack, ShoulderFront, ElbowFront, HipBack, HipFront }
    private static readonly string[] PartOf = { null, "torso", "head", "upperArmBack", "forearmBack", "upperArmFront", "forearmFront", "legBack", "legFront" };
    private static readonly int[] OrderOf = { 0, 0, 2, -2, -2, 3, 3, -3, -1 };
    private static readonly Bone[] ParentOf = {
        Bone.Pelvis, Bone.Pelvis, Bone.Torso, Bone.Torso, Bone.ShoulderBack, Bone.Torso, Bone.ShoulderFront, Bone.Pelvis, Bone.Pelvis,
    };
    // joints in the Sprite's space (freny_rig.py prints them): the pelvis absolute, the rest
    // relative to their parent joint
    private static readonly Vector2[] Origin = {
        new Vector2(0.02f, -0.62f),    // pelvis: the belt
        Vector2.zero,                  // torso turns about it
        new Vector2(0f, 0.52f),        // neck
        new Vector2(-0.20f, 0.36f),    // shoulder back
        new Vector2(0f, -0.12f),       // elbow back
        new Vector2(0.24f, 0.36f),     // shoulder front
        new Vector2(0f, -0.12f),       // elbow front
        new Vector2(-0.12f, -0.04f),   // hip back
        new Vector2(0.12f, -0.04f),    // hip front
    };
    private static readonly Vector2 FistOffset = new Vector2(0f, -0.10f);   // the fist below the elbow

    // bone angles in degrees, + = swung forward (the way he faces), 0 = hanging straight down
    public struct Pose {
        public float torso, head, upperBack, foreBack, upperFront, foreFront, legBack, legFront;
        public float this[Bone b] {
            get {
                switch (b) {
                    case Bone.Torso: return torso;
                    case Bone.Neck: return head;
                    case Bone.ShoulderBack: return upperBack;
                    case Bone.ElbowBack: return foreBack;
                    case Bone.ShoulderFront: return upperFront;
                    case Bone.ElbowFront: return foreFront;
                    case Bone.HipBack: return legBack;
                    case Bone.HipFront: return legFront;
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
                legBack = Mathf.Lerp(a.legBack, b.legBack, t), legFront = Mathf.Lerp(a.legFront, b.legFront, t),
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

    private static Sprite[][] sheets;
    private static Dictionary<string, Sprite> blades;

    private Player player;
    private Rigidbody2D rb;
    private Dash dash;
    private FrenyMotion motion;
    private SpriteRenderer body;
    private Transform[] bones;
    private Transform fist;
    private Transform[] parts;
    private SpriteRenderer[] partRenderers;
    private SpriteRenderer blade;
    private float[] totalAngle;
    private bool drawing;
    private float phase;
    private int slashStep = 1;
    private float slashStart = -10f;
    private float swingClock = 10f;
    private float frozenUntil;
    private float swingArm, swingBlade;
    public bool Drawing => drawing;
    public string Clip { get; private set; } = "idle";

    // PlayerAnimation adds it: nothing happens unless the flag is on
    public static FrenyRig Attach(GameObject playerObject) {
        if (!on && PlayerPrefs.GetInt("freny.rig.freny", 0) > 0) {
            on = true;
        }
        return on && playerObject.GetComponent<FrenyRig>() == null ? playerObject.AddComponent<FrenyRig>() : null;
    }

    private static void LoadSheets() {
        if (sheets != null) {
            return;
        }
        sheets = new Sprite[PartOf.Length][];
        for (int i = 1; i < PartOf.Length; i++) {
            Texture2D tex = Resources.Load<Texture2D>(Sheets + PartOf[i]);
            sheets[i] = new Sprite[Frames];
            for (int f = 0; f < Frames && tex != null; f++) {
                Rect r = new Rect(f * FrameSize, 0f, FrameSize, FrameSize);
                sheets[i][f] = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sheets[i][f].name = PartOf[i] + "_" + f;
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
        bones = new Transform[Origin.Length];
        totalAngle = new float[Origin.Length];
        for (int i = 0; i < bones.Length; i++) {
            Transform b = new GameObject("bone " + (Bone)i).transform;
            b.SetParent(i == 0 ? body.transform : bones[(int)ParentOf[i]], false);
            b.localPosition = Origin[i];
            bones[i] = b;
        }
        fist = new GameObject("bone Fist").transform;
        fist.SetParent(bones[(int)Bone.ElbowFront], false);
        fist.localPosition = FistOffset;

        parts = new Transform[Origin.Length];
        partRenderers = new SpriteRenderer[Origin.Length];
        for (int i = 1; i < Origin.Length; i++) {
            partRenderers[i] = Layer("part " + PartOf[i], OrderOf[i]);
            parts[i] = partRenderers[i].transform;
        }
        blade = Layer("part blade", 4);
        MeleeAttack.FrenyHit += OnSwordHit;
    }

    private SpriteRenderer Layer(string name, int order) {
        SpriteRenderer sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(body.transform, false);
        sr.sharedMaterial = body.sharedMaterial;
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + order;
        sr.enabled = false;
        return sr;
    }

    private void OnDestroy() {
        MeleeAttack.FrenyHit -= OnSwordHit;
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
        HideHelmets();

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 vel = rb ? rb.Vel() : Vector2.zero;
        float vx = vel.x * facing;
        bool grounded = player.IsGrounded;
        Pose pose;
        bool swinging = Time.time - slashStart < Swings[slashStep - 1].windup + Swings[slashStep - 1].cut + Recover + 0.3f
            || player.ComboContinues;
        if (!grounded) {
            Clip = "jump";
            pose = JumpPose(vel.y);
        } else if (Mathf.Abs(vx) > 0.3f) {
            Clip = "walk";
            phase += Mathf.Abs(vx) * dt / Stride;
            pose = WalkPose(phase);
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
        foreach (SpriteRenderer sr in partRenderers) {
            if (sr) {
                sr.enabled = show && body.enabled;
            }
        }
        if (!show) {
            blade.enabled = false;
            body.color = Color.white;
        }
    }

    // the layers drawn over the body frame's helmet: not where the rig's head is
    private void HideHelmets() {
        if (motion != null && motion.HeadRenderer != null) {
            motion.HeadRenderer.enabled = false;
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

    // ==== poses =================================================================================

    // the walk: legs swing against the arms, the elbows bend as the arm comes forward, the torso
    // leans in a touch (FrenyMotion's lean adds to it)
    private static Pose WalkPose(float phase) {
        float s = Mathf.Sin(phase * Mathf.PI * 2f);
        return new Pose {
            torso = 4f, head = -4f,
            legFront = 35f * s, legBack = -35f * s,
            upperFront = -30f * s, foreFront = 20f * Mathf.Max(0f, -s),
            upperBack = 30f * s, foreBack = 20f * Mathf.Max(0f, s),
        };
    }

    // the jump: knees up and arms thrown back on the rise, legs spread and arms up as he falls
    private static Pose JumpPose(float vy) {
        Pose rise = new Pose { torso = 6f, head = 6f, legFront = 55f, legBack = -30f, upperFront = -50f, foreFront = 50f, upperBack = -70f, foreBack = 40f };
        Pose fall = new Pose { torso = -4f, head = -8f, legFront = 25f, legBack = -25f, upperFront = 120f, foreFront = 30f, upperBack = -120f, foreBack = -30f };
        return Pose.Lerp(fall, rise, Mathf.Clamp01((vy + 3f) / 7f));
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
        for (int i = 1; i < bones.Length; i++) {
            float angle = pose[(Bone)i] * mirror;
            bones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            totalAngle[i] = totalAngle[(int)ParentOf[i]] + angle;
        }
        Transform space = body.transform;
        bool visible = body.enabled;
        for (int i = 1; i < parts.Length; i++) {
            Vector3 local = space.InverseTransformPoint(bones[i].position);
            if (snapPositions) {
                local = new Vector3(Mathf.Round(local.x / ArtPixel) * ArtPixel, Mathf.Round(local.y / ArtPixel) * ArtPixel, 0f);
            }
            int frame = Mathf.Clamp(Mathf.RoundToInt((totalAngle[i] * mirror + 180f) / angleStep), 0, Frames - 1);
            Sprite[] frames = sheets[i];
            partRenderers[i].sprite = frames != null ? frames[frame] : null;
            partRenderers[i].flipX = body.flipX;
            partRenderers[i].enabled = visible;
            parts[i].localPosition = local;
            parts[i].localRotation = Quaternion.identity;
        }
        // the sword: the blade freny_poses.py drew at this moment's angle, in the fist
        float sinceSlash = Time.time - slashStart;
        bool cutting = swinging && sinceSlash < 0.6f + Recover || (swinging && player.ComboContinues);
        string key = (player.GreatSwordActive ? "Freny great blade_" : "Freny blade_") + Snap(swingBlade, -90, 180);
        if (cutting && blades != null && blades.TryGetValue(key, out Sprite drawn)) {
            Vector3 at = space.InverseTransformPoint(fist.position);
            if (snapPositions) {
                at = new Vector3(Mathf.Round(at.x / ArtPixel) * ArtPixel, Mathf.Round(at.y / ArtPixel) * ArtPixel, 0f);
            }
            blade.sprite = drawn;
            blade.flipX = body.flipX;
            blade.transform.localPosition = at;
            blade.sortingOrder = body.sortingOrder + (swingBlade > 120f ? -1 : 4);   // over his head: behind him
            blade.enabled = visible;
        } else {
            blade.enabled = false;
        }
    }

    private static int Snap(float degrees, int min, int max) {
        return Mathf.Clamp(Mathf.RoundToInt(degrees / angleStep) * angleStep, min, max);
    }
}
