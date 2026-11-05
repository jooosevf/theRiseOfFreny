// CI only: this check compiles without the Unity packages, so Assets/Scripts/Coop (Netcode, Relay,
// Lobby) stays out of it. These are the parts of Coop the rest of the game calls; a signature change
// there needs the same change here. The real code is compiled by Unity.
using UnityEngine;

public static class CoopWorld {
    public const float LifeDropChance = 0f;
    public static System.Func<bool> GainLife { get; set; }
    public static Sprite LifeIcon => null;
    public static bool Active { get; set; }
    public static bool HostActive => false;
    public static bool IsClient => false;
    public static Transform PeerTransform { get; set; }
    public static Collider2D PeerBody { get; set; }
    public static bool PeerAlive { get; set; }
    public static int PeerHealth { get; set; }
    public static Collider2D LocalBody { get; set; }
    public static bool AimAtPeer(Player local, Vector3 from, bool current) => false;
    public static void HitPeer(int amount, Vector2 knockback, bool heavy, string source) { }
    public static void ShotFired(Vector3 from, Vector3 direction, float push) { }
    public static void GrenadeThrown(Vector3 from, Vector2 velocity, float fuse) { }
    public static void PortalOpened(Vector3 position, float duration, Color color) { }
    public static void HexCast(Vector3 position, HexballEffect effect, bool homing) { }
}

public static class CoopShared {
    public static bool AskToBreak(Component prop) => false;
    public static void PropRegrew(Component prop) { }
    public static void VaseSpawned(ArenaVase vase, Vector3 floorPoint, int tile) { }
    public static void ChestSpawned(ArenaChest chest) { }
    public static void ItemSpawned(ItemPickup item, Vector3 position, Vector2 toss) { }
    public static void CoinSpawned(Coin coin, Vector2 toss) { }
    public static bool AskToPick(Component pickup) => false;
    public static void Picked(Component pickup) { }
    public static void PowerUpTaken(ItemPickup.Kind kind) { }
}

public static class CoopNet {
    public const string LobbyScene = "Coop Lobby";
    public static bool InRoom => false;
}

public class CoopPuppet : MonoBehaviour {
    public void DamageBy(int amount) { }
}
