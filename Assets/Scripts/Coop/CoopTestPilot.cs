using UnityEngine;

// Only for the test binaries: drives the client's Freny at the puppets, coins and props with his real
// sword and body, since the arena autopilot only knows real enemies. Proves the paths
// sword > puppet > host, touch > coin > host and sword > prop > host.
public class CoopTestPilot : MonoBehaviour, IPlayerInputSource {

    private const float SwordReach = 1.4f;
    private const float StepUp = 1f;
    private Player player;

    private void Awake() {
        player = GetComponent<Player>();
    }

    public PlayerInputFrame ReadInput() {
        PlayerInputFrame frame = default;
        if (CoopArgs.Has("-coopPassive")) {   // standing still: the enemies' hits get tested
            return frame;
        }
        Transform target = null;
        float best = float.MaxValue;
        bool swing = false;
        foreach (CoopPuppet puppet in FindObjectsByType<CoopPuppet>()) {
            Consider(puppet.transform, 0f, ref target, ref best, ref swing);
        }
        foreach (ArenaChest chest in ArenaChest.Active) {
            Consider(chest.transform, 0f, ref target, ref best, ref swing);
        }
        foreach (ArenaVase vase in ArenaVase.Active) {
            Consider(vase.transform, 0f, ref target, ref best, ref swing);
        }
        foreach (Breakable prop in Breakable.Active) {
            if (!prop.Broken) {
                Consider(prop.transform, 0f, ref target, ref best, ref swing);
            }
        }
        foreach (Coin coin in Coin.Active) {
            float distance = Vector2.Distance(coin.transform.position, transform.position);
            if (distance < best) {
                best = distance;
                target = coin.transform;
            }
        }
        if (target == null) {
            return frame;
        }
        Vector2 delta = target.position - transform.position;
        frame.move = Mathf.Abs(delta.x) > 0.3f ? Mathf.Sign(delta.x) : 0f;
        frame.jumpPressed = delta.y > StepUp && player.IsGrounded;
        frame.meleePressed = swing && player.AttackReady;
        return frame;
    }

    private void Consider(Transform thing, float bias, ref Transform target, ref float best, ref bool swing) {
        float distance = Vector2.Distance(thing.position, transform.position) + bias;
        swing |= distance <= SwordReach;
        if (distance < best) {
            best = distance;
            target = thing;
        }
    }
}
