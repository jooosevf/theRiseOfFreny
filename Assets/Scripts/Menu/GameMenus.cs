using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Created once when the game starts (no scene setup needed) and kept across scenes: the pause
// menu, the autosave, the music and the main menu of the menu scene.
// Pause: Esc / P / Start on a gamepad, or the on-screen button on touch screens.
public class GameMenus : MonoBehaviour {

    public const string MainMenuScene = "UI - Main Menu";
    public const string ArenaScene = "ZE Arena";
    // shown in the top right corner on every screen, to know which build is running
    public const string Version = "Alpha 0.0.2";
    private const float AutosaveSeconds = 60f;

    public static GameMenus Instance { get; private set; }
    public static bool Paused => Instance != null && Instance.paused;
    public static bool SawTouch => Application.isMobilePlatform || (Instance != null && Instance.sawTouch);

    private bool paused;
    private bool gameplayScene;
    private bool sawTouch;
    private float nextAutosave;

    private Canvas pauseCanvas;
    private MenuStack pauseStack;
    private Canvas touchCanvas;
    private TouchControls touchControls;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private readonly List<Canvas> hiddenHuds = new List<Canvas>();
    private MenuPage deathRanking;   // the ranking opened after a manual run: Space starts the next one

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() {
        if (Instance != null) {
            return;
        }
        GameObject obj = new GameObject("GameMenus");
        DontDestroyOnLoad(obj);
        obj.AddComponent<GameMenus>();
        obj.AddComponent<MusicPlayer>();
        obj.AddComponent<AmbiencePlayer>();
        obj.AddComponent<Online.CloudProgressSync>();
    }

    public static bool IsGameplayScene(string scene) {
        return CampaignLevels.IsStage(scene) || scene == ArenaScene ||
               (scene != MainMenuScene && !scene.StartsWith("UI - ") && FindAnyObjectByType<Player>() != null);
    }

    // where "Main menu" goes; false while the menu scene is not in the build
    public static bool HasMainMenu => Application.CanStreamedLevelBeLoaded(MainMenuScene);

    public static void LoadMainMenu() {
        SaveSystem.SaveNow();
        if (Instance != null) {
            Instance.Resume();
        }
        SceneManager.LoadScene(MainMenuScene);
    }

    public static void StartStage(string stageId) {
        if (Instance != null) {
            Instance.Resume();
        }
        CampaignRun.LoadStage(stageId);
    }

    // one Space on the ranking after a run reached the menu's Space, the button (Submit) and
    // the arena's own wait at once: the scene loaded three times
    private static int arenaLoadFrame = -1;

    public static void StartArena() {
        if (arenaLoadFrame == Time.frameCount) {
            return;
        }
        arenaLoadFrame = Time.frameCount;
        if (Instance != null) {
            Instance.Resume();
        }
        SceneManager.LoadScene(ArenaScene);
    }

    public static void StartArena(bool manual) {
        ArenaDifficulty.Manual = manual;
        StartArena();
    }

