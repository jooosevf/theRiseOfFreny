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
