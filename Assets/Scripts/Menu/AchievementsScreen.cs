using UnityEngine;
using UnityEngine.UI;

// The Conquistas page (main menu and pause menu): the achievements in two columns, scrolling, each with
// its icon (dark until earned), name, what to do and whether the ranking server confirmed it. The ones
// earned since the page was last opened say "NOVA!" (the run's banner only shows the icon and the name).
public static class AchievementsScreen {

    public static readonly Color Earned = new Color(1f, 0.85f, 0.29f, 1f);
    private static readonly Color LockedIcon = new Color(0.16f, 0.13f, 0.2f, 1f);

    public static MenuPage Page(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Conquistas", 940f, new Vector2(0.5f, 0.5f), 6f);
        page.Label(Achievements.Count + " de " + Achievements.All.Length +
                   " · só valem corridas manuais da Arena (sem o piloto automático)");

        MenuScroll.Create(page, out RectTransform list);
        page.Content = list;
        RectTransform grid = MenuKit.MakeRect("Grid", page.Content);
        GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.cellSize = new Vector2(438f, 78f);
        cells.spacing = new Vector2(8f, 6f);
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = 2;
        int rows = (Achievements.All.Length + 1) / 2;
        MenuKit.Size(grid, -1f, rows * 78f + (rows - 1) * 6f);
        foreach (Achievements.Info info in Achievements.All) {
            Entry(grid, info, Achievements.IsNew(info.id));
        }
        // seen now: the button goes back to "Conquistas"
        Achievements.MarkSeen();
        page.Content = page.Panel;

        page.Space(4f);
        // the titles come from the achievements; the account's stats are on the Perfil
        page.ButtonRow("", new[] { 300f, 300f }, new[] { "Títulos", "Perfil" }, i =>
            stack.Push(i == 0 ? ProfileScreens.Titles(stack) : PerfilScreens.Page(stack)));
        page.Button("Voltar", stack.Back);
        return page;
    }

    // the Conquistas button of the menus: "(nova!)" in gold while there is a new one to see
    public static void Badge(Button button) {
        LiveLabel.Attach(button, () => Achievements.ButtonLabel, () => Achievements.NewCount > 0);
    }

    private static void Entry(Transform parent, Achievements.Info info, bool fresh) {
        bool has = Achievements.Has(info.id);
        Image row = MenuKit.MakeImage("Entry " + info.key, parent,
            fresh ? new Color(1f, 0.85f, 0.29f, 0.07f) : new Color(1f, 1f, 1f, has ? 0.09f : 0.04f));
        if (fresh) {
            Outline edge = row.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.85f, 0.64f, 0.16f, 1f);
            edge.effectDistance = new Vector2(2f, -2f);
        }
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 10, 6, 6);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // locked: the badge in dark, so its shape shows but not the picture
        Image icon = MenuKit.MakeImage("Icon", row.transform, has ? Color.white : LockedIcon);
        icon.sprite = Achievements.Icon(info.id);
        icon.preserveAspect = true;
        MenuKit.Size(icon, 64f, 64f);

        // NOVA! on the status line: beside a long name it went to a line of its own
        string status = !has ? "" : (fresh ? "\n<color=#FFD84A>NOVA!</color> · " : "\n") + (Achievements.Confirmed(info.id)
            ? "<color=#7CFC8A>Conquistada · confirmada online</color>"
            : "<color=#FFD84A>Conquistada</color>");
        Text text = MenuKit.MakeText(row.transform, "", MenuKit.SmallTextSize - 2, MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        text.supportRichText = true;
        text.lineSpacing = 1.25f;
        text.text = "<size=" + MenuKit.MenuSize(MenuKit.SmallTextSize) + "><color=#" +
                    ColorUtility.ToHtmlStringRGB(has ? Earned : MenuKit.TitleColor) + ">" + info.name + "</color></size>\n" +
                    info.how + status;
        MenuKit.Size(text, -1f, -1f).flexibleWidth = 1f;
    }
}
