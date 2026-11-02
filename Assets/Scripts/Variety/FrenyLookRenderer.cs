using System.Collections.Generic;
using UnityEngine;

// Draws Freny's look (FrenyLook) on him in the game: the cosmetic helmet over the body frame, the
// shirt mask tinted with his colour, and the cape behind him, swinging with his speed. All three
// are children of the animated body sprite, like FrenyMotion's own helmet layer, so they follow the
// rig's lean and squash. Runs after FrenyMotion (which may swap the body frame for one of its
// poses in its LateUpdate). Rage and the Berserker armor still show their own helmets: they matter
// in a fight, the look doesn't. Added to every Player by ArenaVariety when a scene loads.
[DefaultExecutionOrder(100)]
public class FrenyLookRenderer : MonoBehaviour {

    private Player player;
    private FrenyMotion motion;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private SpriteRenderer helmet, shirt;
    private SpriteRenderer[] cape;
    private float[] capeAngles;
    private Vector2 capeAnchor;
    private bool hasAnchor;

    private FrenyLook.Item colorItem, capeItem;
    private Dictionary<string, Sprite> helmetSheet;

    private void Start() {
        player = GetComponent<Player>();
        motion = GetComponent<FrenyMotion>();
        rb = GetComponent<Rigidbody2D>();
        Animator anim = GetComponentInChildren<Animator>();
        body = anim ? anim.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        if (body == null || body.transform == transform) {
            enabled = false;
            return;
        }
        helmet = Layer("Look Helmet", body.transform, 2, 0f);
        shirt = Layer("Look Shirt", body.transform, 1, 0.001f);   // under the sword (same order, further back)
        cape = new SpriteRenderer[CapeArt.Widths.Length];
        capeAngles = new float[cape.Length];
        Transform parent = body.transform;
        for (int i = 0; i < cape.Length; i++) {
            cape[i] = Layer("Look Cape " + i, parent, -1, 0.002f);
            parent = cape[i].transform;
        }
        Refresh();
        FrenyLook.Changed += Refresh;
    }

    private void OnDestroy() {
        FrenyLook.Changed -= Refresh;
    }

    private SpriteRenderer Layer(string name, Transform parent, int order, float back) {
        SpriteRenderer layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.transform.localPosition = new Vector3(0f, 0f, back);
        layer.sharedMaterial = body.sharedMaterial;
        layer.sortingLayerID = body.sortingLayerID;
        layer.sortingOrder = body.sortingOrder + order;
        layer.enabled = false;
        return layer;
    }

    private void Refresh() {
        // the Traje brings its own helmet (FrenyMotion's layer) and has no shirt to tint
        bool suit = FrenyLook.WearsTraje;
        FrenyLook.Item helmetItem = suit ? null : FrenyLook.Worn(LookSlot.Helmet);
        helmetSheet = FrenyLook.HelmetSheet(helmetItem);
        colorItem = suit ? null : FrenyLook.Worn(LookSlot.Color);
        capeItem = FrenyLook.Worn(LookSlot.Cape);
        Sprite[] segments = CapeArt.Segments(capeItem);
        for (int i = 0; i < cape.Length; i++) {
            cape[i].sprite = segments != null ? segments[i] : null;
            // each strip hangs from the bottom of the one above
            if (i > 0) {
                cape[i].transform.localPosition = new Vector3(0f, -CapeArt.Heights[i - 1] * CapeArt.ArtPixel, 0f);
            }
        }
    }

    private void LateUpdate() {
        if (body == null) {
            return;
        }
        FrenyLook.Dress(body);   // FrenyMotion stops once he is dead, the Animator doesn't
        bool shown = body.enabled && body.gameObject.activeInHierarchy && (player == null || !player.IsDead);
        Sprite frame = body.sprite;
        string frameName = frame != null ? frame.name : "";

        // the helmet: not while FrenyMotion shows Rage's or the armor's
        bool buffHead = motion != null && motion.HeadRenderer != null && motion.HeadRenderer.enabled;
        Sprite top = null;
        if (shown && !buffHead && helmetSheet != null) {
            helmetSheet.TryGetValue(frameName, out top);
        }
        Show(helmet, top);

        Sprite mask = null;
        if (shown && colorItem != null) {
            FrenyLook.ShirtSheet.TryGetValue(frameName, out mask);
        }
        Show(shirt, mask);
        if (mask != null) {
            shirt.color = colorItem.color;
        }

        UpdateCape(shown, frame);
    }

    private void Show(SpriteRenderer layer, Sprite sprite) {
        layer.enabled = sprite != null;
        if (sprite != null) {
            layer.sprite = sprite;
            layer.flipX = body.flipX;
        }
    }

    private void UpdateCape(bool shown, Sprite frame) {
        bool on = shown && capeItem != null && cape[0].sprite != null;
        if (on && CapeArt.Anchor(frame, body.flipX, out Vector2 anchor)) {
            capeAnchor = anchor;
            hasAnchor = true;
        }
        on &= hasAnchor;
        for (int i = 0; i < cape.Length; i++) {
            cape[i].enabled = on;
            cape[i].sortingOrder = body.sortingOrder - 1;
        }
        if (!on) {
            return;
        }
        cape[0].transform.localPosition = new Vector3(capeAnchor.x, capeAnchor.y, 0.002f);

        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        Vector2 velocity = rb ? rb.Vel() : Vector2.zero;
        float flip = body.flipX ? -1f : 1f;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float blend = 1f - Mathf.Exp(-12f * dt);
        for (int i = 0; i < cape.Length; i++) {
            float target = CapeArt.TargetAngle(i, velocity.x * facing * flip, velocity.y, Time.time) * flip;
            capeAngles[i] = Mathf.LerpAngle(capeAngles[i], target, blend);
            cape[i].transform.localRotation = Quaternion.Euler(0f, 0f, capeAngles[i]);
            cape[i].flipX = body.flipX;
        }
    }
}
