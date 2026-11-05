using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using Online;

// Jogar > Co-op, in the main menu like the other pages: the player's name, the open rooms, making a
// room (password optional) and entering by code. Whoever is in a room waits in its lobby, with who
// else is there and their ping, until the host starts the run.
public static class CoopScreens {

    private const float Width = 680f;
    private const float RefreshSeconds = 4f;   // Lobby allows about one query a second per player
    private const float LobbySeconds = 1f;
    private const int CodeLength = 6;
    private const int MaxNameLength = 16;
    private const int MaxRoomNameLength = 24;
    private const string NamePref = "freny.coop.name";
    private const string DefaultName = "Jogador";
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);
    private static readonly string[] SizeNames = { "2", "3", "4" };

    // ping bars: how many light up, from the lowest
    private const int PingBars = 4;
    private const int GoodPing = 60;
    private const int FairPing = 120;
    private const int PoorPing = 200;
    private static readonly Color GoodColor = new Color(0.45f, 0.85f, 0.45f);
    private static readonly Color FairColor = new Color(0.95f, 0.8f, 0.3f);
    private static readonly Color BadColor = new Color(0.9f, 0.3f, 0.25f);
    private static readonly Color OffColor = new Color(1f, 1f, 1f, 0.12f);

    // the player left a room: the menu opens on this page again
    public static bool ReopenOnMenu { get; set; }

    // what shows over his head: what he typed here, or else his account's nickname
    public static string PlayerName {
        get {
            string test = CoopArgs.Value("-coopName");   // two test copies on one PC share their prefs
            if (test != null) {
                return test;
            }
            string typed = PlayerPrefs.GetString(NamePref, "");
            if (!string.IsNullOrEmpty(typed)) {
                return typed;
            }
            string nick = OnlineServices.Instance != null && OnlineServices.Instance.Save != null ? OnlineServices.Instance.Save.playerName : null;
            return string.IsNullOrEmpty(nick) ? DefaultName : nick;
        }
        set {
            PlayerPrefs.SetString(NamePref, value.Trim());
            PlayerPrefs.Save();
        }
    }

    // MainMenu calls this when it starts
    public static void AfterMenu(MenuStack stack) {
        if (ReopenOnMenu) {
            ReopenOnMenu = false;
            stack.Push(Root(stack));
        }
    }

    public static MenuPage Root(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Co-op", Width, Middle, 6f);
        InputField name = page.Input("Seu nome", PlayerName, false, MaxNameLength);
        name.onEndEdit.AddListener(value => PlayerName = value);
        page.Space(4f);
        page.Button("Salas abertas", () => stack.Push(Rooms(stack)));
        page.Button("Criar sala", () => stack.Push(Create(stack)));
        page.Button("Entrar com código", () => stack.Push(Code(stack)));
        Text status = page.Label(CoopNet.Status);
        page.Button("Voltar", stack.Back);
        CoopNet.SetStatus("");
        status.gameObject.SetActive(!string.IsNullOrEmpty(status.text));
        return page;
    }

    // ---- the list ----

    private static MenuPage Rooms(MenuStack stack, List<Lobby> known = null) {
        MenuPage page = new MenuPage(stack.transform, "Salas abertas", Width, Middle, 6f);
        Text status = page.Label(known == null ? "Procurando salas..." : known.Count == 0 ? "Nenhuma sala aberta agora" : "");
        if (known != null) {
            foreach (Lobby room in known) {
                Lobby target = room;
                page.Button($"{room.Name} ({room.Players.Count}/{room.MaxPlayers}){(room.HasPassword ? " senha" : "")}", () => {
                    if (target.HasPassword) {
                        stack.Push(Password(stack, target));
                    } else {
                        Enter(stack, target, "", status, null);
                    }
                });
            }
        }
        page.Button("Voltar", stack.Back);

        float next = known == null ? 0f : Time.unscaledTime + RefreshSeconds;
        bool asking = false;
        Tick(page, async () => {
            if (asking || Time.unscaledTime < next || !page.Visible || CoopNet.InRoom) {
                return;
            }
            asking = true;
            try {
                List<Lobby> rooms = await CoopRooms.List();
                if (page.Root == null) {
                    return;
                }
                if (known == null || !Same(known, rooms)) {
                    stack.ReplaceTop(Rooms(stack, rooms));
                    return;
                }
            } catch (Exception e) {
                Debug.LogWarning("[Coop] lista de salas: " + e.Message);
                if (page.Root != null) {
                    status.text = "Lista de salas indisponível agora";
                }
            }
            next = Time.unscaledTime + RefreshSeconds;
            asking = false;
        }, null);
        return page;
    }

    private static bool Same(List<Lobby> a, List<Lobby> b) {
        if (a.Count != b.Count) {
            return false;
        }
        for (int i = 0; i < a.Count; i++) {
            if (a[i].Id != b[i].Id || a[i].Players.Count != b[i].Players.Count || a[i].MaxPlayers != b[i].MaxPlayers) {
                return false;
            }
        }
        return true;
    }

    private static MenuPage Password(MenuStack stack, Lobby room) {
        MenuPage page = new MenuPage(stack.transform, room.Name, Width, Middle, 6f);
        InputField password = page.Input("Senha", "", true, 32);
        Text status = page.Label("");
        Button enter = null;
        enter = page.Button("Entrar", () => Enter(stack, room, password.text, status, enter));
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static async void Enter(MenuStack stack, Lobby room, string password, Text status, Button button) {
        status.text = "Entrando na sala...";
        if (button != null) {
            button.interactable = false;
        }
        bool entered = await CoopRooms.Join(room.Id, password);
        if (stack == null) {
            return;
        }
        if (entered && CoopNet.InRoom) {
            stack.ReplaceTop(RoomLobby(stack));
            return;
        }
        if (status != null) {
            status.text = CoopNet.Status;
        }
        if (button != null) {
            button.interactable = true;
        }
    }

    // ---- making a room ----

    private static MenuPage Create(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Criar sala", Width, Middle, 6f);
        InputField roomName = page.Input("Nome da sala", "Sala de " + PlayerName, false, MaxRoomNameLength);
        InputField password = page.Input("Senha (opcional)", "", true, 32);
        page.Choice("Jogadores", SizeNames, CoopNet.RoomSize - CoopNet.MinRoomSize, i => CoopNet.RoomSize = CoopNet.MinRoomSize + i);
        Text status = page.Label($"Com senha, use pelo menos {CoopRooms.MinPasswordLength} caracteres");
        Button create = null;
        create = page.Button("Criar", async () => {
            if (password.text.Length > 0 && password.text.Length < CoopRooms.MinPasswordLength) {
                status.text = $"A senha precisa de pelo menos {CoopRooms.MinPasswordLength} caracteres";
                return;
            }
            create.interactable = false;
            status.text = "Criando a sala...";
            string name = roomName.text.Trim();
            await CoopRooms.Create(name.Length > 0 ? name : "Sala de " + PlayerName, password.text);
            if (page.Root == null) {
                return;
            }
            if (CoopNet.JoinCode != null) {
                stack.ReplaceTop(RoomLobby(stack));
            } else {
                status.text = CoopNet.Status;
                create.interactable = true;
            }
        });
        page.Button("Voltar", stack.Back);
        return page;
    }

    // ---- the lobby: everyone waits here for the host ----

    private static MenuPage RoomLobby(MenuStack stack) {
        bool host = CoopNet.IsHost;
        MenuPage page = new MenuPage(stack.transform, "Lobby", Width, Middle, 6f);
        if (host && CoopNet.JoinCode != null) {
            page.Label("Código da sala: " + CoopNet.JoinCode, MenuKit.TextSize);
        }

        RectTransform list = MenuKit.MakeRect("Players", page.Content);
        VerticalLayoutGroup column = list.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        DrawPlayers(list);

        Text note = page.Label("");
        Button start = null;
        if (host) {
            page.Choice("Jogadores", SizeNames, CoopNet.RoomSize - CoopNet.MinRoomSize, i => {
                CoopNet.RoomSize = Mathf.Max(CoopNet.MinRoomSize + i, CoopNet.Roster.Count);
                CoopRooms.Resize();
            });
            start = page.Button("Começar", CoopNet.StartRun, false);
        }
        page.Button(host ? "Fechar a sala" : "Sair da sala", stack.Back);

        float next = Time.unscaledTime + LobbySeconds;
        Tick(page, () => {
            if (start != null) {
                start.interactable = CoopNet.Roster.Count >= CoopNet.MinRoomSize;
            }
            note.text = host
                ? (CoopNet.Roster.Count < CoopNet.MinRoomSize
                    ? "Esperando outro jogador entrar"
                    : $"Com {CoopNet.Roster.Count} jogadores, a Arena fica {CoopNet.DifficultyFor(CoopNet.Roster.Count)}x mais difícil")
                : CoopNet.Connected ? "Esperando o host começar" : "Entrando na sala...";
            if (Time.unscaledTime >= next) {
                next = Time.unscaledTime + LobbySeconds;
                DrawPlayers(list);
            }
        }, () => {
            // leaving this page leaves the room, unless the run began
            if (CoopNet.InRoom && !CoopNet.Started) {
                CoopNet.Shutdown();
            }
        });
        return page;
    }

    private static void DrawPlayers(RectTransform list) {
        for (int i = list.childCount - 1; i >= 0; i--) {
            UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
        }
        foreach (CoopMember member in CoopNet.Roster) {
            bool me = member.Id == CoopNet.LocalId;
            Image row = MenuKit.MakeRow(list, member.Name + (me ? " (você)" : ""), out Text label);
            row.color = new Color(1f, 1f, 1f, 0.06f);
            label.fontSize = MenuKit.SmallTextSize;
            if (member.IsHost) {
                Text tag = MenuKit.MakeText(row.transform, "host", MenuKit.SmallTextSize, MenuKit.TitleColor, TextAnchor.MiddleRight);
                MenuKit.Size(tag, 92f, -1f);
            } else {
                int ping = me ? Mathf.RoundToInt(CoopNet.LastRtt) : member.Ping;
                Bars(row.transform, ping);
                Text ms = MenuKit.MakeText(row.transform, ping > 0 ? ping + " ms" : "...", MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleRight);
                MenuKit.Size(ms, 92f, -1f);
            }
        }
        for (int i = CoopNet.Roster.Count; i < CoopNet.RoomSize; i++) {
            Image row = MenuKit.MakeRow(list, "Vaga livre", out Text label);
            row.color = new Color(1f, 1f, 1f, 0.02f);
            label.fontSize = MenuKit.SmallTextSize;
            label.color = MenuKit.DimTextColor;
        }
    }

    private static void Bars(Transform parent, int ping) {
        int lit = ping <= 0 ? 0 : ping < GoodPing ? 4 : ping < FairPing ? 3 : ping < PoorPing ? 2 : 1;
        Color color = lit >= 3 ? GoodColor : lit == 2 ? FairColor : BadColor;
        RectTransform box = MenuKit.MakeRect("Ping", parent);
        MenuKit.Size(box, 44f, -1f);
        HorizontalLayoutGroup layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        for (int i = 0; i < PingBars; i++) {
            Image bar = MenuKit.MakeImage("Bar", box, i < lit ? color : OffColor);
            MenuKit.Size(bar, 8f, 8f + i * 6f);
        }
    }

    // ---- by code ----

    private static MenuPage Code(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Entrar com código", Width, Middle, 6f);
        InputField code = page.Input("Código", "", false, CodeLength);
        Text status = page.Label("");
        Button enter = null;
        enter = page.Button("Entrar", async () => {
            if (code.text.Trim().Length != CodeLength) {
                status.text = $"O código tem {CodeLength} letras";
                return;
            }
            enter.interactable = false;
            status.text = "Procurando a sala...";
            await CoopNet.Join(code.text, false);
            if (page.Root == null) {
                return;
            }
            if (CoopNet.InRoom) {
                stack.ReplaceTop(RoomLobby(stack));
            } else {
                status.text = CoopNet.Status;
                enter.interactable = true;
            }
        });
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static void Tick(MenuPage page, Action tick, Action gone) {
        CoopPageTicker ticker = page.Root.gameObject.AddComponent<CoopPageTicker>();
        ticker.OnTick = tick;
        ticker.OnGone = gone;
    }
}

// What a page does while it is open, and when it closes.
public class CoopPageTicker : MonoBehaviour {
    public Action OnTick;
    public Action OnGone;

    private void Update() {
        OnTick?.Invoke();
    }

    private void OnDestroy() {
        OnGone?.Invoke();
    }
}
