using System.Collections.Generic;
using UnityEngine;

// The Ciclope's key (José, 2026-11-01): what a fallen Ciclope can leave besides its chest
// (ArenaBossLoot rolls it). It flies out of the body, lands on the floor and waits there, shining,
// until Freny touches it: it never fades, it is the boss's prize. He carries it for the rest of
// the run (Carried, shown next to the weapons by CombatHud) and spends it on a golden chest
// (GoldenChest). Built at runtime like ItemPickup: a sprite sheet from Resources (BossLoot/Boss
// Key, Tools/ArtGen/boss_loot.py), a halo, a trigger and the same small arc.
public class BossKey : MonoBehaviour {

    private const string SheetPath = "BossLoot/Boss Key";
    private const float FrameTime = 0.09f;
    private const float GlintEvery = 1.6f;      // the glint runs along the key this often
    private const float SparkleEvery = 0.35f;   // a few gold specks rise from it while it waits
    private const float TossGravity = -18f;
    private const float MaxFallSpeed = 14f;
    private const float PickupRadius = 0.5f;
    private const float RestGap = 0.12f;
    private const float BobHeight = 0.08f;
    private static readonly Color HaloColor = new Color(0.85f, 0.55f, 1f, 0.55f);   // the Ciclope's purple
    public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

    public static readonly List<BossKey> Active = new List<BossKey>();

    // keys Freny carries in this run; ArenaBossLoot clears it when a run starts and ends (the key
    // stays in the Arena: nothing to save, nothing for the cloud save or the API to know about)
    public static int Carried { get; set; }

    private static Sprite[] frames;
    private SpriteRenderer sprite;
    private SpriteRenderer halo;
    private Vector2 velocity;
    private bool tossing = true;
    private float hoverY;
    private float restLift = 0.25f;
    private float bobPhase;
    private float nextSparkle;
    private float born;

    public bool Landed => !tossing;

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
                System.Array.Sort(loaded, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));
                frames = loaded;
            }
            return frames;
        }
    }

    // the plain key (the Guia and the HUD)
    public static Sprite Icon => Frames.Length > 0 ? Frames[0] : null;

    private static int FrameIndex(Sprite sprite) {
        int underscore = sprite.name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int i) ? i : 0;
    }

    // the frame at a moment: plain most of the time, the glint running along it every GlintEvery
    public static Sprite FrameAt(float time) {
        Sprite[] all = Frames;
        if (all.Length == 0) {
            return null;
        }
        int frame = Mathf.FloorToInt(Mathf.Repeat(time, GlintEvery) / FrameTime);
        return all[frame < all.Length ? frame : 0];
    }

    public static BossKey Spawn(Vector3 position, Vector2 toss) {
        GameObject obj = new GameObject("Boss Key");
        obj.transform.position = position;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = Icon;
        renderer.sortingOrder = 41;
        if (ItemPickup.UnlitMaterial != null) {
            renderer.sharedMaterial = ItemPickup.UnlitMaterial;
        }

        GameObject haloObj = new GameObject("Halo");
        haloObj.transform.SetParent(obj.transform, false);
        SpriteRenderer haloRenderer = haloObj.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = ArenaBossLoot.HaloSprite();
        haloRenderer.color = HaloColor;
        haloRenderer.sortingOrder = 40;
        if (ItemPickup.UnlitMaterial != null) {
            haloRenderer.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        haloObj.transform.localScale = Vector3.one * 1.4f;

        CircleCollider2D trigger = obj.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = PickupRadius;
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;   // triggers need a body; the arc moves it by hand

        BossKey key = obj.AddComponent<BossKey>();
        key.sprite = renderer;
        key.halo = haloRenderer;
        key.velocity = toss;
        key.born = Time.time;
        key.restLift = RestGap + (renderer.sprite != null ? renderer.sprite.bounds.extents.y : 0.25f);
        // a bright flash where it comes out, so it is seen in the middle of the boss's death
        Fx.Burst(position, Gold, 26, 4f, 0.1f, 0.8f);
        Fx.Burst(position, new Color(0.75f, 0.4f, 1f), 14, 3f, 0.08f, 0.6f);
        Sfx.Play("level_up", position, 1f, 1.25f);
        return key;
    }

    private void Update() {
        float age = Time.time - born;
        Sprite frame = FrameAt(age);
        if (sprite && frame != null) {
            sprite.sprite = frame;
        }
        if (halo) {
            Color c = HaloColor;
            c.a *= 0.65f + 0.35f * Mathf.Sin(Time.time * 4f + bobPhase);
            halo.color = c;
        }
        if (Time.time >= nextSparkle) {
            nextSparkle = Time.time + SparkleEvery;
            Fx.Rise(transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.15f, 0.15f), 0f),
                    Color.Lerp(Gold, Color.white, Random.value * 0.5f), Random.Range(0.05f, 0.09f), Random.Range(0.5f, 0.9f));
        }
        if (!tossing) {
            Vector3 p = transform.position;
            p.y = hoverY + (Mathf.Sin(Time.time * 3f + bobPhase) + 1f) * 0.5f * BobHeight;
            transform.position = p;
            return;
        }

        velocity.y = Mathf.Max(velocity.y + TossGravity * Time.deltaTime, -MaxFallSpeed);
        Vector3 pos = transform.position;
        Vector3 next = pos + (Vector3)(velocity * Time.deltaTime);
        if (velocity.y < 0f) {
            float drop = pos.y - next.y;
            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.down, drop + restLift, LayerMask.GetMask("Ground"));
            if (hit.collider != null) {
                next.y = hit.point.y + restLift;
                tossing = false;
                hoverY = next.y;
                bobPhase = Random.Range(0f, Mathf.PI * 2f);
                Fx.Dust(hit.point, 3);
            }
        }
        if (ArenaBounds.Active) {
            next.x = ArenaBounds.ClampX(next.x, 0.5f);
            if (next.y < ArenaBounds.Bottom - 3f) {
                // it must not be lost: back to the middle of the arena, falling again
                next = new Vector3((ArenaBounds.Left + ArenaBounds.Right) * 0.5f, ArenaBounds.Bottom + 12f, 0f);
                velocity = Vector2.zero;
            }
        }
        transform.position = next;
    }

    private void OnTriggerStay2D(Collider2D other) {
        if (!other.CompareTag("Player")) {
            return;
        }
        Player player = other.GetComponent<Player>();
        if (player == null || player.IsDead) {
            return;
        }
        Carried++;
        Fx.Burst(transform.position, Gold, 18, 2.5f, 0.08f, 0.5f);
        Sfx.Play("upgrade", transform.position, 1f, 1.15f);
        Sfx.Play("door", transform.position, 0.6f, 1.6f);   // the click of a key
        // the damage numbers' box is made for "-2": a phrase wrapped and got cut, so it goes in the
        // chest's wide floating text
        ArenaBossLoot.Say("Chave do Ciclope!", player.HeadPoint + Vector3.up * 0.6f, Gold, 1.6f);
        Destroy(gameObject);
    }
}
