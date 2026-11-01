using System;
using UnityEngine;

public enum GameAction {
    Jump,
    Melee,
    Ranged,
    Dash,
    Run,
    Grenade,
    Potion,
    Pistol,
    Shotgun,
    Interact,
    Pause,
    Net,       // the Rede (NetThrower), 2026-11-01: last, so the saved bindings keep their names and order
}

// Which key and which gamepad button do each action, editable in Settings > Controls and saved
// in PlayerPrefs. Walking stays on the Horizontal axis (arrows, A/D, left stick, d-pad).
// Gamepad buttons follow the Xbox layout on Windows (0 = A, 1 = B, 2 = X, 3 = Y, 4 = LB, 5 = RB,
// 6 = Back, 7 = Start, 8 = left stick click, 9 = right stick click).
public static class InputBindings {

    public static event Action Changed;

    private const string KeyboardPrefix = "freny.bind.kb.";
    private const string GamepadPrefix = "freny.bind.pad.";
    private const float StickUpThreshold = 0.6f;

    public static readonly GameAction[] All = (GameAction[])Enum.GetValues(typeof(GameAction));

    // while a menu is open (and for a frame after it closes) Freny ignores the controls, so the
    // button that closed the menu does not also jump or dash
    private static bool blocked;
    private static int blockedUntilFrame = -1;

    public static bool GameplayBlocked => blocked || Time.frameCount <= blockedUntilFrame;

    public static void BlockGameplay(bool block) {
        if (blocked && !block) {
            blockedUntilFrame = Time.frameCount + 1;
        }
        blocked = block;
    }

    public static KeyCode DefaultKeyboard(GameAction action) {
        switch (action) {
            case GameAction.Jump: return KeyCode.Space;
            case GameAction.Melee: return KeyCode.Mouse0;
            case GameAction.Ranged: return KeyCode.Mouse1;
            case GameAction.Dash: return KeyCode.LeftControl;
            case GameAction.Run: return KeyCode.LeftShift;
            case GameAction.Grenade: return KeyCode.E;
            case GameAction.Potion: return KeyCode.Q;
            case GameAction.Pistol: return KeyCode.Alpha1;
            case GameAction.Shotgun: return KeyCode.Alpha2;
            case GameAction.Interact: return KeyCode.W;
            case GameAction.Pause: return KeyCode.Escape;
            case GameAction.Net: return KeyCode.F;
            default: return KeyCode.None;
        }
    }

    public static KeyCode DefaultGamepad(GameAction action) {
        switch (action) {
            case GameAction.Jump: return KeyCode.JoystickButton0;     // A
            case GameAction.Melee: return KeyCode.JoystickButton2;    // X
            case GameAction.Ranged: return KeyCode.JoystickButton3;   // Y
            case GameAction.Dash: return KeyCode.JoystickButton1;     // B
            case GameAction.Run: return KeyCode.JoystickButton5;      // RB
            case GameAction.Grenade: return KeyCode.JoystickButton4;  // LB, hold to charge
            case GameAction.Potion: return KeyCode.JoystickButton6;   // Back
            case GameAction.Pistol: return KeyCode.JoystickButton8;   // left stick click
            case GameAction.Shotgun: return KeyCode.JoystickButton9;  // right stick click
            case GameAction.Interact: return KeyCode.None;            // stick or d-pad up
            case GameAction.Pause: return KeyCode.JoystickButton7;    // Start
            case GameAction.Net: return KeyCode.None;                 // every button is taken: set it in Controles
            default: return KeyCode.None;
        }
    }

    public static KeyCode Keyboard(GameAction action) {
        return (KeyCode)PlayerPrefs.GetInt(KeyboardPrefix + action, (int)DefaultKeyboard(action));
    }

    public static KeyCode Gamepad(GameAction action) {
        return (KeyCode)PlayerPrefs.GetInt(GamepadPrefix + action, (int)DefaultGamepad(action));
    }

    public static void SetKeyboard(GameAction action, KeyCode key) {
        Set(KeyboardPrefix, action, key, Keyboard);
    }

    public static void SetGamepad(GameAction action, KeyCode button) {
        Set(GamepadPrefix, action, button, Gamepad);
    }

