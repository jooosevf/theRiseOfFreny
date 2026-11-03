using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The menu screens of "cada corrida diferente" (José, 2026-11-01), opened from Jogar (MainMenu):
//   Visual    Freny's suit, helmets, capes and torso colours (FrenyLook), with him wearing them
//   Renascer  trading the Armeira's upgrades for a bonus that stays (Rebirth), behind a warning
public static class VarietyScreens {

    // ---- Visual ----

    private static readonly LookSlot[] Slots = { LookSlot.Suit, LookSlot.Helmet, LookSlot.Cape, LookSlot.Color };

    private const float CategoryRow = 50f;
    private const string GreenText = "#7CFC8A", RedText = "#FF7A7A", GoldText = "#FFD84A";

    // José, 2026-11-02: one thing at a time. This page is Freny as he is now in the middle and the
    // four categories under him; each opens its own page (SlotPage) listing what it has.
    public static MenuPage Visual(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Visual", 620f, new Vector2(0.5f, 0.5f), 6f);
        FrenyLookPreview preview = Window(page, 180f, 270f);
        Text coins = page.Label("", MenuKit.TextSize);

        List<Action> rows = new List<Action>();
        Action refresh = () => {
            coins.text = "Moedas: " + FrenyProgress.Coins;
            preview.Show(WornOrFirst(LookSlot.Helmet), WornOrFirst(LookSlot.Cape), WornOrFirst(LookSlot.Color), WornOrFirst(LookSlot.Suit));
            foreach (Action row in rows) {
                row();
            }
        };
        foreach (LookSlot slot in Slots) {
            LookSlot s = slot;
            if (FrenyLook.InSlot(s).Count < 2) {
                continue;   // a look switched off (Features)
            }
            Button open = page.Button("", () => stack.Push(SlotPage(stack, s, refresh)), true, CategoryRow);
            MenuKit.Opens(open);
            Text label = open.GetComponentInChildren<Text>();
            label.supportRichText = true;
            rows.Add(() => {
                bool locked = FrenyLook.WearsTraje && s != LookSlot.Suit;
                open.interactable = !locked;
                label.text = SlotName(s) + "\n<size=" + MenuKit.SmallTextSize + ">" + (locked ? "Bloqueado: tire o Traje" : WornOrFirst(s).name) + "</size>";
            });
        }
        page.Button("Voltar", stack.Back);
        refresh();
        return page;
    }

    // one category: Freny trying on the row in focus, and every row saying if it is on him, his,
    // for sale or locked. A click puts on what is his and buys what is for sale.
    private static MenuPage SlotPage(MenuStack stack, LookSlot slot, Action onChange) {
        MenuPage page = new MenuPage(stack.transform, SlotName(slot), 620f, new Vector2(0.5f, 0.5f), 4f);
        FrenyLookPreview preview = Window(page, 150f, 225f);
        Text status = page.Label("");
        status.supportRichText = true;
        MenuKit.Size(status, -1f, 36f);

        List<Action> rows = new List<Action>();
        FrenyLook.Item focus = WornOrFirst(slot);
        Action refresh = () => {
            ShowOn(preview, slot, focus);
            status.text = Describe(focus);
            foreach (Action row in rows) {
                row();
            }
        };

        MenuScroll.Create(page, out RectTransform list);
        page.Content = list;
        Selectable first = null;
        foreach (FrenyLook.Item look in FrenyLook.InSlot(slot)) {
            FrenyLook.Item item = look;
            Text right = InventoryScreens.ItemButton(page, "", null, () => {
                focus = item;
                if (FrenyLook.Owns(item) || FrenyLook.TryBuy(item)) {
                    FrenyLook.Wear(item);
                    onChange();
                } else {
                    Sfx.Ui("ui_error");
                }
                refresh();
            }, out Text name, out Button button);
            AddIcon(button, item);
            name.text = item.name;
            button.gameObject.AddComponent<MenuHover>().OnFocus = () => {
                focus = item;
                refresh();
            };
            if (item == focus) {
                first = button;
            }
            right.supportRichText = true;
            rows.Add(() => {
                right.text = RowState(item);
                name.color = FrenyLook.Owns(item) || FrenyLook.ForSale(item) ? MenuKit.TextColor : MenuKit.DimTextColor;
            });
        }
        page.Content = page.Panel;
        page.Button("Voltar", stack.Back);
        page.SelectFirst = first;
        refresh();
        return page;
    }

    // the window Freny stands in; size is the side of his 200 px frame on screen
    private static FrenyLookPreview Window(MenuPage page, float height, float size) {
        Image window = MenuKit.MakeImage("Window", page.Content, new Color(0f, 0f, 0f, 0.3f));
        window.raycastTarget = false;
        MenuKit.Size(window, -1f, height);
        window.gameObject.AddComponent<RectMask2D>();
        return FrenyLookPreview.Create(window.rectTransform, size);
    }

    private static FrenyLook.Item WornOrFirst(LookSlot slot) => FrenyLook.Worn(slot) ?? FrenyLook.InSlot(slot)[0];

    private static bool IsNothing(FrenyLook.Item item) => item.unlock == FrenyLook.Unlock.Free;

    private static bool IsWorn(FrenyLook.Item item) => FrenyLook.Owns(item) && WornOrFirst(item.slot) == item;

    // he wears what he has, with this one tried on
    private static void ShowOn(FrenyLookPreview preview, LookSlot slot, FrenyLook.Item item) {
        FrenyLook.Item Look(LookSlot s) => s == slot ? item : WornOrFirst(s);
        preview.Show(Look(LookSlot.Helmet), Look(LookSlot.Cape), Look(LookSlot.Color), Look(LookSlot.Suit));
    }

