using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The menu screens of "cada corrida diferente" (José, 2026-11-01), opened from Jogar (MainMenu):
//   Visual    Freny's helmets, capes and shirt colours (FrenyLook), with him wearing them
//   Renascer  trading the Armeiro's upgrades for a bonus that stays (Rebirth), behind a warning
public static class VarietyScreens {

    // ---- Visual ----

    private static readonly LookSlot[] Slots = { LookSlot.Helmet, LookSlot.Cape, LookSlot.Color };

    public static MenuPage Visual(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Visual", 620f, new Vector2(0.5f, 0.5f), 6f);

        Image window = MenuKit.MakeImage("Window", page.Content, new Color(0f, 0f, 0f, 0.3f));
        window.raycastTarget = false;
        MenuKit.Size(window, -1f, 210f);
        window.gameObject.AddComponent<RectMask2D>();
        FrenyLookPreview preview = FrenyLookPreview.Create(window.rectTransform, 300f);

        Text coins = page.Label("", MenuKit.TextSize);

        // what each picker shows (maybe not owned yet: he tries it on), and the slot touched last
        Dictionary<LookSlot, FrenyLook.Item> shown = new Dictionary<LookSlot, FrenyLook.Item>();
        LookSlot current = LookSlot.Helmet;
        Text status = null;
        Button action = null;
        Action refresh = () => {
            coins.text = "Moedas: " + FrenyProgress.Coins;
            preview.Show(shown[LookSlot.Helmet], shown[LookSlot.Cape], shown[LookSlot.Color]);
            FrenyLook.Item item = shown[current];
            bool owned = FrenyLook.Owns(item);
            bool worn = owned && (FrenyLook.Worn(current) ?? FrenyLook.InSlot(current)[0]) == item;
            status.text = item.name + ": " + (worn ? "usando" : owned ? "é seu" : FrenyLook.HowToGet(item));
            action.GetComponentInChildren<Text>().text = worn ? "Usando" : owned ? "Usar"
                : item.unlock == FrenyLook.Unlock.Coins ? "Comprar · " + item.price : "Bloqueado";
        };

        foreach (LookSlot slot in Slots) {
            LookSlot s = slot;
            List<FrenyLook.Item> items = FrenyLook.InSlot(s);
            FrenyLook.Item worn = FrenyLook.Worn(s) ?? items[0];
            shown[s] = worn;
            string[] names = items.ConvertAll(i => FrenyLook.Owns(i) ? i.name : i.name + " (?)").ToArray();
            page.Choice(SlotName(s), names, items.IndexOf(worn), i => {
                current = s;
                shown[s] = items[i];
                if (FrenyLook.Owns(items[i])) {
                    FrenyLook.Wear(items[i]);   // what is his goes on at once
                }
                refresh();
            });
        }
        status = page.Label("");
        action = page.Button("", () => {
            FrenyLook.Item item = shown[current];
            if (FrenyLook.Owns(item)) {
                FrenyLook.Wear(item);
            } else if (!FrenyLook.TryBuy(item)) {
                Sfx.Ui("ui_error");
                status.text = item.unlock == FrenyLook.Unlock.Coins ? "Faltam moedas: " + item.price + " para " + item.name + "."
                    : item.name + ": " + FrenyLook.HowToGet(item) + ".";
                return;
            } else {
                FrenyLook.Wear(item);
            }
            refresh();
        });
        page.Label("Elmos, capas e cores não mudam nada na luta: são só para o Freny ficar bonito.");
        page.Button("Voltar", stack.Back);
        refresh();
        return page;
    }

    private static string SlotName(LookSlot slot) {
        switch (slot) {
            case LookSlot.Helmet: return "Elmo";
            case LookSlot.Cape: return "Capa";
            default: return "Cor da roupa";
        }
    }

    // ---- Renascer ----

    public static string RebirthButtonLabel => Rebirth.Count > 0 ? "Renascer " + Rebirth.Star(Rebirth.Count) : "Renascer";

    public static MenuPage RebirthPage(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Renascer", 640f, new Vector2(0.5f, 0.5f), 6f);
        int count = Rebirth.Count;
        page.Label(count == 0 ? "O Freny ainda não renasceu."
            : "Renascimentos: " + count + "  <color=#FFD84A>" + Rebirth.Star(count) + "</color>\n" +
              "Bônus: +" + Percent(Rebirth.DamageMultiplier) + " de dano e +" + Percent(Rebirth.CoinMultiplier) + " de moedas",
            MenuKit.TextSize);
        page.Space(4f);
        page.Label("Com tudo do Armeiro comprado e no nível máximo, o Freny pode trocar todas as melhorias " +
                   "por um bônus para sempre: +5% de dano e +5% de moedas a cada renascimento, somando, e uma " +
                   "estrela ★ ao lado do nome no ranking. Alguns visuais só abrem renascendo.");
        List<string> missing = Rebirth.Missing();
        if (missing.Count > 0) {
            int shownCount = Mathf.Min(4, missing.Count);
            string list = string.Join(", ", missing.GetRange(0, shownCount)) +
                          (missing.Count > shownCount ? " e mais " + (missing.Count - shownCount) : "");
            page.Space(4f);
            page.Label("<color=#FF7A7A>Falta no Armeiro: " + list + ".</color>");
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
        page.Label("<color=#FF7A7A>Você perde:</color> todas as melhorias do Armeiro (vida, velocidade, recarga, " +
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
