using System.IO;
using UnityEngine;

// The first scene of the co-op test build: make a room or type the other player's code.
// Plain IMGUI on purpose: this page exists to measure latency and goes away with the prototype.
public class CoopLobby : MonoBehaviour {

    private const int CodeLength = 6;
    private string code = "";
    private bool busy;
    private GUIStyle title, label, button, field;

    private void Start() {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        if (CoopArgs.Has("-coopMute")) {
            AudioListener.volume = 0f;
        }
        if (CoopArgs.AutoHost) {
            Host();
        } else if (CoopArgs.AutoJoin != null) {
            code = CoopArgs.AutoJoin;
            Join();
        }
    }

    private async void Host() {
        busy = true;
        await CoopNet.Host(CoopArgs.Direct);
        busy = false;
    }

    private async void Join() {
        busy = true;
        await CoopNet.Join(code, CoopArgs.Direct);
        busy = false;
    }

    private void Styles() {
        if (title != null) {
            return;
        }
        title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(0.78f, 0.16f, 0.14f);
        label = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        label.normal.textColor = new Color(0.82f, 0.78f, 0.72f);
        button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
    }

    private void OnGUI() {
        Styles();
        float w = 460f;
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, Screen.height * 0.12f, w, Screen.height * 0.8f));
        GUILayout.Label("Freny: teste de co-op", title, GUILayout.Height(70f));
        GUILayout.Space(16f);

        if (CoopNet.JoinCode != null && CoopNet.IsHost) {
            GUILayout.Label("Código da sala", label);
            GUILayout.Label(CoopNet.JoinCode, title, GUILayout.Height(70f));
            GUILayout.Label("Esperando o outro jogador. A Arena começa quando ele entrar.", label);
        } else {
            GUI.enabled = !busy;
            if (GUILayout.Button("Criar sala", button, GUILayout.Height(54f))) {
                Host();
            }
            GUILayout.Space(24f);
            GUILayout.Label("ou digite o código da sala", label);
            code = GUILayout.TextField(code, CodeLength, field, GUILayout.Height(50f));
            GUI.enabled = !busy && code.Trim().Length == CodeLength;
            if (GUILayout.Button("Entrar", button, GUILayout.Height(54f))) {
                Join();
            }
            GUI.enabled = true;
        }
        GUILayout.Space(16f);
        GUILayout.Label(CoopNet.Status, label);
        GUILayout.EndArea();
    }
}
