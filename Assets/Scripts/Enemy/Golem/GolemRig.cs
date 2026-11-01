using UnityEngine;

// Skeletal-animation prototype on the plain Golem (José, 2026-11-01: "testar a parte do esqueleto
// animado"). Off by default, so the Golem keeps its frame-by-frame Animator; with a Mode set, the
// body renderer is hidden and the Golem is drawn from six parts (head, torso, two arms, two legs,
// cut from the idle frame by Tools/ArtGen/golem_rig.py) hung on a small bone hierarchy and posed
// in code (idle, walk, attack, hit; the poses are plain curves of time, no clips or controller).
//   Cutout      pixel art kept: every part is a frame pre-rotated at a whole angle (15 degree
//               steps, rotated at the 40x40 art resolution) and sits on the art's 0.04 u pixel
//               grid, so the blocks stay square and aligned whatever the bone does
//   CutoutFree  the same parts turned by the transforms: smooth angles, tilted uneven blocks
//   Mesh        the 2D Animation package's SpriteSkin bending one idle frame (GolemRigMesh)
// The mode is a static flag (tests set it; the PlayerPrefs int "freny.rig" 1..3 is read once for a
// quick look in the editor). The red Golem stays on frames: its recolour comes from SpriteSkin.
public class GolemRig : MonoBehaviour {
    public enum Mode { Off, Cutout, CutoutFree, Mesh }
    public static Mode mode = Mode.Off;
    public static int angleStep = 15;          // the step the part sheets were drawn at
    public static bool snapPositions = true;   // Cutout: parts on the art's pixel grid

    public const float ArtPixel = 0.04f;       // 4 texels at 100 PPU: one pixel of the 40x40 art
    private const string Sheets = "GolemRig/";
    private const int FrameSize = 196;         // one pre-rotated part, 49x49 art pixels
    private const int Frames = 13;             // -90..90 degrees
    private const float Forward = -1f;         // the sheet faces left: a swing "forward" is clockwise

    public enum Bone { Pelvis, Torso, Neck, ShoulderBack, ShoulderFront, HipBack, HipFront }
    private static readonly string[] PartOf = { null, "torso", "head", "armBack", "armFront", "legBack", "legFront" };
    private static readonly int[] OrderOf = { 0, 0, 2, -2, 3, -1, 1 };
    // bone origins in the Sprite's space (golem_rig.py prints them): the pelvis at the hips, the
    // rest relative to their parent
    private static readonly Vector2[] Origin = {
        new Vector2(-0.02f, -0.50f),   // pelvis
        Vector2.zero,                  // torso turns about the hips
        new Vector2(0f, 0.72f),        // neck
        new Vector2(-0.28f, 0.56f),    // shoulders
        new Vector2(0.36f, 0.56f),
        new Vector2(-0.08f, -0.04f),   // hips
        new Vector2(0.12f, -0.04f),
    };
    private static readonly Bone[] ParentOf = { Bone.Pelvis, Bone.Pelvis, Bone.Torso, Bone.Torso, Bone.Torso, Bone.Pelvis, Bone.Pelvis };

    // one pose: bone angles in degrees (+ = swung forward, towards where the Golem faces) and the
    // pelvis lift in units
    public struct Pose {
        public float torso, head, armBack, armFront, legBack, legFront, lift;
        public float this[Bone b] {
            get {
                switch (b) {
                    case Bone.Torso: return torso;
                    case Bone.Neck: return head;
                    case Bone.ShoulderBack: return armBack;
                    case Bone.ShoulderFront: return armFront;
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
                armBack = Mathf.Lerp(a.armBack, b.armBack, t), armFront = Mathf.Lerp(a.armFront, b.armFront, t),
                legBack = Mathf.Lerp(a.legBack, b.legBack, t), legFront = Mathf.Lerp(a.legFront, b.legFront, t),
                lift = Mathf.Lerp(a.lift, b.lift, t),
            };
        }
    }

