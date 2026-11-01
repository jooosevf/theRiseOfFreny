using System.Collections.Generic;
using UnityEngine;

// A floating platform of the Poço de Ácido that gives way (AcidPitArena picks two each run): its
// tiles are drawn as cracked stone blocks; Freny standing on it makes them shake for CrackSeconds,
// then they fall apart and he drops through. It is back RegrowSeconds later (as soon as nobody is
// where it would be). Added to the platform's root, the one with the one-way collider.
public class CrumbleBlock : MonoBehaviour {
    private const float CrackSeconds = 0.7f;
    private const float RegrowSeconds = 5f;
    private const float CrumbleFps = 14f;
    private const float Shake = 0.05f;

    // the ones in the scene now (the autopilot keeps off a cracking one)
    public static readonly List<CrumbleBlock> Active = new List<CrumbleBlock>();

    private enum State { Solid, Cracking, Gone }
    private State state;
    private float stateStart;
    private BoxCollider2D box;
    private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> blocks = new List<SpriteRenderer>();
    private readonly List<Vector3> blockHomes = new List<Vector3>();
    private Sprite[] frames;
    private Player hero;

    public bool Solid => state != State.Gone;
    public bool Cracking => state == State.Cracking;
    // where it stands (a switched-off collider has empty bounds, so they are kept from the start)
    public Bounds Bounds { get; private set; }

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    private void Start() {
        box = GetComponent<BoxCollider2D>();
        hero = FindAnyObjectByType<Player>();
        frames = FrameAnimator.Load("Traps/Crumbling block");
        if (box == null || frames == null || frames.Length < 9) {
            enabled = false;
            return;
        }
        Bounds = box.bounds;
        float top = Bounds.max.y;
        foreach (SpriteRenderer tile in GetComponentsInChildren<SpriteRenderer>()) {
            tiles.Add(tile);
            tile.enabled = false;
            // one stone block per tile, its top level with the platform's
            SpriteRenderer block = new GameObject("Crumbling Block").AddComponent<SpriteRenderer>();
            block.transform.SetParent(transform, true);
            Vector3 home = new Vector3(tile.bounds.center.x, top - 0.5f, 0f);
            block.transform.position = home;
            block.sprite = frames[0];
            block.color = tile.color;   // the Poço's green tint, set on the tiles already
            block.sortingOrder = tile.sortingOrder + 1;
            blocks.Add(block);
            blockHomes.Add(home);
        }
    }

    private void Update() {
        float t = Time.time - stateStart;
        switch (state) {
            case State.Solid:
                if (StoodOn()) {
                    SetState(State.Cracking);
                    Sfx.Play("chest_break", transform.position, 0.6f, 1.5f);
                }
                break;
            case State.Cracking:
                for (int i = 0; i < blocks.Count; i++) {
                    blocks[i].sprite = frames[1];
                    blocks[i].transform.position = blockHomes[i] + (Vector3)(Random.insideUnitCircle * Shake);
                }
                if (Random.value < 0.3f) {
                    Fx.Dust(new Vector3(Random.Range(Bounds.min.x, Bounds.max.x), Bounds.min.y - 0.4f, 0f), 1);
                }
                if (t >= CrackSeconds) {
                    SetState(State.Gone);
                    box.enabled = false;
                    Sfx.Play("chest_break", transform.position, 0.9f, 0.8f);
                    for (int i = 0; i < blocks.Count; i++) {
                        blocks[i].transform.position = blockHomes[i];
                        Fx.Burst(blockHomes[i], new Color(0.3f, 0.35f, 0.25f), 8, 2.5f, 0.1f, 0.5f);
                    }
                }
                break;
            case State.Gone: {
                // the blocks fall apart (frames 2..8), then nothing until it grows back
                int frame = 2 + (int)(t * CrumbleFps);
                foreach (SpriteRenderer block in blocks) {
                    block.enabled = frame < frames.Length;
                    if (frame < frames.Length) {
                        block.sprite = frames[frame];
                    }
                }
                if (t >= RegrowSeconds && !Occupied()) {
                    SetState(State.Solid);
                    box.enabled = true;
                    foreach (SpriteRenderer block in blocks) {
                        block.enabled = true;
                        block.sprite = frames[0];
                        Fx.Dust(block.transform.position + Vector3.up * 0.5f, 2);
                    }
                }
                break;
            }
        }
    }

    private void SetState(State next) {
        state = next;
        stateStart = Time.time;
    }

    // Freny's feet resting on its top
    private bool StoodOn() {
        if (hero == null || hero.IsDead || !hero.IsGrounded) {
            return false;
        }
        Bounds b = Bounds;
        Vector3 feet = hero.Feet;
        return feet.x > b.min.x - 0.1f && feet.x < b.max.x + 0.1f && Mathf.Abs(feet.y - b.max.y) < 0.3f;
    }

    // someone where it would come back (it would trap them inside)
    private bool Occupied() {
        Bounds b = Bounds;
        b.Expand(new Vector3(0.2f, 1.2f, 0f));
        if (hero != null && b.Contains(new Vector3(hero.BodyCenter.x, hero.BodyCenter.y, b.center.z))) {
            return true;
        }
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy != null && !enemy.IsDead && b.Contains(new Vector3(enemy.Center.x, enemy.Center.y, b.center.z))) {
                return true;
            }
        }
        return false;
    }
}
