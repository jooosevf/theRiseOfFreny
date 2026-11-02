using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Online;

// The Perfil (José, 2026-11-02), from the main menu and the pause menu: Freny as he is dressed, the
// nickname and the code, and everything the account has done (PlayerStats), on four tabs so it fits
// a 16:9 screen and a phone lying down. Also the Amigos page and a friend's page, side by side.
public static class PerfilScreens {

    private const float PageWidth = 1040f;
    private const float RowHeight = 28f;
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
    private static readonly string[] Tabs = { "Resumo", "Inimigos", "Armas", "Mortes" };
    private static int lastTab;

    private static OnlineServices Online => OnlineServices.Instance;

    // "Perfil", or "Perfil (2)" with friend requests waiting
    public static string ButtonLabel {
        get {
            FriendsService friends = Online != null ? Online.Friends : null;
            return friends != null && friends.Incoming.Count > 0 ? "Perfil (" + friends.Incoming.Count + ")" : "Perfil";
        }
    }

    // ---- the page ----

    public static MenuPage Page(MenuStack stack) {
        OnlineServices online = Online;
        MenuPage page = new MenuPage(stack.transform, "Perfil", PageWidth, Middle, 5f);
        bool account = online != null && online.Auth != null && online.Auth.IsSignedIn && !online.Auth.IsAnonymous;
        string name = account && online.Profiles.HasNick ? online.Profiles.Nick : online != null ? online.Save.playerName : "Freny";

        string line = account
            ? (online.Profiles.HasNick ? "Código de amigo: " + online.Profiles.Code : "Escolha um apelido na Conta")
            : "Convidado: crie uma conta para salvar no ranking";
        string since = PlayerStats.CreatedAt != null ? "\nJogando desde " + PlayerStats.CreatedAt.Value.AddHours(-3).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";
        Head(page, name, line + since, FrenyLook.WornId(LookSlot.Helmet), FrenyLook.WornId(LookSlot.Cape), FrenyLook.WornId(LookSlot.Color));

        RectTransform holder = MenuKit.MakeRect("Stats", page.Content);
        MenuKit.Size(holder, -1f, 9 * RowHeight);
        page.Choice("Mostrar", Tabs, lastTab, i => {
            lastTab = i;
            Fill(holder, i);
        }).transform.SetSiblingIndex(holder.GetSiblingIndex());
        Fill(holder, lastTab);

        Button[] row = page.ButtonRow("", new[] { 310f, 310f, 310f }, new[] { "Amigos", "Conta", "Ver meu perfil no site" }, i => {
            if (i == 0) stack.Push(Friends(stack));
            else if (i == 1) stack.Push(AccountScreens.Account(stack));
            else Application.OpenURL(online.Profiles.SiteLink);
        });
        row[0].interactable = account;
        row[2].interactable = account && online.Profiles.HasNick;
        page.Button("Voltar", stack.Back);
        return page;
    }

