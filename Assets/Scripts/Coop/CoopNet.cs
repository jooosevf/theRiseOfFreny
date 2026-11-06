using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

// The room's connection: up to 4 players, one of them the host. Clients only talk to the host, which
// forwards what the others need. Netcode for GameObjects carries named messages only, with no
// NetworkObjects, so nothing in the game's scenes or prefabs changes. In WebGL the Relay connection
// has to be WebSockets ("wss"), which runs over TCP: an "unreliable" message is delivered anyway, and
// a lost packet delays the ones behind it.
public static class CoopNet {

    public const string ArenaScene = "ZE Arena";
    private const ushort ProtocolVersion = 11;
    public const int MinRoomSize = 2;
    public const int MaxRoomSize = 4;
    private const float PingInterval = 0.5f;
    private const float RosterInterval = 1f;
    private const float NameInterval = 2f;
    private const int RttWindow = 240;   // the last 2 minutes of pings
    private const int RosterBytes = 512;
    // José, 2026-11-05: with 2 or 3 players the arena is 2x harder, with 4 it is 4x
    public static int DifficultyFor(int players) => players >= MaxRoomSize ? 4 : 2;

    public static NetworkManager Net { get; private set; }
    public static UnityTransport Transport { get; private set; }
    public static bool IsHost => Net != null && Net.IsHost;
    public static bool InRoom => Net != null && Net.IsListening;
    public static bool Connected { get; private set; }
    public static bool Started { get; private set; }
    public static int RoomSize { get; set; } = MinRoomSize;
    public static int Difficulty { get; private set; } = 1;
    public static ulong LocalId => Net != null ? Net.LocalClientId : 0;
    public static readonly List<CoopMember> Roster = new List<CoopMember>();
    public static string JoinCode { get; private set; }
    public static string Status { get; private set; } = "";

    public static event Action<ulong, FastBufferReader> PlayerState;
    public static event Action<ulong, FastBufferReader> EnemyState;
    public static event Action<ulong, FastBufferReader> EnemyHit;
    public static event Action<ulong, FastBufferReader> Hurt;
    public static event Action<ulong, FastBufferReader> HexCast;
    public static event Action<ulong, FastBufferReader> World;
    public static event Action<ulong, FastBufferReader> Effect;
    public static event Action LifeGift;

    // ---- what the overlay and the log report
    public static float LastRtt { get; private set; }
    public static int BytesPerSecond { get; private set; }
    private static readonly List<float> rtts = new List<float>();
    private static int bytesThisSecond;
    private static float secondStart;
    private static float nextPing;
    private static float nextRoster;
    private static float nextName;
    private static string hostName;
    private static readonly List<ulong> clients = new List<ulong>();
    private static readonly Dictionary<ulong, string> names = new Dictionary<ulong, string>();
    private static readonly Dictionary<ulong, float> pings = new Dictionary<ulong, float>();

    public static string RttSummary() {
        if (rtts.Count == 0) {
            return "sem medidas";
        }
        List<float> sorted = new List<float>(rtts);
        sorted.Sort();
        float sum = 0f;
        foreach (float rtt in sorted) {
            sum += rtt;
        }
        return $"atual {LastRtt:0} ms, min {sorted[0]:0}, média {sum / sorted.Count:0}, " +
               $"p95 {sorted[(int)(sorted.Count * 0.95f)]:0}, máx {sorted[sorted.Count - 1]:0} ({sorted.Count} pings)";
    }

    public static string NameOf(ulong id) {
        foreach (CoopMember member in Roster) {
            if (member.Id == id) {
                return member.Name;
            }
        }
        return null;
    }

    public static bool InRoster(ulong id) => NameOf(id) != null;

    // this player's place among the room's, for where he starts
    public static int Slot(ulong id) {
        List<ulong> ids = new List<ulong>();
        foreach (CoopMember member in Roster) {
            ids.Add(member.Id);
        }
        ids.Sort();
        return Mathf.Max(0, ids.IndexOf(id));
    }

