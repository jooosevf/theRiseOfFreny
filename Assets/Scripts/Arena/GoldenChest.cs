using System.Collections.Generic;
using UnityEngine;

// The golden chest (José, 2026-11-01): the Ciclope's treasure, which only its key opens. It comes
// through a gold portal on one of the highest platforms when a Ciclope drops its key
// (ArenaBossLoot), out of the flood and out of the way, and stays until it is opened (no timer:
// the key never fades either). Touching it or hitting it with the key opens it; without one it
// says so and stays shut. The art is the normal chest recoloured gold (Tools/ArtGen/boss_loot.py),
// with the same four opening frames.
//
// Inside, the Arena's best haul, all of it at once and none of it points (the API counts only
// kills, so a reward in points would make honest runs fail its check):
// - CoinCount coins worth CoinsInside in all, plus DiamondsInside diamonds (about 145 coins,
//   one Mercador item; a normal chest gives 10-25);
// - life and armor refilled to the top;
// - the Fúria right away (20 s of harder sword hits), the temporary super item;
// - the revolver reserve, the shells (with the shotgun) and the grenades filled up;
// - the great sword's big swings, when he can use them (not when he already owns it);
// - FrenyLook.ChestLookChance of a look found nowhere else (FrenyLook.Unlock.GoldenChest).
public class GoldenChest : MonoBehaviour, IDamageable {

    private const string SheetPath = "BossLoot/Golden Chest";
    private const float Scale = 1.15f;            // a little bigger than the normal chest
    private const int CoinCount = 8;
    private const int CoinsInside = 96;           // split over the coins
    private const int DiamondsInside = 2;
    private const float FrameTime = 0.1f;
    private const float StayOpen = 2.5f;          // open on the platform, then it fades
    private const float FadeFor = 0.8f;
    private const float LockedGap = 1.5f;         // between two "needs the key" messages
    private static readonly Color HaloColor = new Color(1f, 0.85f, 0.35f, 0.5f);

    public static readonly List<GoldenChest> Active = new List<GoldenChest>();

    public int Health { get; set; } = 1;
    public bool Opened { get; private set; }

    private static Sprite[] frames;
    private SpriteRenderer sprite;
    private SpriteRenderer halo;
    private GameObject coinPrefab;
    private float openedAt;
    private float nextLocked;
    private float nextSparkle;

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    public static Sprite[] Frames {
        get {
            if (frames == null || frames.Length == 0) {
                Sprite[] loaded = Resources.LoadAll<Sprite>(SheetPath);
                System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                frames = loaded;
            }
            return frames;
        }
    }

    public static Sprite Icon => Frames.Length > 0 ? Frames[0] : null;

    // `floor` is a point on the platform it stands on; coinPrefab is the coin the enemies drop (the
    // Ciclope's own), with none the coins go straight to Freny
    public static GoldenChest Spawn(Vector3 floor, GameObject coinPrefab) {
        GameObject obj = new GameObject("Golden Chest");
        obj.transform.localScale = Vector3.one * Scale;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = Icon;
        renderer.sortingOrder = 30;
        if (ItemPickup.UnlitMaterial != null) {
            renderer.sharedMaterial = ItemPickup.UnlitMaterial;   // bright in the dark corners too
        }
        // the cell's bottom edge is the art's bottom: stand it on the floor
        float halfHeight = renderer.sprite != null ? renderer.sprite.bounds.extents.y * Scale : 0.5f;
        obj.transform.position = new Vector3(floor.x, floor.y + halfHeight, 0f);

        GameObject haloObj = new GameObject("Halo");
        haloObj.transform.SetParent(obj.transform, false);
        haloObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        SpriteRenderer haloRenderer = haloObj.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = ArenaBossLoot.HaloSprite();
        haloRenderer.color = HaloColor;
        haloRenderer.sortingOrder = 29;
        if (ItemPickup.UnlitMaterial != null) {
            haloRenderer.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        haloObj.transform.localScale = Vector3.one * 1.5f;

        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.8f, 0.7f);
        box.offset = new Vector2(0f, -0.15f);
        HitBody.MakeHittable(obj);

        GoldenChest chest = obj.AddComponent<GoldenChest>();
        chest.sprite = renderer;
        chest.halo = haloRenderer;
        chest.coinPrefab = coinPrefab;
        return chest;
    }

    private void Update() {
        if (!Opened) {
            if (halo) {
                Color c = HaloColor;
                c.a *= 0.65f + 0.35f * Mathf.Sin(Time.time * 3f);
                halo.color = c;
            }
            if (Time.time >= nextSparkle && sprite) {
                // glints over the lid now and then, so it reads as treasure from across the arena
                nextSparkle = Time.time + Random.Range(0.25f, 0.5f);
                Bounds b = sprite.bounds;
                Vector3 at = new Vector3(Random.Range(b.min.x + 0.15f, b.max.x - 0.15f), Random.Range(b.min.y + 0.1f, b.center.y + 0.1f), 0f);
                Fx.Rise(at, Color.Lerp(BossKey.Gold, Color.white, Random.value), Random.Range(0.05f, 0.1f), Random.Range(0.4f, 0.8f));
            }
            return;
        }
        float age = Time.time - openedAt;
        Sprite[] all = Frames;
        if (sprite && all.Length > 0) {
            sprite.sprite = all[Mathf.Min(all.Length - 1, Mathf.FloorToInt(age / FrameTime))];
        }
        float fade = Mathf.Clamp01((age - StayOpen) / FadeFor);
        if (sprite) {
            sprite.color = new Color(1f, 1f, 1f, 1f - fade);
        }
        if (halo) {
            Color c = HaloColor;
            c.a *= (1f - fade) * (age < 0.6f ? 2f : 1f);
            halo.color = c;
        }
        if (fade >= 1f) {
            Destroy(gameObject);
        }
    }

