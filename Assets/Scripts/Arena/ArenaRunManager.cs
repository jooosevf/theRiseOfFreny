using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One arena run lasts until Freny dies (GDD). In automatic mode nobody is there to press
// Space, so this restarts the scene a few seconds after death, always: the autopilot is left
// farming (José, 2026-11-01; it used to stop and ask after 10k points). A record of the
// autopilot's board is saved on its own when the next run starts.
// In manual mode the next run waits for the jump key (Space), so the player can look at the
// score first. Coins, XP and
// upgrades live in FrenyProgress (PlayerPrefs), so they survive the reload.
public class ArenaRunManager : MonoBehaviour {

    // the automatic run starts again after this (José, 2026-10-29: 10 s, with "Jogar agora" to skip it)
    private const float restartDelay = 10f;
    [SerializeField] private float minDeathTime = 1f;   // a jump held while dying does not skip the score

    private Player player;
    private FrenyInput input;
    private float runStart;
    private float deathTime = -1f;
    private Text banner;
    private GameObject saveButton;   // "Salvar recorde", while the record can be saved
    private Canvas bannerCanvas;
    private bool bannerHasRecord;    // built with the NOVO RECORDE lines (the record is known a frame late)
    private bool asking;   // an automatic run with a big score waits for the player

    public float RunSeconds => deathTime < 0f ? Time.time - runStart : deathTime - runStart;
    public bool RunOver => deathTime >= 0f;
    // the autopilot was driving when Freny fell
    private bool AutoRun => input != null && input.AutoEnabled;
    public bool WaitsForKey => input == null || !input.AutoEnabled || asking;
    public float SecondsUntilRestart => RunOver ? Mathf.Max(0f, deathTime + restartDelay - Time.time) : 0f;

    private void Start() {
        player = GetComponent<Player>();
        input = GetComponent<FrenyInput>();
        runStart = Time.time;
        GameCursor.ShowWhile(this, () => RunOver);   // "Jogar de novo" and "Menu" are clicked
    }

    private void Update() {
        if (player == null) {
            return;
        }

        if (!RunOver && player.IsDead) {
            deathTime = Time.time;
            if (RunSeconds > FrenyProgress.BestRunSeconds) {
                FrenyProgress.BestRunSeconds = RunSeconds;
            }
            ArenaVariant.NoteScore(UIManager.Instance != null ? UIManager.Instance.Score : 0);   // 50 000 opens the Poço de Ácido
            asking = false;
            PlayerPrefs.Save();
        }
        if (!RunOver || GameMenus.Paused) {
            return;
        }
        if (CoopWorld.Active) {   // whoever falls watches and comes back; CoopArena ends the run when both have fallen
            if (!player.IsDead) {
                deathTime = -1f;
            }
            if (player.gameOver != null && player.gameOver.activeSelf) {
                player.gameOver.SetActive(false);
            }
            return;
        }

        bool restart = WaitsForKey
            ? Time.time - deathTime >= minDeathTime && (InputBindings.GetDown(GameAction.Jump) || Input.GetButtonDown("Submit"))
            : SecondsUntilRestart <= 0f;
        if (restart) {
            PlayAgain();
            return;
        }
        ShowBanner();
    }

