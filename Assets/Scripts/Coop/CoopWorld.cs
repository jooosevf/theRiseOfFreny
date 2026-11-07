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
    // José, 2026-11-06: picking a blessing doesn't pause a room; until then the enemies leave this
    // player alone (ArenaRunVariety sets it, Time.time)
    public static float ShieldedUntil { get; set; }
    public static bool LocalShielded => Active && Time.time < ShieldedUntil;
    public static int DifficultyFactor { get; set; } = 1;  // ArenaDirector: more enemies, sooner

    // on the host: the other players' bodies, and their life as they last reported it
    public static readonly List<CoopBody> Bodies = new List<CoopBody>();
    public static Collider2D LocalBody { get; set; }

    // which Freny this enemy goes after: one of the others, or null for the host's own
    public static CoopBody PickTarget(Player local, Vector3 from, CoopBody current) {
        bool localAlive = local != null && !local.IsDead && !LocalShielded;
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
    // Wave, Flood, Beam and Mine are the host's attacks (the Ciclope's, the red Golem's mine): each
    // client runs its own copy, which only hurts the Freny of the side it runs on
    public enum Fx : byte { Shot, Grenade, Portal, Slash, Wave, Flood, Beam, Mine, BeamCharge, Spell, Spider, Net, NetOpen, Kill }

    // this player threw the Rede: the others draw it
    public static void NetThrown(Vector3 from, Vector2 velocity, float facing, float feetY) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Net);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe(velocity);
        writer.WriteValueSafe(facing);
        writer.WriteValueSafe(feetY);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // the client's net opened there: the host catches its real enemies
    public static void NetOpened(Vector3 at) {
        if (!IsClient) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.NetOpen);
        writer.WriteValueSafe((Vector2)at);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void SpiderBlast(Vector3 at, int damage) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(24, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Spider);
        writer.WriteValueSafe((Vector2)at);
        writer.WriteValueSafe((byte)Mathf.Clamp(damage, 0, 255));
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // on the client: Ciclopes the host has out, so the torches and the music change here too
    public static int BossesShown { get; set; }

    // on the host, while a client's hit is applied: a kill then is his, not the host's (Fúria, Vampiro)
    public static bool RemoteHit { get; set; }

    public static bool IsPuppet(object thing) {
        return thing is CoopPuppet || thing is CoopPuppetPart;
    }

    // a sword hit on a puppet with its real damage; false when it isn't one
    public static bool DamagePuppet(object thing, int damage) {
        CoopPuppet puppet = thing is CoopPuppet p ? p : thing is CoopPuppetPart part ? part.Owner : null;
        if (puppet == null) {
            return false;
        }
        puppet.DamageBy(damage);
        return true;
    }

    // this player cast a ball with the Cajado do Mago: the others draw it
    public static void SpellCast(Vector3 from, Vector3 direction) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Spell);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe((Vector2)direction);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // the client: something of his (a staff ball) touching a puppet there; damage 0 only tells
    // whether one is there. True when one was
    public static bool HitPuppetAt(Vector2 point, float radius, int damage) {
        foreach (Collider2D col in Physics2D.OverlapCircleAll(point, radius)) {
            CoopPuppet puppet = col.GetComponentInParent<CoopPuppet>();
            if (puppet != null) {
                if (damage > 0) {
                    puppet.DamageBy(damage);
                }
                return true;
            }
        }
        return false;
    }

    public static void BossWave(Vector3 feet, float dir) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Wave);
        writer.WriteValueSafe((Vector2)feet);
        writer.WriteValueSafe(dir);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void BossFlood(float fromX, float floorY, float xMin, float xMax, float seconds) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Flood);
        writer.WriteValueSafe(new Vector2(fromX, floorY));
        writer.WriteValueSafe(xMin);
        writer.WriteValueSafe(xMax);
        writer.WriteValueSafe(seconds);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // the eye starts to glow: the others see the warning too, not only the beam
    public static void BossBeamCharge(Vector3 eye, float seconds) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.BeamCharge);
        writer.WriteValueSafe((Vector2)eye);
        writer.WriteValueSafe(seconds);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void BossBeam(Vector3 eye, Vector3 at, float seconds) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Beam);
        writer.WriteValueSafe((Vector2)eye);
        writer.WriteValueSafe((Vector2)at);
        writer.WriteValueSafe(seconds);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void MineThrown(Vector3 from, Vector2 velocity, float gravity, float size, float fuse) {
        if (!HostActive) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(40, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Mine);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe(velocity);
        writer.WriteValueSafe(gravity);
        writer.WriteValueSafe(size);
        writer.WriteValueSafe(fuse);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    // this player swung the sword: the others draw it on his Freny
    public static void Slashed(int step) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Slash);
        writer.WriteValueSafe(Vector2.zero);
        writer.WriteValueSafe(CoopNet.LocalId);
        writer.WriteValueSafe((byte)step);
        CoopNet.Send("coop.fx", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void ShotFired(Vector3 from, Vector3 direction, bool shotgun) {
        if (!Active) {
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)Fx.Shot);
        writer.WriteValueSafe((Vector2)from);
        writer.WriteValueSafe((Vector2)direction);
        writer.WriteValueSafe((byte)(shotgun ? 1 : 0));
        writer.WriteValueSafe(CoopNet.LocalId);   // whose copy shows the shot (the shotgun poses)
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
