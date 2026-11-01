using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Jogar > Inventário (what Freny takes into the next stage, shown on Freny) and Jogar > Armeiro
// (the shop, paid with the saved coins, with the merchant beside it). Same upgrades and prices
// as the arena shop.
public static class InventoryScreens {

    private const float ItemRow = 38f;
    private const float SideWidth = 360f;

    private static readonly FrenyUpgrade[] Equipment = {
        FrenyUpgrade.GreatSword, FrenyUpgrade.Shotgun, FrenyUpgrade.Grenade, FrenyUpgrade.DoubleJump,
    };

    private static readonly FrenyUpgrade[] Attributes = {
        FrenyUpgrade.MaxHealth, FrenyUpgrade.MoveSpeed, FrenyUpgrade.AttackSpeed,
    };

    private static readonly FrenyUpgrade[] Brain = {
        FrenyUpgrade.Reaction, FrenyUpgrade.DashDodge, FrenyUpgrade.AutoPotion, FrenyUpgrade.SmartTarget,
    };

    private static readonly string[] EquipOptions = { "Guardado", "Equipado" };

    private static readonly string[] Greetings = {
        "Bem-vindo! Chegou aço novo hoje.",
        "Olha só quem voltou inteiro! O que vai levar?",
        "Moedas no balcão e a arma é sua.",
    };

    // ---- Inventário ----

    public static MenuPage Inventory(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Inventário", 600f, new Vector2(0.5f, 0.5f), 6f);
        FrenyView freny = new FrenyView(SidePanel(page, "Freny"));
        page.Label("O que o Freny leva para a próxima fase");

        foreach (FrenyUpgrade upgrade in Equipment) {
            FrenyUpgrade item = upgrade;
            if (FrenyProgress.GetLevel(item) > 0) {
                MenuChoice choice = page.Choice(ItemName(item), EquipOptions, FrenyProgress.IsEquipped(item) ? 1 : 0, i => {
                    FrenyProgress.SetEquipped(item, i == 1);
                    freny.Refresh();
                    freny.Say(i == 1 ? "Essa eu levo: " + ItemName(item).ToLower() + "." : ItemName(item) + " fica guardado.");
                });
                Hover(choice, () => freny.Say(Description(item)));
            } else {
                page.Button(ItemName(item) + ": no Armeiro", null, false, ItemRow);
            }
        }
        string helmet = Loadout.Helmet;
        if (Loadout.Owns(helmet)) {
            MenuChoice choice = page.Choice("Capacete", EquipOptions, Loadout.IsEquipped(helmet) ? 1 : 0, i => {
                Loadout.SetEquipped(helmet, i == 1);
                freny.Refresh();
                freny.Say(i == 1 ? "Capacete na cabeça. Pode vir!" : "Hoje vou sem o capacete.");
            });
            Hover(choice, () => freny.Say(HelmetDescription));
        } else {
            page.Button(Loadout.DisplayName(helmet) + ": no Armeiro", null, false, ItemRow);
        }

        page.Space(4f);
        page.Label("O que cai dos inimigos (espadão, capacete, munição) vale só na fase em que caiu.");
        page.Space(4f);
        Button armory = page.Button("Armeiro", () => stack.Push(Armory(stack)));
        Hover(armory, () => freny.Say("Dá pra comprar mais coisa com a armeira."));
        page.Button("Voltar", stack.Back);

        freny.Refresh();
        freny.Say("Vamos ver o que eu levo hoje.");
        return page;
    }

    // Freny standing beside the inventory, wearing the helmet when it is equipped, with the
    // equipment slots under him and his face saying a line
    private class FrenyView {

        private readonly MenuPortrait body;
        private readonly MenuSpeech speech;
        private readonly List<Action> slots = new List<Action>();
        private readonly Text attributes;