    // a sword swing or a bullet (MeleeAttack, RangedAttack)
    public void Damage() {
        TryOpen(FindPlayer());
    }

    private void OnTriggerStay2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            TryOpen(other.GetComponent<Player>());
        }
    }

    private static Player FindPlayer() {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        return obj ? obj.GetComponent<Player>() : null;
    }

    private void TryOpen(Player player) {
        if (Opened || player == null || player.IsDead) {
            return;
        }
        if (BossKey.Carried <= 0) {
            if (Time.time >= nextLocked) {
                nextLocked = Time.time + LockedGap;
                Sfx.Play("door_locked", transform.position);
                ArenaBossLoot.Say("Precisa da chave do Ciclope", transform.position + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.5f), 1.6f);
            }
            return;
        }
        BossKey.Carried--;
        Open(player);
    }

    private void Open(Player player) {
        Opened = true;
        Health = 0;
        openedAt = Time.time;
        Vector3 center = transform.position;
        Fx.Burst(center, BossKey.Gold, 40, 5f, 0.12f, 1f);
        Fx.Burst(center, Color.white, 20, 3.5f, 0.08f, 0.7f);
        Fx.Burst(center, new Color(0.75f, 0.4f, 1f), 16, 3f, 0.1f, 0.8f);
        for (int i = 0; i < 24; i++) {
            Fx.Rise(center + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.5f), 0f),
                    Color.Lerp(BossKey.Gold, Color.white, Random.value * 0.6f), Random.Range(0.06f, 0.14f), Random.Range(0.8f, 1.6f));
        }
        Sfx.Play("door", center, 1f, 0.8f);
        Sfx.Play("chest_break", center, 1f, 0.8f);
        Sfx.Play("new_record", center, 0.8f, 1f);
        ArenaBossLoot.Say("Tesouro do Ciclope!", center + Vector3.up * 1f, BossKey.Gold, 2.5f);
        ScreenKick.Kick(0.15f, 0.2f);

        ScatterCoins();
        for (int i = 0; i < DiamondsInside; i++) {
            ItemPickup.Spawn(ItemPickup.Kind.Diamond, 1, center + Vector3.up * 0.3f, new Vector2(i == 0 ? -0.9f : 0.9f, 6.5f));
        }
        Reward(player);
        ExclusiveLook(center);
        // José, 2026-11-02: and a blessing, one of three, on top of those he already has
        ArenaRunVariety run = player != null ? player.GetComponent<ArenaRunVariety>() : null;
        if (run != null) {
            run.OfferBlessing();
        }
    }

    // sometimes a look only this chest has (FrenyLook.ChestDrop), his for good: saved like a bought
    // one, so it is waiting on the Visual screen after the run
    private static void ExclusiveLook(Vector3 center) {
        FrenyLook.Item look = FrenyLook.ChestDrop();
        if (look == null) {
            return;
        }
        Color purple = new Color(0.8f, 0.6f, 1f);
        ArenaBossLoot.Say("Novo visual: " + look.name + "!", center + Vector3.up * 1.6f, purple, 3.5f);
        Fx.Burst(center + Vector3.up * 0.4f, purple, 18, 3f, 0.1f, 0.9f);
    }

    // everything that is not on the floor goes straight to Freny
    private static void Reward(Player player) {
        if (player == null) {
            return;
        }
        player.CollectPotion(player.MaxHealth);   // false at full life
        player.GiveArmor();
        player.GiftRage();
        player.AddBullets(999);
        player.AddShells(999);
        player.AddGrenades(999);
        player.GiveGreatSword();                  // false when he owns it or still has its swings
    }

    // the same fan as the normal chest, bigger
    private void ScatterCoins() {
        int each = Mathf.Max(1, CoinsInside / CoinCount);
        for (int i = 0; i < CoinCount; i++) {
            if (coinPrefab == null) {
                Player player = FindPlayer();
                if (player != null) {
                    player.AddCoins(each * (CoinCount - i));
                }
                return;
            }
            float side = Mathf.Lerp(-1f, 1f, i / (float)(CoinCount - 1));
            Vector2 velocity = new Vector2(side * 2.6f * Random.Range(0.75f, 1f), Random.Range(5f, 7f));
            GameObject dropped = Instantiate(coinPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
            Coin coin = dropped.GetComponent<Coin>();
            if (coin) {
                coin.coins = each;
                coin.Toss(velocity);
            }
        }
    }
}
