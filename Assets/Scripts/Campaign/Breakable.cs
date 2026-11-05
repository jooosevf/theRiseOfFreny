using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The vases, barrels and crates painted on the stages' tilemaps (Tileset_45..47, 56 and 66) become
// objects that any hit breaks (sword, bullet, grenade), like the arena's ArenaVase: CampaignBootstrap
// calls FromTilemaps, which takes each of those tiles off its tilemap and puts this in its place.
// A broken one bursts into shards and rolls the same drop as an arena vase (ItemPickup.RollVaseDrop).
// ArenaBootstrap does the same for ZE Arena's props, which grow back a while after breaking, since
// an arena run never leaves the screen.
public class Breakable : MonoBehaviour, IDamageable {

    public enum Kind { Vase, Barrel, Crate }

    private const float FrameTime = 0.07f;
    private const float VaseFrameScale = 0.7f;   // the arena's breaking vase is drawn bigger than the tileset's

    public static readonly List<Breakable> Active = new List<Breakable>();

    public int Health { get; set; } = 1;
    private Kind kind;
    private bool broken;
    private SpriteRenderer sprite;
    private Sprite tileSprite;
    private Color tileColor;
    private Vector3 tilePosition;
    private float regrowAfter;   // 0: gone for good once broken (the campaign)
    private string worldKey;     // the test dungeon remembers it was broken (CampaignWorld)

    private static Kind? KindOf(Sprite sprite) {
        if (sprite == null) {
            return null;
        }
        switch (sprite.name) {
            case "Tileset_45":
            case "Tileset_46":
            case "Tileset_47": return Kind.Vase;
            case "Tileset_56": return Kind.Barrel;
            case "Tileset_66": return Kind.Crate;
            default: return null;
        }
    }

