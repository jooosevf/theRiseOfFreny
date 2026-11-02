using UnityEngine;
using UnityEngine.UI;

// The Missões do dia page (main menu): today's three missions with their progress bars. The coins
// come the moment a mission is done, in the Arena (DailyMissions), so there is nothing to collect.
public static class MissionsScreen {

    private static readonly Color BarBack = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color BarFill = new Color(0.68f, 0.24f, 0.82f, 1f);
    private static readonly Color BarDone = new Color(0.3f, 0.75f, 0.35f, 1f);

    public static MenuPage Page(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Missões do dia", 700f, new Vector2(0.5f, 0.5f), 6f);
        System.TimeSpan next = DailyMissions.UntilNextDay;
        page.Label("As mesmas para todos os jogadores · só valem corridas manuais da Arena\n" +
                   "Novas missões em " + (next.Hours > 0 ? next.Hours + " h " : "") + next.Minutes + " min (meia-noite de Brasília)");

        DailyMissions.Mission[] today = DailyMissions.Current;
        for (int i = 0; i < today.Length; i++) {
            Entry(page.Content, today[i], DailyMissions.Progress(i), DailyMissions.Done(i));
        }

        page.Label("As moedas entram na hora em que a missão é cumprida.");
        page.Space(4f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void Entry(Transform parent, DailyMissions.Mission m, int progress, bool done) {
        Image row = MenuKit.MakeImage("Mission " + m.id, parent, new Color(1f, 1f, 1f, done ? 0.09f : 0.05f));
        MenuKit.Size(row, -1f, 84f);
        VerticalLayoutGroup column = row.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(14, 14, 8, 10);
        column.spacing = 6f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        Text text = MenuKit.MakeText(row.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleLeft);
        text.supportRichText = true;
        text.text = "<size=" + MenuKit.SmallTextSize + "><color=#" + ColorUtility.ToHtmlStringRGB(MenuKit.TitleColor) + ">" +
                    DailyMissions.TierNames[m.tier] + "</color> · <color=#FFD84A>" + m.Reward + " moedas</color></size>\n" + m.text;
        MenuKit.Size(text, -1f, 40f);

        // the bar, with the numbers over it
        Image back = MenuKit.MakeImage("Bar", row.transform, BarBack);
        MenuKit.Size(back, -1f, 18f);
        Image fill = MenuKit.MakeImage("Fill", back.transform, done ? BarDone : BarFill);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(done ? 1f : Mathf.Clamp01((float)progress / m.target), 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        Text count = MenuKit.MakeText(back.transform, done ? "Cumprida!" : Format(m, progress) + " / " + Format(m, m.target),
            MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
        MenuKit.Stretch(count.rectTransform);
    }

    private static string Format(DailyMissions.Mission m, int value) {
        switch (m.kind) {
            case DailyMissions.Kind.Combo: return (value / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "x";
            case DailyMissions.Kind.Survive: return value / 60 + ":" + (value % 60).ToString("00");
            case DailyMissions.Kind.Score:
            case DailyMissions.Kind.ScoreNoHeal: return value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');   // 20.000
            default: return value.ToString();
        }
    }
}
