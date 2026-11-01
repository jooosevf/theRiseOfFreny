using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The Settings and Controls pages, shared by the main menu and the pause menu.
public static class SettingsScreens {

    public static MenuPage Settings(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Configurações", 640f, new Vector2(0.5f, 0.5f));

        page.Slider("Volume geral", 0, 10, Tenths(GameSettings.MasterVolume), Percent, v => GameSettings.MasterVolume = v / 10f);
        page.Slider("Música", 0, 10, Tenths(GameSettings.MusicVolume), Percent, v => GameSettings.MusicVolume = v / 10f);
        page.Slider("Efeitos", 0, 10, Tenths(GameSettings.SfxVolume), Percent, v => GameSettings.SfxVolume = v / 10f);

        page.Slider("Brilho", 0, GameSettings.BrightnessSteps, GameSettings.Brightness,
            v => (v - GameSettings.DefaultBrightness).ToString("+0;-0;0"), v => GameSettings.Brightness = v);

        if (GameSettings.CanChangeScreen) {
            page.Choice("Tela cheia", new[] { "Não", "Sim" }, GameSettings.Fullscreen ? 1 : 0,
                i => GameSettings.Fullscreen = i == 1);
        }
        if (GameSettings.CanChangeResolution) {
            Vector2Int[] sizes = GameSettings.Resolutions;
            string[] names = new string[sizes.Length];
            int current = sizes.Length - 1;
            for (int i = 0; i < sizes.Length; i++) {
                names[i] = sizes[i].x + "x" + sizes[i].y;
                if (sizes[i] == GameSettings.Resolution) {
                    current = i;
                }
            }
            page.Choice("Resolução", names, current, i => GameSettings.Resolution = sizes[i]);
        }

        page.Button("Controles", () => stack.Push(Controls(stack)));
        page.Button("Voltar", stack.Back);
        return page;
    }

    public static MenuPage Controls(MenuStack stack) {
        // 16 actions since the walk keys and the Rede (2026-11-01): lower rows, closer together, so the
        // page still fits a 720p screen
        MenuPage page = new MenuPage(stack.transform, "Controles", 720f, new Vector2(0.5f, 0.5f), 3f);
        page.Label("As setas também andam. No controle, anda com o analógico. Esc cancela.", MenuKit.SmallTextSize);

        MenuKeyCapture capture = stack.GetComponent<MenuKeyCapture>();
        if (capture == null) {
            capture = stack.gameObject.AddComponent<MenuKeyCapture>();
        }

        float[] widths = { 220f, 130f };
        foreach (GameAction action in InputBindings.All) {
            GameAction bound = action;
            Button[] buttons = null;
            buttons = page.ButtonRow(InputBindings.ActionName(action), widths,
                new[] { InputBindings.KeyName(InputBindings.Keyboard(action)), PadLabel(action) },
                column => {
                    if (capture.Waiting || (column == 1 && InputBindings.IsMovement(bound))) {
                        return;
                    }
                    bool gamepad = column == 1;
                    Text text = buttons[column].GetComponentInChildren<Text>();
                    text.text = gamepad ? "Aperte..." : "Tecla...";
                    stack.Busy = true;
                    capture.Begin(gamepad, key => {
                        if (key != KeyCode.None) {
                            if (gamepad) {
                                InputBindings.SetGamepad(bound, key);
                            } else {
                                InputBindings.SetKeyboard(bound, key);
                            }
                        }
                        stack.StartCoroutine(Release(stack));
                        Refresh(page);
                    });
                }, 30f);
            buttons[0].name = "kb:" + action;
            buttons[1].name = "pad:" + action;
        }

        page.ButtonRow("", new[] { 300f, 200f }, new[] { "Restaurar padrão", "Voltar" }, column => {
            if (column == 0) {
                InputBindings.ResetAll();
                Refresh(page);
            } else {
                stack.Back();
            }
        }, 44f);
        return page;
    }

    // rebinding one key can move another, so every button label is rewritten
    private static void Refresh(MenuPage page) {
        foreach (Button button in page.Content.GetComponentsInChildren<Button>(true)) {
            string[] parts = button.name.Split(':');
            if (parts.Length != 2 || !System.Enum.TryParse(parts[1], out GameAction action)) {
                continue;
            }
            button.GetComponentInChildren<Text>().text = parts[0] == "pad"
                ? PadLabel(action) : InputBindings.KeyName(InputBindings.Keyboard(action));
        }
    }

    // the gamepad walks with the stick, so those rows only show it
    private static string PadLabel(GameAction action) {
        return InputBindings.IsMovement(action) ? "Analógico" : InputBindings.KeyName(InputBindings.Gamepad(action));
    }

    // the key that finished the capture must not also press the selected button
    private static IEnumerator Release(MenuStack stack) {
        yield return null;
        yield return null;
        stack.Busy = false;
    }

    private static int Tenths(float value) {
        return Mathf.RoundToInt(value * 10f);
    }

    private static string Percent(int tenths) {
        return tenths * 10 + "%";
    }
}