    // a key can only do one thing: the action that had it gets the old key of this one
    private static void Set(string prefix, GameAction action, KeyCode key, Func<GameAction, KeyCode> current) {
        KeyCode previous = current(action);
        foreach (GameAction other in All) {
            if (other != action && key != KeyCode.None && current(other) == key) {
                PlayerPrefs.SetInt(prefix + other, (int)previous);
            }
        }
        PlayerPrefs.SetInt(prefix + action, (int)key);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetAll() {
        foreach (GameAction action in All) {
            PlayerPrefs.DeleteKey(KeyboardPrefix + action);
            PlayerPrefs.DeleteKey(GamepadPrefix + action);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // ---- reading ----

    public static bool Get(GameAction action) {
        return Key(Keyboard(action)) || Key(Gamepad(action)) || Extra(action, Input.GetKey) || TouchControls.Get(action);
    }

    public static bool GetDown(GameAction action) {
        return KeyDown(Keyboard(action)) || KeyDown(Gamepad(action)) || Extra(action, Input.GetKeyDown) || TouchControls.GetDown(action) ||
               (action == GameAction.Interact && StickUpPressed());
    }

    public static bool GetUp(GameAction action) {
        return KeyUp(Keyboard(action)) || KeyUp(Gamepad(action)) || Extra(action, Input.GetKeyUp) || TouchControls.GetUp(action);
    }

    // fixed second keys that were there before the bindings existed
    private static bool Extra(GameAction action, Func<KeyCode, bool> read) {
        switch (action) {
            case GameAction.Run: return read(KeyCode.RightShift);
            case GameAction.Interact: return read(KeyCode.UpArrow);
            case GameAction.Pause: return read(KeyCode.P);
            default: return false;
        }
    }

    private static bool Key(KeyCode key) => key != KeyCode.None && Input.GetKey(key);
    private static bool KeyDown(KeyCode key) => key != KeyCode.None && Input.GetKeyDown(key);
    private static bool KeyUp(KeyCode key) => key != KeyCode.None && Input.GetKeyUp(key);

    // the stick (or d-pad) pushed up this frame, for doors
    private static int stickFrame = -1;
    private static bool stickUpArmed = true;
    private static bool stickUpNow;
    private const float StickUpRelease = 0.4f; // a stick shaking around the threshold must not knock twice

    private static bool StickUpPressed() {
        if (stickFrame != Time.frameCount) {
            stickFrame = Time.frameCount;
            float vertical = Input.GetAxisRaw("Vertical");
            stickUpNow = stickUpArmed && vertical > StickUpThreshold;
            if (stickUpNow) {
                stickUpArmed = false;
            } else if (vertical < StickUpRelease) {
                stickUpArmed = true;
            }
        }
        return stickUpNow;
    }

    // ---- names for the menus ----

    public static string ActionName(GameAction action) {
        switch (action) {
            case GameAction.Jump: return "Pular";
            case GameAction.Melee: return "Espada";
            case GameAction.Ranged: return "Atirar";
            case GameAction.Dash: return "Dash";
            case GameAction.Run: return "Correr";
            case GameAction.Grenade: return "Granada";
            case GameAction.Potion: return "Poção";
            case GameAction.Pistol: return "Pistola";
            case GameAction.Shotgun: return "Escopeta";
            case GameAction.Interact: return "Porta";
            case GameAction.Pause: return "Pausar";
            case GameAction.Net: return "Rede";
            default: return action.ToString();
        }
    }

    public static string KeyName(KeyCode key) {
        switch (key) {
            case KeyCode.None: return "-";
            case KeyCode.Mouse0: return "Mouse esq.";
            case KeyCode.Mouse1: return "Mouse dir.";
            case KeyCode.Mouse2: return "Mouse meio";
            case KeyCode.Space: return "Espaço";
            case KeyCode.LeftShift: return "Shift esq.";
            case KeyCode.RightShift: return "Shift dir.";
            case KeyCode.LeftControl: return "Ctrl esq.";
            case KeyCode.RightControl: return "Ctrl dir.";
            case KeyCode.Return: return "Enter";
            case KeyCode.UpArrow: return "Seta cima";
            case KeyCode.DownArrow: return "Seta baixo";
            case KeyCode.LeftArrow: return "Seta esq.";
            case KeyCode.RightArrow: return "Seta dir.";
            case KeyCode.JoystickButton0: return "A";
            case KeyCode.JoystickButton1: return "B";
            case KeyCode.JoystickButton2: return "X";
            case KeyCode.JoystickButton3: return "Y";
            case KeyCode.JoystickButton4: return "LB";
            case KeyCode.JoystickButton5: return "RB";
            case KeyCode.JoystickButton6: return "Back";
            case KeyCode.JoystickButton7: return "Start";
            case KeyCode.JoystickButton8: return "L3";
            case KeyCode.JoystickButton9: return "R3";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) {
            return ((int)(key - KeyCode.Alpha0)).ToString();
        }
        if (IsGamepadButton(key)) {
            return "Botão " + (key - KeyCode.JoystickButton0);
        }
        return key.ToString();
    }

    public static bool IsGamepadButton(KeyCode key) {
        return key >= KeyCode.JoystickButton0 && key <= KeyCode.JoystickButton19;
    }
}
