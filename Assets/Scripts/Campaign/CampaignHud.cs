using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary campaign HUD drawn with IMGUI, like ArenaHud: stage, time, deaths, the door hint,
// the "stage clear" panel and a stage list (Tab) to jump between open stages while testing.
// Hearts, coins, score, combo, weapons and cooldowns come from the arena's HUD (the FrenyHud
// prefab), added here to stages that have no uGUI HUD (UIManager) of their own.
public class CampaignHud : MonoBehaviour {

    // where the stage panel starts on touch screens (screen pixels from the top, below the coins)
    public const float TouchStatusTop = 140f;

    private static string DoorKey => TouchControls.Active ? "Direcional para cima" : "W";

    [SerializeField] private KeyCode stageListKey = KeyCode.Tab;

    private Player player;
    private CampaignRun run;
    private bool showStageList;
    private GUIStyle boxStyle;
    private GUIStyle bigStyle;

    private void Awake() {
        // runs from CampaignBootstrap at scene load, before the Player's Start, so the Player
        // finds the UIManager and fills in its hearts and coins
        GameObject hud = Resources.Load<GameObject>("FrenyHud");
        if (hud == null) {
            return;
        }
        if (UIManager.Instance == null) {
            Instantiate(hud).name = hud.name;
        } else if (FindAnyObjectByType<CombatHud>() == null) {
            // older stages carry their own hearts and coins HUD: add the weapons and cooldowns to it
            CombatHud icons = hud.GetComponent<CombatHud>();
            CombatHud combat = UIManager.Instance.gameObject.AddComponent<CombatHud>();
            combat.revolverIcon = icons.revolverIcon;
            combat.shotgunIcon = icons.shotgunIcon;
            combat.swordIcon = icons.swordIcon;
            combat.greatSwordIcon = icons.greatSwordIcon;
            combat.grenadeIcon = icons.grenadeIcon;
            combat.rageHelmetIcon = icons.rageHelmetIcon;
            combat.armorHelmetIcon = icons.armorHelmetIcon;
            combat.armorHeart = icons.armorHeart;
        }
    }

    private void Start() {
        player = GetComponent<Player>();
        run = GetComponent<CampaignRun>();
        GameCursor.ShowWhile(this, () => showStageList);
    }

    private void Update() {
        if (Input.GetKeyDown(stageListKey)) {
            showStageList = !showStageList;
        }
    }

    private void OnGUI() {
        if (GameMenus.Paused) {
            return;
        }
        if (player == null || run == null) {
            return;
        }
        if (boxStyle == null) {
            boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28, fontStyle = FontStyle.Bold };
        }

        DrawStatus();

        if (LevelDoor.Current && LevelDoor.Current.pressUp && !run.Finished && !player.IsDead) {
            string what = LevelDoor.Current.Locked ? "Derrote o chefe para abrir a porta"
                : LevelDoor.Current.kind == LevelDoor.Kind.Exit ? DoorKey + ": sair da fase" : DoorKey + ": entrar";
            GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 40), what, bigStyle);
        }

        if (WorldInteract.Current && !player.IsDead) {
            GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 40), WorldInteract.Current.Hint, bigStyle);
        }

        if (CampaignWorld.Active && Time.time < CampaignWorld.ToastUntil) {
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), CampaignWorld.Toast, bigStyle);
        }

        if (player.IsDead) {
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60),
                $"Freny caiu. Voltando em {run.SecondsUntilRespawn:0.0}s", bigStyle);
        }

        if (run.Finished) {
            DrawStageClear();
        }

        if (showStageList) {
            DrawStageList();
        }
    }

    private void DrawStatus() {
        // bottom right: the HUD has hearts and weapons top left, score and combo top center, coins top right;
        // on touch screens the buttons own the bottom right, so it goes under the coins
        float top = TouchControls.Active ? TouchStatusTop : Screen.height - 110;
        GUILayout.BeginArea(new Rect(Screen.width - 290, top, 280, 100));
        GUILayout.BeginVertical(boxStyle);
        int index = CampaignLevels.IndexOf(run.StageId);
        CampaignLevels.Stage stage = CampaignLevels.All[index];
        if (stage.world) {
            GUILayout.Label($"Masmorra de teste · Sala {stage.id}");
            GUILayout.Label($"Vidas guardadas {CampaignWorld.Lives}/{CampaignWorld.MaxLives} (Q)");
            GUILayout.Label($"Não salvo: {CampaignWorld.State.BagCoins} moedas, {CampaignWorld.State.BagXp} XP");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            return;
        }
        GUILayout.Label($"Masmorra {stage.dungeon} · Fase {stage.DisplayName}");
        GUILayout.Label($"Tempo {run.Elapsed:0.0}s  Quedas {CampaignRun.Deaths}");
        if (UIManager.Instance == null) {
            GUILayout.Label($"Vida {Hearts()}  Moedas {player.coins}");
        }
        GUILayout.Label("Tab: lista de fases");
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawStageClear() {
        if (CampaignWorld.Active) {
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 160), "Você saiu da masmorra de teste!", bigStyle);
            return;
        }
        float best = CampaignProgress.BestTime(run.StageId);
        string text = $"Fase {run.StageId} concluída!\n{run.Elapsed:0.0}s (recorde {best:0.0}s)";
        if (run.IsLastStage) {
            text += "\nVocê chegou ao fim da campanha (por enquanto).";
        } else {
            text += $"\nPróxima fase em {run.SecondsUntilNextStage:0}s";
        }
        GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 160), text, bigStyle);
    }

    private void DrawStageList() {
        GUILayout.BeginArea(new Rect(Screen.width / 2 - 160, 60, 320, Screen.height - 120));
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Fases (Tab fecha)");
        foreach (CampaignLevels.Stage stage in CampaignLevels.All) {
            if (stage.world) {
                continue;
            }
            // in the Editor every stage is open, to test without playing through
            bool open = Application.isEditor || CampaignProgress.IsUnlocked(stage.id);
            float best = CampaignProgress.BestTime(stage.id);
            string label = $"{stage.DisplayName}" + (best > 0f ? $"   recorde {best:0.0}s" : "") + (open ? "" : "   (fechada)");
            GUI.enabled = open && Application.CanStreamedLevelBeLoaded(stage.id);
            if (GUILayout.Button(label)) {
                CampaignRun.LoadStage(stage.id);
            }
        }
        GUI.enabled = Application.CanStreamedLevelBeLoaded("ZE Arena");
        if (GUILayout.Button("Arena")) {
            SceneManager.LoadScene("ZE Arena");
        }
        GUI.enabled = true;
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private string Hearts() {
        string hearts = "";
        for (int i = 0; i < player.MaxHealth; i++) {
            hearts += i < player.Health ? "♥" : "♡";
        }
        return hearts;
    }
}