        public FrenyView(RectTransform side) {
            MenuArt art = MenuArt.Get;

            // the idle frames are 200 px squares with Freny in the bottom two thirds: a window
            // shows that part, bigger
            RectTransform window = MenuKit.MakeRect("Freny", side);
            MenuKit.Size(window, 260f, 200f);
            window.gameObject.AddComponent<RectMask2D>();
            Image image = MenuKit.MakeImage("Body", window, Color.white);
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(300f, 300f);
            body = image.gameObject.AddComponent<MenuPortrait>();
            body.Init(art != null ? art.frenyIdle : null, null, 2.5f, 0f);
            body.Loop = true;

            RectTransform row = Row(side, 64f);
            foreach (FrenyUpgrade upgrade in Equipment) {
                FrenyUpgrade item = upgrade;
                slots.Add(Slot(row, Icon(item), ShortName(item),
                    () => FrenyProgress.GetLevel(item) > 0, () => FrenyProgress.IsEquipped(item)));
            }
            slots.Add(Slot(row, art != null ? MenuArt.Trim(art.berserkerHelmet) : null, "Capacete",
                () => Loadout.Owns(Loadout.Helmet), () => Loadout.IsEquipped(Loadout.Helmet)));

            attributes = MenuKit.MakeText(side, "", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
            MenuKit.Size(attributes, -1f, -1f).flexibleWidth = 1f;
            speech = SpeechBox(side, art != null ? art.frenyFace : null, null, 64f);
        }

        public void Refresh() {
            body.OverlaySprites = Loadout.IsEquipped(Loadout.Helmet) ? BerserkerHeads() : null;
            foreach (Action slot in slots) {
                slot();
            }
            attributes.text = AttributesText();
        }

        public void Say(string line) {
            speech.Say(line);
        }
    }

    private static Dictionary<string, Sprite> berserkerHeads;

    // the same helmet layer the game puts on Freny (FrenyMotion)
    private static Dictionary<string, Sprite> BerserkerHeads() {
        if (berserkerHeads == null) {
            berserkerHeads = new Dictionary<string, Sprite>();
            foreach (Sprite head in Resources.LoadAll<Sprite>("FrenyHeads/berserker")) {
                berserkerHeads[head.name] = head;
            }
        }
        return berserkerHeads;
    }

    // one equipment slot: lit when equipped, dim when stored, almost gone when not bought
    private static Action Slot(Transform row, Sprite icon, string label, Func<bool> owned, Func<bool> equipped) {
        Image box = MenuKit.MakeImage("Slot", row, Color.white);
        MenuKit.Size(box, 60f, 64f);
        Image picture = MenuKit.MakeImage("Icon", box.transform, Color.white);
        picture.sprite = icon;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        RectTransform pictureRect = picture.rectTransform;
        pictureRect.anchorMin = new Vector2(0f, 0.3f);
        pictureRect.anchorMax = Vector2.one;
        pictureRect.offsetMin = new Vector2(8f, 2f);
        pictureRect.offsetMax = new Vector2(-8f, -6f);
        Text text = MenuKit.MakeText(box.transform, icon == null ? "?" : "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
        MenuKit.Stretch(text.rectTransform);
        text.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        Text name = MenuKit.MakeText(box.transform, label, 7, MenuKit.DimTextColor, TextAnchor.LowerCenter);
        MenuKit.Stretch(name.rectTransform);
        name.rectTransform.offsetMin = new Vector2(0f, 3f);
        name.horizontalOverflow = HorizontalWrapMode.Overflow;

        return () => {
            bool has = owned();
            bool on = has && equipped();
            box.color = on ? MenuKit.SelectedColor * new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.06f);
            float alpha = on ? 1f : has ? 0.45f : 0.15f;
            picture.color = new Color(1f, 1f, 1f, picture.sprite != null ? alpha : 0f);
            text.color = new Color(1f, 1f, 1f, alpha);
            name.color = on ? MenuKit.TextColor : MenuKit.DimTextColor;
        };
    }

    private static string AttributesText() {
        List<string> parts = new List<string>();
        if (FrenyProgress.BonusMaxHealth > 0) {
            parts.Add("Vida +" + FrenyProgress.BonusMaxHealth);
        }
        if (FrenyProgress.GetLevel(FrenyUpgrade.MoveSpeed) > 0) {
            parts.Add("Velocidade +" + Mathf.RoundToInt((FrenyProgress.SpeedMultiplier - 1f) * 100f) + "%");
        }
        if (FrenyProgress.GetLevel(FrenyUpgrade.AttackSpeed) > 0) {
            parts.Add("Recarga -" + Mathf.RoundToInt((1f - FrenyProgress.AttackCooldownMultiplier) * 100f) + "%");
        }
        return "Atributos: " + (parts.Count > 0 ? string.Join(" · ", parts) : "nenhum ainda");
    }

