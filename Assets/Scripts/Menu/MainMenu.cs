using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu, built over the art of the "UI - Main Menu" scene (background, title, Freny);
// the scene's old Start / Options / Quit images are hidden. Added by GameMenus.
public class MainMenu : MonoBehaviour {

    private static readonly string[] OldButtons = { "Start", "Options", "Quit" };
    private const float CompactRow = 38f;

    // the campaign stays closed until the beta: only the Arena is played for now
    public static readonly bool CampaignOpen = false;

    private MenuStack stack;

    private void Start() {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
                if (Array.IndexOf(OldButtons, child.name) >= 0 && child.GetComponent<Button>() != null) {
                    child.gameObject.SetActive(false);
                }
            }
        }

        // the scene's title art sits where the menu goes: smaller and higher, menu below it
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) {
            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true)) {
                if (child.name == "Title") {
                    child.localScale = Vector3.one * 0.62f;
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, 360f);
                }
            }
        }

        Canvas canvas = MenuKit.CreateCanvas("Main Menu", 10, transform);
        stack = canvas.gameObject.AddComponent<MenuStack>();
        stack.ShadeSubPages = true;
        stack.OnRootCancel = () => { };
        stack.ResetTo(MainPage());
        // the first time the game opens: Entrar, Criar conta or Jogar como convidado
        Online.OnlineServices online = Online.OnlineServices.Instance;
        if (online != null) {
            online.AccountChanged += OnPulled;
            StartCoroutine(AskAccount(online));
        }
    }

    // a frame later: OnlineServices starts its login in its own Start, maybe after this one
    private System.Collections.IEnumerator AskAccount(Online.OnlineServices online) {
        yield return null;
        if (online.NeedsAccountChoice && stack.Depth == 1) {
            stack.Push(AccountScreens.Welcome(stack));
        }
    }

    private void OnEnable() {
        Online.CloudProgressSync.Pulled += OnPulled;
    }

    private void OnDisable() {
        Online.CloudProgressSync.Pulled -= OnPulled;
        if (Online.OnlineServices.Instance != null) {
            Online.OnlineServices.Instance.AccountChanged -= OnPulled;
        }
    }

    // progress came down from the cloud: redraw "Continue" with it
    private void OnPulled() {
        if (stack != null && stack.Depth == 1) {
            stack.ResetTo(MainPage());
        }
    }

    private MenuPage MainPage() {
        MenuPage page = new MenuPage(stack.transform, "", 440f, new Vector2(0.5f, 0.5f), 4f);
        // under the title, on the right half of the art (title centre: x = 0.716 of the screen)
        page.Panel.anchorMin = page.Panel.anchorMax = new Vector2(0.716f, 0f);
        page.Panel.pivot = new Vector2(0.5f, 0f);
        page.Panel.anchoredPosition = new Vector2(0f, 60f);
        page.Panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 14, 14);
        SaveData save = SaveSystem.Current;

        if (!CampaignOpen) {
            // nothing to continue yet: Jogar leads to the Arena
        } else if (save.HasProgress) {
            page.BigButton("Continuar", save.ShortSummary, GameMenus.Continue);
        } else {
            page.Button("Começar", () => GameMenus.StartStage(CampaignLevels.All[0].id), true, CompactRow);
        }
        page.Button("Jogar", () => stack.Push(PlayPage()), true, CompactRow);
        page.Button("Configurações", () => stack.Push(SettingsScreens.Settings(stack)), true, CompactRow);
        page.Button("Ajuda", () => stack.Push(HelpScreen.Help(stack)), true, CompactRow);
        page.Button("Guia", () => stack.Push(GuideScreen.Guide(stack)), true, CompactRow);
        if (Features.DailyMissions) {
            page.Button(MissionsLabel(), () => stack.Push(MissionsScreen.Page(stack)), true, CompactRow);
        }
        AchievementsScreen.Badge(page.Button(Achievements.ButtonLabel, () => stack.Push(AchievementsScreen.Page(stack)), true, CompactRow));
        if (Online.OnlineServices.Instance != null && Online.OnlineServices.Instance.State != Online.OnlineServices.Status.Disabled) {
            page.Button("Ranking", () => stack.Push(OnlineScreens.Ranking(stack, this, ArenaDifficulty.Ranked[0])), true, CompactRow);
            page.Button(PerfilScreens.ButtonLabel, () => stack.Push(PerfilScreens.Page(stack)), true, CompactRow);
        }
        if (GameMenus.CanQuit) {
            page.Button("Sair", GameMenus.Quit, true, CompactRow);
        }
        return page;
    }

    // "Missões do dia (1/3)": how many of today's are done
    private static string MissionsLabel() {
        return "Missões do dia (" + DailyMissions.DoneCount + "/3)";
    }

    // Jogar: everything that starts or prepares a run
    private MenuPage PlayPage() {
        MenuPage page = new MenuPage(stack.transform, "Jogar", 520f, new Vector2(0.5f, 0.5f));
        page.Button(CampaignOpen ? "Campanha" : "Campanha (na versão beta)", () => stack.Push(CampaignPage()), CampaignOpen);
        Button arena = page.Button("Arena", () => stack.Push(ArenaPage()), Application.CanStreamedLevelBeLoaded(GameMenus.ArenaScene));
        page.Button("Inventário", () => stack.Push(InventoryScreens.Inventory(stack)));
        page.Button("Armeira", () => stack.Push(InventoryScreens.Armory(stack)));
        // Freny's looks and Renascer (VarietyScreens)
        if (Features.Looks) {
            page.Button("Visual", () => stack.Push(VarietyScreens.Visual(stack)));
        }
        if (Features.Rebirth) {
            page.Button(VarietyScreens.RebirthButtonLabel, () => stack.Push(VarietyScreens.RebirthPage(stack)));
        }
        page.Button("Voltar", stack.Back);
        page.SelectFirst = CampaignOpen ? null : arena;
        return page;
    }

    private MenuPage CampaignPage() {
        MenuPage page = new MenuPage(stack.transform, "Campanha", 520f, new Vector2(0.5f, 0.5f));
        page.Button("Novo jogo", () => stack.Push(SlotsPage(true)));
        page.Button("Carregar jogo", () => stack.Push(SlotsPage(false)));
        page.Button("Fases", () => stack.Push(StagesPage()));
        page.Button("Voltar", stack.Back);
        return page;
    }

    // newGame: pick a slot to start over in; otherwise pick a slot to continue
    private MenuPage SlotsPage(bool newGame) {
        MenuPage page = new MenuPage(stack.transform, newGame ? "Novo jogo" : "Carregar jogo", 640f, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < SaveSystem.SlotCount; i++) {
            int slot = i;
            SaveData data = slot == SaveSystem.ActiveSlot ? SaveSystem.Current : SaveSystem.Load(slot);
            bool used = data != null && data.HasProgress;
            string title = "Espaço " + (slot + 1) + (slot == SaveSystem.ActiveSlot ? " (atual)" : "");
            string detail = used ? data.Summary : "vazio";

            if (newGame) {
                page.BigButton(title, detail, () => {
                    if (used) {
                        stack.Push(ConfirmPage("Apagar o espaço " + (slot + 1) + " e começar do zero?", "Sim, apagar", () => StartNew(slot)));
                    } else {
                        StartNew(slot);
                    }
                });
            } else {
                page.BigButton(title, detail, () => {
                    SaveSystem.SwitchTo(slot);
                    stack.ResetTo(MainPage());
                }, used);
            }
        }
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void StartNew(int slot) {
        SaveSystem.NewGame(slot);
        GameMenus.StartStage(CampaignLevels.All[0].id);
    }

    private MenuPage ConfirmPage(string question, string yes, Action onYes) {
        MenuPage page = new MenuPage(stack.transform, "Tem certeza?", 560f, new Vector2(0.5f, 0.5f));
        page.Label(question, MenuKit.TextSize);
        page.Space();
        page.Button("Não", stack.Back);
        page.Button(yes, onYes);
        return page;
    }

    // Automático or Manual, then Easy / Medium / Hard / Freny (ArenaDifficulty, the arena reads
    // both), then the run starts
    private MenuPage ArenaPage() {
        MenuPage page = new MenuPage(stack.transform, "Arena", 560f, new Vector2(0.5f, 0.5f));
        page.Choice("Controle", new[] { "Automático", "Manual" }, ArenaDifficulty.Manual ? 1 : 0,
            i => ArenaDifficulty.Manual = i == 1);
        ArenaVariant.AddChoice(page);   // Clássica or Poço de Ácido, once it is open
        page.Label("Só corridas manuais do começo ao fim valem para o ranking. O oficial é o do Freny.");
        if (Features.WeeklyMutators) {
            page.Label(WeeklyMutators.MenuText);   // the same rule for everyone this week
        }
        Button current = null;
        foreach (ArenaLevel level in System.Enum.GetValues(typeof(ArenaLevel))) {
            ArenaLevel chosen = level;
            Button button = page.BigButton(ArenaDifficulty.Label(level), ArenaBlurb(level), () => {
                ArenaDifficulty.Current = chosen;
                GameMenus.StartArena();
            });
            if (level == ArenaDifficulty.Current) {
                current = button;
            }
        }
        page.Button("Voltar", stack.Back);
        page.SelectFirst = current;
        return page;
    }

    private static string ArenaBlurb(ArenaLevel level) {
        switch (level) {
            case ArenaLevel.Easy: return "Poucos inimigos, o ritmo sobe devagar";
            case ArenaLevel.Medium: return "O ritmo normal da Arena";
            default: return "Mais inimigos, mais fortes, mais rápido";
        }
    }

    // stages in rows of three, one block per dungeon
    private MenuPage StagesPage() {
        MenuPage page = new MenuPage(stack.transform, "Fases", 720f, new Vector2(0.5f, 0.5f), 6f);
        var stages = CampaignLevels.All;
        float[] widths = { 150f, 150f, 150f };
        for (int start = 0; start < stages.Count; start += 3) {
            int count = Mathf.Min(3, stages.Count - start);
            string[] labels = new string[count];
            float[] rowWidths = new float[count];
            bool[] open = new bool[count];
            for (int i = 0; i < count; i++) {
                CampaignLevels.Stage stage = stages[start + i];
                // in the Editor every stage is open, to test without playing through
                open[i] = (Application.isEditor || CampaignProgress.IsUnlocked(stage.id)) &&
                          Application.CanStreamedLevelBeLoaded(stage.id);
                labels[i] = open[i] ? (stage.boss ? stage.id + " Chefe" : stage.id) : "fechada";
                rowWidths[i] = widths[i];
            }
            string rowLabel = start % 9 == 0 ? "Masmorra " + stages[start].dungeon : "";
            int first = start;
            Button[] buttons = page.ButtonRow(rowLabel, rowWidths, labels, i => {
                if (open[i]) {
                    GameMenus.StartStage(stages[first + i].id);
                }
            });
            for (int i = 0; i < count; i++) {
                buttons[i].interactable = open[i];
            }
        }
        page.Button("Voltar", stack.Back);
        return page;
    }
}
