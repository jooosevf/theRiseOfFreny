using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Only for the test binaries (-coopMenuTest): walks the real menu to the room list, like a player,
// and takes a picture of each step.
public class CoopMenuTest : MonoBehaviour {

    private const float StepSeconds = 2.5f;

    private IEnumerator Start() {
        DontDestroyOnLoad(gameObject);
        string shots = CoopArgs.ShotDir;
        yield return new WaitForSecondsRealtime(StepSeconds);
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
        Shot(shots, "1_menu");
        Click("Jogar");
        yield return new WaitForSecondsRealtime(StepSeconds);
        Shot(shots, "2_jogar");
        Click("Co-op");
        yield return new WaitForSecondsRealtime(StepSeconds * 2f);
        Debug.Log($"[Coop] menu test: cena {SceneManager.GetActiveScene().name}");
        Shot(shots, "3_salas");
        yield return new WaitForSecondsRealtime(StepSeconds);
        Application.Quit();
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
