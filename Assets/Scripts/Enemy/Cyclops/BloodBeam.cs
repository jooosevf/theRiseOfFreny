using System.Collections.Generic;
using UnityEngine;

// The Ciclope's red beam in the Lua de Sangue (José, 2026-10-30): a thick red ray out of its eye,
// fired along a locked direction across the whole screen. Freny caught in it loses Damage hearts;
// every other enemy in its way is disintegrated (no score for Freny). The Ciclopes are spared
public class BloodBeam : MonoBehaviour {
    public const int Damage = 3;
    private const float Length = 60f;
    private const float Radius = 0.55f;       // how far from its middle line it reaches
    private const float GlowWidth = 1.5f;
    private const float CoreWidth = 0.55f;
    private static readonly Color Glow = new Color(1f, 0.08f, 0.1f, 0.55f);
    private static readonly Color Core = new Color(1f, 0.75f, 0.7f, 1f);
    private static Material material;

    private Vector3 from;
    private Vector3 direction;
    private float seconds;
    private float started;
    private bool hitFreny;
    private Player freny;
    private LineRenderer glow, core;
    private readonly HashSet<Enemy> burned = new HashSet<Enemy>();

    public static BloodBeam Fire(Vector3 eye, Vector3 at, float seconds, Player player, SpriteRenderer over) {
        GameObject go = new GameObject("Blood Beam");
        BloodBeam beam = go.AddComponent<BloodBeam>();
        beam.from = eye;
        Vector3 d = at - eye;
        beam.direction = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
        beam.seconds = seconds;
        beam.started = Time.time;
        beam.freny = player;
        // drawn on the Ciclope's own sorting layer, over its sprite, so it comes out of the eye
        // instead of from behind the head
        int layer = over != null ? over.sortingLayerID : 0;
        int order = over != null ? over.sortingOrder : 45;
        beam.glow = beam.Line("Glow", GlowWidth, Glow, layer, order + 2);
        beam.core = beam.Line("Core", CoreWidth, Core, layer, order + 3);
        ScreenKick.Kick(0.25f, seconds);
        Fx.Burst(eye, Glow, 24, 4f, 0.1f, 0.4f);
        return beam;
    }

    private LineRenderer Line(string name, float width, Color color, int layer, int order) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.sortingLayerID = layer;
        line.sortingOrder = order;
        line.sharedMaterial = Material();
        line.startColor = line.endColor = color;
        line.widthMultiplier = width;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, from + direction * Length);
        return line;
    }

    private void Update() {
        float t = (Time.time - started) / seconds;
        if (t >= 1f) {
            Destroy(gameObject);
            return;
        }
        // it bursts out wide, throbs, and narrows away at the end
        float swell = t < 0.1f ? t / 0.1f : 1f - Mathf.Clamp01((t - 0.75f) / 0.25f);
        float throb = 1f + Mathf.Sin(Time.time * 50f) * 0.12f;
        glow.widthMultiplier = GlowWidth * swell * throb;
        core.widthMultiplier = CoreWidth * swell * throb;
        // red specks rising off it
        for (int i = 0; i < 3; i++) {
            Fx.Rise(from + direction * Random.Range(0.5f, 25f) + (Vector3)(Random.insideUnitCircle * 0.4f), Glow, 0.12f, 0.6f);
        }
        if (swell > 0.5f) {
            Burn();
        }
    }

    private float Distance(Vector3 point) {
        Vector3 d = point - from;
        float along = Vector3.Dot(d, direction);
        if (along < 0f) {
            return float.PositiveInfinity;   // behind the eye
        }
        return (d - direction * along).magnitude;
    }

    private void Burn() {
        if (!hitFreny && freny != null && !freny.IsDead && Distance(freny.BodyCenter) < Radius + 0.4f) {
            hitFreny = true;
            freny.TryDamage(Damage);
        }
        for (int i = Enemy.Active.Count - 1; i >= 0; i--) {
            Enemy enemy = Enemy.Active[i];
            if (enemy == null || enemy.IsDead || enemy is Cyclops || burned.Contains(enemy)) {
                continue;
            }
            if (Distance(enemy.Center) < Radius + enemy.HalfWidth) {
                burned.Add(enemy);
                enemy.HitBy(null, 9999);   // it dies in a cloud of specks (Enemy.Die)
            }
        }
    }

    // a soft round red-white glow (the charging eye, the mouth of the beam)
    private static Sprite glowSprite;

    public static SpriteRenderer Orb(string name, SpriteRenderer over) {
        if (glowSprite == null) {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    Color c = Color.Lerp(new Color(1f, 0.1f, 0.1f), new Color(1f, 0.9f, 0.85f), Mathf.Clamp01(1f - d * 2.2f));
                    texture.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            }
            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        GameObject go = new GameObject(name);
        SpriteRenderer orb = go.AddComponent<SpriteRenderer>();
        orb.sprite = glowSprite;
        orb.sortingLayerID = over != null ? over.sortingLayerID : 0;
        orb.sortingOrder = (over != null ? over.sortingOrder : 45) + 4;   // over the beam and the Ciclope's face
        if (ItemPickup.UnlitMaterial != null) {
            orb.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        return orb;
    }

    private static Material Material() {
        if (material == null) {
            Material unlit = ItemPickup.UnlitMaterial;
            material = unlit != null ? new Material(unlit) : new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = Texture2D.whiteTexture;
        }
        return material;
    }
}
