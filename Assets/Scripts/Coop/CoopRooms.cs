using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

// The list of rooms (Unity Lobby). A room lives in the list until the host starts the run: it carries
// the Relay code, and the game itself goes over Relay.
public static class CoopRooms {

    public const int MinPasswordLength = 8;   // Lobby refuses a shorter one
    private const string RelayKey = "relay";
    private const float HeartbeatSeconds = 15f;   // Lobby drops a room that stops sending these

    private static string openRoom;
    private static string joinedRoom;
    private static float nextHeartbeat;

    public static async Task Create(string name, string password) {
        await CoopNet.Host(false);
        if (CoopNet.JoinCode == null) {
            return;   // CoopNet said why
        }
        try {
            CreateLobbyOptions options = new CreateLobbyOptions {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject> {
                    { RelayKey, new DataObject(DataObject.VisibilityOptions.Member, CoopNet.JoinCode) },
                },
            };
            if (!string.IsNullOrEmpty(password)) {
                options.Password = password;
            }
            Lobby room = await LobbyService.Instance.CreateLobbyAsync(name, CoopNet.RoomSize, options);
            openRoom = room.Id;
            nextHeartbeat = Time.unscaledTime + HeartbeatSeconds;
            Debug.Log($"[Coop] sala \"{room.Name}\" criada{(room.HasPassword ? " com senha" : "")}");
        } catch (LobbyServiceException e) {
            Debug.LogError($"[Coop] a sala não entrou na lista: {e}");
            CoopNet.SetStatus($"A sala não entrou na lista ({e.Reason}). O código abaixo ainda funciona.");
        }
    }

    public static async Task<List<Lobby>> List() {
        await CoopNet.SignIn();
        QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 20 });
        return response.Results;
    }

    // false: wrong password, or the room is gone (CoopNet.Status says which)
    public static async Task<bool> Join(string roomId, string password) {
        try {
            await CoopNet.SignIn();
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions();
            if (!string.IsNullOrEmpty(password)) {
                options.Password = password;
            }
            Lobby room = await LobbyService.Instance.JoinLobbyByIdAsync(roomId, options);
            joinedRoom = room.Id;
            await CoopNet.Join(room.Data[RelayKey].Value, false);
            return true;
        } catch (LobbyServiceException e) {
            Debug.LogWarning($"[Coop] não entrou na sala: {e}");
            CoopNet.SetStatus(e.Reason == LobbyExceptionReason.IncorrectPassword || e.Reason == LobbyExceptionReason.ValidationError
                ? "Senha errada."
                : e.Reason == LobbyExceptionReason.LobbyNotFound || e.Reason == LobbyExceptionReason.LobbyFull
                    ? "Essa sala não está mais disponível."
                    : $"Não foi possível entrar ({e.Reason}).");
            return false;
        }
    }

    public static void Tick() {
        if (openRoom != null && Time.unscaledTime >= nextHeartbeat) {
            nextHeartbeat = Time.unscaledTime + HeartbeatSeconds;
            _ = Beat(openRoom);
        }
    }

    private static async Task Beat(string room) {
        try {
            await LobbyService.Instance.SendHeartbeatPingAsync(room);
        } catch (Exception e) {
            Debug.LogWarning($"[Coop] heartbeat da sala falhou: {e.Message}");
        }
    }

    // the host changed the room's size in the lobby
    public static void Resize() {
        if (openRoom != null) {
            _ = Update(openRoom, CoopNet.RoomSize);
        }
    }

    private static async Task Update(string room, int size) {
        try {
            await LobbyService.Instance.UpdateLobbyAsync(room, new UpdateLobbyOptions { MaxPlayers = size });
        } catch (Exception e) {
            Debug.LogWarning($"[Coop] a sala não mudou de tamanho: {e.Message}");
        }
    }

    // the run began or the room closed: it leaves the list; a guest gives up his place
    public static void Close() {
        if (joinedRoom != null) {
            string joined = joinedRoom;
            joinedRoom = null;
            _ = LeaveRoom(joined);
        }
        if (openRoom == null) {
            return;
        }
        string room = openRoom;
        openRoom = null;
        _ = Delete(room);
    }

    private static async Task LeaveRoom(string room) {
        try {
            await LobbyService.Instance.RemovePlayerAsync(room, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
        } catch (Exception e) {
            Debug.LogWarning($"[Coop] não saiu da lista da sala: {e.Message}");
        }
    }

    private static async Task Delete(string room) {
        try {
            await LobbyService.Instance.DeleteLobbyAsync(room);
        } catch (Exception e) {
            Debug.LogWarning($"[Coop] a sala não saiu da lista: {e.Message}");
        }
    }
}