    private static string RowState(FrenyLook.Item item) {
        if (IsWorn(item)) {
            return "<color=" + GreenText + ">Usando</color>";
        }
        if (FrenyLook.Owns(item)) {
            return "Usar";
        }
        if (!FrenyLook.ForSale(item)) {
            return "<color=" + RedText + ">Bloqueado</color>";
        }
        return "<color=" + (FrenyProgress.Coins >= item.price ? GoldText : RedText) + ">" + item.price + "</color>";
    }

    private static string Describe(FrenyLook.Item item) {
        // two short lines, the page has no room for a third: the row in focus already names the item
        string coins = "Moedas: " + FrenyProgress.Coins + "\n";
        if (IsWorn(item)) {
            return coins + "<color=" + GreenText + ">É o que ele está usando.</color>";
        }
        if (FrenyLook.Owns(item)) {
            return coins + "É seu. Clique para usar.";
        }
        if (!FrenyLook.ForSale(item)) {
            return coins + "<color=" + RedText + ">" + FrenyLook.HowToGet(item) + ".</color>";
        }
        int missing = item.price - FrenyProgress.Coins;
        return coins + (missing > 0 ? "<color=" + RedText + ">Faltam " + missing + " moedas.</color>"
            : "Custa " + item.price + " moedas. Clique para comprar.");
    }

    // a helmet shows itself; a cape or a torso, its colour; "nothing" and the Traje have no icon
    private static void AddIcon(Button button, FrenyLook.Item item) {
        if (IsNothing(item) || item.slot == LookSlot.Suit) {
            return;
        }
        Sprite helmet = null;
        Dictionary<string, Sprite> sheet = FrenyLook.HelmetSheet(item);
        if (sheet != null) {
            sheet.TryGetValue("Player idle 1_1", out helmet);
        }
        Image icon = MenuKit.MakeImage("Icon", button.transform, helmet != null ? Color.white : item.color);
        icon.sprite = helmet;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(10f + (helmet != null ? 0f : 6f), 0f);
        rect.sizeDelta = Vector2.one * (helmet != null ? InventoryScreens.IconBox : 18f);
    }

    private static string SlotName(LookSlot slot) {
        switch (slot) {
            case LookSlot.Helmet: return "Elmo";
            case LookSlot.Cape: return "Capa";
            case LookSlot.Suit: return "Traje";
            default: return "Torso";
        }
    }

    // ---- Renascer ----

    public static string RebirthButtonLabel => Rebirth.Count > 0 ? "Renascer <color=#FFD84A>" + Rebirth.Star(Rebirth.Count) + "</color>" : "Renascer";

    public static MenuPage RebirthPage(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Renascer", 640f, new Vector2(0.5f, 0.5f), 6f);
        int count = Rebirth.Count;
        page.Label(count == 0 ? "O Freny ainda não renasceu."
            : "Renascimentos: " + count + "  <color=#FFD84A>" + Rebirth.Star(count) + "</color>\n" +
              "Bônus: +" + Percent(Rebirth.DamageMultiplier) + " de dano e +" + Percent(Rebirth.CoinMultiplier) + " de moedas",
            MenuKit.TextSize);
        page.Space(4f);
        page.Label("Com tudo da Armeira comprado e no nível máximo, o Freny pode trocar todas as melhorias " +
                   "por um bônus para sempre: +5% de dano e +5% de moedas a cada renascimento, somando, e uma " +
                   "estrela ★ ao lado do nome no ranking." + (Features.ExtraLooks ? " Alguns visuais só abrem renascendo." : ""));
        List<string> missing = Rebirth.Missing();
        if (missing.Count > 0) {
            int shownCount = Mathf.Min(4, missing.Count);
            string list = string.Join(", ", missing.GetRange(0, shownCount)) +
                          (missing.Count > shownCount ? " e mais " + (missing.Count - shownCount) : "");
            page.Space(4f);
            page.Label("<color=#FF7A7A>Falta na Armeira: " + list + ".</color>");
        }
        page.Space(4f);
        Button go = page.Button("Renascer", () => stack.Push(ConfirmRebirth(stack)), missing.Count == 0);
        page.Button("Voltar", stack.Back);
        page.SelectFirst = missing.Count == 0 ? (Selectable)go : null;
        return page;
    }

    // the warning: what goes, what stays, what comes; "Não" is selected first
    private static MenuPage ConfirmRebirth(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Tem certeza?", 680f, new Vector2(0.5f, 0.5f), 6f);
        int next = Rebirth.Count + 1;
        page.Label("<color=#FF7A7A>ATENÇÃO: não dá para desfazer.</color>", MenuKit.TextSize);
        page.Space(4f);
        page.Label("<color=#FF7A7A>Você perde:</color> todas as melhorias da Armeira (vida, velocidade, recarga, " +
                   "pulo duplo, armas e o Capacete Berserker), o piloto automático inteiro e o nível, que volta ao 1.");
        page.Label("<color=#7CFC8A>Fica:</color> moedas, recordes, conquistas, fases da campanha e o visual.");
        page.Label("<color=#FFD84A>Você ganha:</color> +" + Percent(1f + Rebirth.BonusPerRebirth * next) + " de dano e de moedas " +
                   "para sempre e a estrela " + Rebirth.Star(next) + " no ranking.");
        page.Space(8f);
        Button no = page.Button("Não, deixa assim", stack.Back);
        page.Button("Sim, renascer", () => {
            if (Rebirth.Perform()) {
                stack.Back();
                stack.ReplaceTop(RebirthPage(stack));
            } else {
                Sfx.Ui("ui_error");
                stack.Back();
            }
        });
        page.SelectFirst = no;
        return page;
    }

    private static string Percent(float multiplier) {
        return Mathf.RoundToInt((multiplier - 1f) * 100f) + "%";
    }
}
