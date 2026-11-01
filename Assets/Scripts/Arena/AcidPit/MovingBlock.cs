using System.Collections.Generic;
using UnityEngine;

// A floating platform of the Poço de Ácido that slides from side to side (AcidPitArena picks one or
// two each run, never a crumbling one): it eases out Reach units each way and back over Period
// seconds, carrying whoever stands on it. Its tiles get a lighter tint so it reads as different
// from the fixed ones. Added to the platform's root, the one with the one-way collider; the path is
// checked first, and a platform that would run into a wall or another platform stays still.
public class MovingBlock : MonoBehaviour {
    public const float Reach = 1.5f;
    private const float Period = 6f;
    private static readonly Color Tint = new Color(0.8f, 1f, 0.85f);

    // the ones in the scene now
    public static readonly List<MovingBlock> Active = new List<MovingBlock>();

    private BoxCollider2D box;
    private Rigidbody2D body;
    private Vector3 home;
    private float phase;
    private Player hero;
    private Rigidbody2D heroBody;

    public Bounds Bounds => box != null ? box.bounds : new Bounds(transform.position, Vector3.zero);

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    // true when the platform can slide Reach each way without touching anything solid
    public static bool PathClear(BoxCollider2D box) {
        Bounds b = box.bounds;
        if (!ArenaBounds.Inside(b.min.x - Reach, 0.3f) || !ArenaBounds.Inside(b.max.x + Reach, 0.3f)) {
            return false;
        }
        Vector2 size = new Vector2(b.size.x + 2f * Reach, Mathf.Max(0.3f, b.size.y) + 0.6f);
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(b.center, size, 0f, LayerMask.GetMask("Ground"))) {
            if (hit != box && !hit.isTrigger && !hit.transform.IsChildOf(box.transform)) {
                return false;
            }
        }
        return true;
    }

    private void Start() {
        box = GetComponent<BoxCollider2D>();
        if (box == null) {
            enabled = false;
            return;
        }
        home = transform.position;
        phase = Random.value * Period;
        // a kinematic body: the physics moves the collider smoothly instead of teleporting it
        body = GetComponent<Rigidbody2D>();
        if (body == null) {
            body = gameObject.AddComponent<Rigidbody2D>();
        }
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        foreach (SpriteRenderer tile in GetComponentsInChildren<SpriteRenderer>()) {
            tile.color *= Tint;
        }
        hero = FindAnyObjectByType<Player>();
        heroBody = hero != null ? hero.GetComponent<Rigidbody2D>() : null;
    }

    private void FixedUpdate() {
        float t = (Time.time + phase) / Period * 2f * Mathf.PI;
        Vector2 target = (Vector2)home + Vector2.right * (Reach * Mathf.Sin(t));
        Vector2 delta = target - body.position;
        Bounds b = box.bounds;
        // whoever stands on it moves with it (Freny's own code sets his velocity every frame, so
        // the platform's friction alone left him behind)
        if (hero != null && heroBody != null && !hero.IsDead && StandsOn(hero.Feet, b)) {
            heroBody.position += delta;
        }
        for (int i = Enemy.Active.Count - 1; i >= 0; i--) {
            Enemy enemy = Enemy.Active[i];
            if (enemy != null && !enemy.IsDead && StandsOn(enemy.Feet, b)) {
                enemy.transform.position += (Vector3)delta;
            }
        }
        body.MovePosition(target);
    }

    private static bool StandsOn(Vector3 feet, Bounds b) {
        return feet.x > b.min.x - 0.1f && feet.x < b.max.x + 0.1f && Mathf.Abs(feet.y - b.max.y) < 0.15f;
    }
}