    // ---- Armeiro ----

    public static MenuPage Armory(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Armeiro", 600f, new Vector2(0.5f, 0.5f), 6f);
        MenuArt art = MenuArt.Get;
        RectTransform side = SidePanel(page, "Armeira");
        RectTransform frame = MenuKit.MakeRect("Portrait", side);
        MenuKit.Size(frame, 200f, 279f);
        Image face = MenuKit.MakeImage("Face", frame, Color.white);
        MenuKit.Stretch(face.rectTransform);
        face.raycastTarget = false;
        MenuPortrait merchant = face.gameObject.AddComponent<MenuPortrait>();
        Sprite[] faces = art != null ? art.merchantFace : null;
        bool canBlink = faces != null && faces.Length > 5;
        merchant.Init(canBlink ? new[] { faces[4], faces[0], faces[2], faces[1], faces[3] } : faces,
            canBlink ? faces[5] : null, 9f, 3f);
        MenuSpeech speech = SpeechBox(side, null, merchant, 96f);

        Text coins = page.Label("", MenuKit.TextSize);
        List<Action> refresh = new List<Action>();
        Action refreshAll = () => {
            coins.text = "Moedas: " + FrenyProgress.Coins;
            foreach (Action action in refresh) {
                action();
            }
        };

        foreach (FrenyUpgrade upgrade in Equipment) {
            UpgradeRow(page, upgrade, refresh, refreshAll, speech);
        }
        foreach (FrenyUpgrade upgrade in Attributes) {
            UpgradeRow(page, upgrade, refresh, refreshAll, speech);
        }

        string helmet = Loadout.Helmet;
        Button helmetButton = null;
        Text helmetPrice = ItemButton(page, Loadout.DisplayName(helmet), art != null ? MenuArt.Trim(art.berserkerHelmet) : null, () => {
            if (Loadout.TryBuy(helmet)) {
                speech.Say("O capacete é seu! Cada fase você começa com armadura cheia.");
                refreshAll();
            } else {
                Refuse(speech, Loadout.Owns(helmet) ? "Esse capacete você já tem, querido." : NoCoins());
            }
        }, out _, out helmetButton);
        Hover(helmetButton, () => speech.Say(HelmetDescription));
        refresh.Add(() => helmetPrice.text = Loadout.Owns(helmet) ? "comprado" : Loadout.Cost(helmet).ToString());

        page.Space(4f);
        Button brain = page.Button("", () => stack.Push(BrainPage(stack)), true, ItemRow);
        Hover(brain, () => speech.Say("O piloto automático da Arena aprende com pontos de nível, não com moedas."));
        Text brainText = brain.GetComponentInChildren<Text>();
        brainText.fontSize = MenuKit.SmallTextSize;
        refresh.Add(() => brainText.text = "Piloto automático · " + PointsText());
        page.Button("Voltar", stack.Back);

        refreshAll();
        speech.Say(Greetings[UnityEngine.Random.Range(0, Greetings.Length)]);
        return page;
    }

    // the autopilot's brain, paid with the points from levelling up
    private static MenuPage BrainPage(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Piloto automático", 640f, new Vector2(0.5f, 0.5f), 6f);
        Text points = page.Label("", MenuKit.TextSize);
        Text message = null;
        List<Action> refresh = new List<Action>();
        Action refreshAll = () => {
            points.text = "Nível " + FrenyProgress.Level + " · " + PointsText();
            foreach (Action action in refresh) {
                action();
            }
        };
        foreach (FrenyUpgrade upgrade in Brain) {
            UpgradeRow(page, upgrade, refresh, refreshAll, null, () => message);
        }
        page.Space(4f);
        message = page.Label("Cada nível ganho na Arena vale 1 ponto. Os níveis ficam mais longos.");
        page.Button("Voltar", stack.Back);
        refreshAll();
        return page;
    }

    private static string PointsText() {
        int points = FrenyProgress.BrainPoints;
        return points == 1 ? "1 ponto" : points + " pontos";
    }

    private static string NoCoins() {
        return "Faltam moedas, Freny. Volta depois de uma corrida na Arena.";
    }

