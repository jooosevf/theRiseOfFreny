using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One screen of a menu: a panel with a title and a column of buttons, sliders and pickers.
public class MenuPage {

    public readonly RectTransform Root;      // full screen, holds the panel
    public readonly RectTransform Panel;
    public RectTransform Content;            // the column; what is added goes here (a MenuScroll's content for a while)
    public GameObject LastSelected;

    private readonly List<Selectable[]> rows = new List<Selectable[]>();

    public MenuPage(Transform parent, string title, float width, Vector2 anchor, float spacing = 10f) {
        Root = MenuKit.MakeRect("Page " + title, parent);
        MenuKit.Stretch(Root);

        Image panel = MenuKit.MakeImage("Panel", Root, MenuKit.PanelColor);
        Panel = panel.rectTransform;
        Panel.anchorMin = anchor;
        Panel.anchorMax = anchor;
        Panel.pivot = anchor;
        Panel.anchoredPosition = new Vector2(anchor.x < 0.5f ? 60f : anchor.x > 0.5f ? -60f : 0f, 0f);
        Panel.sizeDelta = new Vector2(width, 0f);
        VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(28, 28, 26, 28);
        column.spacing = spacing;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Content = Panel;

        if (!string.IsNullOrEmpty(title)) {
            Text titleText = MenuKit.MakeText(Content, title, MenuKit.TitleSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
            MenuKit.Size(titleText, -1f, 56f);
        }
    }

    public bool Visible {
        get => Root.gameObject.activeSelf;
        set => Root.gameObject.SetActive(value);
    }

    public Selectable SelectFirst;   // what is selected when the page opens (default: the first row)

    // run each time the page shows again, after the page above it closes (MenuStack.Back doesn't
    // rebuild it): e.g. the Armeira's shelves after buying on one of them
    public Action OnShow;

    public Selectable First => SelectFirst != null ? SelectFirst : rows.Count > 0 ? rows[0][0] : null;

    public Button Button(string label, Action onClick, bool interactable = true, float height = MenuKit.RowHeight) {
        Button button = MenuKit.MakeButton(Content, label, onClick);
        MenuKit.Size(button, -1f, height);
        button.interactable = interactable;
        if (interactable) {
            rows.Add(new Selectable[] { button });
        }
        return button;
    }

    // a taller button with a second, smaller line (save slots)
    public Button BigButton(string label, string detail, Action onClick, bool interactable = true) {
        Button button = MenuKit.MakeButton(Content, label, onClick);
        MenuKit.Size(button, -1f, 64f);
        Text text = button.GetComponentInChildren<Text>();
        text.supportRichText = true;
        text.text = label + "\n<size=" + MenuKit.SmallTextSize + ">" + detail + "</size>";
        button.interactable = interactable;
        if (interactable) {
            rows.Add(new Selectable[] { button });
        }
        return button;
    }

    public Slider Slider(string label, int min, int max, int value, Func<int, string> format, Action<int> onChange) {
        Slider slider = MenuKit.MakeSlider(Content, label, min, max, value, format, onChange);
        rows.Add(new Selectable[] { slider });
        return slider;
    }

    public InputField Input(string label, string value, bool password = false, int maxLength = 64) {
        InputField input = MenuKit.MakeInput(Content, label, value, password, maxLength);
        rows.Add(new Selectable[] { input });
        return input;
    }

    public MenuChoice Choice(string label, string[] options, int index, Action<int> onChange) {
        MenuChoice choice = MenuKit.MakeChoice(Content, label, options, index, onChange);
        rows.Add(new Selectable[] { choice });
        return choice;
    }

    public Text Label(string text, int size = MenuKit.SmallTextSize, TextAnchor anchor = TextAnchor.MiddleCenter) {
        Text label = MenuKit.MakeText(Content, text, size, MenuKit.DimTextColor, anchor);
        return label;
    }

    // a row with a label and several buttons side by side (controls screen)
    public Button[] ButtonRow(string label, float[] widths, string[] texts, Action<int> onClick, float height = 40f) {
        Image row = MenuKit.MakeRow(Content, label, out Text labelText);
        bool bare = string.IsNullOrEmpty(label);
        row.color = new Color(1f, 1f, 1f, bare ? 0f : 0.06f);
        MenuKit.Size(row, -1f, height);
        if (bare) {
            // no label: nothing beside the buttons, centred on the page under the full-width rows
            labelText.gameObject.SetActive(false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
        }
        Button[] buttons = new Button[texts.Length];
        for (int i = 0; i < texts.Length; i++) {
            int index = i;
            buttons[i] = MenuKit.MakeButton(row.transform, texts[i], () => onClick(index), MenuKit.SmallTextSize);
            MenuKit.Size(buttons[i], widths[i], -1f);
        }
        rows.Add(buttons);
        return buttons;
    }

    public void Space(float height = 8f) {
        RectTransform space = MenuKit.MakeRect("Space", Content);
        MenuKit.Size(space, -1f, height);
    }

    public void Finish() {
        if (rows.Count > 0) {
            MenuKit.LinkNavigation(rows);
        }
    }
}

// The open pages of one menu, top one visible. Cancel (Esc / B) goes back one page; on the
// first page it calls OnRootCancel. Keeps something selected so a gamepad can always move.
public class MenuStack : MonoBehaviour {

    public Action OnRootCancel;
    public Func<bool> ExtraCancel;   // e.g. the pause button also closes the pause menu
    public bool Busy;                // waiting for a key to rebind: no navigation, no cancel
    public bool ShadeSubPages;       // darken the scene behind every page but the first

    private readonly List<MenuPage> pages = new List<MenuPage>();
    private int shownFrame = -1;
    private GameObject lastSelected;   // to tick when the selection moves

    public MenuPage Top => pages.Count > 0 ? pages[pages.Count - 1] : null;
    public int Depth => pages.Count;

    public void Push(MenuPage page) {
        if (Top != null) {
            Top.LastSelected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            Top.Visible = false;
        }
        page.Finish();
        if (ShadeSubPages && pages.Count > 0) {
            Image shade = MenuKit.MakeImage("Shade", page.Root, MenuKit.ShadeColor);
            MenuKit.Stretch(shade.rectTransform);
            shade.transform.SetAsFirstSibling();
        }
        pages.Add(page);
        Show(page, page.First ? page.First.gameObject : null);
    }

    // replaces the whole stack with one page
    public void ResetTo(MenuPage page) {
        Clear();
        Push(page);
    }

    // swaps the top page for a fresh one (e.g. the account page after signing in)
    public void ReplaceTop(MenuPage page) {
        if (Top != null) {
            MenuPage top = Top;
            pages.RemoveAt(pages.Count - 1);
            Destroy(top.Root.gameObject);
        }
        Push(page);
    }

    public void Back() {
        Sfx.Ui("ui_back");
        if (pages.Count <= 1) {
            OnRootCancel?.Invoke();
            return;
        }
        MenuPage top = Top;
        pages.RemoveAt(pages.Count - 1);
        Destroy(top.Root.gameObject);
        Show(Top, Top.LastSelected ? Top.LastSelected : Top.First ? Top.First.gameObject : null);
    }

    public void Clear() {
        foreach (MenuPage page in pages) {
            Destroy(page.Root.gameObject);
        }
        pages.Clear();
    }

    private void Show(MenuPage page, GameObject select) {
        page.Visible = true;
        page.OnShow?.Invoke();
        shownFrame = Time.frameCount;
        MenuKit.EnsureEventSystem();
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(select);
        }
    }

    private void Update() {
        if (Top == null) {
            return;
        }
        EventSystem events = EventSystem.current;
        if (events != null) {
            events.sendNavigationEvents = !Busy;
        }
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != lastSelected) {
            if (selected != null && lastSelected != null && Time.frameCount != shownFrame) {
                Sfx.Ui("ui_move");
            }
            lastSelected = selected;
        }
        if (Busy || Time.frameCount == shownFrame) {
            return;
        }

        // typing in a text box: Esc only leaves the box (InputField does that), letters are text
        InputField typing = selected != null ? selected.GetComponent<InputField>() : null;
        if (typing != null && typing.isFocused) {
            return;
        }

        if (Input.GetButtonDown("Cancel") || (ExtraCancel != null && ExtraCancel())) {
            Back();
            return;
        }

        // the mouse can leave nothing selected; the first key or stick push selects again
        if (events != null && (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy)) {
            bool moved = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f ||
                         Input.GetButtonDown("Submit");
            if (moved && Top.First != null) {
                events.SetSelectedGameObject(Top.LastSelected && Top.LastSelected.activeInHierarchy ? Top.LastSelected : Top.First.gameObject);
            }
        }
    }
}
