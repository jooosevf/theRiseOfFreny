using UnityEngine;
using UnityEngine.UI;

// On-screen controls for phones and tablets (also the web build opened on a phone), laid out like a
// Super Nintendo pad: a fixed directional pad in the bottom-left corner (walk, crouch, aim up, push
// up at a door to enter; tap a direction twice and hold it to run, or to dash in the air) and four
// face buttons in a diamond on the right (B jump, Y shoot, A sword, X grenade), with two small
// shoulder buttons above them (swap weapon, potion) and a third over those for the Rede. GameMenus shows them in gameplay scenes on
// touch screens. They feed the same actions as the keyboard through InputBindings and
// KeyboardInputSource. HelpScreen copies the labels.
// Touches are read straight from Input.touches before anyone reads input this frame, so a tap
// is never missed and several fingers work at once.
[DefaultExecutionOrder(-100)]
public class TouchControls : MonoBehaviour {

    // canvas units (reference 1280x720)
    private const float StickRadius = 80f;
    private static readonly Vector2 StickCenter = new Vector2(165f, 165f);   // from the bottom-left corner
    private const float StickGrabRadius = StickRadius * 2.6f;                // a thumb that lands near the pad still grabs it
    private const float StickDeadzone = 0.22f;
    private const float WalkSlope = 0.38f;      // |x| of the direction needed to walk (up to about 67° off horizontal)
    private const float VerticalSlope = 0.75f;  // |y| needed to crouch or aim up (steeper than 49°), so walking doesn't crouch
    private const float DoubleTapSeconds = 0.4f;

    private static readonly Vector2 DiamondCenter = new Vector2(-175f, 165f); // from the bottom-right corner
    private const float DiamondSpread = 80f;
    private const float FaceSize = 92f;
    private const float HitScale = 1.35f;       // touch area beyond the drawn disc; neighbours overlap so one thumb can press two
    private static readonly Vector2 ShoulderSize = new Vector2(118f, 54f);

    private static readonly Color BaseColor = new Color(0.20f, 0.12f, 0.33f, 0.40f);
    private static readonly Color KnobColor = new Color(0.68f, 0.24f, 0.82f, 0.65f);
    private static readonly Color RunColor = new Color(0.95f, 0.55f, 0.25f, 0.80f);
    private static readonly Color MarkColor = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color ShoulderColor = new Color(0.20f, 0.12f, 0.33f, 0.55f);
    private static readonly Color PressedColor = new Color(0.68f, 0.24f, 0.82f, 0.80f);
    // muted Super Famicom colours
    private static readonly Color JumpColor = new Color(0.85f, 0.70f, 0.15f, 0.55f);   // B yellow
    private static readonly Color ShootColor = new Color(0.20f, 0.60f, 0.30f, 0.55f);  // Y green
    private static readonly Color MeleeColor = new Color(0.80f, 0.20f, 0.20f, 0.55f);  // A red
    private static readonly Color GrenadeColor = new Color(0.25f, 0.35f, 0.80f, 0.55f); // X blue

    private static TouchControls active;

    private class Pad {
        public GameAction action;
        public RectTransform rect;
        public Image image;
        public Color color;
        public float hitRadius;     // 0 for the pill-shaped shoulder buttons, which use their rectangle
        public bool held;
        public bool down;
        public bool up;
    }

    private Canvas canvas;
    private Pad[] pads;
    private Pad swap;
    private RectTransform stickBase;
    private RectTransform stickKnob;
    private Image stickKnobImage;
    private int stickFinger = -1;
    private Vector2 stick;          // quantized: each axis -1, 0 or 1
    private float stickUpBefore;
    private float lastTapTime = float.NegativeInfinity;
    private float lastTapDirection;
    private bool running;
    private bool runDown;
    private bool runUp;

    public static bool Active => active != null && active.isActiveAndEnabled;
    public static float Horizontal => Active ? active.stick.x : 0f;
    public static float Vertical => Active ? active.stick.y : 0f;

    public static bool Get(GameAction action) {
        if (action == GameAction.Run) {
            return Active && active.running;
        }
        Pad pad = Find(action);
        return pad != null && pad.held;
    }

