using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// The room's shared things: crates, vases and chests, and what lies on the floor. The host owns them.
// The client draws the same objects with their own code (arc, bobbing, breaking frames); when its
// Freny touches or hits one it asks, and the first request to reach the host wins. Whoever picks up
// a coin or an item keeps it, and it is gone for the other. A power-up works for both.
public static class CoopShared {

    private enum Op : byte { Item, Coin, Vase, Chest, Broken, Regrown, Gone, Granted, PowerUp }
    private enum Ask : byte { Break, Pick }

    private const ushort FirstDynamicProp = 10000;

    private static readonly Dictionary<ushort, Component> props = new Dictionary<ushort, Component>();
    private static readonly Dictionary<Component, ushort> propIds = new Dictionary<Component, ushort>();
    private static readonly Dictionary<ushort, Component> pickups = new Dictionary<ushort, Component>();
    private static readonly Dictionary<Component, ushort> pickupIds = new Dictionary<Component, ushort>();
    private static readonly HashSet<ushort> asked = new HashSet<ushort>();
    private static ushort nextProp = FirstDynamicProp;
    private static ushort nextPickup = 1;
    private static bool applying;   // the client is carrying out what the host decided
    private static GameObject chestPrefab;
    private static GameObject coinPrefab;

    public static int CoinsGranted { get; private set; }
    public static int ItemsGranted { get; private set; }
    public static int PropsBrokenByAsk { get; private set; }

    public static string Counts() {
        int standing = 0;
        foreach (Breakable prop in Breakable.Active) {
            if (prop != null && !prop.Broken) {
                standing++;
            }
        }
        return $"moedas {Coin.Active.Count}, itens {ItemPickup.Active.Count}, cenário {standing}, baús {ArenaChest.Active.Count}, vasos {ArenaVase.Active.Count}";
    }

    // a new run: the scene's props get the same numbers on both sides, by where they stand
    public static void Begin(GameObject chest) {
        props.Clear();
        propIds.Clear();
        pickups.Clear();
        pickupIds.Clear();
        asked.Clear();
        nextProp = FirstDynamicProp;
        nextPickup = 1;
        chestPrefab = chest;
        ArenaChest chestScript = chest != null ? chest.GetComponent<ArenaChest>() : null;
        coinPrefab = chestScript != null ? chestScript.CoinPrefab : null;

        List<Breakable> scene = new List<Breakable>(Breakable.Active);
        scene.Sort((a, b) => {
            int byX = a.transform.position.x.CompareTo(b.transform.position.x);
            return byX != 0 ? byX : a.transform.position.y.CompareTo(b.transform.position.y);
        });
        ushort id = 1;
        foreach (Breakable prop in scene) {
            RegisterProp(id++, prop);
        }
    }

    private static void RegisterProp(ushort id, Component prop) {
        props[id] = prop;
        propIds[prop] = id;
    }

    private static void RegisterPickup(ushort id, Component pickup) {
        pickups[id] = pickup;
        pickupIds[pickup] = id;
    }

    private static FastBufferWriter Writer(Op op) {
        FastBufferWriter writer = new FastBufferWriter(48, Allocator.Temp);
        writer.WriteValueSafe((byte)op);
        return writer;
    }

    private static void SendAsk(Ask ask, ushort id) {
        using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe((byte)ask);
        writer.WriteValueSafe(id);
        CoopNet.Send("coop.a", writer, NetworkDelivery.ReliableSequenced);
    }

    // ---- what the game's classes call. Outside a room each of these does nothing and returns false.

    // true: this side doesn't break it now (the client asked the host)
    public static bool AskToBreak(Component prop) {
        if (!CoopWorld.Active || !propIds.TryGetValue(prop, out ushort id)) {
            return false;
        }
        if (CoopWorld.IsClient) {
            if (applying) {
                return false;
            }
            SendAsk(Ask.Break, id);
            return true;
        }
        using FastBufferWriter writer = Writer(Op.Broken);
        writer.WriteValueSafe(id);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
        return false;
    }

    public static void PropRegrew(Component prop) {
        if (CoopWorld.HostActive && propIds.TryGetValue(prop, out ushort id)) {
            using FastBufferWriter writer = Writer(Op.Regrown);
            writer.WriteValueSafe(id);
            CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
        }
    }

