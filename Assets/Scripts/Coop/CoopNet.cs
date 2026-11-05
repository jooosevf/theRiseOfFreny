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

// Throwaway co-op prototype (branch proto/coop-relay): the connection and the latency numbers.
// Netcode for GameObjects carries named messages only, with no NetworkObjects, so nothing in the
// game's scenes or prefabs changes. In WebGL the Relay connection has to be WebSockets ("wss"),
// which runs over TCP: an "unreliable" message is delivered anyway, and a lost packet delays the
// ones behind it. The test measures that.
public static class CoopNet {

    public const string ArenaScene = "ZE Arena";
    private const ushort ProtocolVersion = 8;
    private const int MaxPlayers = 2;
    private const float PingInterval = 0.5f;
    private const int RttWindow = 240;   // the last 2 minutes of pings

    public static NetworkManager Net { get; private set; }
    public static UnityTransport Transport { get; private set; }
    public static bool IsHost => Net != null && Net.IsHost;
    public static bool InRoom => Net != null && Net.IsListening;
    public static bool Connected { get; private set; }
    public static ulong PeerId { get; private set; }
    public static string JoinCode { get; private set; }
    public static string Status { get; private set; } = "";

    public static event Action<ulong, FastBufferReader> PlayerState;
    public static event Action<ulong, FastBufferReader> EnemyState;
    public static event Action<ulong, FastBufferReader> EnemyHit;
    public static event Action<ulong, FastBufferReader> Hurt;
    public static event Action<ulong, FastBufferReader> HexCast;
    public static event Action<ulong, FastBufferReader> World;
    public static event Action<ulong, FastBufferReader> Effect;
    public static event Action<ulong, FastBufferReader> PeerName;

    // ---- what the overlay and the log report
    public static float LastRtt { get; private set; }
    public static int BytesPerSecond { get; private set; }
    private static readonly List<float> rtts = new List<float>();
    private static int bytesThisSecond;
    private static float secondStart;
    private static float nextPing;

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

    public static int TransportRtt => Connected && Transport != null ? (int)Transport.GetCurrentRtt(PeerId) : 0;

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
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CoopArgs.RelayConnection));
                Transport.UseWebSockets = CoopArgs.UseWebSockets;
            }
            if (!Net.StartHost()) {
                throw new Exception("o host não iniciou");
            }
            Listen();
            Status = "Sala criada. Passe o código para o outro jogador.";
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
            PeerId = NetworkManager.ServerClientId;
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
            ConnectionApproval = false,
            ProtocolVersion = ProtocolVersion,   // a build with other messages can't join by mistake
        };
        go.AddComponent<CoopPump>();
    }

    public static void Shutdown() {
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
        JoinCode = null;
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
        messages.RegisterNamedMessageHandler("coop.start", (_, __) => LoadArena());
        messages.RegisterNamedMessageHandler("coop.ping", OnPing);
        messages.RegisterNamedMessageHandler("coop.pong", OnPong);
        messages.RegisterNamedMessageHandler("coop.p", (id, r) => PlayerState?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.e", (id, r) => EnemyState?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.h", (id, r) => EnemyHit?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.hurt", (id, r) => Hurt?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.hex", (id, r) => HexCast?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.w", (id, r) => World?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.a", CoopShared.OnAsk);
        messages.RegisterNamedMessageHandler("coop.fx", (id, r) => Effect?.Invoke(id, r));
        messages.RegisterNamedMessageHandler("coop.name", (id, r) => PeerName?.Invoke(id, r));
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
        PeerId = id;
        Connected = true;
        CoopRooms.Close();
        Debug.Log($"[Coop] jogador {id} entrou");
        using FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp);
        Send("coop.start", writer, NetworkDelivery.ReliableSequenced);
        LoadArena();
    }

    private static void OnDisconnected(ulong id) {
        Debug.Log($"[Coop] desconectou: {id} ({Net.DisconnectReason})");
        if (IsHost && id != PeerId) {
            return;
        }
        Connected = false;
        Status = "O outro jogador saiu.";
        if (!IsHost) {
            Leave();
            Status = "O host saiu, a sala acabou.";
        }
    }

    // back to the Co-op page of the menu, with nothing left of this room
    public static void Leave() {
        CoopRooms.Close();
        Reset();
        Status = "";
        CoopScreens.ReopenOnMenu = true;
        GameMenus.LoadMainMenu();
    }

    // both Frenys have fallen: the host starts the next run on both sides
    public static void RestartRun() {
        using FastBufferWriter writer = new FastBufferWriter(4, Allocator.Temp);
        Send("coop.restart", writer, NetworkDelivery.ReliableSequenced);
        LoadArena();
    }

    private static void LoadArena() {
        // no first-run help page or menu state in the way: both players go straight in
        PlayerPrefs.SetInt("freny.help.seen", 1);
        ArenaDifficulty.Manual = !CoopArgs.Autopilot;
        SceneManager.LoadScene(ArenaScene);
    }

    public static void Send(string name, FastBufferWriter writer, NetworkDelivery delivery) {
        if (!Connected || Net == null || !Net.IsListening) {
            return;
        }
        bytesThisSecond += writer.Length;
        Net.CustomMessagingManager.SendNamedMessage(name, PeerId, writer, delivery);
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
        if (!Connected || now < nextPing) {
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
        Send("coop.pong", writer, NetworkDelivery.UnreliableSequenced);
    }

    private static void OnPong(ulong sender, FastBufferReader reader) {
        reader.ReadValueSafe(out double sent);
        LastRtt = (float)((Time.realtimeSinceStartupAsDouble - sent) * 1000.0);
        rtts.Add(LastRtt);
        if (rtts.Count > RttWindow) {
            rtts.RemoveAt(0);
        }
    }
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