    private static Sprite[][] sheets;          // per part name index: the 13 rotated frames
    private static Sprite bodySprite;

    private Enemy enemy;
    private SpriteRenderer body;
    private Animator anim;
    private Transform[] bones;
    private Transform[] parts;
    private SpriteRenderer[] partRenderers;
    private float[] totalAngle;
    private Mode active;
    private float windup;
    private Vector3 lastPosition;
    private string clip = "idle";
    private float clipStart;
    private float attackStart = -10f;
    public string Clip => clip;
    public Mode Active => active;
    public Transform[] Bones => bones;

    // called by Golem.Init: nothing happens unless a mode is on
    public static GolemRig Attach(Enemy owner, SpriteRenderer renderer, float attackWindup) {
        if (mode == Mode.Off && PlayerPrefs.HasKey("freny.rig")) {
            mode = (Mode)Mathf.Clamp(PlayerPrefs.GetInt("freny.rig"), 0, 3);
        }
        if (mode == Mode.Off || renderer == null) {
            return null;
        }
        GolemRig rig = renderer.gameObject.AddComponent<GolemRig>();
        rig.Build(owner, renderer, attackWindup);
        return rig;
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
        Texture2D bodyTex = Resources.Load<Texture2D>(Sheets + "body");
        if (bodyTex != null) {
            bodySprite = Sprite.Create(bodyTex, new Rect(0f, 0f, bodyTex.width, bodyTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            bodySprite.name = "GolemRig body";
        }
    }

    private void Build(Enemy owner, SpriteRenderer renderer, float attackWindup) {
        LoadSheets();
        enemy = owner;
        body = renderer;
        anim = renderer.GetComponent<Animator>();
        windup = attackWindup;
        active = mode;
        lastPosition = owner.transform.position;
        Enemy.Died += OnDied;

        bones = new Transform[Origin.Length];
        totalAngle = new float[Origin.Length];
        for (int i = 0; i < bones.Length; i++) {
            Transform b = new GameObject("bone " + (Bone)i).transform;
            b.SetParent(i == 0 ? body.transform : bones[(int)ParentOf[i]], false);
            b.localPosition = Origin[i];
            bones[i] = b;
        }

        if (active == Mode.Mesh) {
            // the whole frame bent by the bones: the Animator would keep swapping the sprite
            if (anim) {
                anim.enabled = false;
            }
            if (!GolemRigMesh.Skin(body, bodySprite, bones)) {
                active = Mode.CutoutFree;   // the mesh could not be built: the parts still show something
                Debug.LogWarning("GolemRig: mesh skin failed, using the free cutout");
            }
        }
        if (active != Mode.Mesh) {
            parts = new Transform[Origin.Length];
            partRenderers = new SpriteRenderer[Origin.Length];
            for (int i = 1; i < Origin.Length; i++) {
                GameObject go = new GameObject("part " + PartOf[i]);
                go.transform.SetParent(body.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = body.sortingOrder + OrderOf[i];
                sr.material = body.sharedMaterial;
                sr.color = body.color;
                parts[i] = go.transform;
                partRenderers[i] = sr;
            }
            body.enabled = false;   // the frames stay on the Animator, just not drawn
        }
        Apply(PoseAt("idle", 0f));
    }

    private void OnDestroy() {
        Enemy.Died -= OnDied;
    }

    // Enemy.Die reads the body renderer's bounds for the crumble, then hides every renderer
    private void OnDied(Enemy e) {
        if (e != enemy || body == null) {
            return;
        }
        body.enabled = true;
        if (active != Mode.Mesh && bodySprite != null) {
            body.sprite = bodySprite;
        }
        enabled = false;
    }

    private void LateUpdate() {
        if (enemy == null || body == null || enemy.IsDead) {
            return;
        }
        Vector3 position = enemy.transform.position;
        bool moving = Mathf.Abs(position.x - lastPosition.x) > 0.2f * Time.deltaTime;
        lastPosition = position;

        string want;
        if (enemy.IsAttacking) {
            if (clip != "attack") {
                attackStart = Time.time;
            }
            want = "attack";
        } else if (Time.time - enemy.LastHitTime < 0.3f) {
            want = "hit";
        } else {
            want = moving ? "walk" : "idle";
        }
        if (want != clip) {
            clip = want;
            clipStart = Time.time;
        }
        Apply(PoseAt(clip, Time.time - clipStart));
    }

    // the poses: idle breathes, the walk swings arms against legs, the attack winds the front arm
    // up and over then punches ahead with a step, the hit throws the chest back
    private Pose PoseAt(string name, float t) {
        switch (name) {
            case "walk": {
                float phase = t / 0.7f * Mathf.PI * 2f;
                float swing = Mathf.Sin(phase);
                return new Pose {
                    torso = 8f, head = -4f,
                    legFront = 32f * swing, legBack = -32f * swing,
                    armFront = -28f * swing, armBack = 28f * swing,
                    lift = Mathf.Abs(Mathf.Cos(phase)) * ArtPixel,
                };
            }
            case "attack": {
                Pose wound = new Pose { torso = -14f, head = -10f, armFront = -150f, armBack = 20f, legFront = 10f, legBack = -6f };
                Pose punch = new Pose { torso = 18f, head = 6f, armFront = 95f, armBack = -30f, legFront = 28f, legBack = -24f, lift = ArtPixel };
                Pose rest = PoseAt("idle", 0f);
                float w = Mathf.Max(0.1f, windup);
                if (t < w) {
                    return Pose.Lerp(rest, wound, Mathf.SmoothStep(0f, 1f, t / w));
                }
                if (t < w + 0.1f) {
                    return Pose.Lerp(wound, punch, (t - w) / 0.1f);   // the blow: quick
                }
                return Pose.Lerp(punch, rest, Mathf.SmoothStep(0f, 1f, (t - w - 0.1f) / 0.35f));
            }
            case "hit": {
                float k = 1f - Mathf.Clamp01(t / 0.3f);
                return new Pose { torso = -16f * k, head = -18f * k, armFront = -24f * k, armBack = -24f * k, lift = -ArtPixel * k };
            }
            default: {
                float breath = Mathf.Sin(t / 2.4f * Mathf.PI * 2f);
                return new Pose { torso = 2f * breath, head = -3f * breath, armFront = 5f * breath, armBack = 5f * breath, lift = breath > 0f ? 0f : -ArtPixel };
            }
        }
    }

    public void Apply(Pose pose) {
        bones[0].localPosition = Origin[0] + Vector2.up * (snapPositions && active == Mode.Cutout ? Mathf.Round(pose.lift / ArtPixel) * ArtPixel : pose.lift);
        for (int i = 1; i < bones.Length; i++) {
            float angle = pose[(Bone)i] * Forward;
            bones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            totalAngle[i] = totalAngle[(int)ParentOf[i]] + angle;
        }
        if (parts == null) {
            return;   // Mesh: the SpriteSkin reads the bones itself
        }
        Transform space = body.transform;
        for (int i = 1; i < parts.Length; i++) {
            Vector3 local = space.InverseTransformPoint(bones[i].position);
            Sprite[] frames = sheets[i];
            if (active == Mode.Cutout) {
                if (snapPositions) {
                    local = new Vector3(Mathf.Round(local.x / ArtPixel) * ArtPixel, Mathf.Round(local.y / ArtPixel) * ArtPixel, 0f);
                }
                int frame = Mathf.Clamp(Mathf.RoundToInt((totalAngle[i] + 90f) / angleStep), 0, Frames - 1);
                parts[i].localRotation = Quaternion.identity;
                partRenderers[i].sprite = frames != null ? frames[frame] : null;
            } else {
                parts[i].localRotation = Quaternion.Euler(0f, 0f, totalAngle[i]);
                partRenderers[i].sprite = frames != null ? frames[Frames / 2] : null;
            }
            parts[i].localPosition = local;
            partRenderers[i].color = body.color;
        }
    }
}
