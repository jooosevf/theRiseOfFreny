using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// What the game's own classes need to know about a co-op room, so they don't depend on the arena's
// sync code. Outside a room Active is false and every game class behaves as it always did.
public static class CoopWorld {

    // an enemy leaves one Freny for the other only when that one is this much closer, so it doesn't
    // turn back and forth when the two stand close together
    private const float SwitchMargin = 1.5f;

    public static bool Active { get; set; }
    public static bool HostActive => Active && CoopNet.IsHost;
    public static bool IsClient => Active && !CoopNet.IsHost;

    // on the host: the other player's body, and his life as he last reported it
    public static Transform PeerTransform { get; set; }
    public static Collider2D PeerBody { get; set; }
    public static bool PeerAlive { get; set; }
    public static int PeerHealth { get; set; }
    public static Collider2D LocalBody { get; set; }

    // which Freny this enemy goes after; true when it is the other player
    public static bool AimAtPeer(Player local, Vector3 from, bool current) {
        bool localAlive = local != null && !local.IsDead;
        if (!PeerAlive || PeerTransform == null) {
            return false;
        }
        if (!localAlive) {
            return true;
        }
        float toLocal = Vector2.Distance(from, local.transform.position);
        float toPeer = Vector2.Distance(from, PeerTransform.position);
        return current ? toPeer <= toLocal + SwitchMargin : toPeer + SwitchMargin < toLocal;
    }

    // an enemy's swing reached the other Freny: only his owner can take the hit
    public static void HitPeer(int amount, Vector2 knockback, bool heavy, string source) {
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((byte)Mathf.Clamp(amount, 0, 255));
        writer.WriteValueSafe(knockback);
        writer.WriteValueSafe((byte)(heavy ? 1 : 0));
        writer.WriteValueSafe(new FixedString32Bytes(source ?? ""));
        CoopNet.Send("coop.hurt", writer, NetworkDelivery.ReliableSequenced);
    }

    // a Sorcerer cast on the host: the client flies its own copy, and each copy only hurts the Freny
    // of the side it flies on
    public static void HexCast(Vector3 position, HexballEffect effect, bool homing) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((Vector2)position);
        writer.WriteValueSafe((Vector2)effect.Direction);
        writer.WriteValueSafe(effect.speedMultiplier);
        writer.WriteValueSafe((byte)Mathf.Clamp(effect.damage, 0, 255));
        writer.WriteValueSafe(effect.knockback);
        writer.WriteValueSafe(homing ? effect.homingTurn : 0f);
        CoopNet.Send("coop.hex", writer, NetworkDelivery.ReliableSequenced);
    }
}
