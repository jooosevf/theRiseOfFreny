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
    public static int Lives { get; set; }
    public static int DifficultyFactor { get; set; } = 1;
    public static Collider2D LocalBody { get; set; }
    public static CoopBody PickTarget(Player local, Vector3 from, CoopBody current) => null;
    public static void HitPeer(ulong owner, int amount, Vector2 knockback, bool heavy, string source) { }
    public static void ShotFired(Vector3 from, Vector3 direction, bool shotgun) { }
    public static void Slashed(int step) { }
    public static void GrenadeThrown(Vector3 from, Vector2 velocity, float fuse) { }
    public static void PortalOpened(Vector3 position, float duration, Color color) { }
    public static void HexCast(Vector3 position, HexballEffect effect, CoopBody homingAt, bool homing) { }
}

public class CoopBody {
    public ulong Owner;
    public Transform Transform;
    public Collider2D Collider;
    public bool Alive;
    public int Health;
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
    public static bool InRoom => false;
}

public static class CoopScreens {
    public static MenuPage Root(MenuStack stack) => null;
    public static void AfterMenu(MenuStack stack) { }
}

public class CoopPuppet : MonoBehaviour {
    public void DamageBy(int amount) { }
}
