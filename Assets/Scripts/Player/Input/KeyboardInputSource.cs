using UnityEngine;

// The human side, on the old Input Manager: keyboard, mouse and gamepad through InputBindings
// (remappable in Settings > Controls). Walking takes the Left/Right/Up/Down keys or the gamepad
// stick and d-pad, whichever is pushed further, filtered by StickFilter.
public class KeyboardInputSource : IPlayerInputSource {

    private readonly StickFilter stick = new StickFilter();

    public float RawMove { get; private set; }

    public PlayerInputFrame ReadInput() {
        if (InputBindings.GameplayBlocked) {
            RawMove = 0f;
            stick.Reset();
            return new PlayerInputFrame();
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
            // José, 2026-11-01: one throw distance; holding the button only burns the fuse (Player.ThrowGrenade)
            grenadeHeld = InputBindings.Get(GameAction.Grenade),
            grenadeCharge = Player.GrenadeTapCharge,
            furyPressed = InputBindings.GetDown(GameAction.Fury),
        };
    }

    private static float Stronger(float a, float b) {
        return Mathf.Abs(b) > Mathf.Abs(a) ? b : a;
    }
}