    // Freny on the left, the name and a line of text on the right
    private static void Head(MenuPage page, string name, string text, string helmet, string cape, string color) {
        RectTransform head = MenuKit.MakeRect("Head", page.Content);
        MenuKit.Size(head, -1f, 110f);
        HorizontalLayoutGroup layout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        LookBox(head, 110f, helmet, cape, color);
        Text label = MenuKit.MakeText(head, "", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        label.supportRichText = true;
        label.text = "<size=" + MenuKit.TextSize + "><color=#" + ColorUtility.ToHtmlStringRGB(MenuKit.TitleColor) + ">" + name + "</color></size>\n" + text;
        MenuKit.Size(label, -1f, -1f).flexibleWidth = 1f;
    }

    private static void LookBox(Transform parent, float size, string helmet, string cape, string color) {
        RectTransform box = MenuKit.MakeRect("Look", parent);
        MenuKit.Size(box, size, size);
        FrenyLookPreview preview = FrenyLookPreview.Create(box, size);
        preview.Show(FrenyLook.Find(helmet), FrenyLook.Find(cape), FrenyLook.Find(color));
    }

    private static void Fill(RectTransform holder, int tab) {
        foreach (Transform child in holder) {
            UnityEngine.Object.Destroy(child.gameObject);
        }
        List<string[]> rows = tab == 0 ? Summary() : tab == 1 ? Killed() : tab == 2 ? ByWeapon() : Deaths();
        RectTransform grid = MakeGrid(holder, 490f);
        foreach (string[] row in rows) {
            Cell(grid, row[0], row[1]);
        }
    }

    private static List<string[]> Summary() {
        List<string[]> rows = new List<string[]> {
            new[] { "Corridas manuais", Points(PlayerStats.Get(PlayerStats.RunsManual)) },
            new[] { "Corridas no automático", Points(PlayerStats.Get(PlayerStats.RunsAuto)) },
            new[] { "Tempo de jogo", Duration(PlayerStats.Seconds) },
            new[] { "Tempo no manual", Duration(PlayerStats.Get(PlayerStats.SecondsManual)) },
            new[] { "Tempo no automático", Duration(PlayerStats.Get(PlayerStats.SecondsAuto)) },
            new[] { "Corrida mais longa", Duration(PlayerStats.Get(PlayerStats.LongestRun)) },
            new[] { "Maior combo", Combo(PlayerStats.Get(PlayerStats.BestCombo)) },
            new[] { "Noites sobrevividas", Points(PlayerStats.Get(PlayerStats.Nights)) },
            new[] { "Luas de Sangue sobrevividas", Points(PlayerStats.Get(PlayerStats.BloodMoons)) },
            new[] { "Moedas ganhas", Points(PlayerStats.Get(PlayerStats.CoinsEarned)) },
            new[] { "Moedas gastas", Points(PlayerStats.Get(PlayerStats.CoinsSpent)) },
            new[] { "Baús abertos", Points(PlayerStats.Get(PlayerStats.Chests)) },
        };
        foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
            rows.Add(new[] { "Recorde no " + ArenaDifficulty.Label(level), Points(PlayerStats.Get(PlayerStats.BestKey(level))) +
                " · " + Points(PlayerStats.Get(PlayerStats.RunsKey(level))) + " corridas" });
        }
        return rows;
    }

    private static List<string[]> Killed() {
        List<string[]> rows = new List<string[]> {
            new[] { "Inimigos derrotados", Points(PlayerStats.Get(PlayerStats.Kills)) },
            new[] { "Pelo piloto automático", Points(PlayerStats.Get(PlayerStats.KillsAuto)) },
        };
        for (int i = 0; i < PlayerStats.Enemies.GetLength(0); i++) {
            rows.Add(new[] { PlayerStats.Enemies[i, 2], Points(PlayerStats.Get(PlayerStats.KillKey(PlayerStats.Enemies[i, 1]))) });
        }
        return rows;
    }

    private static List<string[]> ByWeapon() {
        List<string[]> rows = new List<string[]>();
        for (int i = 0; i < PlayerStats.Weapons.GetLength(0); i++) {
            rows.Add(new[] { PlayerStats.Weapons[i, 1], Points(PlayerStats.Get(PlayerStats.WeaponKey(PlayerStats.Weapons[i, 0]))) });
        }
        return rows;
    }

    private static List<string[]> Deaths() {
        List<string[]> rows = new List<string[]> { new[] { "Quedas do Freny", Points(PlayerStats.Get(PlayerStats.Deaths)) } };
        for (int i = 0; i < PlayerStats.Enemies.GetLength(0); i++) {
            rows.Add(new[] { PlayerStats.Enemies[i, 2], Points(PlayerStats.Get(PlayerStats.DeathKey(PlayerStats.Enemies[i, 1]))) });
        }
        for (int i = 0; i < PlayerStats.Causes.GetLength(0); i++) {
            rows.Add(new[] { PlayerStats.Causes[i, 1], Points(PlayerStats.Get(PlayerStats.DeathKey(PlayerStats.Causes[i, 0]))) });
        }
        return rows;
    }