    public static bool GetDown(GameAction action) {
        if (!Active) {
            return false;
        }
        switch (action) {
            case GameAction.Interact:
                return active.stick.y > 0.5f && active.stickUpBefore <= 0.5f;
            case GameAction.Run:
                return active.runDown;
            // one button swaps: asking for both makes Player switch to whichever gun is not in hand
            case GameAction.Pistol:
            case GameAction.Shotgun:
                return active.swap.down;
        }
        Pad pad = Find(action);
        return pad != null && pad.down;
    }

    public static bool GetUp(GameAction action) {
        if (action == GameAction.Run) {
            return Active && active.runUp;
        }
        Pad pad = Find(action);
        return pad != null && pad.up;
    }

    private static Pad Find(GameAction action) {
        if (!Active) {
            return null;
        }
        foreach (Pad pad in active.pads) {
            if (pad.action == action) {
                return pad;
            }
        }
        return null;
    }

    public static TouchControls Create(Transform parent) {
        Canvas canvas = MenuKit.CreateCanvas("Touch Controls", 28000, parent);
        TouchControls controls = canvas.gameObject.AddComponent<TouchControls>();
        controls.canvas = canvas;
        controls.Build();
        return controls;
    }

    private void Build() {
        Transform root = canvas.transform;

        stickBase = MakeCircle("Stick", root, StickRadius * 2f, BaseColor);
        stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
        stickBase.anchoredPosition = StickCenter;
        // a mark on each side, like the arms of a d-pad
        for (int i = 0; i < 4; i++) {
            Vector2 direction = Quaternion.Euler(0f, 0f, 90f * i) * Vector2.right;
            RectTransform mark = MakeCircle("Mark", stickBase, 18f, MarkColor);
            mark.anchoredPosition = direction * (StickRadius - 16f);
        }
        stickKnob = MakeCircle("Knob", stickBase, StickRadius, KnobColor);
        stickKnobImage = stickKnob.GetComponent<Image>();

        Vector2 c = DiamondCenter;
        float s = DiamondSpread;
        pads = new[] {
            MakeFace(root, GameAction.Jump, "Pular", c + new Vector2(0f, -s), JumpColor),
            MakeFace(root, GameAction.Ranged, "Tiro", c + new Vector2(-s, 0f), ShootColor),
            MakeFace(root, GameAction.Melee, "Golpe", c + new Vector2(s, 0f), MeleeColor),
            MakeFace(root, GameAction.Grenade, "Gran.", c + new Vector2(0f, s), GrenadeColor),
            MakeShoulder(root, GameAction.Potion, "Poção", c + new Vector2(s, s * 2.25f)),
            swap = MakeShoulder(root, GameAction.Shotgun, "Arma", c + new Vector2(-s, s * 2.25f)),
            MakeShoulder(root, GameAction.Net, "Rede", c + new Vector2(0f, s * 3.1f)),
        };
    }

    private Pad MakeFace(Transform root, GameAction action, string label, Vector2 fromBottomRight, Color color) {
        Pad pad = MakePad(root, action, label, fromBottomRight, new Vector2(FaceSize, FaceSize), color);
        pad.hitRadius = FaceSize * 0.5f * HitScale;
        return pad;
    }

    private Pad MakeShoulder(Transform root, GameAction action, string label, Vector2 fromBottomRight) {
        return MakePad(root, action, label, fromBottomRight, ShoulderSize, ShoulderColor);
    }

    private Pad MakePad(Transform root, GameAction action, string label, Vector2 fromBottomRight, Vector2 size, Color color) {
        RectTransform rect = MakeCircle(label, root, size.x, color);
        rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.anchoredPosition = fromBottomRight;
        Text text = MenuKit.MakeText(rect, label, MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
        MenuKit.Stretch(text.rectTransform);
        text.raycastTarget = false;
        return new Pad { action = action, rect = rect, image = rect.GetComponent<Image>(), color = color };
    }

    private static RectTransform MakeCircle(string name, Transform parent, float size, Color color) {
        Image image = MenuKit.MakeImage(name, parent, color);
        image.sprite = Circle();
        image.raycastTarget = false; // touches are read directly, never through the EventSystem
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(size, size);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static Sprite circle;

    // a soft-edged white disc drawn once at runtime, tinted by each Image
    private static Sprite Circle() {
        if (circle != null) {
            return circle;
        }
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float radius = size / 2f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }
        texture.Apply();
        circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return circle;
    }

    private void OnEnable() {
        active = this;
        // a finger on the screen must not also count as a mouse click (the melee is on the left mouse button)
        Input.simulateMouseWithTouches = false;
    }

    private void OnDisable() {
        Input.simulateMouseWithTouches = true;
        ReleaseStick();
        stickUpBefore = 0f;
        running = runDown = runUp = false;
        if (pads != null) {
            foreach (Pad pad in pads) {
                pad.held = pad.down = pad.up = false;
                pad.image.color = pad.color;
            }
        }
    }

    private void Update() {
        stickUpBefore = stick.y;
        float horizontalBefore = stick.x;
        UpdateStick();
        UpdateRun(horizontalBefore);

        float scale = canvas.scaleFactor;
        foreach (Pad pad in pads) {
            bool held = false;
            for (int i = 0; i < Input.touchCount; i++) {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == stickFinger || touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                    continue;
                }
                if (Hits(pad, touch.position, scale)) {
                    held = true;
                    break;
                }
            }
            pad.down = held && !pad.held;
            pad.up = !held && pad.held;
            pad.held = held;
            pad.image.color = held ? PressedColor : pad.color;
        }
    }

