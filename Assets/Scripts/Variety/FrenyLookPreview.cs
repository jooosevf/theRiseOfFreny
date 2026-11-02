using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Freny on the Visual screen (VarietyScreens), wearing a look: his idle frames (MenuArt.frenyIdle)
// with the same layers the game draws (FrenyLookRenderer): the helmet and the tinted shirt over the
// body, the cape behind it, swaying gently. Unscaled time, like the other menu portraits.
// Build it with Create on a window; Show picks what he wears.
public class FrenyLookPreview : MonoBehaviour {

    private const float Fps = 2.5f;

    private Image body, shirt, helmet;
    private RectTransform back;              // behind the body, same rect: the cape hangs here
    private readonly RectTransform[] capeParts = new RectTransform[3];
    private readonly Image[] capeImages = new Image[3];
    private Sprite[] frames;
    private FrenyLook.Item helmetItem, capeItem, colorItem;

    // the idle frames are 200 px squares with Freny in the bottom two thirds; size is the square's
    // side on screen, anchored at the bottom centre of the window
    public static FrenyLookPreview Create(RectTransform window, float size) {
        MenuArt art = MenuArt.Get;
        RectTransform holder = MenuKit.MakeRect("Freny Look", window);
        MenuKit.Stretch(holder);
        FrenyLookPreview preview = holder.gameObject.AddComponent<FrenyLookPreview>();
        preview.frames = art != null ? art.frenyIdle : null;

        preview.back = MenuKit.MakeRect("Cape", holder);
        preview.body = MenuKit.MakeImage("Body", holder, Color.white);
        foreach (RectTransform rect in new[] { preview.back, preview.body.rectTransform }) {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(size, size);
        }
        preview.body.raycastTarget = false;
        preview.shirt = preview.Overlay("Shirt");
        preview.helmet = preview.Overlay("Helmet");

        Transform parent = preview.back;
        for (int i = 0; i < preview.capeParts.Length; i++) {
            Image part = MenuKit.MakeImage("Cape " + i, parent, Color.white);
            part.raycastTarget = false;
            RectTransform rect = part.rectTransform;
            rect.pivot = new Vector2(0.5f, 1f);
            // the first hangs from a point set every frame; the others from the bottom of the one above
            rect.anchorMin = rect.anchorMax = i == 0 ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            preview.capeParts[i] = rect;
            preview.capeImages[i] = part;
            parent = rect;
        }
        return preview;
    }

    private Image Overlay(string name) {
        Image image = MenuKit.MakeImage(name, body.transform, Color.white);
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    public void Show(FrenyLook.Item helmetLook, FrenyLook.Item capeLook, FrenyLook.Item colorLook) {
        helmetItem = helmetLook != null && helmetLook.unlock != FrenyLook.Unlock.Free ? helmetLook : null;
        capeItem = capeLook != null && capeLook.unlock != FrenyLook.Unlock.Free ? capeLook : null;
        colorItem = colorLook != null && colorLook.unlock != FrenyLook.Unlock.Free ? colorLook : null;
        Sprite[] segments = CapeArt.Segments(capeItem);
        for (int i = 0; i < capeImages.Length; i++) {
            capeImages[i].sprite = segments != null ? segments[i] : null;
            capeImages[i].enabled = segments != null;
        }
        Update();
    }

    private void Update() {
        if (frames == null || frames.Length == 0) {
            body.enabled = false;
            return;
        }
        Sprite frame = frames[Mathf.FloorToInt(Time.unscaledTime * Fps) % frames.Length];
        body.sprite = frame;
        float scale = body.rectTransform.rect.width / frame.rect.width;   // screen pixels per texel

        Sprite top = null;
        Dictionary<string, Sprite> helmets = FrenyLook.HelmetSheet(helmetItem);
        if (helmets != null) {
            helmets.TryGetValue(frame.name, out top);
        }
        Place(helmet, frame, top, scale);

        Sprite mask = null;
        if (colorItem != null) {
            FrenyLook.ShirtSheet.TryGetValue(frame.name, out mask);
        }
        Place(shirt, frame, mask, scale);
        if (mask != null) {
            shirt.color = colorItem.color;
        }

        if (capeItem != null && CapeArt.Anchor(frame, false, out Vector2 anchor)) {
            float unit = frame.pixelsPerUnit * scale;   // screen pixels per unit
            Vector2 pivot = new Vector2(frame.pivot.x * scale - back.rect.width / 2f, frame.pivot.y * scale - back.rect.height / 2f);
            capeParts[0].anchoredPosition = pivot + anchor * unit;
            for (int i = 0; i < capeParts.Length; i++) {
                Sprite segment = capeImages[i].sprite;
                capeParts[i].sizeDelta = segment != null ? segment.rect.size * (unit / CapeArt.PixelsPerUnit) : Vector2.zero;
                if (i > 0) {
                    capeParts[i].anchoredPosition = Vector2.zero;
                }
                float angle = CapeArt.TargetAngle(i, 0.6f, 0f, Time.unscaledTime);   // a light breeze
                capeParts[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    // a layer cut for this body frame: its pivot on the frame's pivot (MenuPortrait does the same)
    private static void Place(Image overlay, Sprite frame, Sprite top, float scale) {
        if (top == null) {
            overlay.enabled = false;
            return;
        }
        RectTransform o = overlay.rectTransform;
        o.anchorMin = o.anchorMax = new Vector2(frame.pivot.x / frame.rect.width, frame.pivot.y / frame.rect.height);
        o.pivot = new Vector2(top.pivot.x / top.rect.width, top.pivot.y / top.rect.height);
        o.sizeDelta = top.rect.size * scale;
        o.anchoredPosition = Vector2.zero;
        overlay.sprite = top;
        overlay.enabled = true;
    }
}
