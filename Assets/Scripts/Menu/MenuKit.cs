using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the menu screens in code with uGUI, so they need no prefabs or scene edits and work
// the same with mouse, touch, keyboard and gamepad (uGUI navigation: arrows / stick / d-pad,
// Submit = Enter / A, Cancel = Esc / B).
public static class MenuKit {

    public static readonly Color PanelColor = new Color(0.07f, 0.04f, 0.13f, 0.94f);
    public static readonly Color ButtonColor = new Color(0.20f, 0.12f, 0.33f, 1f);
    public static readonly Color SelectedColor = new Color(0.68f, 0.24f, 0.82f, 1f);
    public static readonly Color PressedColor = new Color(0.90f, 0.55f, 1f, 1f);
    public static readonly Color DisabledColor = new Color(0.14f, 0.10f, 0.20f, 0.6f);
    public static readonly Color TextColor = new Color(0.97f, 0.93f, 1f, 1f);
    public static readonly Color DimTextColor = new Color(0.70f, 0.60f, 0.82f, 1f);
    public static readonly Color TitleColor = new Color(0.93f, 0.56f, 0.99f, 1f);
    public static readonly Color ShadeColor = new Color(0.02f, 0.01f, 0.05f, 0.65f);

    public const int TextSize = 16;
    public const int SmallTextSize = 12;
    public const int TitleSize = 28;
    public const float RowHeight = 44f;

    // José, 2026-11-05: menus read Jersey 10, page titles Jacquard 12. Font stays Press Start 2P,
    // for everything seen during a run (score, combo, banners, numbers): see MakeCombatText
    private const float MenuFontScale = 1.5f;   // both draw smaller than Press Start at the same size
    private static Font font, bodyFont, titleFont;