    private void PlayAgain() {
        if (GameMenus.Instance != null) {
            GameMenus.StartArena(!input || !input.AutoEnabled);
        } else {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // the end of the run in the middle of the screen, in the menus' style
    private void ShowBanner() {
        if (player.gameOver != null && player.gameOver.activeSelf) {
            player.gameOver.SetActive(false);   // the scene's old "Press SPACE to play again..."
        }
        bool hasRecord = Record != Online.OnlineServices.RecordStatus.None;
        if (banner != null && bannerHasRecord != hasRecord) {
            Destroy(bannerCanvas.gameObject);
            banner = null;
            saveButton = null;
        }
        if (banner == null) {
            Canvas canvas = MenuKit.CreateCanvas("Run Over", 60, null);   // not under the player, who flips
            bannerCanvas = canvas;
            bannerHasRecord = hasRecord;
            Image panel = MenuKit.MakeImage("Panel", canvas.transform, MenuKit.PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, AutoRun || hasRecord ? 70f : 40f);
            rect.sizeDelta = new Vector2(AutoRun || hasRecord ? 820f : 640f, (AutoRun ? 190f : 130f) + (hasRecord ? 100f : 0f));
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = MenuKit.ButtonColor;
            edge.effectDistance = new Vector2(3f, -3f);
            banner = MenuKit.MakeCombatText(panel.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
            banner.raycastTarget = false;
            banner.supportRichText = true;
            banner.lineSpacing = AutoRun || hasRecord ? 1.3f : 1.6f;
            MenuKit.Stretch(banner.rectTransform);
            if (asking) {
                AddChoices(canvas.transform, "Jogar de novo");
            } else if (!WaitsForKey) {
                AddChoices(canvas.transform, "Jogar agora");   // skips the countdown
            }
        }
        bool needsAccount = Record == Online.OnlineServices.RecordStatus.NeedsAccount;
        if (saveButton == null && (Record == Online.OnlineServices.RecordStatus.CanSave || needsAccount)) {
            AddSaveButton(bannerCanvas.transform, needsAccount);
        }
        if (saveButton != null) {
            saveButton.SetActive(Record == Online.OnlineServices.RecordStatus.CanSave || needsAccount);
        }
        if (AutoRun) {
            int score = UIManager.Instance != null ? UIManager.Instance.Score : 0;
            banner.text = RecordText() +
                          "<color=#EE8FFC>AUTOPILOTO - RANKING DO AUTOMÁTICO</color>\n" +
                          "<size=12>" + score + " pontos · Freny caiu após " + RunSeconds.ToString("0") + "s\n" +
                          "No automático caem menos moedas e menos XP</size>\n" +
                          "<size=12>Jogue no Manual para o ranking principal</size>" +
                          (asking ? "" : "\n<size=12>Nova corrida em " + Mathf.CeilToInt(SecondsUntilRestart) + "s</size>");
            return;
        }
        string result = Online.OnlineHUD.Instance != null ? Online.OnlineHUD.Instance.LastResult : null;
        string next = WaitsForKey ? Time.time - deathTime >= minDeathTime ? Key(GameAction.Jump) + ": nova corrida" : ""
            : "Nova corrida em " + Mathf.CeilToInt(SecondsUntilRestart) + "s";
        banner.text = RecordText() +
                      "<color=#EE8FFC>Freny caiu após " + RunSeconds.ToString("0") + "s</color>\n" +
                      (string.IsNullOrEmpty(result) ? "" : "<size=12>" + result + "</size>\n") + next;
    }

    private static Online.OnlineServices.RecordStatus Record =>
        Online.OnlineServices.Instance != null ? Online.OnlineServices.Instance.Record : Online.OnlineServices.RecordStatus.None;

    // NOVO RECORDE on top of the banner when the run beat the board's best, and what happens to it:
    // it can be saved, it is being saved, it was, or why it can't be (José, 2026-10-29: players
    // must know when a record does not go to the ranking)
    private static string RecordText() {
        Online.OnlineServices online = Online.OnlineServices.Instance;
        if (online == null || online.Record == Online.OnlineServices.RecordStatus.None) {
            return "";
        }
        string status;
        switch (online.Record) {
            case Online.OnlineServices.RecordStatus.CanSave: status = "Salve o recorde para ele ir para o ranking"; break;
            case Online.OnlineServices.RecordStatus.NeedsAccount: status = "<color=#FFD84A>Crie uma conta para salvar este recorde no ranking</color>"; break;
            case Online.OnlineServices.RecordStatus.Saving: status = "Salvando o recorde..."; break;
            case Online.OnlineServices.RecordStatus.Saved: status = "<color=#7CFC8A>Recorde salvo no ranking!</color>"; break;
            case Online.OnlineServices.RecordStatus.InReview: status = "<color=#FFD84A>Em revisão: entra no ranking quando for aprovado</color>"; break;
            case Online.OnlineServices.RecordStatus.Refused: status = "<color=#FF7A7A>Recusado pelo servidor: " + online.RecordNote + "</color>"; break;
            default: status = "<color=#FF7A7A>Não vai para o ranking: " + online.RecordNote + "</color>"; break;
        }
        return "<size=30><color=#FFD84A>" + (online.RecordOnlyToday ? "RECORDE DO DIA!" : "NOVO RECORDE!") + "</color></size>\n" +
               "<size=16>" + online.RecordScore + " pontos no " + ArenaDifficulty.Label(online.RecordLevel) +
               (online.RecordAuto ? " (automático)" : "") + "</size>\n" +
               "<size=14>" + status + "</size>\n";
    }

    // a guest's record: the button creates the account, and the record goes right after it
    private void AddSaveButton(Transform canvas, bool needsAccount) {
        MenuKit.EnsureEventSystem();
        RectTransform row = MenuKit.MakeRect("Save Record", canvas);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, -120f);   // under the record banner
        row.sizeDelta = new Vector2(needsAccount ? 620f : 420f, 64f);
        Button save = needsAccount
            ? MenuKit.MakeButton(row, "CRIAR CONTA PARA SALVAR", () => GameMenus.Instance?.ShowCreateAccount(), MenuKit.TextSize + 6)
            : MenuKit.MakeButton(row, "SALVAR RECORDE", () => Online.OnlineServices.Instance?.SaveRecord(), MenuKit.TextSize + 6);
        MenuKit.Stretch(save.GetComponent<RectTransform>());
        save.Select();
        saveButton = row.gameObject;
    }

    // "Jogar de novo" or "Jogar agora" (also on Space) and "Menu", under the banner
    private void AddChoices(Transform canvas, string playLabel) {
        MenuKit.EnsureEventSystem();
        RectTransform row = MenuKit.MakeRect("Choices", canvas);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, bannerHasRecord ? -190f : -60f);   // under the auto panel and "Salvar recorde"
        row.sizeDelta = new Vector2(600f, 44f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        Button again = MenuKit.MakeButton(row, playLabel, PlayAgain);
        if (GameMenus.HasMainMenu) {
            MenuKit.MakeButton(row, "Menu", GameMenus.LoadMainMenu);
        }
        again.Select();
    }

    private static string Key(GameAction action) {
        return InputBindings.KeyName(InputBindings.Keyboard(action)).Replace(" esq.", "").Replace(" dir.", "");
    }
}
