using UnityEngine;

// Picks who controls Freny this frame: the person or an autopilot (any IPlayerInputSource
// component on the player, e.g. ArenaAutopilot). Any human input takes over immediately;
// after takeoverDelay seconds without input the autopilot comes back, if Auto is on.
// Runs before Player so Player always reads this frame's input.
[DefaultExecutionOrder(-50)]
public class FrenyInput : MonoBehaviour {

    [SerializeField] private float takeoverDelay = 3f;
    [SerializeField] private bool autoEnabled = true;
    [SerializeField] private KeyCode toggleAutoKey = KeyCode.T;

    private readonly KeyboardInputSource human = new KeyboardInputSource();
    private IPlayerInputSource autopilot;
    private float lastHumanInputTime = float.NegativeInfinity;

    public PlayerInputFrame Current { get; private set; }
    public bool IsAutopilotDriving { get; private set; }
    public float RawHumanMove => human.RawMove; // for the HUD: shows a drifting stick
    public bool AutoEnabled => autoEnabled;
    public bool HasAutopilot => autopilot != null;

    // the Freny on screen, for loot and XP that pay less while the autopilot plays (FrenyProgress)
    public static FrenyInput Active { get; private set; }
    public static bool AutopilotDrivingNow => Active != null && Active.IsAutopilotDriving;

    private void OnEnable() {
        Active = this;
    }

    private void OnDisable() {
        if (Active == this) {
            Active = null;
        }
    }

    // seconds until the autopilot takes back control, 0 when it is already driving
    public float SecondsUntilAutopilot =>
        Mathf.Max(0f, lastHumanInputTime + takeoverDelay - Time.time);

    private void Awake() {
        FindAutopilot();
    }

    public void FindAutopilot() {
        autopilot = null;
        foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>()) {
            if (behaviour is IPlayerInputSource source && behaviour.enabled) {
                autopilot = source;
                break;
            }
        }
    }

    public void SetAutoEnabled(bool enabled) {
        autoEnabled = enabled;
    }

    public void ToggleAuto() {
        autoEnabled = !autoEnabled;
    }

    private void Update() {
        if (Input.GetKeyDown(toggleAutoKey)) {
            ToggleAuto();
        }

        PlayerInputFrame humanFrame = human.ReadInput();
        if (!humanFrame.IsEmpty) {
            lastHumanInputTime = Time.time;
        }

        bool autopilotReady = autoEnabled && autopilot != null && SecondsUntilAutopilot <= 0f;
        IsAutopilotDriving = autopilotReady;
        Current = autopilotReady ? autopilot.ReadInput() : humanFrame;
    }
}