    public static Font Font {
        get {
            if (font == null) {
                font = Resources.Load<Font>("Fonts/PressStart2P");
            }
            if (font == null) {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }
    }

    public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent) {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) {
            obj.transform.SetParent(parent, false);
        }
        Canvas canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // the scene's own EventSystem when it has one, otherwise a new one that dies with the scene
    public static void EnsureEventSystem() {
        if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) {
            return;
        }
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public static RectTransform MakeRect(string name, Transform parent) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    public static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Image MakeImage(string name, Transform parent, Color color) {
        RectTransform rect = MakeRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Font BodyFont => bodyFont != null ? bodyFont : bodyFont = Resources.Load<Font>("Fonts/Jersey10-Regular") ?? Font;
    public static Font TitleFont => titleFont != null ? titleFont : titleFont = Resources.Load<Font>("Fonts/Jacquard12-Regular") ?? Font;

    // the real font size of a menu text written at the sizes above
    public static int MenuSize(int size) {
        return Mathf.RoundToInt(size * MenuFontScale);
    }

    // a text seen during a run, in the combat font, at exactly this size
    public static Text MakeCombatText(Transform parent, string text, int size, Color color, TextAnchor anchor) {
        Text label = MakeText(parent, text, size, color, anchor);
        label.font = Font;
        label.fontSize = size;
        return label;
    }

    public static Text MakeText(Transform parent, string text, int size, Color color, TextAnchor anchor) {
        RectTransform rect = MakeRect("Text", parent);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = size >= TitleSize ? TitleFont : BodyFont;
        label.fontSize = MenuSize(size);
        label.color = color;
        label.alignment = anchor;
        label.text = text;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.lineSpacing = 1.4f;
        return label;
    }

    public static ColorBlock Colors() {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = SelectedColor;
        colors.selectedColor = SelectedColor;
        colors.pressedColor = PressedColor;
        colors.disabledColor = DisabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        return colors;
    }

    public static LayoutElement Size(Component component, float width, float height) {
        LayoutElement layout = component.gameObject.GetComponent<LayoutElement>();
        if (layout == null) {
            layout = component.gameObject.AddComponent<LayoutElement>();
        }
        if (width >= 0f) {
            layout.preferredWidth = width;
            layout.minWidth = width;
        }
        if (height >= 0f) {
            layout.preferredHeight = height;
            layout.minHeight = height;
        }
        return layout;
    }

    public static Button MakeButton(Transform parent, string label, Action onClick, int textSize = TextSize) {
        Image background = MakeImage("Button", parent, Color.white);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.colors = Colors();
        button.onClick.AddListener(() => Sfx.Ui("ui_select"));
        if (onClick != null) {
            button.onClick.AddListener(() => onClick());
        }
        Text text = MakeText(background.transform, label, textSize, TextColor, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(10f, 0f);
        text.rectTransform.offsetMax = new Vector2(-10f, 0f);
        return button;
    }

    // a ">" at the end of a button that opens another page, so it reads as a thing to click even
    // when it came selected
    public static void Opens(Button button) {
        Text arrow = MakeText(button.transform, ">", TextSize, TextColor, TextAnchor.MiddleRight);
        Stretch(arrow.rectTransform);
        arrow.rectTransform.offsetMax = new Vector2(-14f, 0f);
    }

    // a row with the label on the left and a control on the right; the row lights up when selected
    public static Image MakeRow(Transform parent, string label, out Text labelText) {
        Image row = MakeImage("Row", parent, Color.white);
        Size(row, -1f, RowHeight);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 6, 6);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        labelText = MakeText(row.transform, label, TextSize, TextColor, TextAnchor.MiddleLeft);
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Size(labelText, -1f, -1f).flexibleWidth = 1f;
        return row;
    }

    public static Slider MakeSlider(Transform parent, string label, int min, int max, int value,
                                Func<int, string> format, Action<int> onChange) {
        Image row = MakeRow(parent, label, out _);

        RectTransform area = MakeRect("Slider", row.transform);
        Size(area, 220f, -1f);
        Slider slider = area.gameObject.AddComponent<Slider>();

        Image track = MakeImage("Track", area, new Color(0.03f, 0.02f, 0.06f, 1f));
        Stretch(track.rectTransform);
        track.rectTransform.offsetMin = new Vector2(0f, 10f);
        track.rectTransform.offsetMax = new Vector2(0f, -10f);

        RectTransform fillArea = MakeRect("Fill Area", area);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(0f, 10f);
        fillArea.offsetMax = new Vector2(0f, -10f);
        Image fill = MakeImage("Fill", fillArea, TitleColor);
        fill.rectTransform.sizeDelta = Vector2.zero;

        RectTransform handleArea = MakeRect("Handle Area", area);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(6f, 0f);
        handleArea.offsetMax = new Vector2(-6f, 0f);
        Image handle = MakeImage("Handle", handleArea, TextColor);
        handle.rectTransform.sizeDelta = new Vector2(12f, 0f);

        Text valueText = MakeText(row.transform, format(value), TextSize, TextColor, TextAnchor.MiddleRight);
        Size(valueText, 96f, -1f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = value;
        slider.targetGraphic = row;
        slider.colors = Colors();
        slider.onValueChanged.AddListener(v => {
            int whole = Mathf.RoundToInt(v);
            valueText.text = format(whole);
            Sfx.Ui("ui_move");
            onChange(whole);
        });
        return slider;
    }

    // a text box (name, email, password) on the right of a labelled row; Submit / click starts typing
    public static InputField MakeInput(Transform parent, string label, string value, bool password, int maxLength) {
        Image row = MakeRow(parent, label, out _);
        Image box = MakeImage("Input", row.transform, new Color(0.03f, 0.02f, 0.06f, 1f));
        Size(box, 300f, -1f);
        Text text = MakeText(box.transform, "", SmallTextSize, TextColor, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(8f, 0f);
        text.rectTransform.offsetMax = new Vector2(-8f, 0f);
        InputField input = box.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.targetGraphic = row;
        input.colors = Colors();
        input.characterLimit = maxLength;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        input.caretColor = TitleColor;
        input.selectionColor = new Color(0.68f, 0.24f, 0.82f, 0.5f);
        input.text = value ?? "";
        return input;
    }

    public static MenuChoice MakeChoice(Transform parent, string label, string[] options, int index, Action<int> onChange) {
        Image row = MakeRow(parent, label, out _);
        Text valueText = MakeText(row.transform, "", TextSize, TextColor, TextAnchor.MiddleRight);
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Size(valueText, 260f, -1f);
        MenuChoice choice = row.gameObject.AddComponent<MenuChoice>();
        choice.targetGraphic = row;
        choice.colors = Colors();
        choice.Init(options, index, valueText, onChange);
        return choice;
    }

    // up/down walk the rows (wrapping), left/right walk inside a row
    public static void LinkNavigation(List<Selectable[]> rows) {
        for (int r = 0; r < rows.Count; r++) {
            Selectable[] row = rows[r];
            Selectable[] up = rows[(r - 1 + rows.Count) % rows.Count];
            Selectable[] down = rows[(r + 1) % rows.Count];
            for (int c = 0; c < row.Length; c++) {
                Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = up[Mathf.Min(c, up.Length - 1)];
                nav.selectOnDown = down[Mathf.Min(c, down.Length - 1)];
                nav.selectOnLeft = c > 0 ? row[c - 1] : null;
                nav.selectOnRight = c < row.Length - 1 ? row[c + 1] : null;
                row[c].navigation = nav;
            }
        }
    }
}