    // returns how many it made; regrowAfter > 0 brings each broken one back after that many seconds
    public static int FromTilemaps(float regrowAfter = 0f) {
        int made = 0;
        foreach (Tilemap tilemap in FindCompat.All<Tilemap>()) {
            // solid tilemaps (the ground) never hold props
            if (tilemap.GetComponent<TilemapCollider2D>() != null) {
                continue;
            }
            TilemapRenderer layer = tilemap.GetComponent<TilemapRenderer>();
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin) {
                Sprite tileSprite = tilemap.GetSprite(cell);
                Kind? kind = KindOf(tileSprite);
                if (kind == null) {
                    continue;
                }
                Matrix4x4 matrix = tilemap.GetTransformMatrix(cell);
                Color color = tilemap.color * tilemap.GetColor(cell);
                Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
                Vector3 position = tilemap.CellToWorld(cell) + Vector3.Scale(tilemap.tileAnchor, cellSize);
                tilemap.SetTile(cell, null);
                Create(kind.Value, tileSprite, position, color, matrix.lossyScale.x < 0f, layer, regrowAfter);
                made++;
            }
        }
        return made;
    }

    // a vase placed in a room of the test dungeon, which has no painted props
    public static void PlaceVase(Vector3 position, TilemapRenderer layer, string worldKey) {
        Sprite[] tiles = ArenaAssets.Instance ? ArenaAssets.Instance.vaseTiles : null;
        if (tiles == null || tiles.Length == 0) {
            return;
        }
        Breakable vase = Create(Kind.Vase, tiles[Random.Range(0, tiles.Length)], position, Color.white, false, layer, 0f);
        vase.worldKey = worldKey;
    }

    private static Breakable Create(Kind kind, Sprite tileSprite, Vector3 position, Color color, bool flip, TilemapRenderer layer, float regrowAfter) {
        GameObject obj = new GameObject(kind.ToString());
        obj.transform.position = position;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = tileSprite;
        renderer.color = color;
        renderer.flipX = flip;
        if (layer != null) {
            renderer.sharedMaterial = layer.sharedMaterial;   // lit by the torches like the tilemap was
            renderer.sortingLayerID = layer.sortingLayerID;
            renderer.sortingOrder = layer.sortingOrder;
        }

        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;   // the sword, bullets and grenades find it; Freny walks through it
        Bounds visible = VisibleBounds(tileSprite);
        box.size = visible.size;
        box.offset = visible.center;

        HitBody.MakeHittable(obj);

        Breakable breakable = obj.AddComponent<Breakable>();
        breakable.kind = kind;
        breakable.sprite = renderer;
        breakable.tileSprite = tileSprite;
        breakable.tileColor = color;
        breakable.tilePosition = position;
        breakable.regrowAfter = regrowAfter;
        return breakable;
    }

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    public bool Broken => broken;

    // what the autopilot calls it ("Quebrando o barril")
    public string Describe() {
        switch (kind) {
            case Kind.Barrel: return "o barril";
            case Kind.Crate: return "a caixa";
            default: return "o vaso";
        }
    }

    // the opaque part of a 100 px tile (the props stand at its bottom), in the sprite's local units
    private static Bounds VisibleBounds(Sprite sprite) {
        Vector2 full = sprite.bounds.size;
        Vector2 size = new Vector2(full.x * 0.55f, full.y * 0.6f);
        Vector2 center = new Vector2(0f, -full.y * 0.5f + size.y * 0.5f);
        return new Bounds(center, size);
    }

    public Vector3 Center => transform.position + (Vector3)GetComponent<BoxCollider2D>().offset;

    public void Damage() {
        if (broken) {
            return;
        }
        broken = true;
        Health = 0;
        if (worldKey != null) {
            CampaignWorld.Mark(worldKey);
        }
        Active.Remove(this);
        Vector3 center = Center;
        foreach (Collider2D c in GetComponents<Collider2D>()) {
            c.enabled = false;
        }
        Color shards = kind == Kind.Vase ? new Color(0.55f, 0.42f, 0.38f) : new Color(0.42f, 0.27f, 0.18f);
        Fx.Burst(center, shards, 14, 2.5f, 0.09f, 0.5f);
        Fx.Dust(center + Vector3.down * 0.25f, 3);
        Sfx.Play("chest_break", center, 0.8f, kind == Kind.Vase ? 1.4f : 0.9f);
        ItemPickup.RollVaseDrop(center);
        StartCoroutine(Break());
    }

    private IEnumerator Break() {
        Sprite[] frames = ArenaAssets.Instance ? ArenaAssets.Instance.vaseFrames : null;
        if (kind == Kind.Vase && frames != null && frames.Length > 0) {
            // the clay vase breaks frame by frame, standing where the tile's vase stood
            transform.localScale = Vector3.one * VaseFrameScale;
            transform.position += Vector3.down * (0.5f - VaseFrameScale * 0.5f);
            foreach (Sprite frame in frames) {
                if (frame != null) {
                    sprite.sprite = frame;
                }
                yield return new WaitForSeconds(FrameTime);
            }
        } else {
            // wood has no breaking frames: a quick squash before the shards take over
            Vector3 start = transform.localScale;
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) {
                float k = t / 0.12f;
                transform.localScale = new Vector3(start.x * (1f + 0.25f * k), start.y * (1f - 0.6f * k), 1f);
                yield return null;
            }
        }
        Color color = sprite.color;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime) {
            sprite.color = new Color(color.r, color.g, color.b, color.a * (1f - t / 0.4f));
            yield return null;
        }
        if (regrowAfter <= 0f) {
            Destroy(gameObject);
            yield break;
        }
        sprite.enabled = false;
        yield return new WaitForSeconds(regrowAfter * Random.Range(0.8f, 1.2f));
        Regrow();
    }

    // back as it was painted, with a puff of dust
    private void Regrow() {
        transform.localScale = Vector3.one;
        transform.position = tilePosition;
        sprite.sprite = tileSprite;
        sprite.color = tileColor;
        sprite.enabled = true;
        foreach (Collider2D c in GetComponents<Collider2D>()) {
            c.enabled = true;
        }
        broken = false;
        Health = 1;
        Active.Add(this);
        Fx.Dust(Center + Vector3.down * 0.25f, 3);
    }
}