    public static void VaseSpawned(ArenaVase vase, Vector3 floorPoint, int tile) {
        if (!CoopWorld.HostActive) {
            return;
        }
        ushort id = nextProp++;
        RegisterProp(id, vase);
        using FastBufferWriter writer = Writer(Op.Vase);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe((Vector2)floorPoint);
        writer.WriteValueSafe((byte)tile);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void ChestSpawned(ArenaChest chest) {
        if (!CoopWorld.HostActive) {
            return;
        }
        ushort id = nextProp++;
        RegisterProp(id, chest);
        using FastBufferWriter writer = Writer(Op.Chest);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe((Vector2)chest.transform.position);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void ItemSpawned(ItemPickup item, Vector3 position, Vector2 toss) {
        if (!CoopWorld.HostActive) {
            return;
        }
        ushort id = nextPickup++;
        RegisterPickup(id, item);
        using FastBufferWriter writer = Writer(Op.Item);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe((byte)item.kind);
        writer.WriteValueSafe((byte)Mathf.Clamp(item.amount, 0, 255));
        writer.WriteValueSafe((Vector2)position);
        writer.WriteValueSafe(toss);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
    }

    public static void CoinSpawned(Coin coin, Vector2 toss) {
        if (!CoopWorld.HostActive) {
            return;
        }
        ushort id = nextPickup++;
        RegisterPickup(id, coin);
        using FastBufferWriter writer = Writer(Op.Coin);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe((ushort)Mathf.Clamp(coin.coins, 0, ushort.MaxValue));
        writer.WriteValueSafe((Vector2)coin.transform.position);
        writer.WriteValueSafe(toss);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
    }

    // true: the client's Freny touched it and asked; it only takes it if the host says so
    public static bool AskToPick(Component pickup) {
        if (!CoopWorld.IsClient || applying || !pickupIds.TryGetValue(pickup, out ushort id)) {
            return false;
        }
        if (asked.Add(id)) {
            SendAsk(Ask.Pick, id);
        }
        return true;
    }

    // the host's Freny took it: it goes on the other side too
    public static void Picked(Component pickup) {
        if (CoopWorld.HostActive && pickupIds.TryGetValue(pickup, out ushort id)) {
            using FastBufferWriter writer = Writer(Op.Gone);
            writer.WriteValueSafe(id);
            CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
        }
    }

    public static void PowerUpTaken(ItemPickup.Kind kind) {
        if (CoopWorld.HostActive) {
            using FastBufferWriter writer = Writer(Op.PowerUp);
            writer.WriteValueSafe((byte)kind);
            CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
        }
    }

    // ---- the host hears the client's requests
    public static void OnAsk(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out byte ask);
        reader.ReadValueSafe(out ushort id);
        if ((Ask)ask == Ask.Break) {
            if (props.TryGetValue(id, out Component prop) && prop != null && prop is IDamageable target) {
                PropsBrokenByAsk++;
                target.Damage();   // AskToBreak tells the client
            }
            return;
        }
        if (!pickups.TryGetValue(id, out Component pickup) || pickup == null) {
            return;   // someone got there first
        }
        pickups.Remove(id);
        pickupIds.Remove(pickup);
        if (pickup is ItemPickup item && ArenaPowerUps.IsPowerUp(item.kind)) {
            ArenaPowerUps.Take(item.kind);   // on both sides
        }
        Object.Destroy(pickup.gameObject);
        using FastBufferWriter writer = Writer(Op.Granted);
        writer.WriteValueSafe(id);
        CoopNet.Send("coop.w", writer, NetworkDelivery.ReliableSequenced);
    }

    // ---- the client carries out what the host decided
    public static void OnWorld(ulong sender, FastBufferReader reader, Player player) {
        reader.ReadValueSafe(out byte raw);
        switch ((Op)raw) {
            case Op.Item: {
                reader.ReadValueSafe(out ushort id);
                reader.ReadValueSafe(out byte kind);
                reader.ReadValueSafe(out byte amount);
                reader.ReadValueSafe(out Vector2 position);
                reader.ReadValueSafe(out Vector2 toss);
                RegisterPickup(id, ItemPickup.Spawn((ItemPickup.Kind)kind, amount, position, toss));
                break;
            }
            case Op.Coin: {
                reader.ReadValueSafe(out ushort id);
                reader.ReadValueSafe(out ushort value);
                reader.ReadValueSafe(out Vector2 position);
                reader.ReadValueSafe(out Vector2 toss);
                if (coinPrefab != null) {
                    Coin coin = Object.Instantiate(coinPrefab, position, Quaternion.identity).GetComponent<Coin>();
                    if (coin != null) {
                        coin.coins = value;
                        coin.Toss(toss);
                        RegisterPickup(id, coin);
                    }
                }
                break;
            }
            case Op.Vase: {
                reader.ReadValueSafe(out ushort id);
                reader.ReadValueSafe(out Vector2 floorPoint);
                reader.ReadValueSafe(out byte tile);
                ArenaVase vase = ArenaVase.Create(floorPoint, tile);
                if (vase != null) {
                    RegisterProp(id, vase);
                }
                break;
            }
            case Op.Chest: {
                reader.ReadValueSafe(out ushort id);
                reader.ReadValueSafe(out Vector2 position);
                if (chestPrefab != null) {
                    RegisterProp(id, Object.Instantiate(chestPrefab, position, Quaternion.identity).GetComponent<ArenaChest>());
                }
                break;
            }
            case Op.Broken: {
                reader.ReadValueSafe(out ushort id);
                if (props.TryGetValue(id, out Component prop) && prop != null && prop is IDamageable target) {
                    applying = true;
                    target.Damage();
                    applying = false;
                }
                break;
            }
            case Op.Regrown: {
                reader.ReadValueSafe(out ushort id);
                if (props.TryGetValue(id, out Component prop) && prop is Breakable breakable && breakable != null) {
                    breakable.Regrow();
                }
                break;
            }
            case Op.Gone: {
                reader.ReadValueSafe(out ushort id);
                if (pickups.TryGetValue(id, out Component pickup) && pickup != null) {
                    Object.Destroy(pickup.gameObject);
                }
                break;
            }
            case Op.Granted: {
                reader.ReadValueSafe(out ushort id);
                if (pickups.TryGetValue(id, out Component pickup) && pickup != null) {
                    applying = true;
                    Grant(pickup, player);
                    applying = false;
                }
                break;
            }
            case Op.PowerUp: {
                reader.ReadValueSafe(out byte kind);
                ArenaPowerUps.ShowFromHost((ItemPickup.Kind)kind);
                break;
            }
        }
    }

    private static void Grant(Component pickup, Player player) {
        if (pickup is Coin coin) {
            if (player != null) {
                player.AddCoins(RunModifiers.ScaleCoins(coin.coins));
                CoinsGranted++;
            }
            Sfx.Play("coin", coin.transform.position);
            Object.Destroy(coin.gameObject);
        } else if (pickup is ItemPickup item) {
            item.GrantTo(player);
            ItemsGranted++;
        }
    }
}
