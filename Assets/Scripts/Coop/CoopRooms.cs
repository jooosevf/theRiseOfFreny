using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

// The list of rooms (Unity Lobby). A room only lives in the list until the second player arrives:
// it carries the Relay code, and the game itself goes over Relay.
public static class CoopRooms {

    public const int MinPasswordLength = 8;   // Lobby refuses a shorter one
    private const string RelayKey = "relay";
    private const int RoomSize = 2;
    private const float HeartbeatSeconds = 15f;   // Lobby drops a room that stops sending these

    private static string openRoom;
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
            Lobby room = await LobbyService.Instance.CreateLobbyAsync(name, RoomSize, options);
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

    // the second player is in: the room leaves the list
    public static void Close() {
        if (openRoom == null) {
            return;
        }
        string room = openRoom;
        openRoom = null;
        _ = Delete(room);
    }

    private static async Task Delete(string room) {
        try {
            await LobbyService.Instance.DeleteLobbyAsync(room);
        } catch (Exception e) {
            Debug.LogWarning($"[Coop] a sala não saiu da lista: {e.Message}");
        }
    }
}