    // ---- starting: Relay in the real test, plain UDP on this machine with -coopDirect
    public static async Task Host(bool direct) {
        Reset();
        try {
            CreateManager();
            if (direct) {
                Transport.SetConnectionData("127.0.0.1", CoopArgs.DirectPort, "127.0.0.1");
                JoinCode = "DIRETO";
            } else {
                Status = "Conectando ao Unity Relay...";
                await SignIn();
                // always the most it can hold: the host may change the room's size in the lobby
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxRoomSize - 1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CoopArgs.RelayConnection));
                Transport.UseWebSockets = CoopArgs.UseWebSockets;
            }
            if (!Net.StartHost()) {
                throw new Exception("o host não iniciou");
            }
            Listen();
            Connected = true;
            hostName = CoopScreens.PlayerName;
            Status = "";
            Debug.Log($"[Coop] host pronto, código {JoinCode}");
            CoopArgs.WriteCode(JoinCode);
        } catch (Exception e) {
            Fail(e);
        }
    }

    public static async Task Join(string code, bool direct) {
        Reset();
        try {
            CreateManager();
            if (direct) {
                Transport.SetConnectionData("127.0.0.1", CoopArgs.DirectPort);
            } else {
                Status = "Procurando a sala...";
                await SignIn();
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                Transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CoopArgs.RelayConnection));
                Transport.UseWebSockets = CoopArgs.UseWebSockets;
            }
            if (!Net.StartClient()) {
                throw new Exception("o cliente não iniciou");
            }
            Listen();
            Status = "Entrando na sala...";
        } catch (Exception e) {
            Fail(e);
        }
    }

    public static void SetStatus(string status) {
        Status = status;
    }

    public static async Task SignIn() {
        if (UnityServices.State != ServicesInitializationState.Initialized) {
            // two copies on one PC would share a player id, and a room refuses its own creator
            string profile = CoopArgs.Value("-coopProfile");
            InitializationOptions options = new InitializationOptions();
            if (!string.IsNullOrEmpty(profile)) {
                options.SetProfile(profile);
            }
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn) {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private static void CreateManager() {
        Application.runInBackground = true;   // a room doesn't stop for the other player when this window loses focus
        GameObject go = new GameObject("Coop Net");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Transport = go.AddComponent<UnityTransport>();
        Net = go.AddComponent<NetworkManager>();
        Net.NetworkConfig = new NetworkConfig {
            NetworkTransport = Transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            ProtocolVersion = ProtocolVersion,   // a build with other messages can't join by mistake
        };
        Net.ConnectionApprovalCallback = Approve;
        go.AddComponent<CoopPump>();
    }

    // the room is closed once the run began, and holds no more than the host picked
    private static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) {
        bool full = clients.Count + 1 >= RoomSize;
        response.Approved = !Started && !full;
        response.CreatePlayerObject = false;
        response.Reason = Started ? "A partida já começou." : "A sala está cheia.";
    }

    public static void Shutdown() {
        CoopRooms.Close();
        Reset();
        Status = "";
    }

    private static void Reset() {
        if (Net != null) {
            UnityEngine.Object.Destroy(Net.gameObject);
        }
        Net = null;
        Transport = null;
        Connected = false;
        Started = false;
        JoinCode = null;
        Difficulty = 1;
        Roster.Clear();
        clients.Clear();
        names.Clear();
        pings.Clear();
        rtts.Clear();
        LastRtt = 0f;
    }

    private static void Fail(Exception e) {
        Debug.LogError($"[Coop] falhou: {e}");
        Status = "Falhou: " + e.Message;
    }

    private static void Listen() {
        Net.OnClientConnectedCallback += OnConnected;
        Net.OnClientDisconnectCallback += OnDisconnected;
        CustomMessagingManager messages = Net.CustomMessagingManager;
        messages.RegisterNamedMessageHandler("coop.start", OnStart);
        messages.RegisterNamedMessageHandler("coop.r", OnRoster);
        messages.RegisterNamedMessageHandler("coop.gift", OnGift);
        messages.RegisterNamedMessageHandler("coop.ping", OnPing);
        messages.RegisterNamedMessageHandler("coop.pong", OnPong);
        messages.RegisterNamedMessageHandler("coop.p", (id, r) => {
            Forward("coop.p", id, r, NetworkDelivery.UnreliableSequenced);
            PlayerState?.Invoke(id, r);
        });
        messages.RegisterNamedMessageHandler("coop.e", (id, r) => EnemyState?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.h", (id, r) => EnemyHit?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.hurt", (id, r) => Hurt?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.hex", (id, r) => HexCast?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.w", (id, r) => World?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.a", CoopShared.OnAsk);
        messages.RegisterNamedMessageHandler("coop.fx", (id, r) => {
            Forward("coop.fx", id, r, NetworkDelivery.ReliableSequenced);
            Effect?.Invoke(id, r);
        });
        messages.RegisterNamedMessageHandler("coop.name", OnName);
        messages.RegisterNamedMessageHandler("coop.restart", (_, __) => LoadArena());
    }

    private static void OnConnected(ulong id) {
        if (!IsHost) {
            if (id == Net.LocalClientId) {
                Connected = true;
                Status = "Conectado. Esperando o host.";
                Debug.Log("[Coop] conectado ao host");
            }
            return;
        }
        if (id == NetworkManager.ServerClientId) {
            return;
        }
        clients.Add(id);
        nextRoster = 0f;
        Debug.Log($"[Coop] jogador {id} entrou, {clients.Count + 1} na sala");
    }

    // the host pressed Começar
    public static void StartRun() {
        if (!IsHost || Started) {
            return;
        }
        Started = true;
        CoopRooms.Close();
        SendRoster();
        Difficulty = DifficultyFor(clients.Count + 1);
        using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe((byte)Difficulty);
        Send("coop.start", writer, NetworkDelivery.ReliableSequenced);
        Debug.Log($"[Coop] começou com {clients.Count + 1} jogadores, dificuldade {Difficulty}x");
        LoadArena();
    }

    private static void OnStart(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out byte difficulty);
        Difficulty = difficulty;
        Started = true;
        Debug.Log($"[Coop] começou, dificuldade {Difficulty}x");
        LoadArena();
    }

    private static void OnDisconnected(ulong id) {
        string reason = Net.DisconnectReason;
        Debug.Log($"[Coop] desconectou: {id} ({reason})");
        if (IsHost) {
            clients.Remove(id);
            names.Remove(id);
            pings.Remove(id);
            nextRoster = 0f;
            return;
        }
        Connected = false;
        bool neverIn = !Started && Roster.Count == 0;
        Leave();
        Status = neverIn && !string.IsNullOrEmpty(reason) ? reason : "O host saiu, a sala acabou.";
    }

    // back to the Co-op page of the menu, with nothing left of this room
    public static void Leave() {
        CoopRooms.Close();
        Reset();
        Status = "";
        Time.timeScale = 1f;
        CoopScreens.ReopenOnMenu = true;
        GameMenus.LoadMainMenu();
    }

    // everyone is out of lives: the host starts the next run on all sides
    public static void RestartRun() {
        using FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp);
        Send("coop.restart", writer, NetworkDelivery.ReliableSequenced);
        LoadArena();
    }

    private static void LoadArena() {
        // no first-run help page or menu state in the way: both players go straight in
        PlayerPrefs.SetInt("freny.help.seen", 1);
        ArenaDifficulty.Manual = !CoopArgs.Autopilot;   // no autopilot in a room; only the test binaries have it
        SceneManager.LoadScene(ArenaScene);
    }

    public static void Send(string name, FastBufferWriter writer, NetworkDelivery delivery) {
        if (!Connected || Net == null || !Net.IsListening) {
            return;
        }
        if (IsHost) {
            if (clients.Count > 0) {
                bytesThisSecond += writer.Length * clients.Count;
                Net.CustomMessagingManager.SendNamedMessage(name, clients, writer, delivery);
            }
            return;
        }
        bytesThisSecond += writer.Length;
        Net.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, writer, delivery);
    }

    public static void SendTo(ulong id, string name, FastBufferWriter writer, NetworkDelivery delivery) {
        if (!Connected || Net == null || !Net.IsListening) {
            return;
        }
        bytesThisSecond += writer.Length;
        Net.CustomMessagingManager.SendNamedMessage(name, id, writer, delivery);
    }

    public static void SendExcept(ulong id, string name, FastBufferWriter writer, NetworkDelivery delivery) {
        foreach (ulong client in clients) {
            if (client != id) {
                SendTo(client, name, writer, delivery);
            }
        }
    }

    // on the host: what one client sent goes on to the others, and the reader stays where it was
    private static void Forward(string name, ulong sender, FastBufferReader reader, NetworkDelivery delivery) {
        if (!IsHost || clients.Count < 2) {
            return;
        }
        int start = reader.Position;
        int length = reader.Length - start;
        byte[] data = new byte[length];
        reader.ReadBytesSafe(ref data, length);
        reader.Seek(start);
        using FastBufferWriter writer = new FastBufferWriter(length + 8, Allocator.Temp);
        writer.WriteBytesSafe(data, length);
        SendExcept(sender, name, writer, delivery);
    }

    // a life for someone who has none left: the host hands it over
    public static void SendGift(ulong target) {
        if (IsHost && target == LocalId) {
            LifeGift?.Invoke();
            return;
        }
        using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(target);
        if (IsHost) {
            SendTo(target, "coop.gift", writer, NetworkDelivery.ReliableSequenced);
        } else {
            SendTo(NetworkManager.ServerClientId, "coop.gift", writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private static void OnGift(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out ulong target);
        if (target == LocalId) {
            LifeGift?.Invoke();
        } else if (IsHost) {
            SendGift(target);
        }
    }

    // ---- who is in the room: the host keeps it and tells the others
    private static void OnName(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out FixedString64Bytes name);
        names[sender] = name.ToString();
    }

    private static void SendRoster() {
        Roster.Clear();
        Roster.Add(new CoopMember { Id = LocalId, Name = hostName, Ping = 0, IsHost = true });
        foreach (ulong client in clients) {
            names.TryGetValue(client, out string name);
            pings.TryGetValue(client, out float ping);
            Roster.Add(new CoopMember { Id = client, Name = string.IsNullOrEmpty(name) ? "Jogador" : name, Ping = Mathf.RoundToInt(ping) });
        }
        using FastBufferWriter writer = new FastBufferWriter(RosterBytes, Allocator.Temp);
        writer.WriteValueSafe((byte)RoomSize);
        writer.WriteValueSafe((byte)Roster.Count);
        foreach (CoopMember member in Roster) {
            writer.WriteValueSafe(member.Id);
            writer.WriteValueSafe(new FixedString64Bytes(member.Name));
            writer.WriteValueSafe((ushort)Mathf.Clamp(member.Ping, 0, ushort.MaxValue));
        }
        Send("coop.r", writer, NetworkDelivery.ReliableSequenced);
    }

    private static void OnRoster(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out byte size);
        reader.ReadValueSafe(out byte count);
        RoomSize = size;
        Roster.Clear();
        for (int i = 0; i < count; i++) {
            reader.ReadValueSafe(out ulong id);
            reader.ReadValueSafe(out FixedString64Bytes name);
            reader.ReadValueSafe(out ushort ping);
            Roster.Add(new CoopMember { Id = id, Name = name.ToString(), Ping = ping, IsHost = id == NetworkManager.ServerClientId });
        }
    }

    // ---- ping: a message of ours through the same path as the game's, so the time a frame takes
    // on each side counts, which is what a player feels
    public static void Tick() {
        CoopRooms.Tick();
        float now = Time.unscaledTime;
        if (now - secondStart >= 1f) {
            BytesPerSecond = bytesThisSecond;
            bytesThisSecond = 0;
            secondStart = now;
        }
        if (!Connected) {
            return;
        }
        if (IsHost) {
            if (now >= nextRoster) {
                nextRoster = now + RosterInterval;
                SendRoster();
            }
        } else if (now >= nextName) {
            nextName = now + NameInterval;
            using FastBufferWriter nameWriter = new FastBufferWriter(80, Allocator.Temp);
            nameWriter.WriteValueSafe(new FixedString64Bytes(CoopScreens.PlayerName));
            SendTo(NetworkManager.ServerClientId, "coop.name", nameWriter, NetworkDelivery.ReliableSequenced);
        }
        if (now < nextPing) {
            return;
        }
        nextPing = now + PingInterval;
        using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(Time.realtimeSinceStartupAsDouble);
        Send("coop.ping", writer, NetworkDelivery.UnreliableSequenced);
    }

    private static void OnPing(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out double sent);
        using FastBufferWriter writer = new FastBufferWriter(8, Allocator.Temp);
        writer.WriteValueSafe(sent);
        SendTo(sender, "coop.pong", writer, NetworkDelivery.UnreliableSequenced);
    }

    private static void OnPong(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out double sent);
        float rtt = (float)((Time.realtimeSinceStartupAsDouble - sent) * 1000.0);
        if (IsHost) {
            pings[sender] = rtt;   // each client's, for the lobby
            return;
        }
        LastRtt = rtt;
        rtts.Add(LastRtt);
        if (rtts.Count > RttWindow) {
            rtts.RemoveAt(0);
        }
    }
}

