using UnityEngine;

// Only for the test binaries: drives the client's Freny at the puppets with his real sword, since
// the arena autopilot only knows real enemies. Proves the path sword > puppet > message > host.
public class CoopTestPilot : MonoBehaviour, IPlayerInputSource {

    private const float SwordReach = 1.4f;
    private const float StepUp = 1f;
    private Player player;

    private void Awake() {
        player = GetComponent<Player>();
    }

    public PlayerInputFrame ReadInput() {
        CoopPuppet nearest = null;
        float best = float.MaxValue;
        foreach (CoopPuppet puppet in FindObjectsByType<CoopPuppet>()) {
            float distance = Vector2.Distance(puppet.transform.position, transform.position);
            if (distance < best) {
                best = distance;
                nearest = puppet;
            }
        }
        PlayerInputFrame frame = default;
        if (nearest == null) {
            return frame;
        }
        Vector2 delta = nearest.transform.position - transform.position;
        frame.move = Mathf.Abs(delta.x) > SwordReach * 0.5f ? Mathf.Sign(delta.x) : 0f;
        frame.jumpPressed = delta.y > StepUp && player.IsGrounded;
        frame.meleePressed = best <= SwordReach && player.AttackReady;
        return frame;
    }
}
