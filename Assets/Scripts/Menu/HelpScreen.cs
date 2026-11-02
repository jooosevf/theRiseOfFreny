using UnityEngine;
using UnityEngine.UI;

// The Help page (main menu and pause menu): every control and what it does, for the keyboard,
// a gamepad or the touch screen. Keyboard and gamepad show the player's own bindings
// (Settings > Controls). It also opens by itself the first time a stage or the arena starts.
public static class HelpScreen {

    public enum Device { Keyboard, Gamepad, Touch }

    private const string SeenKey = "freny.help.seen";
    private const float LineHeight = 24f;
    private static readonly string[] DeviceNames = { "Teclado", "Controle", "Toque" };

    // true once, the first time a run starts on this device
    public static bool FirstTime {
        get {
            if (PlayerPrefs.HasKey(SeenKey)) {
                return false;
            }
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            return true;
        }
    }

    public static MenuPage Help(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Ajuda", 900f, new Vector2(0.5f, 0.5f), 4f);

        RectTransform lines = MenuKit.MakeRect("Lines", page.Content);
        VerticalLayoutGroup column = lines.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 3f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        Device device = Guess();
        page.Choice("Mostrar", DeviceNames, (int)device, i => Fill(lines, (Device)i));
        // the picker is created after the lines' container but goes above it
        lines.SetAsLastSibling();
        Fill(lines, device);

        Text tip = page.Label("", MenuKit.SmallTextSize);
        tip.text = "Agachar+Pular desce plataformas. Teclas: Configurações > Controles";
        page.Space(6f);
        page.Button("Voltar", stack.Back);
        return page;
    }

    // the device the player is most likely holding
    private static Device Guess() {
        if (GameMenus.SawTouch) {
            return Device.Touch;
        }
        foreach (string name in Input.GetJoystickNames()) {
            if (!string.IsNullOrEmpty(name)) {
                return Device.Gamepad;
            }
        }
        return Device.Keyboard;
    }

    private static void Fill(RectTransform lines, Device device) {
        for (int i = lines.childCount - 1; i >= 0; i--) {
            Object.Destroy(lines.GetChild(i).gameObject);
        }
        string move = device == Device.Keyboard ? Keys(GameAction.Left, GameAction.Right) + " ou setas"
            : device == Device.Gamepad ? "Analógico" : "Direcional (esquerda)";
        string vertical = device == Device.Keyboard ? Keys(GameAction.Down, GameAction.Up) + " ou setas"
            : device == Device.Gamepad ? "Analógico" : "Direcional baixo / cima";
        Line(lines, "Andar", "Anda para os lados", move);
        Line(lines, "Agachar/Mirar", "Baixo agacha, cima mira", vertical);

        foreach (GameAction action in InputBindings.All) {
            if (InputBindings.IsMovement(action)) {
                continue;
            }
            Line(lines, InputBindings.ActionName(action), WhatItDoes(action), Button(action, device));
        }

        if (device == Device.Keyboard) {
            Line(lines, "Piloto (Arena)", "Liga e desliga o automático", "T");
            Line(lines, "Minimapa", "Mostra ou esconde o mapa", "M");
        }
    }

    private static string Keys(GameAction a, GameAction b) {
        return InputBindings.KeyName(InputBindings.Keyboard(a)) + " / " + InputBindings.KeyName(InputBindings.Keyboard(b));
    }

    private static string WhatItDoes(GameAction action) {
        switch (action) {
            case GameAction.Jump: return "Pula";
            case GameAction.Melee: return "Golpe de espada, de perto";
            case GameAction.Ranged: return "Atira com a arma escolhida";
            case GameAction.Dash: return "Avança rápido no ar";
            case GameAction.Run: return "Corre no chão; no ar dá dash";
            case GameAction.Grenade: return "Joga a granada (5 s de pavio); segure para queimar o pavio na mão";
            case GameAction.Potion: return "Bebe uma poção e cura";
            case GameAction.Pistol: return "Troca para a pistola";
            case GameAction.Shotgun: return "Troca para a escopeta";
            case GameAction.Interact: return "Entra em portas e abre a loja";
            case GameAction.Pause: return "Pausa, ajuda e configurações";
            case GameAction.Fury: return "Com a barra cheia, ativa a Fúria";
            case GameAction.Net: return "Joga a rede: prende inimigos";
            default: return "";
        }
    }

    private static string Button(GameAction action, Device device) {
        switch (device) {
            case Device.Keyboard:
                string key = InputBindings.KeyName(InputBindings.Keyboard(action));
                if (action == GameAction.Interact) {
                    return key + " ou Seta cima";
                }
                if (action == GameAction.Pause) {
                    return key + " ou P";
                }
                return key;
            case Device.Gamepad:
                if (action == GameAction.Interact) {
                    return "Analógico p/ cima";
                }
                return InputBindings.KeyName(InputBindings.Gamepad(action));
            default:
                return TouchButton(action);
        }
    }

    // the labels of the on-screen buttons (TouchControls)
    private static string TouchButton(GameAction action) {
        switch (action) {
            case GameAction.Jump: return "Pular";
            case GameAction.Melee: return "Golpe";
            case GameAction.Ranged: return "Tiro";
            case GameAction.Dash: return "2 toques no direcional, no ar";
            case GameAction.Run: return "2 toques no direcional e segure";
            case GameAction.Grenade: return "Gran.";
            case GameAction.Potion: return "Poção";
            case GameAction.Pistol: return "Arma (troca)";
            case GameAction.Shotgun: return "Arma (troca)";
            case GameAction.Interact: return "Direcional p/ cima";
            case GameAction.Pause: return "II no topo";
            case GameAction.Fury: return "Fúria";
            case GameAction.Net: return "Rede";
            default: return "-";
        }
    }

    // name | what it does | button, one line
    private static void Line(Transform parent, string name, string what, string button) {
        Image row = MenuKit.MakeImage("Line " + name, parent, new Color(1f, 1f, 1f, 0.05f));
        MenuKit.Size(row, -1f, LineHeight);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 2, 2);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        Text nameText = MenuKit.MakeText(row.transform, name, MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.MiddleLeft);
        MenuKit.Size(nameText, 190f, -1f);
        Text whatText = MenuKit.MakeText(row.transform, what, MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleLeft);
        MenuKit.Size(whatText, -1f, -1f).flexibleWidth = 1f;
        Text buttonText = MenuKit.MakeText(row.transform, button, MenuKit.SmallTextSize, MenuKit.TitleColor, TextAnchor.MiddleRight);
        MenuKit.Size(buttonText, 230f, -1f);
    }
}
