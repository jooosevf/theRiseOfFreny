using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using Online;

// Jogar > Co-op, in the main menu like the other pages: the player's name, the open rooms, making a
// room (password optional) and entering by code.
public static class CoopScreens {

    private const float Width = 680f;
    private const float RefreshSeconds = 4f;   // Lobby allows about one query a second per player
    private const int CodeLength = 6;
    private const int MaxNameLength = 16;
    private const int MaxRoomNameLength = 24;
    private const string NamePref = "freny.coop.name";
    private const string DefaultName = "Jogador";
    private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

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
        Tick(page, () => { }, null);
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
                page.Button(room.Name + (room.HasPassword ? "  (senha)" : ""), () => {
                    if (target.HasPassword) {
                        stack.Push(Password(stack, target));
                    } else {
                        Enter(target, "", status, null);
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
            if (a[i].Id != b[i].Id) {
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
        enter = page.Button("Entrar", () => Enter(room, password.text, status, enter));
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static async void Enter(Lobby room, string password, Text status, Button button) {
        status.text = "Entrando na sala...";
        if (button != null) {
            button.interactable = false;
        }
        await CoopRooms.Join(room.Id, password);
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
                stack.ReplaceTop(Waiting(stack));
            } else {
                status.text = CoopNet.Status;
                create.interactable = true;
            }
        });
        page.Button("Voltar", stack.Back);
        return page;
    }

    private static MenuPage Waiting(MenuStack stack) {
        MenuPage page = new MenuPage(stack.transform, "Sala criada", Width, Middle, 6f);
        page.Label("A Arena começa quando o outro jogador entrar", MenuKit.TextSize);
        page.Space(4f);
        page.Label("Código, para entrar sem a lista:");
        Text code = MenuKit.MakeText(page.Content, CoopNet.JoinCode, MenuKit.TitleSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
        MenuKit.Size(code, -1f, 56f);
        page.Button("Fechar a sala", stack.Back);
        // Esc and the button both land here: leaving this page closes the room, unless the game began
        Tick(page, () => { }, () => {
            if (CoopNet.InRoom && !CoopNet.Connected) {
                CoopRooms.Close();
                CoopNet.Shutdown();
            }
        });
        return page;
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
            if (page.Root != null) {
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
