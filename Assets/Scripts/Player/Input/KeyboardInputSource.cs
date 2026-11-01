using UnityEngine;

// The human side, on the old Input Manager: keyboard, mouse and gamepad through InputBindings
// (remappable in Settings > Controls). Walking takes the Left/Right/Up/Down keys or the gamepad
// stick and d-pad, whichever is pushed further, filtered by StickFilter.
public class KeyboardInputSource : IPlayerInputSource {

    public const float MaxChargeSeconds = 1.2f; // hold the grenade button this long for a full-power throw (Worms style)
    private float grenadeHoldStart = -1f;
    private readonly StickFilter stick = new StickFilter();

    public float RawMove { get; private set; }

    public PlayerInputFrame ReadInput() {
        if (InputBindings.GameplayBlocked) {
            grenadeHoldStart = -1f;
            RawMove = 0f;
            stick.Reset();
            return new PlayerInputFrame();
        }

        bool held = InputBindings.Get(GameAction.Grenade);
        bool released = InputBindings.GetUp(GameAction.Grenade);
        if (held && grenadeHoldStart < 0f) {
            grenadeHoldStart = Time.time;
        }
        float charge = grenadeHoldStart < 0f ? 0f : Mathf.Clamp01((Time.time - grenadeHoldStart) / MaxChargeSeconds);
        if (!held) {
            grenadeHoldStart = -1f;
        }

        RawMove = Stronger(InputBindings.KeyAxis(GameAction.Left, GameAction.Right), InputBindings.PadHorizontal);
        float vertical = Stronger(InputBindings.KeyAxis(GameAction.Down, GameAction.Up), InputBindings.PadVertical);
        // the on-screen stick of touch screens, when it is pushed further than keys or gamepad
        if (Mathf.Abs(TouchControls.Horizontal) > Mathf.Abs(RawMove)) {
            RawMove = TouchControls.Horizontal;
        }
        if (Mathf.Abs(TouchControls.Vertical) > Mathf.Abs(vertical)) {
            vertical = TouchControls.Vertical;
        }
        stick.Update(RawMove, vertical);

        return new PlayerInputFrame {
            move = stick.Move,
            jumpPressed = InputBindings.GetDown(GameAction.Jump),
            jumpHeld = InputBindings.Get(GameAction.Jump),
            meleePressed = InputBindings.GetDown(GameAction.Melee),
            rangedPressed = InputBindings.GetDown(GameAction.Ranged),
            // Shift is the sprint on the ground and the dash in the air (Dash only fires in the air)
            dashPressed = InputBindings.GetDown(GameAction.Dash) || InputBindings.GetDown(GameAction.Run),
            runHeld = InputBindings.Get(GameAction.Run),
            crouchHeld = stick.Down,
            aimUpHeld = stick.Up,
            selectPistol = InputBindings.GetDown(GameAction.Pistol),
            selectShotgun = InputBindings.GetDown(GameAction.Shotgun),
            potionPressed = InputBindings.GetDown(GameAction.Potion),
            grenadePressed = released,
            grenadeHeld = held,
            grenadeCharge = charge,
        };
    }

    private static float Stronger(float a, float b) {
        return Mathf.Abs(b) > Mathf.Abs(a) ? b : a;
    }
}
