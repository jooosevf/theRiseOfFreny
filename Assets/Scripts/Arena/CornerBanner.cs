using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The small notice at the top right, under the coins (José, 2026-11-03: the big one in the middle
// got in the way of the game; 2026-11-05: the same for the Missões do dia). Icon and a line or two
// of text, one at a time: a Conquista and a Missão that come at the same moment show one after the
// other, never on top of each other. Made the first time something is shown.
public class CornerBanner : MonoBehaviour {

    private const float BannerSeconds = 3.5f;
    private const float BannerHeight = 40f;                         // the least; two lines make it taller
    private const float Gap = 18f;                                  // between the coins and the banner
    private static readonly Vector2 Fallback = new Vector2(-28f, -98f);   // no coins on screen

    private struct Notice {
        public Sprite icon;
        public string text;
        public string sound;
    }

    private static CornerBanner instance;

    private readonly Queue<Notice> waiting = new Queue<Notice>();
    private Canvas canvas;
    private RectTransform panel;
    private Image icon;
    private Text title;
    private float shownUntil;
    private Vector2 spot;
    private bool placed;

    // text may carry rich text colors; sound is an Sfx name, played when the notice comes on screen
    public static void Show(Sprite icon, string text, string sound) {
        if (instance == null) {
            GameObject go = new GameObject("CornerBanner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CornerBanner>();
        }
        instance.waiting.Enqueue(new Notice { icon = icon, text = text, sound = sound });
    }

    private void Update() {
        if (canvas != null && GameMenus.Paused) {
            // not behind the pause menu: it waits and shows its full time after
            canvas.gameObject.SetActive(false);
            if (shownUntil > Time.unscaledTime) shownUntil += Time.unscaledDeltaTime;
            return;
        }
        if (canvas != null && Time.unscaledTime < shownUntil) {
            canvas.gameObject.SetActive(true);
            if (!placed) Place();
            float t = shownUntil - Time.unscaledTime;
            // slides in from the right edge and back out at the end
            float slide = Mathf.Clamp01(Mathf.Min(BannerSeconds - t, t) * 5f);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(panel.sizeDelta.x + 40f, spot.x, slide), spot.y);
            return;
        }
        if (waiting.Count == 0) {
            if (canvas != null) canvas.gameObject.SetActive(false);
            return;
        }
        Notice next = waiting.Dequeue();
        Build();
        canvas.gameObject.SetActive(true);
        icon.sprite = next.icon;
        icon.enabled = next.icon != null;
        title.text = next.text;
        placed = false;
        panel.anchoredPosition = new Vector2(Screen.width, 0f);     // out of sight until Place
        shownUntil = Time.unscaledTime + BannerSeconds;
        Sfx.Play(next.sound);
    }

    // a frame after the text is set: a canvas made this frame still has scaleFactor 1, which put the
    // first notice of the session in the wrong place
    private void Place() {
        placed = true;
        float height = Mathf.Max(BannerHeight, Mathf.Ceil(title.preferredHeight) + 12f);
        panel.sizeDelta = new Vector2(Mathf.Ceil(title.preferredWidth) + BannerHeight + 22f, height);
        spot = UnderCoins();
    }

    // the top right corner of the banner: right under the HUD's coin and lined up with it, in this
    // canvas' units from the screen's top right corner
    private Vector2 UnderCoins() {
        UIManager ui = UIManager.Instance;
        Text coins = ui != null ? ui.coinCountHUD : null;
        if (coins == null || !coins.isActiveAndEnabled || coins.transform.parent == null) {
            return Fallback;
        }
        Canvas hud = coins.canvas != null ? coins.canvas.rootCanvas : null;
        Camera cam = hud != null && hud.renderMode != RenderMode.ScreenSpaceOverlay ? hud.worldCamera : null;
        // the coin icon beside the number has a rect that fits it, unlike the number's text
        float right = float.MinValue, bottom = float.MaxValue;
        Vector3[] corners = new Vector3[4];
        foreach (Image image in coins.transform.parent.GetComponentsInChildren<Image>()) {
            if (!image.isActiveAndEnabled) continue;
            image.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++) {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                right = Mathf.Max(right, p.x);
                bottom = Mathf.Min(bottom, p.y);
            }
        }
        if (right == float.MinValue) {
            return Fallback;
        }
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 at = new Vector2((right - Screen.width) / scale, (bottom - Screen.height) / scale - Gap);
        // something odd (a HUD off the screen): the usual place
        return at.x > 0f || at.x < -400f || at.y > 0f || at.y < -300f ? Fallback : at;
    }

    private void Build() {
        if (canvas != null) {
            return;
        }
        canvas = MenuKit.CreateCanvas("Corner Banner", 25000, transform);
        Image back = MenuKit.MakeImage("Panel", canvas.transform, new Color(0.08f, 0.05f, 0.11f, 0.88f));
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        Outline edge = back.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.85f, 0.64f, 0.16f, 1f);
        edge.effectDistance = new Vector2(2f, -2f);
        back.raycastTarget = false;

        icon = MenuKit.MakeImage("Icon", panel, Color.white);
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        iconRect.sizeDelta = new Vector2(BannerHeight - 8f, BannerHeight - 8f);

        title = MenuKit.MakeCombatText(panel, "", MenuKit.SmallTextSize, AchievementsScreen.Earned, TextAnchor.MiddleLeft);
        title.supportRichText = true;
        title.raycastTarget = false;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform textRect = title.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(BannerHeight + 4f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
    }
}