    // ---- the Amigos page ----

    private const int FriendsPerPage = 5;

    public static MenuPage Friends(MenuStack stack, int firstFriend = 0) {
        OnlineServices online = Online;
        MenuPage page = new MenuPage(stack.transform, "Amigos", PageWidth, Middle, 5f);
        page.Label("O seu código de amigo: " + (online.Profiles.Code ?? "-") + "   ·   até " + FriendsService.MaxFriends + " amigos");
        InputField search = page.Input("Código ou apelido", "", false, 20);
        Text message = null;
        page.Button("Adicionar amigo", () => {
            message.text = "Aguarde...";
            int before = online.Friends.Lista.Count;
            online.StartCoroutine(online.Friends.Add(search.text, error => {
                if (page.Root == null) return;
                if (error != null) message.text = error;
                else if (online.Friends.Lista.Count > before) stack.ReplaceTop(Friends(stack, firstFriend));   // that player had already asked
                else message.text = "Pedido enviado!";
            }));
        });
        message = page.Label("");

        RectTransform requests = MenuKit.MakeRect("Requests", page.Content);
        VerticalLayoutGroup requestRows = Column(requests);
        RectTransform list = MenuKit.MakeRect("List", page.Content);
        VerticalLayoutGroup listRows = Column(list);
        page.Button("Voltar", stack.Back);

        Action draw = () => DrawFriends(stack, page, requests, list, message, firstFriend);
        draw();
        online.StartCoroutine(LoadFriends(page, draw));
        return page;
    }

    private static VerticalLayoutGroup Column(RectTransform rect) {
        VerticalLayoutGroup column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        return column;
    }

    private static IEnumerator LoadFriends(MenuPage page, Action draw) {
        OnlineServices online = Online;
        yield return online.Friends.Sync();
        yield return online.Friends.Refresh();
        if (page.Root != null) draw();
    }