// One player in the room, as the lobby and the name tags show him.
public class CoopMember {
    public ulong Id;
    public string Name;
    public int Ping;
    public bool IsHost;
}

// Update for the static CoopNet: the pings and the bytes counter keep going in any scene.
public class CoopPump : MonoBehaviour {
    private void Update() {
        CoopNet.Tick();
    }
}

// Command line of the test binaries; the WebGL build has none of it and runs with the defaults.
public static class CoopArgs {
    public const ushort DirectPort = 7777;

    private static string[] args;
    private static string[] All => args ??= System.Environment.GetCommandLineArgs();

    public static bool Has(string name) => Array.IndexOf(All, name) >= 0;
    public static string Value(string name) {
        int i = Array.IndexOf(All, name);
        return i >= 0 && i + 1 < All.Length ? All[i + 1] : null;
    }

    // the browser only has WebSockets; a desktop build (Steam) can use real UDP, encrypted (dtls)
#if UNITY_WEBGL
    public static bool UseWebSockets => true;
#else
    public static bool UseWebSockets => Has("-coopWss");
#endif
    public static string RelayConnection => UseWebSockets ? "wss" : "dtls";

    public static bool Direct => Has("-coopDirect");
    public static bool Autopilot => Has("-coopAutopilot");
    public static bool AutoHost => Has("-coopHost");
    public static string AutoJoin => Value("-coopJoin");
    public static string ShotDir => Value("-coopShots");
    public static float ShotEvery => float.TryParse(Value("-coopShotEvery"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds) ? seconds : 5f;
    public static float DieEvery => float.TryParse(Value("-coopDieEvery"), out float seconds) ? seconds : 0f;
    public static float DieAt => float.TryParse(Value("-coopDieAt"), out float seconds) ? seconds : 0f;
    public static float QuitAfter => float.TryParse(Value("-coopQuitAfter"), out float seconds) ? seconds : 0f;

    // the test runner reads the Relay code from this file and hands it to the client
    public static void WriteCode(string code) {
        string path = Value("-coopCodeFile");
        if (!string.IsNullOrEmpty(path)) {
            System.IO.File.WriteAllText(path, code);
        }
    }
}
