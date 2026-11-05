using UnityEngine;

// One frame of player intent. Player.cs only reads this, so a person and the arena
// autopilot drive Freny through exactly the same actions, physics and cooldowns.
public struct PlayerInputFrame {
    public float move;          // -1 left .. 1 right
    public bool jumpPressed;    // this frame only
    public bool jumpHeld;       // not used by the fixed-height jump; kept for a variable jump later
    public bool meleePressed;
    public bool rangedPressed;
    public bool rangedHeld;      // the Flagelante holds his shield up with the same button
    public bool dashPressed;
    public bool runPressed;      // the sprint's button went down: a dash too in the air (Dash)
    public bool runHeld;         // sprint while held (short, with a cooldown)
    public bool crouchHeld;      // down: crouch while on the ground
    public bool aimUpHeld;       // up: the gun fires straight up
    public bool selectPistol;    // weapon keys
    public bool selectShotgun;
    public bool potionPressed;
    public bool grenadePressed;  // a throw now, with the whole fuse (the autopilot)
    public bool grenadeHeld;     // held: the fuse burns in his hand; let go, it is thrown
    public float grenadeCharge;  // 0..1 power of the throw; the keyboard always sends Player.GrenadeTapCharge (-1), the fixed lob
    public bool furyPressed;     // a full Fúria meter starts the Fúria
    public bool netPressed;      // the Rede (NetThrower): thrown at once
    public float netCharge;      // 0..1 how far; the keyboard throws at NetThrower.TapCharge

    public bool IsEmpty =>
        Mathf.Approximately(move, 0f) && !jumpPressed && !jumpHeld && !meleePressed &&
        !rangedPressed && !rangedHeld && !dashPressed && !potionPressed && !grenadePressed && !grenadeHeld && !furyPressed && !runHeld && !crouchHeld && !aimUpHeld && !selectPistol && !selectShotgun && !netPressed;
}

public interface IPlayerInputSource {
    PlayerInputFrame ReadInput();
}