    private static bool Hits(Pad pad, Vector2 position, float scale) {
        if (pad.hitRadius > 0f) {
            return Vector2.Distance(position, pad.rect.position) <= pad.hitRadius * scale;
        }
        return RectTransformUtility.RectangleContainsScreenPoint(pad.rect, position, null);
    }

    private void UpdateStick() {
        float scale = canvas.scaleFactor;
        Vector2 center = stickBase.position;
        for (int i = 0; i < Input.touchCount; i++) {
            Touch touch = Input.GetTouch(i);
            if (stickFinger < 0 && touch.phase == TouchPhase.Began && InStickZone(touch.position, center, scale) && !OnPad(touch.position, scale)) {
                stickFinger = touch.fingerId;
            }
            if (touch.fingerId != stickFinger) {
                continue;
            }
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                ReleaseStick();
                return;
            }
            // the pad stays put: the direction is from its centre, wherever the thumb wanders
            Vector2 offset = Vector2.ClampMagnitude(touch.position - center, StickRadius * scale);
            stickKnob.anchoredPosition = offset / scale;
            stick = Quantize(offset / (StickRadius * scale));
            return;
        }
        // the finger left without an Ended phase (e.g. the app lost focus)
        if (stickFinger >= 0) {
            ReleaseStick();
        }
    }

    // like a d-pad: walking forgives a thumb that drifts up or down, crouch and aim need a clear push
    private static Vector2 Quantize(Vector2 value) {
        if (value.magnitude < StickDeadzone) {
            return Vector2.zero;
        }
        Vector2 direction = value.normalized;
        return new Vector2(
            Mathf.Abs(direction.x) >= WalkSlope ? Mathf.Sign(direction.x) : 0f,
            Mathf.Abs(direction.y) >= VerticalSlope ? Mathf.Sign(direction.y) : 0f);
    }

    private void ReleaseStick() {
        stickFinger = -1;
        stick = Vector2.zero;
        if (stickKnob != null) {
            stickKnob.anchoredPosition = Vector2.zero;
        }
    }

    // tap a direction twice (lift the thumb or bring it back to the centre in between) and hold it to run
    private void UpdateRun(float horizontalBefore) {
        runDown = runUp = false;
        float horizontal = stick.x;
        if (horizontal != horizontalBefore) {
            if (running) {
                running = false;
                runUp = true;
            }
            if (horizontal != 0f) {
                if (horizontal == lastTapDirection && Time.unscaledTime - lastTapTime <= DoubleTapSeconds) {
                    running = runDown = true;
                    lastTapDirection = 0f; // a third tap starts a new pair
                } else {
                    lastTapDirection = horizontal;
                    lastTapTime = Time.unscaledTime;
                }
            }
        }
        stickKnobImage.color = running ? RunColor : KnobColor;
    }

    // around the pad, on the left half and below the pause button at the top
    private static bool InStickZone(Vector2 position, Vector2 center, float scale) {
        return position.x < Screen.width * 0.5f && position.y < Screen.height * 0.8f &&
               Vector2.Distance(position, center) <= StickGrabRadius * scale;
    }

    private bool OnPad(Vector2 position, float scale) {
        foreach (Pad pad in pads) {
            if (Hits(pad, position, scale)) {
                return true;
            }
        }
        return false;
    }
}