    // a shop row; what happens is said by the merchant (speech) or written under the list (message)
    private static void UpgradeRow(MenuPage page, FrenyUpgrade upgrade, List<Action> refresh, Action refreshAll,
                                   MenuSpeech speech, Func<Text> message = null) {
        Action<string, bool> tell = (line, ok) => {
            if (!ok) {
                Sfx.Ui("ui_error");
            }
            if (speech != null) {
                speech.Say(line);
            } else if (message != null && message() != null) {
                message().text = line;
            }
        };
        Text price = ItemButton(page, "", Icon(upgrade), () => {
            if (FrenyProgress.TryBuy(upgrade, null)) {
                tell(speech != null ? Bought(upgrade) : FrenyProgress.DisplayName(upgrade) + " comprado.", true);
                refreshAll();
            } else {
                tell(FrenyProgress.IsMaxed(upgrade) ? "Esse já está no máximo."
                    : FrenyProgress.IsBrain(upgrade) ? "Sem pontos: suba de nível na Arena." : NoCoins(), false);
            }
        }, out Text name, out Button button);
        if (speech != null) {
            Hover(button, () => speech.Say(Description(upgrade)));
        }
        refresh.Add(() => {
            int level = FrenyProgress.GetLevel(upgrade);
            int max = FrenyProgress.MaxLevel(upgrade);
            name.text = FrenyProgress.DisplayName(upgrade) + (max > 1 ? " (" + level + "/" + max + ")" : "");
            price.text = FrenyProgress.IsMaxed(upgrade) ? (max > 1 ? "máximo" : "comprado")
                : FrenyProgress.CostText(upgrade);
        });
    }

    private static void Refuse(MenuSpeech speech, string why) {
        Sfx.Ui("ui_error");
        speech.Say(why);
    }

    // a shop row: the item's icon and name on the left, its price on the right
    private static Text ItemButton(MenuPage page, string label, Sprite icon, Action onClick, out Text name, out Button button) {
        button = page.Button(label, onClick, true, ItemRow);
        name = button.GetComponentInChildren<Text>();
        name.alignment = TextAnchor.MiddleLeft;
        name.fontSize = MenuKit.SmallTextSize;
        name.rectTransform.offsetMin = new Vector2(IconBox + 18f, 0f);
        name.rectTransform.offsetMax = new Vector2(-PriceWidth, 0f);   // the price has its own room
        if (icon != null) {
            Image picture = MenuKit.MakeImage("Icon", button.transform, Color.white);
            picture.sprite = icon;
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            RectTransform rect = picture.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, 0f);
            rect.sizeDelta = Vector2.one * IconBox;
        }
        Text price = MenuKit.MakeText(button.transform, "", MenuKit.SmallTextSize, MenuKit.TitleColor, TextAnchor.MiddleRight);
        MenuKit.Stretch(price.rectTransform);
        price.rectTransform.anchorMin = new Vector2(1f, 0f);
        price.rectTransform.offsetMin = new Vector2(-PriceWidth, 0f);
        price.rectTransform.offsetMax = new Vector2(-12f, 0f);
        price.horizontalOverflow = HorizontalWrapMode.Overflow;   // "comprado" on one line
        return price;
    }

    // ---- shared pieces ----

    // a panel beside the page's own, on its left, for the art; the page moves right to make room
    private static RectTransform SidePanel(MenuPage page, string title) {
        const float gap = 20f;
        float pageWidth = page.Panel.sizeDelta.x;
        float total = SideWidth + gap + pageWidth;
        page.Panel.anchoredPosition = new Vector2(total / 2f - pageWidth / 2f, 0f);

        Image side = MenuKit.MakeImage("Side", page.Root, MenuKit.PanelColor);
        side.raycastTarget = false;
        RectTransform rect = side.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-total / 2f + SideWidth / 2f, 0f);
        rect.sizeDelta = new Vector2(SideWidth, 0f);
        VerticalLayoutGroup column = side.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(20, 20, 18, 20);
        column.spacing = 10f;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;
        side.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text name = MenuKit.MakeText(rect, title, MenuKit.TextSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
        MenuKit.Size(name, -1f, 24f).flexibleWidth = 1f;
        return rect;
    }

