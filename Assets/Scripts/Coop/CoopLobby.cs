using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;

// The first scene of the co-op test build: the list of rooms, make one (password optional), or type a
// code. Plain IMGUI for now; it becomes a page of the real menu (Jogar > Co-op) when this goes to dev.
public class CoopLobby : MonoBehaviour {

    private const int CodeLength = 6;
    private const float RefreshSeconds = 4f;   // Lobby allows about one query a second per player

    private string code = "";
    private string roomName = "Sala do Freny";
    private string password = "";
    private string joinPassword = "";
    private bool busy;
    private bool joining;
    private float nextRefresh;
    private bool refreshing;
    private List<Lobby> rooms = new List<Lobby>();
    private string listError;
    private Vector2 scroll;
    private GUIStyle title, label, small, button, field;

    private void Start() {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        if (CoopArgs.Has("-coopMute")) {
            AudioListener.volume = 0f;
        }
        if (CoopArgs.AutoHost) {
            if (CoopArgs.Direct) {
                Host();
            } else {
                password = CoopArgs.Value("-coopPass") ?? "";
                MakeRoom();
            }
        } else if (CoopArgs.AutoJoin != null) {
            code = CoopArgs.AutoJoin;
            Join();
        }
    }

    private void Update() {
        bool browsing = CoopNet.JoinCode == null && !CoopArgs.Direct && !busy;
        if (browsing && !refreshing && Time.unscaledTime >= nextRefresh) {
            Refresh();
        }
    }

    private async void Refresh() {
        refreshing = true;
        try {
            rooms = await CoopRooms.List();
            listError = null;
            string first = CoopArgs.Value("-coopJoinRoom");
            if (first != null && rooms.Count > 0 && !joining) {
                joinPassword = first;
                EnterRoom(rooms[0]);
            }
        } catch (Exception e) {
            listError = "Lista de salas indisponível: " + e.Message;
            Debug.LogWarning("[Coop] " + listError);
        }
        nextRefresh = Time.unscaledTime + RefreshSeconds;
        refreshing = false;
    }

    private async void Host() {
        busy = true;
        await CoopNet.Host(CoopArgs.Direct);
        busy = false;
    }

    private async void MakeRoom() {
        busy = true;
        await CoopRooms.Create(roomName.Trim().Length > 0 ? roomName.Trim() : "Sala do Freny", password);
        busy = false;
    }

    private async void Join() {
        busy = true;
        await CoopNet.Join(code, CoopArgs.Direct);
        busy = false;
    }

    private async void EnterRoom(Lobby room) {
        busy = joining = true;
        bool entered = await CoopRooms.Join(room.Id, room.HasPassword ? joinPassword : "");
        joining = entered;
        busy = false;
    }

    private void Styles() {
        if (title != null) {
            return;
        }
        title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(0.78f, 0.16f, 0.14f);
        label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        label.normal.textColor = new Color(0.82f, 0.78f, 0.72f);
        small = new GUIStyle(label) { fontSize = 14 };
        button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
    }

    private void OnGUI() {
        Styles();
        float w = Mathf.Min(620f, Screen.width - 32f);
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, 16f, w, Screen.height - 32f));
        GUILayout.Label("Freny: co-op", title, GUILayout.Height(56f));

        if (CoopNet.JoinCode != null && CoopNet.IsHost) {
            GUILayout.Label("Sala criada. A Arena começa quando o outro jogador entrar.", label);
            GUILayout.Label("Código, para entrar sem a lista:", small);
            GUILayout.Label(CoopNet.JoinCode, title, GUILayout.Height(56f));
        } else {
            GUI.enabled = !busy;
            MakeRoomGui();
            GUILayout.Space(10f);
            RoomList();
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Código:", small, GUILayout.Width(70f));
            code = GUILayout.TextField(code, CodeLength, field, GUILayout.Height(36f));
            GUI.enabled = !busy && code.Trim().Length == CodeLength;
            if (GUILayout.Button("Entrar", button, GUILayout.Width(110f), GUILayout.Height(36f))) {
                Join();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }
        GUILayout.Space(8f);
        GUILayout.Label(CoopNet.Status, label);
        GUILayout.EndArea();
    }

    private void MakeRoomGui() {
        GUILayout.BeginHorizontal();
        roomName = GUILayout.TextField(roomName, 24, field, GUILayout.Height(36f));
        password = GUILayout.PasswordField(password, '*', 32, field, GUILayout.Width(170f), GUILayout.Height(36f));
        bool passwordOk = password.Length == 0 || password.Length >= CoopRooms.MinPasswordLength;
        bool was = GUI.enabled;
        GUI.enabled = was && passwordOk;
        if (GUILayout.Button("Criar sala", button, GUILayout.Width(130f), GUILayout.Height(36f))) {
            MakeRoom();
        }
        GUI.enabled = was;
        GUILayout.EndHorizontal();
        GUILayout.Label(passwordOk
            ? "Senha opcional, à direita do nome"
            : $"A senha precisa de pelo menos {CoopRooms.MinPasswordLength} caracteres", small);
    }

    private void RoomList() {
        GUILayout.Label(rooms.Count == 0 ? (listError ?? "Nenhuma sala aberta agora") : "Salas abertas", label);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(90f, Screen.height * 0.3f)));
        foreach (Lobby room in rooms) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(room.Name + (room.HasPassword ? "  (com senha)" : ""), label);
            if (room.HasPassword) {
                joinPassword = GUILayout.PasswordField(joinPassword, '*', 32, field, GUILayout.Width(150f), GUILayout.Height(32f));
            }
            if (GUILayout.Button("Entrar", button, GUILayout.Width(100f), GUILayout.Height(32f))) {
                EnterRoom(room);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }
}
