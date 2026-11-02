using System.Collections.Generic;
using UnityEngine;

// Treasure chest dropped into the arena by the director. It has to be broken: a sword swing or a
// bullet makes it burst into one reward: a handful of coins (often with a diamond), grenades, or a shotgun (shells when
// he already has one), so the weapons get used without buying them first.
public class ArenaChest : MonoBehaviour, IDamageable {

    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinCount = 5;
    [SerializeField] private int minCoins = 10;     // total value, split over the coins
    [SerializeField] private int maxCoins = 25;
    [SerializeField] private float coinsWeight = 30f;  // chances of each reward
    [SerializeField] private float grenadesWeight = 35f;
    [SerializeField] private float shotgunWeight = 35f;
    [SerializeField] private int grenadesInside = 2;
    [SerializeField] private int shotgunShells = 6;
    [SerializeField] private float diamondChance = 0.5f; // a coin chest also holds a diamond this often
    [SerializeField] private float scatter = 2.2f;  // sideways speed of the outermost coins (about 1.3 units of spread)
    [SerializeField] private float lifetime = 20f;  // disappears if nobody breaks it

    public int Health { get; set; } = 1;
    private bool broken;

    public static readonly List<ArenaChest> Active = new List<ArenaChest>();

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    private void Awake() {
        HitBody.MakeHittable(gameObject);
    }

    private void Start() {
        Destroy(gameObject, lifetime);
        // rest on the floor instead of hanging at the spawner's height
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        float halfHeight = sprite ? sprite.bounds.extents.y : 0.3f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f, Vector2.down, 6f, LayerMask.GetMask("Ground"));
        if (hit.collider != null) {
            transform.position = new Vector3(transform.position.x, hit.point.y + halfHeight, transform.position.z);
        }
    }

    // one hit is enough (MeleeAttack and RangedAttack find this through IDamageable)
    public void Damage() {
        if (broken) {
            return;
        }
        broken = true;
        Health = 0;
        StatsTracker.ChestOpened();

        Vector3 center = transform.position + Vector3.up * 0.2f;
        Fx.Burst(center, new Color(0.55f, 0.35f, 0.15f), 18, 3f, 0.1f, 0.6f);   // wood
        Fx.Burst(center, new Color(1f, 0.85f, 0.2f), 24, 3.5f, 0.1f, 0.8f);    // gold
        Sfx.Play("chest_break", center);

        // the weights follow what Freny is short of: no grenades when his pouch is full, the
        // shotgun more often while he has none (ItemPickup.Need, 1 when half short)
        Player player = FindAnyObjectByType<Player>();
        float grenades = grenadesWeight * NeedFactor(ItemPickup.Kind.Grenades, player);
        float shotgun = shotgunWeight * NeedFactor(player != null && player.ShotgunOwned ? ItemPickup.Kind.Shells : ItemPickup.Kind.Shotgun, player);
        float total = coinsWeight + grenades + shotgun;
        float roll = Random.Range(0f, total);
        Vector3 from = transform.position + Vector3.up * 0.3f;
        if (roll < grenades) {
            ItemPickup.Spawn(ItemPickup.Kind.Grenades, grenadesInside, from, new Vector2(Random.Range(-0.8f, 0.8f), 5.5f));
        } else if (roll < grenades + shotgun) {
            ItemPickup.Spawn(ItemPickup.Kind.Shotgun, shotgunShells, from, new Vector2(Random.Range(-0.8f, 0.8f), 5.5f));
        } else {
            ScatterCoins();
            if (Random.value < diamondChance * FrenyProgress.CoinShare) {
                ItemPickup.Spawn(ItemPickup.Kind.Diamond, 1, from, new Vector2(Random.Range(-0.5f, 0.5f), 6f));
            }
        }
        Destroy(gameObject);
    }

    // 0 when he can't use it, 1 when half short, 1.5 when out of it
    private static float NeedFactor(ItemPickup.Kind kind, Player player) {
        return 0.75f * ItemPickup.Need(kind, player);
    }

        // the coins fly out in a small fan and land back on this platform (Coin.Toss)
    private void ScatterCoins() {
        if (coinPrefab == null || coinCount <= 0) {
            return;
        }
        int total = Random.Range(minCoins, maxCoins + 1);
        int each = Mathf.Max(1, total / coinCount);
        // with the autopilot playing, a third of the coins come out (FrenyProgress.CoinShare)
        int count = Mathf.Max(1, Mathf.RoundToInt(coinCount * FrenyProgress.CoinShare));
        for (int i = 0; i < count; i++) {
            float side = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(count - 1));
            Vector2 velocity = new Vector2(side * scatter * Random.Range(0.75f, 1f), Random.Range(4.5f, 6f));
            GameObject dropped = Instantiate(coinPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
            Coin coin = dropped.GetComponent<Coin>();
            if (coin) {
                coin.coins = each;
                coin.Toss(velocity);
            }
        }
    }
}
