using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A face (or Freny) on a menu: idles with a slow bob and a blink now and then, and moves its
// mouth while MenuSpeech is typing. Unscaled time, so it also lives in the pause menu.
public class MenuPortrait : MonoBehaviour {

    private Image image;
    private RectTransform rect;
    private Sprite[] talking;
    private Sprite idle;
    private Sprite blink;
    private float frameSeconds;
    private float bob;
    private Vector2 home;
    private float talkUntil;
    private float nextBlink;
    private float phase;

    public Image Image => image;

    // frames: the idle frame first; talking cycles all of them; blink may be null
    public void Init(Sprite[] frames, Sprite blinkFrame, float fps, float bobPixels) {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        talking = frames;
        idle = frames != null && frames.Length > 0 ? frames[0] : null;
        blink = blinkFrame;
        frameSeconds = 1f / Mathf.Max(1f, fps);
        bob = bobPixels;
        home = rect.anchoredPosition;
        phase = Random.value * 10f;
        nextBlink = Time.unscaledTime + Random.Range(2f, 4f);
        image.sprite = idle;
        image.enabled = idle != null;   // no art: nothing rather than a white box
    }

    // Freny's idle loops its frames all the time
    public bool Loop;

    // a layer drawn over each frame, picked by the frame's name (Freny's Berserker helmet);
    // null hides it
    public Dictionary<string, Sprite> OverlaySprites { get; set; }

    // swaps each frame for another one (Freny's Traje); null leaves them
    public System.Func<Sprite, Sprite> FrameSkin { get; set; }
    private Image overlay;

    public void Talk(float seconds) {
        talkUntil = Time.unscaledTime + seconds;
    }

    private void Update() {
        if (image == null || talking == null || talking.Length == 0) {
            return;
        }
        float now = Time.unscaledTime;
        rect.anchoredPosition = home + new Vector2(0f, Mathf.Round(Mathf.Sin(now * 1.8f + phase) * bob));

        int frame = Mathf.FloorToInt(now / frameSeconds);
        if (Loop || now < talkUntil) {
            image.sprite = talking[frame % talking.Length];
        } else if (blink != null && now >= nextBlink) {
            image.sprite = blink;
            if (now >= nextBlink + 0.15f) {
                nextBlink = now + Random.Range(2.5f, 5f);
            }
        } else {
            image.sprite = idle;
        }
        if (FrameSkin != null) {
            image.sprite = FrameSkin(image.sprite);
        }
        UpdateOverlay();
    }

    // the overlay's pivot sits on the frame's pivot, as the helmet does on Freny in the game
    private void UpdateOverlay() {
        Sprite top = null;
        if (OverlaySprites != null && image.sprite != null) {
            OverlaySprites.TryGetValue(image.sprite.name, out top);
        }
        if (top == null) {
            if (overlay != null) {
                overlay.enabled = false;
            }
            return;
        }
        if (overlay == null) {
            overlay = MenuKit.MakeImage("Overlay", transform, Color.white);
            overlay.raycastTarget = false;
        }
        Sprite frame = image.sprite;
        float scale = rect.rect.width / frame.rect.width;
        RectTransform o = overlay.rectTransform;
        o.anchorMin = o.anchorMax = new Vector2(frame.pivot.x / frame.rect.width, frame.pivot.y / frame.rect.height);
        o.pivot = new Vector2(top.pivot.x / top.rect.width, top.pivot.y / top.rect.height);
        o.sizeDelta = top.rect.size * scale;
        o.anchoredPosition = Vector2.zero;
        overlay.sprite = top;
        overlay.enabled = true;
    }
}
