using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Another player's Freny as the host's enemies see it.
public class CoopBody {
    public ulong Owner;
    public Transform Transform;
    public Collider2D Collider;
    public bool Alive;
    public int Health;
}

// What the game's own classes need to know about a co-op room, so they don't depend on the arena's
// sync code. Outside a room Active is false and every game class behaves as it always did.
public static class CoopWorld {

    // an enemy leaves one Freny for the other only when that one is this much closer, so it doesn't
    // turn back and forth when the two stand close together
    private const float SwitchMargin = 1.5f;

    public const float LifeDropChance = 0.01f;   // per kill, like the rare power-ups; to tune after playing
    public static System.Func<bool> GainLife { get; set; }

    private static Sprite lifeIcon;
    public static Sprite LifeIcon {
        get {
            if (lifeIcon == null) {
                Sprite[] heads = Resources.LoadAll<Sprite>("FrenyHeads/berserker");
                lifeIcon = heads.Length > 0 ? heads[0] : null;
            }
            return lifeIcon;
        }
    }

    public static bool Active { get; set; }
    public static bool HostActive => Active && CoopNet.IsHost;
    public static bool IsClient => Active && !CoopNet.IsHost;

    public static int Lives { get; set; }                  // this player's, for the HUD
    public static int DifficultyFactor { get; set; } = 1;  // ArenaDirector: more enemies, sooner

    // on the host: the other players' bodies, and their life as they last reported it
    public static readonly List<CoopBody> Bodies = new List<CoopBody>();
    public static Collider2D LocalBody { get; set; }

    // which Freny this enemy goes after: one of the others, or null for the host's own
    public static CoopBody PickTarget(Player local, Vector3 from, CoopBody current) {
        bool localAlive = local != null && !local.IsDead;
        CoopBody best = null;
        float bestDistance = localAlive ? Vector2.Distance(from, local.transform.position) : float.MaxValue;
        if (localAlive && current == null) {
            bestDistance -= SwitchMargin;
        }
        foreach (CoopBody body in Bodies) {
            if (!body.Alive || body.Transform == null) {
                continue;
            }
            float distance = Vector2.Distance(from, body.Transform.position);
            if (body == current) {
                distance -= SwitchMargin;
            }
            if (distance < bestDistance) {
                best = body;
                bestDistance = distance;
            }
        }
        return best;
    }

    // an enemy's swing reached another Freny: only his owner can take the hit
    public static void HitPeer(ulong owner, int amount, Vector2 knockback, bool heavy, string source) {
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((byte)Mathf.Clamp(amount, 0, 255));
        writer.WriteValueSafe(knockback);
        writer.WriteValueSafe((byte)(heavy ? 1 : 0));
        writer.WriteValueSafe(new FixedString32Bytes(source ?? ""));
        CoopNet.SendTo(owner, "coop.hurt", writer, NetworkDelivery.ReliableSequenced);
    }

    // ---- what one side draws and the other has to draw too; the copies do no damage
    public enum Fx : byte { Shot, Grenade, Portal }

    public static void ShotFired(Vector3 from, Vector3 direction, float push) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Shot);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe((Vector2)direction);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void GrenadeThrown(Vector3 from, Vector2 velocity, float fuse) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Grenade);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe(velocity);
        writer.WriteValueSafe(fuse);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void PortalOpened(Vector3 position, float duration, Color color) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(40, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Portal);
        writer.WriteValueSafe((Vector2)position);
        writer.WriteValueSafe(duration);
        writer.WriteValueSafe(color);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // a Sorcerer cast on the host: each client flies its own copy, and each copy only hurts the Freny
    // of the side it flies on; a homing one only turns for the one it was aimed at
    public static void HexCast(Vector3 position, HexballEffect effect, CoopBody homingAt, bool homing) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((Vector2)position);
        writer.WriteValueSafe((Vector2)effect.Direction);
        writer.WriteValueSafe(effect.speedMultiplier);
        writer.WriteValueSafe((byte)Mathf.Clamp(effect.damage, 0, 255));
        writer.WriteValueSafe(effect.knockback);
        writer.WriteValueSafe(homing && homingAt != null ? effect.homingTurn : 0f);
        writer.WriteValueSafe(homingAt != null ? homingAt.Owner : 0UL);
        CoopNet.Send("coop.hex", writer, NetworkDelivery.ReliableSequenced);
    }
}