    // the last stage (or the arena) of the active save, else the furthest open stage
    public static void Continue() {
        string scene = SaveSystem.Current.lastScene;
        if (!string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene)) {
            if (CampaignLevels.IsStage(scene)) {
                StartStage(scene);
            } else {
                StartArena();
            }
            return;
        }
        StartStage(CampaignLevels.All[CampaignProgress.UnlockedIndex].id);
    }

    public static bool CanQuit =>
        !Application.isEditor && !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;

    public static void Quit() {
        SaveSystem.SaveNow();
        Application.Quit();
    }

    // ---- lifecycle ----

    private void Awake() {
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start() {
        CreateVersionLabel();
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    // small and dim, at the bottom centre: at the top right it covered the coin counter
    private void CreateVersionLabel() {
        Canvas canvas = MenuKit.CreateCanvas("Version", 30000, transform);
        // small, in the bottom-right corner (José, 2026-10-30: the boss bar takes the middle)
        Text label = MenuKit.MakeText(canvas.transform, Version, MenuKit.SmallTextSize - 3, MenuKit.DimTextColor, TextAnchor.LowerRight);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-8f, 4f);
        rect.sizeDelta = new Vector2(300f, 22f);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (mode != LoadSceneMode.Single) {
            return;
        }
        Resume();
        gameplayScene = IsGameplayScene(scene.name);
        if (CampaignLevels.IsStage(scene.name) || scene.name == ArenaScene) {
            SaveSystem.SetLastScene(scene.name);
            StartCoroutine(Loadout.ApplyAtStageStart());
            if (HelpScreen.FirstTime) {
                StartCoroutine(ShowHelpFirstTime());
            }
        }
        SaveSystem.SaveNow();
        nextAutosave = Time.unscaledTime + AutosaveSeconds;

        if (scene.name == MainMenuScene) {
            new GameObject("MainMenu").AddComponent<MainMenu>();
        }
    }

    private void Update() {
        if (Input.touchCount > 0) {
            sawTouch = true;
        }
        UpdateTouchButton();
        RedrawTextsOnResize();
        GameCursor.Apply(gameplayScene && !paused && Application.isFocused);

        if (!gameplayScene) {
            return;
        }
        if (!paused) {
            SaveSystem.AddPlayTime(Time.unscaledDeltaTime);
            if (InputBindings.GetDown(GameAction.Pause)) {
                Pause();
            }
        } else if (deathRanking != null && pauseStack.Top == deathRanking && InputBindings.GetDown(GameAction.Jump)) {
            StartArena(ArenaDifficulty.Manual);
        }
        if (Time.unscaledTime >= nextAutosave) {
            nextAutosave = Time.unscaledTime + AutosaveSeconds;
            SaveSystem.SaveNow();
        }
    }

    private void OnApplicationPause(bool pausing) {
        if (!pausing) {
            return;
        }
        SaveSystem.SaveNow();
        // a phone call or switching apps pauses the game; on a PC the idle arena keeps running
        if (Application.isMobilePlatform && gameplayScene && !paused) {
            Pause();
        }
    }

    private void OnApplicationQuit() {
        SaveSystem.SaveNow();
    }

    // ---- pause ----

    public void Pause() {
        if (paused) {
            return;
        }
        paused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Sfx.Ui("ui_pause");
        InputBindings.BlockGameplay(true);
        HideHuds();

        if (pauseCanvas == null) {
            pauseCanvas = MenuKit.CreateCanvas("Pause Menu", 30000, transform);
            Image shade = MenuKit.MakeImage("Shade", pauseCanvas.transform, MenuKit.ShadeColor);
            MenuKit.Stretch(shade.rectTransform);
            pauseStack = pauseCanvas.gameObject.AddComponent<MenuStack>();
            pauseStack.OnRootCancel = Resume;
            pauseStack.ExtraCancel = () => InputBindings.GetDown(GameAction.Pause) || (RankingOpen && Input.GetKeyDown(KeyCode.Tab));
        }
        pauseCanvas.gameObject.SetActive(true);
        pauseStack.ResetTo(PausePage());
    }

    // the arena ranking over the paused game (Tab, or on its own after a manual run ends);
    // afterRun adds "Nova corrida", also on Space
    public void ShowRanking(bool afterRun) {
        Pause();
        deathRanking = OnlineScreens.Ranking(pauseStack, this, ArenaDifficulty.Current,
            afterRun ? (System.Action)(() => StartArena(ArenaDifficulty.Manual)) : null, afterRun ? "Ver a arena" : "Voltar ao jogo");
        pauseStack.ResetTo(deathRanking);
        if (!afterRun) {
            deathRanking = null;
        }
    }

    public bool RankingOpen => paused && pauseStack != null && pauseStack.Top != null && pauseStack.Top.Root.name == "Page Ranking";

    // testers did not know the buttons: the first run opens the pause menu on the Help page
    private System.Collections.IEnumerator ShowHelpFirstTime() {
        yield return null;   // after the scene's Start, so its HUD is there to hide
        yield return null;
        Pause();
        pauseStack.Push(HelpScreen.Help(pauseStack));
    }

    public void Resume() {
        if (!paused) {
            return;
        }
        paused = false;
        deathRanking = null;
        if (pauseStack != null) {
            pauseStack.Clear();
            pauseStack.Busy = false;
        }
        if (pauseCanvas != null) {
            pauseCanvas.gameObject.SetActive(false);
        }
        AudioListener.pause = false;
        foreach (Canvas hud in hiddenHuds) {
            if (hud != null) {
                hud.enabled = true;
            }
        }
        hiddenHuds.Clear();
        InputBindings.BlockGameplay(false);
        GameSpeed.Apply();
    }

    // the scene's HUD canvases (hearts, score, combo) sort above the shade in some scenes
    private void HideHuds() {
        hiddenHuds.Clear();
        foreach (Canvas canvas in FindCompat.All<Canvas>()) {
            if (canvas.isRootCanvas && canvas.enabled && canvas.renderMode != RenderMode.WorldSpace &&
                !canvas.transform.IsChildOf(transform)) {
                canvas.enabled = false;
                hiddenHuds.Add(canvas);
            }
        }
    }

    private MenuPage PausePage() {
        string scene = SceneManager.GetActiveScene().name;
        bool arena = scene == ArenaScene;
        // the arena's extra rows (mode, note, ranking) need a tighter column to fit a 720p screen
        MenuPage page = new MenuPage(pauseStack.transform, "Pausado", 600f, new Vector2(0.5f, 0.5f), arena ? 5f : 10f);

        page.Button("Continuar", Resume);
        if (CampaignLevels.IsStage(scene)) {
            page.Button("Recomeçar fase", () => StartStage(scene));
        } else if (arena) {
            ArenaControls(page);
        } else {
            page.Button(scene == ArenaScene ? "Nova corrida" : "Recomeçar", () => {
                Resume();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });
        }
        page.Button("Ajuda", () => pauseStack.Push(HelpScreen.Help(pauseStack)));
        page.Button("Guia", () => pauseStack.Push(GuideScreen.Guide(pauseStack)));
        page.Button("Conquistas", () => pauseStack.Push(AchievementsScreen.Page(pauseStack)));
        page.Button("Configurações", () => pauseStack.Push(SettingsScreens.Settings(pauseStack)));

        // the arena keeps its progress (coins, XP, upgrades) as it goes: nothing to save by hand
        if (!arena) {
            Button save = null;
            save = page.Button("Salvar jogo", () => {
                SaveSystem.SaveNow();
                save.GetComponentInChildren<Text>().text = "Salvo no espaço " + (SaveSystem.ActiveSlot + 1);
            });
        }

        page.Button("Menu principal", LoadMainMenu, HasMainMenu);
        if (CanQuit) {
            page.Button("Sair do jogo", Quit);
        }
        if (!arena) {
            page.Label("O jogo também salva sozinho a cada minuto e ao trocar de fase.");
        }
        return page;
    }

    // the arena: Auto on or off right here, and "Nova corrida" asks automatic or manual, so the
    // mode changes without going back to the main menu. Only a run played by hand from start to
    // end counts for the ranking (OnlineServices), so switching mid-run says what that means.
    private void ArenaControls(MenuPage page) {
        FrenyInput input = FindAnyObjectByType<FrenyInput>();
        Online.OnlineServices online = Online.OnlineServices.Instance;
        bool ranked = online != null && online.State != Online.OnlineServices.Status.Disabled;
        Text note = null;
        System.Action<bool> explain = on => {
            if (on) {
                note.text = ranked ? "Modo automático ligado: esta corrida não vale para o ranking se o piloto jogar" : "Modo automático ligado";
            } else if (ranked && online.RunAssisted) {
                note.text = "Modo manual: só vai contar para o ranking a partir da próxima corrida";
            } else {
                note.text = ranked ? "Modo manual: esta corrida vale para o ranking" : "Modo manual";
            }
        };
        bool auto = input != null ? input.AutoEnabled : !ArenaDifficulty.Manual;
        page.Choice("Modo automático", new[] { "Ligado", "Desligado" }, auto ? 0 : 1, i => {
            bool on = i == 0;
            if (input != null) {
                input.SetAutoEnabled(on);
            }
            ArenaDifficulty.Manual = !on;   // the next run starts the same way
            explain(on);
        });
        note = page.Label("");
        explain(auto);
        page.Button("Nova corrida", () => pauseStack.Push(NewRunPage()));
        if (ranked) {
            page.Button("Ranking", () => pauseStack.Push(OnlineScreens.Ranking(pauseStack, this, ArenaDifficulty.Current)));
        }
    }

    private MenuPage NewRunPage() {
        MenuPage page = new MenuPage(pauseStack.transform, "Nova corrida", 560f, new Vector2(0.5f, 0.5f));
        page.Label("Dificuldade: " + ArenaDifficulty.Label(ArenaDifficulty.Current), MenuKit.TextSize);
        Button auto = page.BigButton("Automática", "O piloto joga quando você para. Não vale para o ranking", () => StartArena(false));
        Button manual = page.BigButton("Manual", "Só você joga. Vale para o ranking", () => StartArena(true));
        page.Button("Voltar", pauseStack.Back);
        page.SelectFirst = ArenaDifficulty.Manual ? manual : auto;
        return page;
    }

    // In the web build the canvas can change size after the menus are built (page layout, phone
    // rotation, address bar); text built at the old size once showed as giant blurry glyphs, so
    // every text is rebuilt whenever the screen size changes.
    private void RedrawTextsOnResize() {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) {
            return;
        }
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        foreach (Text text in FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            text.SetAllDirty();
        }
    }

    // touch screens: a pause button and the on-screen controls
    private void UpdateTouchButton() {
        bool show = gameplayScene && !paused && (Application.isMobilePlatform || sawTouch);
        if (!show) {
            if (touchCanvas != null) {
                touchCanvas.gameObject.SetActive(false);
            }
            if (touchControls != null) {
                touchControls.gameObject.SetActive(false);
            }
            return;
        }
        if (touchControls == null) {
            touchControls = TouchControls.Create(transform);
        } else if (!touchControls.gameObject.activeSelf) {
            touchControls.gameObject.SetActive(true);
        }
        if (touchCanvas == null) {
            touchCanvas = MenuKit.CreateCanvas("Touch Pause", 29000, transform);
            Button button = MenuKit.MakeButton(touchCanvas.transform, "II", Pause, MenuKit.TitleSize);
            RectTransform rect = (RectTransform)button.transform;
            // top right under the coins, clear of the score and combo at the top centre
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -70f);
            rect.sizeDelta = new Vector2(72f, 72f);
            button.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
            Navigation none = new Navigation { mode = Navigation.Mode.None };
            button.navigation = none;
        }
        if (!touchCanvas.gameObject.activeSelf) {
            touchCanvas.gameObject.SetActive(true);
            MenuKit.EnsureEventSystem();
        }
    }
}
