using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A clay vase standing on a platform. Any hit (sword, bullet, grenade) breaks it: it plays the
// breaking frames of Items/Vase 100x100 and sometimes leaves an item (ItemPickup.RollVaseDrop).
// Built at runtime by the ArenaDirector, so no prefab is needed; the sprites come from ArenaAssets.
// It stands as one of the tileset's whole vases (the ones painted on the stages, see Breakable):
// the breaking frames start already cracked, and a vase spawned with the first of them looked
// broken from the start (José, 2026-10-29).
public class ArenaVase : MonoBehaviour, IDamageable {

    private const float TileSize = 1f;          // the 100 px tile; the vase stands at its bottom
    private const float VaseFrameScale = 0.7f;  // the breaking vase is drawn bigger than the tileset's
    private const float FrameTime = 0.07f;
    private const float Lifetime = 60f;         // goes away if nobody breaks it

    public static readonly List<ArenaVase> Active = new List<ArenaVase>();

    public int Health { get; set; } = 1;
    private bool broken;
    private SpriteRenderer sprite;
    private Vector3 floorPoint;

    // tile: which of the tileset's vases (co-op: the host's pick); -1 picks one
    public static ArenaVase Create(Vector3 floorPoint, int tile = -1) {
        ArenaAssets assets = ArenaAssets.Instance;
        if (assets == null) {
            return null;
        }
        Sprite standing = null;
        if (assets.vaseTiles != null && assets.vaseTiles.Length > 0) {
            tile = tile >= 0 && tile < assets.vaseTiles.Length ? tile : Random.Range(0, assets.vaseTiles.Length);
            standing = assets.vaseTiles[tile];
        }
        if (standing == null) {
            return null;
        }
        GameObject obj = new GameObject("Vase");
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = standing;
        renderer.sortingOrder = 35;
        float height = standing.bounds.size.y;
        obj.transform.localScale = Vector3.one * (height > 0.001f ? TileSize / height : 1f);
        // the tile is centred and the vase sits at its bottom: lift it so it stands on the floor
        obj.transform.position = floorPoint + Vector3.up * (TileSize * 0.5f - 0.02f);

        // the opaque part of the tile, like Breakable's
        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;   // the sword, bullets and grenades find it; Freny walks through it
        Vector2 full = standing.bounds.size;
        box.size = new Vector2(full.x * 0.55f, full.y * 0.6f);
        box.offset = new Vector2(0f, -full.y * 0.5f + box.size.y * 0.5f);

        HitBody.MakeHittable(obj);

        ArenaVase vase = obj.AddComponent<ArenaVase>();
        vase.sprite = renderer;
        vase.floorPoint = floorPoint;
        Fx.Dust(floorPoint, 4);
        CoopShared.VaseSpawned(vase, floorPoint, tile);
        return vase;
    }

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    private void Start() {
        Destroy(gameObject, Lifetime);
    }

    // the middle of the vase (the autopilot walks to it and swings)
    public Vector3 Center => floorPoint + Vector3.up * (TileSize * 0.3f);

    public void Damage() {
        if (broken || CoopShared.AskToBreak(this)) {
            return;
        }
        broken = true;
        Health = 0;
        Active.Remove(this);
        foreach (Collider2D c in GetComponents<Collider2D>()) {
            c.enabled = false;
        }
        Fx.Burst(Center, new Color(0.55f, 0.42f, 0.38f), 14, 2.5f, 0.09f, 0.5f);
        Sfx.Play("chest_break", Center, 0.8f, 1.4f);
        ItemPickup.RollVaseDrop(Center);
        StartCoroutine(Break());
    }

    private IEnumerator Break() {
        Sprite[] frames = ArenaAssets.Instance ? ArenaAssets.Instance.vaseFrames : null;
        if (frames != null && frames.Length > 0 && frames[0] != null) {
            // the breaking frames, standing where the whole vase stood
            float height = frames[0].bounds.size.y;
            transform.localScale = Vector3.one * (height > 0.001f ? TileSize * VaseFrameScale / height : 1f);
            transform.position = floorPoint + Vector3.up * (TileSize * VaseFrameScale * 0.5f - 0.02f);
            foreach (Sprite frame in frames) {
                if (frame != null) {
                    sprite.sprite = frame;
                }
                yield return new WaitForSeconds(FrameTime);
            }
        }
        // the shards stay a moment, then fade
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) {
            sprite.color = new Color(1f, 1f, 1f, 1f - t / 0.5f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
