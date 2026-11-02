using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A part of a menu page that scrolls instead of pushing the page past the screen: as tall as its
// content while the page fits the canvas, cut down to what is left when it doesn't. Mouse wheel,
// drag and its bar; a selected row inside it is kept in view; and with Drive, up and down on a row
// outside it (the Guia's topic picker) scroll it, moving on to the next row only at the end.
public class MenuScroll : MonoBehaviour {

    private const float BarWidth = 12f;
    private const float ScreenMargin = 24f;   // what stays free above and below the page
    private const float MinHeight = 100f;
    private const float KeySpeed = 420f;      // units per second

    private LayoutElement area;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect scroll;
    private RectTransform panel;
    private RectTransform canvasRect;
    private Selectable driver, next;
    private GameObject lastSelected;
    private float lastAxis;

    public static MenuScroll Create(MenuPage page, out RectTransform content) {
        Image root = MenuKit.MakeImage("Scroll", page.Content, Color.clear);
        MenuScroll menu = root.gameObject.AddComponent<MenuScroll>();
        menu.panel = page.Panel;
        menu.area = MenuKit.Size(root, -1f, MinHeight);

        RectTransform viewport = MenuKit.MakeRect("Viewport", root.transform);
        MenuKit.Stretch(viewport);
        viewport.offsetMax = new Vector2(-(BarWidth + 6f), 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        menu.viewport = viewport;

        content = MenuKit.MakeRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup column = content.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        menu.content = content;

        Image track = MenuKit.MakeImage("Bar", root.transform, new Color(0.03f, 0.02f, 0.06f, 1f));
        RectTransform bar = track.rectTransform;
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(BarWidth, 0f);
        RectTransform sliding = MenuKit.MakeRect("Sliding", bar);
        MenuKit.Stretch(sliding);
        Image handle = MenuKit.MakeImage("Handle", sliding, MenuKit.TitleColor);
        MenuKit.Stretch(handle.rectTransform);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

        menu.scroll = root.gameObject.AddComponent<ScrollRect>();
        menu.scroll.viewport = viewport;
        menu.scroll.content = content;
        menu.scroll.horizontal = false;
        menu.scroll.movementType = ScrollRect.MovementType.Clamped;
        menu.scroll.scrollSensitivity = 40f;
        menu.scroll.verticalScrollbar = scrollbar;
        menu.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return menu;
    }

    // up and down on `row` scroll this; past the end they go to `nextRow`
    public void Drive(Selectable row, Selectable nextRow) {
        driver = row;
        next = nextRow;
    }

    public void ToTop() {
        content.anchoredPosition = Vector2.zero;
        scroll.velocity = Vector2.zero;
    }

    private void Start() {
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        if (driver != null) {
            // MenuStack linked the page's rows when it took the page: this row's up and down are ours
            Navigation nav = driver.navigation;
            nav.selectOnUp = null;
            nav.selectOnDown = null;
            driver.navigation = nav;
        }
    }

    private void Update() {
        Fit();
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        float axis = Input.GetAxisRaw("Vertical");
        if (selected != lastSelected) {
            lastSelected = selected;
            lastAxis = axis;
            if (selected != null && selected.transform.IsChildOf(content)) {
                KeepInView((RectTransform)selected.transform);
            }
            return;
        }
        if (driver != null && selected == driver.gameObject) {
            Drive(axis);
        }
        lastAxis = axis;
    }

    private void Fit() {
        if (canvasRect == null) {
            return;
        }
        float room = canvasRect.rect.height - ScreenMargin * 2f - (panel.rect.height - area.preferredHeight);
        float height = Mathf.Max(MinHeight, Mathf.Min(content.rect.height, room));
        if (Mathf.Abs(height - area.preferredHeight) > 0.5f) {
            MenuKit.Size(area, -1f, height);
        }
    }

    private float Overflow => Mathf.Max(0f, content.rect.height - viewport.rect.height);

    private void Move(float y) {
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, Overflow));
        scroll.velocity = Vector2.zero;
    }

    private void Drive(float axis) {
        if (Mathf.Abs(axis) < 0.5f) {
            return;
        }
        float y = content.anchoredPosition.y;
        bool atEnd = axis < 0f ? y >= Overflow - 0.5f : y <= 0.5f;
        if (atEnd) {
            if (Mathf.Abs(lastAxis) < 0.5f && next != null && EventSystem.current != null) {
                EventSystem.current.SetSelectedGameObject(next.gameObject);   // a fresh press, not the same one held
            }
            return;
        }
        Move(y - axis * KeySpeed * Time.unscaledDeltaTime);
    }

    private void KeepInView(RectTransform row) {
        Vector3[] corners = new Vector3[4];
        row.GetWorldCorners(corners);
        float bottom = viewport.InverseTransformPoint(corners[0]).y;
        float top = viewport.InverseTransformPoint(corners[1]).y;
        Rect view = viewport.rect;
        float y = content.anchoredPosition.y;
        if (top > view.yMax) {
            Move(y - (top - view.yMax));
        } else if (bottom < view.yMin) {
            Move(y + (view.yMin - bottom));
        }
    }
}