    private static void DrawFriends(MenuStack stack, MenuPage page, RectTransform requests, RectTransform list, Text message, int first) {
        OnlineServices online = Online;
        Action again = () => stack.ReplaceTop(Friends(stack, first));
        Clear(requests);
        Clear(list);
        // up to two requests at a time: answering one brings the next
        for (int i = 0; i < Mathf.Min(2, online.Friends.Incoming.Count); i++) {
            FriendsService.Pedido pedido = online.Friends.Incoming[i];
            Image row = MenuKit.MakeRow(requests, (string.IsNullOrEmpty(pedido.apelido) ? "Alguém" : pedido.apelido) + " quer ser seu amigo", out Text label);
            row.color = new Color(1f, 0.85f, 0.3f, 0.12f);
            label.fontSize = MenuKit.SmallTextSize;
            Button yes = MenuKit.MakeButton(row.transform, "Aceitar", () => online.StartCoroutine(online.Friends.Accept(pedido, e => Answered(page, message, e, again))), MenuKit.SmallTextSize);
            MenuKit.Size(yes, 130f, -1f);
            Button no = MenuKit.MakeButton(row.transform, "Recusar", () => online.StartCoroutine(online.Friends.Decline(pedido, e => Answered(page, message, e, again))), MenuKit.SmallTextSize);
            MenuKit.Size(no, 130f, -1f);
        }

        IReadOnlyList<PerfilInfo> perfis = online.Friends.Perfis;
        if (perfis.Count == 0) {
            MenuKit.MakeText(list, "Você ainda não tem amigos aqui. Passe o seu código ou digite o de um amigo.", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
            return;
        }
        int start = Mathf.Clamp(first, 0, Mathf.Max(0, perfis.Count - 1));
        for (int i = start; i < Mathf.Min(perfis.Count, start + FriendsPerPage); i++) {
            FriendRow(stack, list, perfis[i], message, again);
        }
        if (perfis.Count > FriendsPerPage) {
            Image pager = MenuKit.MakeRow(list, (start + 1) + "-" + Mathf.Min(perfis.Count, start + FriendsPerPage) + " de " + perfis.Count, out Text count);
            count.fontSize = MenuKit.SmallTextSize;
            MenuKit.Size(pager, -1f, 36f);
            pager.color = new Color(1f, 1f, 1f, 0.03f);
            Button back = MenuKit.MakeButton(pager.transform, "<", () => stack.ReplaceTop(Friends(stack, Mathf.Max(0, start - FriendsPerPage))), MenuKit.SmallTextSize);
            MenuKit.Size(back, 70f, -1f);
            back.interactable = start > 0;
            Button next = MenuKit.MakeButton(pager.transform, ">", () => stack.ReplaceTop(Friends(stack, start + FriendsPerPage)), MenuKit.SmallTextSize);
            MenuKit.Size(next, 70f, -1f);
            next.interactable = start + FriendsPerPage < perfis.Count;
        }
    }

    private static void Answered(MenuPage page, Text message, string error, Action again) {
        if (page.Root == null) return;
        if (error != null) message.text = error;
        else again();
    }

    private static void FriendRow(MenuStack stack, RectTransform list, PerfilInfo friend, Text message, Action again) {
        OnlineServices online = Online;
        Image row = MenuKit.MakeRow(list, "", out Text label);
        row.color = new Color(1f, 1f, 1f, 0.05f);
        MenuKit.Size(row, -1f, 54f);
        // the look goes before the name
        RectTransform look = MenuKit.MakeRect("Look", row.transform);
        MenuKit.Size(look, 44f, 44f);
        look.SetAsFirstSibling();
        FrenyLookPreview preview = FrenyLookPreview.Create(look, 44f);
        preview.Show(FrenyLook.Find(friend.helmet), FrenyLook.Find(friend.cape), FrenyLook.Find(friend.color));
        label.fontSize = MenuKit.SmallTextSize;
        label.supportRichText = true;
        label.text = friend.apelido + "\n<size=" + (MenuKit.SmallTextSize - 2) + "><color=#" + ColorUtility.ToHtmlStringRGB(MenuKit.DimTextColor) + ">Recorde " +
                     Points(friend.Best(ArenaLevel.Hard)) + " no Difícil · online " + friend.SeenText() + "</color></size>";
        Button open = MenuKit.MakeButton(row.transform, "Ver", () => stack.Push(Friend(stack, friend)), MenuKit.SmallTextSize);
        MenuKit.Size(open, 90f, -1f);
        Button remove = null;
        remove = MenuKit.MakeButton(row.transform, "Remover", () => {
            Text text = remove.GetComponentInChildren<Text>();
            if (text.text != "Certeza?") {
                text.text = "Certeza?";   // a second click removes
                return;
            }
            online.StartCoroutine(online.Friends.Remove(friend.uid, e => {
                if (e != null) message.text = e;
                else again();
            }));
        }, MenuKit.SmallTextSize);
        MenuKit.Size(remove, 130f, -1f);
    }

    private static void Clear(RectTransform rect) {
        foreach (Transform child in rect) {
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    // ---- a friend, side by side ----

    public static MenuPage Friend(MenuStack stack, PerfilInfo friend) {
        OnlineServices online = Online;
        PerfilInfo me = online.Profiles.Own();
        MenuPage page = new MenuPage(stack.transform, friend.apelido, 860f, Middle, 3f);

        RectTransform looks = MenuKit.MakeRect("Looks", page.Content);
        MenuKit.Size(looks, -1f, 90f);
        HorizontalLayoutGroup layout = looks.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 180f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        LookBox(looks, 90f, me.helmet, me.cape, me.color);
        LookBox(looks, 90f, friend.helmet, friend.cape, friend.color);

        Compare(page, "", me.apelido, friend.apelido);
        Compare(page, "Visto por último", "agora", friend.SeenText());
        foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
            Compare(page, "Recorde no " + ArenaDifficulty.Label(level), Points(me.Best(level)), Points(friend.Best(level)), me.Best(level), friend.Best(level));
        }
        CompareNumber(page, "Inimigos derrotados", PlayerStats.Kills, me, friend);
        CompareNumber(page, "Ciclopes", PlayerStats.KillKey("cyclops"), me, friend);
        CompareNumber(page, "Feiticeiros Supremos", PlayerStats.KillKey("supreme"), me, friend);
        Compare(page, "Maior combo", Combo(me.Number(PlayerStats.BestCombo)), Combo(friend.Number(PlayerStats.BestCombo)),
            me.Number(PlayerStats.BestCombo), friend.Number(PlayerStats.BestCombo));
        Compare(page, "Corrida mais longa", Duration(me.Number(PlayerStats.LongestRun)), Duration(friend.Number(PlayerStats.LongestRun)),
            me.Number(PlayerStats.LongestRun), friend.Number(PlayerStats.LongestRun));
        CompareNumber(page, "Noites sobrevividas", PlayerStats.Nights, me, friend);
        CompareNumber(page, "Luas de Sangue", PlayerStats.BloodMoons, me, friend);
        CompareNumber(page, "Quedas", PlayerStats.Deaths, me, friend);
        page.Space(2f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void CompareNumber(MenuPage page, string label, string key, PerfilInfo me, PerfilInfo friend) {
        Compare(page, label, Points(me.Number(key)), Points(friend.Number(key)), me.Number(key), friend.Number(key));
    }

    private static void Compare(MenuPage page, string label, string mine, string theirs, long a = 0, long b = 0) {
        Image row = MenuKit.MakeRow(page.Content, label, out Text text);
        row.color = new Color(1f, 1f, 1f, 0.04f);
        MenuKit.Size(row, -1f, 26f);
        text.fontSize = MenuKit.SmallTextSize;
        Value(row.transform, mine, a > b);
        Value(row.transform, theirs, b > a);
    }

    private static void Value(Transform row, string text, bool ahead) {
        Text value = MenuKit.MakeText(row, text, MenuKit.SmallTextSize, ahead ? new Color(1f, 0.85f, 0.29f) : MenuKit.TextColor, TextAnchor.MiddleRight);
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        MenuKit.Size(value, 200f, -1f);
    }

    // ---- small helpers ----

    private static RectTransform MakeGrid(Transform parent, float width) {
        RectTransform grid = MenuKit.MakeRect("Grid", parent);
        MenuKit.Stretch(grid);
        GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
        cells.cellSize = new Vector2(width, RowHeight - 3f);
        cells.spacing = new Vector2(8f, 3f);
        cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        cells.constraintCount = 2;
        return grid;
    }

    private static void Cell(Transform grid, string label, string value) {
        Image cell = MenuKit.MakeImage("Cell", grid, new Color(1f, 1f, 1f, 0.06f));
        Text name = MenuKit.MakeText(cell.transform, label, MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        Inset(name.rectTransform);
        Text number = MenuKit.MakeText(cell.transform, value, MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleRight);
        number.horizontalOverflow = HorizontalWrapMode.Overflow;
        Inset(number.rectTransform);
    }

    private static void Inset(RectTransform rect) {
        MenuKit.Stretch(rect);
        rect.offsetMin = new Vector2(10f, 0f);
        rect.offsetMax = new Vector2(-10f, 0f);
    }

    private static string Duration(long seconds) {
        if (seconds >= 3600) return seconds / 3600 + " h " + (seconds / 60 % 60) + " min";
        return seconds / 60 + " min " + (seconds % 60).ToString("00") + " s";
    }

    private static string Combo(long tenths) {
        return (Math.Max(10, tenths) / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "x";
    }

    private static string Points(long value) {
        return value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
