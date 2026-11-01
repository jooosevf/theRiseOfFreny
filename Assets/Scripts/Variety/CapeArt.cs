using System.Collections.Generic;
using UnityEngine;

// Freny's capes, drawn at runtime in his pixel size (4 texels per art pixel at 100 pixels per unit,
// like his sheets): three strips seen from the side, each a little wider than the one above, hung
// one under the other so they can swing apart (FrenyLookRenderer in the game, FrenyLookPreview on
// the Visual screen). The cloth colour, a darker fold on the side against his back and a light hem.
public static class CapeArt {

    public const int Px = 4;
    public const float PixelsPerUnit = 100f;
    public const float ArtPixel = Px / PixelsPerUnit;   // one art pixel, in units

    // art pixels: from the shoulders down
    public static readonly int[] Widths = { 4, 5, 6 };
    public static readonly int[] Heights = { 4, 4, 5 };

    // where the cape hangs from, on the helmet of the frame (FrenyLook.NeckSheet): this many art
    // pixels in from the back edge, and up from the bottom (the neck)
    public const float BackInset = 0f;
    public const float NeckLift = 0.5f;

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    public static Sprite[] Segments(FrenyLook.Item cape) {
        if (cape == null) {
            return null;
        }
        if (cache.TryGetValue(cape.id, out Sprite[] cached) && cached != null && cached[0] != null) {
            return cached;
        }
        Sprite[] sprites = new Sprite[Widths.Length];
        for (int i = 0; i < sprites.Length; i++) {
            sprites[i] = Segment(cape, i, i == sprites.Length - 1);
        }
        cache[cape.id] = sprites;
        return sprites;
    }

    // facing right, his back is on the left: the right column is the fold against his back
    private static Sprite Segment(FrenyLook.Item cape, int index, bool last) {
        int w = Widths[index], h = Heights[index];
        Texture2D tex = new Texture2D(w * Px, h * Px, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Cape " + cape.id + " " + index,
        };
        Color32[] pixels = new Color32[tex.width * tex.height];
        for (int y = 0; y < h; y++) {          // y = 0 is the bottom row
            for (int x = 0; x < w; x++) {
                Color c = x == w - 1 ? cape.shade : cape.color;
                if (last && y == 0) {
                    // the hem, ragged at the corners
                    c = x == 0 || x == w - 1 ? Color.clear : cape.trim;
                } else if (x == 0 && (y + index) % 3 == 0) {
                    c = Color.Lerp(cape.color, cape.trim, 0.35f);   // a light crease on the outside
                }
                for (int py = 0; py < Px; py++) {
                    for (int px = 0; px < Px; px++) {
                        pixels[(y * Px + py) * tex.width + x * Px + px] = c;
                    }
                }
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 1f),
            PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = tex.name;
        return sprite;
    }

    // the cape's top in the body sprite's space (units from its pivot), from the frame's helmet;
    // false for a frame with no helmet (the white hit flash), which keeps the last spot
    public static bool Anchor(Sprite bodyFrame, bool flipX, out Vector2 anchor) {
        anchor = Vector2.zero;
        if (bodyFrame == null || !FrenyLook.NeckSheet.TryGetValue(bodyFrame.name, out Sprite neck)) {
            return false;
        }
        Bounds b = neck.bounds;
        anchor = new Vector2(b.min.x + BackInset * ArtPixel, b.min.y + NeckLift * ArtPixel);
        if (flipX) {
            anchor.x = -anchor.x;
        }
        return true;
    }

    // how far each strip swings (degrees, + towards his front), from his speed in his own frame
    // (forward > 0) and the time: the cape trails behind as he runs, lifts as he falls, and flutters
    public static float TargetAngle(int index, float forwardSpeed, float verticalSpeed, float time) {
        float trail = Mathf.Clamp(forwardSpeed * 9f, -15f, 70f);
        float lift = Mathf.Clamp(-verticalSpeed * 5f, 0f, 55f);
        float flutter = Mathf.Sin(time * 9f + index * 0.9f) * (2f + Mathf.Abs(forwardSpeed) * 1.4f);
        switch (index) {
            case 0: return -(trail * 0.7f + lift * 0.6f) + flutter * 0.5f;
            case 1: return -(trail * 0.25f + lift * 0.3f) + flutter;
            default: return -(trail * 0.15f + lift * 0.2f) + flutter * 1.4f;
        }
    }
}
