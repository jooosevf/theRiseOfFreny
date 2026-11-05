using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Only for the test binaries, driven from the command line: goes into a room without clicks and starts
// the run when enough players came, or walks the real menu like a player and takes a picture of each
// step, or runs the normal Arena.
public class CoopMenuTest : MonoBehaviour {

    private const float StepSeconds = 2.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() {
        if (CoopArgs.Has("-coopMute")) {
            AudioListener.volume = 0f;
        }
        if (CoopArgs.Has("-coopMenuTest") || CoopArgs.AutoHost || CoopArgs.AutoJoin != null || CoopArgs.Value("-coopJoinRoom") != null) {
            new GameObject("Coop Menu Test").AddComponent<CoopMenuTest>();
        }
    }

    private IEnumerator Start() {
        DontDestroyOnLoad(gameObject);
        string shots = CoopArgs.ShotDir;
        yield return new WaitForSecondsRealtime(StepSeconds);
        if (int.TryParse(CoopArgs.Value("-coopPlayers"), out int players)) {
            CoopNet.RoomSize = players;
        }
        if (CoopArgs.AutoHost) {
            Click("Jogar");
            yield return new WaitForSecondsRealtime(0.5f);
            MenuStack stack = FindAnyObjectByType<MenuStack>();
            stack.Push(CoopScreens.Root(stack));
            yield return new WaitForSecondsRealtime(0.5f);
            if (CoopArgs.Direct) {
                _ = CoopNet.Host(true);
            } else {
                _ = CoopRooms.Create("Sala de teste", CoopArgs.Value("-coopPass") ?? "");
            }
            while (CoopNet.Roster.Count < players) {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(StepSeconds);
            Shot(shots, "lobby_lista");
            yield return new WaitForSecondsRealtime(0.5f);
            CoopNet.StartRun();
            yield break;
        }
        if (CoopArgs.AutoJoin != null) {
            _ = CoopNet.Join(CoopArgs.AutoJoin, CoopArgs.Direct);
            yield break;
        }
        string password = CoopArgs.Value("-coopJoinRoom");
        if (password != null) {
            yield return JoinFirstRoom(password);
            yield break;
        }
        if (CoopArgs.Has("-coopSoloTest")) {
            // the normal game, no room: anything of Coop's leaking into it shows here
            PlayerPrefs.SetInt("freny.help.seen", 1);   // or the first-run Help page pauses it
            GameMenus.StartArena(false);
            yield return new WaitForSecondsRealtime(CoopArgs.QuitAfter);
            Player player = FindAnyObjectByType<Player>();
            Debug.Log($"[Coop] solo: cena {SceneManager.GetActiveScene().name}, sala {CoopNet.InRoom}, pontos {(UIManager.Instance ? UIManager.Instance.Score : -1)}, moedas {(player ? player.coins : -1)}, inimigos {Enemy.Active.Count}");
            Shot(shots, "solo");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
            yield break;
        }
        // the real lobby page, as a player reaches it
        foreach (string step in new[] { "Jogar", "Co-op", "Criar sala" }) {
            Click(step);
            yield return new WaitForSecondsRealtime(StepSeconds);
            Shot(shots, step.Replace(" ", "_"));
            yield return new WaitForSecondsRealtime(0.3f);
        }
        Click("Criar");
        float until = Time.unscaledTime + 40f;
        while (CoopNet.Roster.Count < Mathf.Max(1, players) && Time.unscaledTime < until) {
            yield return null;
        }
        yield return new WaitForSecondsRealtime(StepSeconds * 3f);
        Shot(shots, "Lobby");
        yield return new WaitForSecondsRealtime(1f);
        if (CoopNet.Roster.Count >= CoopNet.MinRoomSize) {
            Click("Começar");
            yield break;
        }
        CoopRooms.Close();
        yield return new WaitForSecondsRealtime(1.5f);
        Application.Quit();
    }

    private static IEnumerator JoinFirstRoom(string password) {
        while (!CoopNet.InRoom) {
            System.Threading.Tasks.Task<System.Collections.Generic.List<Unity.Services.Lobbies.Models.Lobby>> list = CoopRooms.List();
            while (!list.IsCompleted) {
                yield return null;
            }
            if (!list.IsFaulted && list.Result.Count > 0) {
                System.Threading.Tasks.Task<bool> join = CoopRooms.Join(list.Result[0].Id, list.Result[0].HasPassword ? password : "");
                while (!join.IsCompleted) {
                    yield return null;
                }
            }
            yield return new WaitForSecondsRealtime(4f);
        }
    }

    private static void Shot(string dir, string name) {
        if (!string.IsNullOrEmpty(dir)) {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
        }
    }

    private static void Click(string label) {
        foreach (Button button in FindObjectsByType<Button>()) {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null && text.text == label) {
                Debug.Log($"[Coop] menu test: clicou {label}");
                button.onClick.Invoke();
                return;
            }
        }
        Debug.LogWarning($"[Coop] menu test: botão {label} não existe");
    }
}
