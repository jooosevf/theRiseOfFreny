using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// The Títulos and Estatísticas pages, opened from the Conquistas page (main menu and pause menu).
public static class ProfileScreens {

    private const float StatHeight = 26f;

    // ---- Títulos: pick the one that shows next to the name in the ranking ----

    public static MenuPage Titles(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Títulos", 860f, new Vector2(0.5f, 0.5f), 6f);
        page.Label("O título escolhido aparece ao lado do seu nome no ranking do jogo e do site.\n" +
                   "Cada um vem de uma conquista confirmada pelo servidor (corridas manuais da Arena).");

        List<string> ids = new List<string> { "" };
        List<string> names = new List<string> { "Nenhum" };
        foreach (PlayerTitles.Title t in PlayerTitles.All) {
            if (PlayerTitles.Unlocked(t.id) || t.id == PlayerTitles.Chosen) {
                ids.Add(t.id);
                names.Add(t.name);
            }
        }
        int current = Mathf.Max(0, ids.IndexOf(PlayerTitles.Chosen));
        Text preview = null;
        page.Choice("Título", names.ToArray(), current, i => {
            PlayerTitles.Choose(ids[i]);
            preview.text = Preview();
        });
        preview = page.Label(Preview(), MenuKit.SmallTextSize);
        preview.supportRichText = true;

        RectTransform grid = Grid(page.Content, PlayerTitles.All.Length, 410f, 40f);
        foreach (PlayerTitles.Title t in PlayerTitles.All) {
            bool has = PlayerTitles.Unlocked(t.id);
            Text text = Cell(grid, has);
            text.text = "<color=#" + (has ? "FFD84A" : ColorUtility.ToHtmlStringRGB(MenuKit.TitleColor)) + ">" + t.name + "</color>" +
                        (has ? "" : "  <size=" + MenuKit.MenuSize(MenuKit.SmallTextSize - 2) + ">(bloqueado)</size>") +
                        "\n<size=" + MenuKit.MenuSize(MenuKit.SmallTextSize - 2) + ">" + t.how + "</size>";
        }

        page.Space(4f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static string Preview() {
        Online.OnlineServices online = Online.OnlineServices.Instance;
        string name = online != null && online.Save != null ? online.Save.playerName : "Freny";
        string self = online != null && online.Auth != null ? online.Auth.UserId : null;
        string shown = self != null ? PlayerTitles.Decorate(name, self) : name;
        string note = PlayerTitles.Chosen == PlayerTitles.King && !PlayerTitles.IsKing(self) ? "  (aparece quando você for o 1º)" : "";
        return "No ranking: " + shown + note;
    }

    // ---- Estatísticas: what this save has done in the Arena ----

    public static MenuPage Stats(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Estatísticas", 860f, new Vector2(0.5f, 0.5f), 6f);
        page.Label("Tudo o que este save fez na Arena, com o piloto automático também. Vai para a nuvem com o save.");
        AccountStats.Data d = AccountStats.Current;
        SaveData save = SaveSystem.Current;

        List<string[]> rows = new List<string[]> {
            new[] { "Corridas na Arena", d.runs + (d.runs > 0 ? " (" + d.manualRuns + " manuais)" : "") },
            new[] { "Tempo de jogo", Duration(save.playSeconds) },
            new[] { "Corrida mais longa", Duration(Mathf.Max(d.longestRun, save.bestRunSeconds)) },
            new[] { "Melhor combo", (Mathf.Max(10, d.bestComboTenths) / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "x" },
        };
        foreach (ArenaLevel level in System.Enum.GetValues(typeof(ArenaLevel))) {
            int best = AccountStats.BestScore(level);
            rows.Add(new[] { "Recorde no " + ArenaDifficulty.Label(level), best > 0 ? Points(best) : "-" });
        }
        rows.Add(new[] { "Inimigos derrotados", Points(d.kills) });
        for (int i = 0; i < AccountStats.Enemies.GetLength(0); i++) {
            rows.Add(new[] { AccountStats.Enemies[i, 1], Points(AccountStats.Kills(AccountStats.Enemies[i, 0])) });
        }
        rows.Add(new[] { "Acertos no olho do Ciclope", Points(d.eyeHits) });
        rows.Add(new[] { "Curas", Points(d.heals) });

        RectTransform grid = Grid(page.Content, rows.Count, 410f, StatHeight);
        foreach (string[] row in rows) {
            Text label = Cell(grid, true);
            label.text = row[0];
            Text value = MenuKit.MakeText(label.transform.parent, row[1], MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleRight);
            Size(value.rectTransform);
        }

        page.Space(4f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static string Duration(float seconds) {
        int s = Mathf.FloorToInt(seconds);
        if (s >= 3600) return s / 3600 + " h " + (s / 60 % 60) + " min";
        return s / 60 + " min " + (s % 60).ToString("00") + " s";
    }

    private static string Points(int value) {
        return value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
    }

    // ---- two columns, like the Conquistas ----

    private static RectTransform Grid(Transform parent, int count, float width, float height) {
        RectTransform grid = MenuKit.MakeRect("Grid", parent);
        GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.cellSize = new Vector2(width, height);
        cells.spacing = new Vector2(8f, 4f);
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = 2;
        int rows = (count + 1) / 2;
        MenuKit.Size(grid, -1f, rows * height + (rows - 1) * 4f);
        return grid;
    }

    // a cell with its text filling it, left aligned
    private static Text Cell(Transform grid, bool lit) {
        Image cell = MenuKit.MakeImage("Cell", grid, new Color(1f, 1f, 1f, lit ? 0.07f : 0.03f));
        Text text = MenuKit.MakeText(cell.transform, "", MenuKit.SmallTextSize, lit ? MenuKit.TextColor : MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        text.supportRichText = true;
        Size(text.rectTransform);
        return text;
    }

    private static void Size(RectTransform rect) {
        MenuKit.Stretch(rect);
        rect.offsetMin = new Vector2(10f, 2f);
        rect.offsetMax = new Vector2(-10f, -2f);
    }
}
