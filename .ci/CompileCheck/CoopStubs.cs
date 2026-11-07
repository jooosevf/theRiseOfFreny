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
    public static void HitPeer(ulong owner, int amount, Vector2 knockback, bool heavy, string source, Enemy from = null) { }
    public static void ShotFired(Vector3 from, Vector3 direction, bool shotgun) { }
    public static void Slashed(int step) { }
    public static void GrenadeThrown(Vector3 from, Vector2 velocity, float fuse) { }
    public static void PortalOpened(Vector3 position, float duration, Color color) { }
    public static void HexCast(Vector3 position, HexballEffect effect, CoopBody homingAt, bool homing) { }
    public static int BossesShown { get; set; }
    public static bool RemoteHit { get; set; }
    public static bool IsPuppet(object thing) => false;
    public static bool DamagePuppet(object thing, int damage) => false;
    public static void SpiderBlast(Vector3 at, int damage) { }
    public static void NetThrown(Vector3 from, Vector2 velocity, float facing, float feetY) { }
    public static void NetOpened(Vector3 at) { }
    public static void SpellCast(Vector3 from, Vector3 direction) { }
    public static bool HitPuppetAt(Vector2 point, float radius, int damage) => false;
    public static float ShieldedUntil { get; set; }
    public static bool LocalShielded => false;
    public static void BossWave(Vector3 feet, float dir) { }
    public static void BossFlood(float fromX, float floorY, float xMin, float xMax, float seconds) { }
    public static void BossBeamCharge(Vector3 eye, float seconds) { }
    public static void BossBeam(Vector3 eye, Vector3 at, float seconds) { }
    public static void MineThrown(Vector3 from, Vector2 velocity, float gravity, float size, float fuse) { }
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
    public static void KeySpawned(BossKey key, Vector3 position, Vector2 toss) { }
    public static void GoldenChestSpawned(GoldenChest chest, Vector3 floor) { }
    public static bool AskToOpen(GoldenChest chest) => false;
    public static void GoldenChestOpened(GoldenChest chest) { }
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
