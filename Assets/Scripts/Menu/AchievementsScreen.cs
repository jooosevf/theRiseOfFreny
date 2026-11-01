using UnityEngine;
using UnityEngine.UI;

// The Conquistas page (main menu and pause menu): the ten achievements in two columns, each with
// its icon (dark until earned), name, what to do and whether the ranking server confirmed it.
public static class AchievementsScreen {

    private static readonly Color Earned = new Color(1f, 0.85f, 0.29f, 1f);
    private static readonly Color LockedIcon = new Color(0.16f, 0.13f, 0.2f, 1f);

    public static MenuPage Page(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Conquistas", 940f, new Vector2(0.5f, 0.5f), 6f);
        page.Label(Achievements.Count + " de " + Achievements.All.Length +
                   " · só valem corridas manuais da Arena (sem o piloto automático)");

        RectTransform grid = MenuKit.MakeRect("Grid", page.Content);
        GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.cellSize = new Vector2(438f, 78f);
        cells.spacing = new Vector2(8f, 6f);
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = 2;
        int rows = (Achievements.All.Length + 1) / 2;
        MenuKit.Size(grid, -1f, rows * 78f + (rows - 1) * 6f);
        foreach (Achievements.Info info in Achievements.All) {
            Entry(grid, info);
        }

        page.Space(4f);
        // the titles come from the achievements; the stats sit next to them
        page.ButtonRow("", new[] { 300f, 300f }, new[] { "Títulos", "Estatísticas" }, i =>
            stack.Push(i == 0 ? ProfileScreens.Titles(stack) : ProfileScreens.Stats(stack)));
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void Entry(Transform parent, Achievements.Info info) {
        bool has = Achievements.Has(info.id);
        Image row = MenuKit.MakeImage("Entry " + info.key, parent, new Color(1f, 1f, 1f, has ? 0.09f : 0.04f));
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

        string status = !has ? "" : Achievements.Confirmed(info.id)
            ? "\n<color=#7CFC8A>Conquistada · confirmada online</color>"
            : "\n<color=#FFD84A>Conquistada</color>";
        Text text = MenuKit.MakeText(row.transform, "", MenuKit.SmallTextSize - 2, MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        text.supportRichText = true;
        text.lineSpacing = 1.25f;
        text.text = "<size=" + MenuKit.SmallTextSize + "><color=#" +
                    ColorUtility.ToHtmlStringRGB(has ? Earned : MenuKit.TitleColor) + ">" + info.name + "</color></size>\n" +
                    info.how + status;
        MenuKit.Size(text, -1f, -1f).flexibleWidth = 1f;
    }
}