    // a dark box with a line typed into it, and a small talking face on its left when given one
    private static MenuSpeech SpeechBox(Transform parent, Sprite[] face, MenuPortrait speaker, float height) {
        Image box = MenuKit.MakeImage("Speech", parent, new Color(0f, 0f, 0f, 0.35f));
        box.raycastTarget = false;
        MenuKit.Size(box, -1f, height).flexibleWidth = 1f;

        float textLeft = 12f;
        if (face != null && face.Length > 0) {
            Image small = MenuKit.MakeImage("Face", box.transform, Color.white);
            small.raycastTarget = false;
            RectTransform rect = small.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(6f, 0f);
            rect.sizeDelta = new Vector2(height * 0.72f, height);   // the face frames are 344 x 480
            speaker = small.gameObject.AddComponent<MenuPortrait>();
            speaker.Init(face, null, 7f, 0f);
            textLeft = height * 0.72f + 14f;
        }

        Text text = MenuKit.MakeText(box.transform, "", MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleLeft);
        text.supportRichText = true;
        MenuKit.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(textLeft, 8f);
        text.rectTransform.offsetMax = new Vector2(-10f, -8f);
        MenuSpeech speech = box.gameObject.AddComponent<MenuSpeech>();
        speech.Init(text, speaker);
        return speech;
    }

    private static RectTransform Row(Transform parent, float height) {
        RectTransform row = MenuKit.MakeRect("Slots", parent);
        MenuKit.Size(row, -1f, height).flexibleWidth = 1f;
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    private static void Hover(Component target, Action onFocus) {
        target.gameObject.AddComponent<MenuHover>().OnFocus = onFocus;
    }

    private static Sprite Icon(FrenyUpgrade upgrade) {
        MenuArt art = MenuArt.Get;
        if (art == null) {
            return null;
        }
        return MenuArt.Trim(RawIcon(art, upgrade));
    }

    private static Sprite RawIcon(MenuArt art, FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.GreatSword: return art.greatSword;
            case FrenyUpgrade.Shotgun: return art.shotgun;
            case FrenyUpgrade.Grenade: return art.grenade;
            case FrenyUpgrade.MaxHealth: return art.heart;
            case FrenyUpgrade.AttackSpeed: return art.sword;
            case FrenyUpgrade.DoubleJump: return art.jump;
            case FrenyUpgrade.MoveSpeed: return art.run;
            default: return null;
        }
    }

    private const float IconBox = 30f;      // icons are trimmed (MenuArt.Trim), so they all fill it
    private const float PriceWidth = 116f;

    private static string ItemName(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.GreatSword: return "Espadão";
            case FrenyUpgrade.Shotgun: return "Escopeta";
            case FrenyUpgrade.Grenade: return "Granadas";
            case FrenyUpgrade.DoubleJump: return "Pulo duplo";
            default: return FrenyProgress.DisplayName(upgrade);
        }
    }

    private static string ShortName(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.GreatSword: return "Espadão";
            case FrenyUpgrade.Shotgun: return "Escopeta";
            case FrenyUpgrade.Grenade: return "Granada";
            default: return "Pulo";
        }
    }

    private const string HelmetDescription = "Capacete Berserker: começa cada fase com 3 de armadura.";

    private static string Description(FrenyUpgrade upgrade) {
        switch (upgrade) {
            case FrenyUpgrade.GreatSword: return "O espadão corta bem mais largo. Ninguém chega perto.";
            case FrenyUpgrade.Shotgun: return "Escopeta: três tiros de uma vez. De perto não sobra nada.";
            case FrenyUpgrade.Grenade: return "Granadas pra limpar a sala inteira.";
            case FrenyUpgrade.DoubleJump: return "Botas leves: dá pra pular de novo no ar.";
            case FrenyUpgrade.MaxHealth: return "Mais um coração pra aguentar o tranco.";
            case FrenyUpgrade.MoveSpeed: return "Pernas mais rápidas: corre e foge melhor.";
            case FrenyUpgrade.AttackSpeed: return "Uma poção que deixa os golpes e tiros mais rápidos.";
            default: return FrenyProgress.DisplayName(upgrade);
        }
    }

    private static string Bought(FrenyUpgrade upgrade) {
        return FrenyProgress.IsMaxed(upgrade) || FrenyProgress.MaxLevel(upgrade) == 1
            ? "Boa escolha! " + ItemName(upgrade) + " é seu."
            : "Feito! Subiu mais um nível. Volta quando quiser o próximo.";
    }
}
