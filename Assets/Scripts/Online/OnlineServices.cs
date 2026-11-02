using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Online {
    // Entry point for the online features. It creates itself when the game starts
    // (no scene setup needed), signs the player in, keeps their save in sync and
    // reports each run to the leaderboard when the player dies.
    public class OnlineServices : MonoBehaviour {
        public enum Status { Disabled, Connecting, Online, Offline }

        public const int LeaderboardSize = 10;
        // a board shown again within this many seconds is not read again (Firestore's free tier counts reads)
        private const float BoardFreshSeconds = 60f;
        public const int MaxNameLength = 16;
        private const string LocalSavePref = "online.save";
        private const string LocalBestPref = "online.best.";   // + level, this account's best on each ranked board
        private const string LocalAutoBestPref = "online.autoBest.";  // + level, best on the autopilot's board
        private const string LocalDailyPref = "online.daily.";  // + level, "<daily board>|<best>" for today's board
        private const string LocalSignedPref = "online.bestSigned.";  // + level, "<score>|<sig>" as the board has it
        private const string GuestChosenPref = "online.guestChosen";  // "Jogar como convidado" was picked

        public static OnlineServices Instance { get; private set; }

        public Status State { get; private set; } = Status.Disabled;
        public string LastError { get; private set; }
        public FirebaseAuthService Auth { get; private set; }
        public FirestoreClient Db => _db;   // for CloudProgressSync (save slots)
        public ProfileService Profiles { get; private set; }
        public FriendsService Friends { get; private set; }
        // the account changed, or its nickname or requests arrived (the main menu redraws)
        public event Action AccountChanged;
        public PlayerSave Save { get; private set; }
        // the autopilot drove at some point of this run: it won't count for the leaderboard
        public bool RunAssisted => _assisted;

        // score, isNewBest on its board, and why the run is off the ranking (null when it counts)
        public event Action<long, bool, string> RunReported;
        // a new best the ranking API did not take, and why
        public event Action<long, string> RankingRefused;

        // The last arena run's record, for the "NOVO RECORDE" box of the end-of-run banner: a run
        // that beats the board's best can be saved with "Salvar recorde" (and is saved anyway
        // when the player moves on), or says why it can't (the autopilot played, no connection).
        // NeedsAccount: a guest's record, kept until the guest creates an account (José, 2026-11-02)
        public enum RecordStatus { None, CanSave, Saving, Saved, CannotSave, Refused, InReview, NeedsAccount }
        public RecordStatus Record { get; private set; }
        public bool RecordOnlyToday { get; private set; }   // beats today's best, not the all-time one
        public long RecordScore { get; private set; }
        public ArenaLevel RecordLevel { get; private set; }
        public bool RecordAuto { get; private set; }        // on the autopilot's board
        public string RecordNote { get; private set; }   // why it can't be / wasn't saved
        public event Action RecordChanged;
        private PendingRecord _pending;

        private class PendingRecord {
            public ArenaLevel level;
            public long score;
            public long previousBest;
            public bool allTime;   // beats the all-time best too, not only today's
            public bool auto;      // the autopilot's board (no daily board)
            public RunLog log;
            public string token;
        }

        private FirestoreClient _db;
        private LeaderboardService _leaderboard;
        private ScoreApi _api;
        private readonly Dictionary<ArenaLevel, List<LeaderboardEntry>> _boards = new Dictionary<ArenaLevel, List<LeaderboardEntry>>();
        private readonly Dictionary<ArenaLevel, List<LeaderboardEntry>> _autoBoards = new Dictionary<ArenaLevel, List<LeaderboardEntry>>();
        private readonly Dictionary<string, float> _boardFetchedAt = new Dictionary<string, float>();
        private CloudSaveService _saves;
        private AchievementService _achievements;

        // Run tracking for the current scene.
        private Player _player;
        private FrenyInput _input;
        private RPlayer _runnerPlayer;
        private UIManager _ui;
        private ScoreManager _runnerScore;
        private bool _sawAlive;
        private bool _reported;
        private bool _assisted;
        private bool _arenaRun;
        private ArenaLevel _runLevel;
        private bool _runAuto;       // started in Automático: its ticket and record are for the autopilot's board
        private int _coinsAtStart;
        private RunLog _runLog;      // the ranked run's kills, for the ranking API
        private string _runToken;    // its ticket from the API (null until it answers)
        private bool _tokenAsked;    // asked for it already (as soon as the game is online)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() {
            if (Instance != null) return;
            GameObject go = new GameObject("OnlineServices");
            DontDestroyOnLoad(go);
            go.AddComponent<OnlineServices>();
            go.AddComponent<OnlineHUD>();
        }

        private void Awake() {
            Instance = this;
            Save = LoadLocal();
            SceneManager.sceneLoaded += OnSceneLoaded;
            UIManager.KillScored += OnKillScored;
            Achievements.Happened += OnAchievementMoment;
            Player.Vital += OnVital;
        }

        private void OnDestroy() {
            Player.Vital -= OnVital;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UIManager.KillScored -= OnKillScored;
            Achievements.Happened -= OnAchievementMoment;
        }

        private void OnKillScored(Enemy enemy, float combo) {
            if (_runLog != null && !_reported) _runLog.Add(enemy, combo);
        }

        private void OnAchievementMoment(string kind, int count) {
            if (_runLog != null && !_reported) _runLog.AddMoment(kind, count);
        }

        private void OnVital(string kind, int count) {
            if (_runLog != null && !_reported) _runLog.AddVital(kind, count);
        }

        // Cheat Engine's speedhack makes the game's clock run faster or slower than the
        // computer's: every couple of seconds both are compared, and several checks in a row more
        // than 25% apart flag the run (the server also compares game time with its own clock)
        private const double ClockCheckSeconds = 2.0;
        private double _clockGame;
        private DateTime _clockWall;
        private int _clockOff;

        private void CheckClock() {
            double game = Time.realtimeSinceStartupAsDouble;
            DateTime wall = DateTime.UtcNow;
            if (_clockWall == default) {
                _clockGame = game;
                _clockWall = wall;
                return;
            }
            double wallPassed = (wall - _clockWall).TotalSeconds;
            if (wallPassed < ClockCheckSeconds) return;
            double ratio = (game - _clockGame) / wallPassed;
            _clockOff = ratio > 1.25 || ratio < 0.75 ? _clockOff + 1 : 0;
            if (_clockOff >= 3) AntiCheat.Flag("speed");
            _clockGame = game;
            _clockWall = wall;
        }

        private void Start() {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

            FirebaseConfig config = FirebaseConfig.Load();
            if (config == null) return;

            Auth = new FirebaseAuthService(config.apiKey, config.authUrl);
            _db = new FirestoreClient(config.projectId, Auth, config.firestoreUrl);
            _leaderboard = new LeaderboardService(_db, Auth);
            _api = new ScoreApi(config.apiUrl, Auth);
            _saves = new CloudSaveService(_db, Auth);
            _achievements = new AchievementService(_db, _api, Auth);
            Profiles = new ProfileService(_db, Auth);
            Friends = new FriendsService(_db, Auth, Profiles);
            StartCoroutine(Connect());
        }

        public IEnumerator Connect() {
            State = Status.Connecting;
            bool ok = false;
            yield return Auth.SignIn((success, error) => {
                ok = success;
                LastError = error;
            });
            if (!ok) {
                State = Status.Offline;
                yield break;
            }
            yield return SyncSave();
            yield return _achievements.Load();
            State = Status.Online;
            yield return AfterLogin();
        }

        // the Perfil's numbers, the nickname and the friends' messages: one read each per login
        private IEnumerator AfterLogin() {
            yield return PlayerStats.Load(_db, Auth);
            yield return Profiles.AfterLogin(Save.playerName, TakeNick);
            yield return Friends.Sync();
            AccountChanged?.Invoke();
        }

        // the account's nickname is its name on the boards too
        private void TakeNick(string nick) {
            if (Save.playerName == nick) return;
            Save.playerName = nick;
            SaveLocal();
            StartCoroutine(PushName());
        }

        // ---- the start screen (AccountScreens.Welcome) ----

        // nobody decided yet on this device: no email account and "convidado" not picked
        public bool NeedsAccountChoice => State != Status.Disabled && Auth != null && Auth.IsAnonymous &&
                                          PlayerPrefs.GetInt(GuestChosenPref, 0) == 0;

        public void ChooseGuest() {
            PlayerPrefs.SetInt(GuestChosenPref, 1);
            PlayerPrefs.Save();
        }

        // the login that runs at launch has to finish first: it would overwrite this one
        private IEnumerator WaitForConnect() {
            while (State == Status.Connecting) yield return null;
        }

        // The top of one ranked board (ArenaDifficulty.Ranked), as last fetched.
        public List<LeaderboardEntry> TopFor(ArenaLevel level) {
            return _boards.TryGetValue(level, out List<LeaderboardEntry> top) ? top : new List<LeaderboardEntry>();
        }

        public List<LeaderboardEntry> AutoTopFor(ArenaLevel level) {
            return _autoBoards.TryGetValue(level, out List<LeaderboardEntry> top) ? top : new List<LeaderboardEntry>();
        }

        public long BestFor(ArenaLevel level, bool auto = false) {
            return long.TryParse(PlayerPrefs.GetString((auto ? LocalAutoBestPref : LocalBestPref) + level, "0"), out long best) ? best : 0;
        }

        private static void SetBest(ArenaLevel level, long best, bool auto = false) {
            PlayerPrefs.SetString((auto ? LocalAutoBestPref : LocalBestPref) + level, best.ToString());
            PlayerPrefs.Save();
        }

        // this account's best on today's board (0 once the day turns)
        public long DailyBestFor(ArenaLevel level) {
            string[] saved = PlayerPrefs.GetString(LocalDailyPref + level, "").Split('|');
            return saved.Length == 2 && saved[0] == LeaderboardService.DailyCollection(level) &&
                   long.TryParse(saved[1], out long best) ? best : 0;
        }

        private static void SetDailyBest(ArenaLevel level, string board, long best) {
            PlayerPrefs.SetString(LocalDailyPref + level, board + "|" + best);
            PlayerPrefs.Save();
        }

        private static void ClearBests() {
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                SetBest(level, 0);
                SetBest(level, 0, true);
                PlayerPrefs.DeleteKey(LocalDailyPref + level);
                PlayerPrefs.DeleteKey(LocalSignedPref + level);
            }
        }

        // this account's best as the board really has it, with the API's signature: what perfis/{uid}
        // can show (firestore.rules checks the signature, so nobody edits a best there)
        public static void SignedBest(ArenaLevel level, out long best, out string sig) {
            best = 0;
            sig = null;
            string[] saved = PlayerPrefs.GetString(LocalSignedPref + level, "").Split('|');
            if (saved.Length == 2 && long.TryParse(saved[0], out long score)) {
                best = score;
                sig = saved[1];
            }
        }

        private static void SetSignedBest(ArenaLevel level, long best, string sig) {
            if (best > 0 && !string.IsNullOrEmpty(sig)) PlayerPrefs.SetString(LocalSignedPref + level, best + "|" + sig);
            else PlayerPrefs.DeleteKey(LocalSignedPref + level);
        }

        // Reads only the board on screen, at most once a minute (10 reads instead of 80 for all eight boards).
        public IEnumerator RefreshBoard(ArenaLevel level, bool auto) {
            if (_leaderboard == null || !Auth.IsSignedIn) yield break;
            string collection = LeaderboardService.Collection(level, auto);
            if (_boardFetchedAt.TryGetValue(collection, out float at) && Time.realtimeSinceStartup - at < BoardFreshSeconds) yield break;
            _boardFetchedAt[collection] = Time.realtimeSinceStartup;
            yield return _leaderboard.GetTop(collection, LeaderboardSize, entries => {
                if (entries == null) {
                    _boardFetchedAt.Remove(collection);
                    return;
                }
                if (auto) _autoBoards[level] = entries;
                else _boards[level] = entries;
            });
        }

        // this account wrote to a board (or changed account), so the next view reads it again
        private void ForgetBoards() {
            _boardFetchedAt.Clear();
        }

        // the Conta page: done(error), null when the nickname is this account's
        public void ChangeNick(string nick, Action<string> done) {
            StartCoroutine(ChangeNickRoutine(nick, done));
        }

        private IEnumerator ChangeNickRoutine(string nick, Action<string> done) {
            string problem = ProfileService.NickProblem(nick);
            if (problem == null && (Auth == null || !Auth.IsSignedIn || Auth.IsAnonymous)) problem = "Crie uma conta primeiro";
            if (problem != null) {
                done?.Invoke(problem);
                yield break;
            }
            nick = nick.Trim();
            string error = null;
            if (Profiles.HasNick) yield return Profiles.Rename(nick, e => error = e);
            else yield return Profiles.Claim(nick, e => error = e);
            if (error == null) TakeNick(nick);
            AccountChanged?.Invoke();
            done?.Invoke(error);
        }

        // Renascer: the boards with a score of his get the new rebirth count (the star) right away,
        // the same write as a rename (LeaderboardService.Rename sends it)
        public void PushRebirths() {
            if (State == Status.Online) StartCoroutine(PushName());
        }

        // Email account actions, used by the account screens. done(errorOrNull), in the player's words
        // (AccountScreens shows them as they come).

        // "Criar conta": nickname, email and password. The guest account becomes this one, same user
        // id, so progress, Conquistas and stats stay. A record waiting for an account goes right after.
        public void CreateAccount(string nick, string email, string password, Action<string> done) {
            StartCoroutine(CreateAccountRoutine(nick, email, password, done));
        }

        private IEnumerator CreateAccountRoutine(string nick, string email, string password, Action<string> done) {
            string problem = ProfileService.NickProblem(nick);
            if (problem != null) {
                done?.Invoke(problem);
                yield break;
            }
            nick = nick.Trim();
            yield return WaitForConnect();
            if (!Auth.IsSignedIn) {
                done?.Invoke("Sem conexão com o servidor");
                yield break;
            }
            if (!Auth.IsAnonymous) {
                done?.Invoke("Você já está numa conta");
                yield break;
            }
            yield return Profiles.CheckFree(nick, e => problem = e);
            if (problem != null) {
                done?.Invoke(problem);
                yield break;
            }
            string error = null;
            yield return Auth.LinkEmail(email.Trim(), password, (ok, e) => error = ok ? null : e);
            if (error != null) {
                done?.Invoke(AccountError(error));
                yield break;
            }
            // somebody may have taken the nickname in the last second: the account stands, and the
            // Conta page asks for another
            yield return Profiles.Claim(nick, e => error = e);
            StartCoroutine(Auth.SendVerifyEmail(null));
            // what the boards really have comes first: a guest's best here was never uploaded, and
            // renaming an entry that isn't there is refused
            yield return SyncBests();
            if (error == null && Save.playerName != nick) {
                Save.playerName = nick;
                SaveLocal();
                yield return PushName();
            }
            SaveGuestRecord();
            AccountChanged?.Invoke();
            done?.Invoke(error != null ? "Conta criada, mas: " + error : null);
        }

        // the guest's run still has its ticket (same user id), so its record goes now
        private void SaveGuestRecord() {
            PendingRecord run = _pending;
            if (run == null || Record != RecordStatus.NeedsAccount) return;
            run.previousBest = BestFor(run.level, run.auto);
            run.allTime = run.score > run.previousBest;
            if (run.allTime) SetBest(run.level, run.score, run.auto);
            StartCoroutine(PushRecord(run));
        }

        public void SignInWithEmail(string email, string password, Action<string> done) {
            StartCoroutine(SignInWithEmailRoutine(email, password, done));
        }

        public void SignOut() {
            Auth.SignOut();
            Profiles.Forget();
            Friends.Forget();
            Save = NewSave();
            ClearBests();
            SaveLocal();
            // back to the start screen
            PlayerPrefs.DeleteKey(GuestChosenPref);
            _pending = null;
            if (Record == RecordStatus.NeedsAccount) SetRecord(RecordStatus.None);
            AccountChanged?.Invoke();
            StartCoroutine(Connect());
        }

        private IEnumerator SignInWithEmailRoutine(string email, string password, Action<string> done) {
            yield return WaitForConnect();
            string error = null;
            yield return Auth.SignInWithEmail(email.Trim(), password, (ok, e) => error = ok ? null : e);
            if (error == null) {
                // This device's local progress belonged to another account; take the cloud one.
                Save = NewSave();
                ClearBests();
                Profiles.Forget();
                Friends.Forget();
                // a guest's record can't go to another account: its ticket has the guest's user id
                if (Record == RecordStatus.NeedsAccount) {
                    _pending = null;
                    SetRecord(RecordStatus.None);
                }
                yield return SyncSave();
                yield return _achievements.Load();
                ForgetBoards();
                State = Status.Online;
                done?.Invoke(null);
                yield return AfterLogin();
                yield break;
            }
            done?.Invoke(AccountError(error));
        }

        // "Esqueci a senha"
        public void SendPasswordReset(string email, Action<string> done) {
            // an email with no account gets the same answer: nobody finds out who has one here
            StartCoroutine(Auth.SendPasswordReset((email ?? "").Trim(),
                (ok, e) => done?.Invoke(ok || (e ?? "").StartsWith("EMAIL_NOT_FOUND") ? null : AccountError(e))));
        }

        public void SendVerifyEmail(Action<string> done) {
            StartCoroutine(Auth.SendVerifyEmail((ok, e) => done?.Invoke(ok ? null : AccountError(e))));
        }

        // Firebase's error codes, in the player's words
        public static string AccountError(string code) {
            code = code ?? "";
            if (code.StartsWith("EMAIL_EXISTS") || code.StartsWith("CREDENTIAL_TOO_OLD")) return "Esse email já tem conta. Use Entrar";
            if (code.StartsWith("INVALID_EMAIL") || code.StartsWith("MISSING_EMAIL")) return "Esse email não parece certo";
            if (code.StartsWith("WEAK_PASSWORD") || code.StartsWith("MISSING_PASSWORD")) return "A senha precisa de pelo menos 6 caracteres";
            if (code.StartsWith("INVALID_LOGIN_CREDENTIALS") || code.StartsWith("EMAIL_NOT_FOUND") || code.StartsWith("INVALID_PASSWORD")) return "Email ou senha errados";
            if (code.StartsWith("TOO_MANY_ATTEMPTS")) return "Muitas tentativas. Espere um pouco e tente de novo";
            if (code.StartsWith("USER_DISABLED")) return "Esta conta está bloqueada";
            if (code.StartsWith("OPERATION_NOT_ALLOWED")) return "O login com email está desligado";
            return "Sem conexão com o servidor";
        }

        // the end of a run: its stats and the public profile, one write each
        public void OnStatsRunEnded() {
            if (State == Status.Online) StartCoroutine(PushAfterRun());
        }

        private IEnumerator PushAfterRun() {
            yield return PlayerStats.Upload(_db, Auth);
            yield return Profiles.Push(false);
        }

        // Merges the cloud save with local progress (e.g. runs played while offline)
        // and uploads the result if the local copy was ahead.
        private IEnumerator SyncSave() {
            PlayerSave cloud = null;
            yield return _saves.Load(s => cloud = s);
            if (cloud == null) yield break;

            bool localAhead = Save.bestScore > cloud.bestScore || Save.runsPlayed > cloud.runsPlayed ||
                              Save.totalCoins > cloud.totalCoins || string.IsNullOrEmpty(cloud.playerName);

            Save = new PlayerSave {
                playerName = string.IsNullOrEmpty(cloud.playerName) ? Save.playerName : cloud.playerName,
                bestScore = Math.Max(Save.bestScore, cloud.bestScore),
                runsPlayed = Math.Max(Save.runsPlayed, cloud.runsPlayed),
                totalCoins = Math.Max(Save.totalCoins, cloud.totalCoins)
            };
            SaveLocal();

            if (localAhead) yield return _saves.Save(Save, null);
            yield return SyncBests();
        }

        // each ranked board: the server's best is the one that counts. A score only gets there
        // at the end of the run that made it (firestore.rules checks it against the run's ticket),
        // so a better local best (an offline run, or an edited save) is not uploaded.
        private IEnumerator SyncBests() {
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                yield return _leaderboard.GetOwnSigned(level, (ok, score, sig) => {
                    if (!ok) return;
                    SetBest(level, score);
                    SetSignedBest(level, score, sig);
                });
                string daily = LeaderboardService.DailyCollection(level);
                long? today = null;
                yield return _leaderboard.GetOwn(daily, s => today = s);
                if (today != null) SetDailyBest(level, daily, today.Value);
                long? auto = null;
                yield return _leaderboard.GetOwn(LeaderboardService.AutoCollection(level), s => auto = s);
                if (auto != null) SetBest(level, auto.Value, true);
            }
        }

        private IEnumerator PushRun() {
            yield return _saves.Save(Save, null);
        }

        // "Salvar recorde" on the end-of-run banner
        public void SaveRecord() {
            if (Record == RecordStatus.CanSave && _pending != null) StartCoroutine(PushRecord(_pending));
        }

        private void SetRecord(RecordStatus status, string note = null) {
            Record = status;
            RecordNote = note;
            RecordChanged?.Invoke();
        }

        // the API checks the run's log and signs the score, then it goes to the board
        private IEnumerator PushRecord(PendingRecord run) {
            if (_pending == run) _pending = null;
            SetRecord(RecordStatus.Saving);
            ScoreApi.Signed signed = null;
            string refused = null;
            string board = LeaderboardService.Collection(run.level, run.auto);
            yield return _api.FinishRun(run.log.ToJson(run.token, board, run.score),
                (s, error) => { signed = s; refused = error; });
            // still the record the banner shows (no newer run ended since)
            bool current = RecordScore == run.score && RecordLevel == run.level && RecordAuto == run.auto;
            if (signed != null && signed.pending) {
                // the server holds it for review: it reaches the board (written by the server) if approved
                if (BestFor(run.level, run.auto) == run.score) SetBest(run.level, run.previousBest, run.auto);
                if (current) SetRecord(RecordStatus.InReview);
                yield break;
            }
            if (signed != null) {
                if (run.allTime) {
                    bool written = false;
                    yield return SubmitWithRetry(board, run.score, signed.sig, ok => written = ok);
                    if (!written) refused = "o ranking não gravou";
                    else if (!run.auto) SetSignedBest(run.level, run.score, signed.sig);
                }
                // today's board too (the site's Diário); a lower score than today's best there is
                // simply not taken
                if (refused == null && !run.auto && !string.IsNullOrEmpty(signed.dailyBoard) && !string.IsNullOrEmpty(signed.dailySig)) {
                    bool written = false;
                    yield return SubmitWithRetry(signed.dailyBoard, run.score, signed.dailySig, ok => written = ok);
                    if (written) SetDailyBest(run.level, signed.dailyBoard, run.score);
                    else if (!run.allTime) refused = "o ranking do dia não gravou";
                }
            }
            if (refused != null) {
                Debug.LogWarning("[Online] Score " + run.score + " not ranked: " + refused);
                if (BestFor(run.level, run.auto) == run.score) SetBest(run.level, run.previousBest, run.auto);
                RankingRefused?.Invoke(run.score, refused);
                if (current) SetRecord(RecordStatus.Refused, refused);
            } else if (current) {
                SetRecord(RecordStatus.Saved);
            }
            ForgetBoards();
            yield return RefreshBoard(run.level, run.auto);
            yield return Profiles.Push(true);   // the new best on the public profile
        }

        private IEnumerator SubmitWithRetry(string board, long score, string sig, Action<bool> done) {
            bool written = false;
            yield return _leaderboard.SubmitBest(board, Save.playerName, score, sig, ok => written = ok);
            if (!written) {
                // once more, for a dropped connection (the rules take one write per 20 s)
                yield return new WaitForSecondsRealtime(21f);
                yield return _leaderboard.SubmitBest(board, Save.playerName, score, sig, ok => written = ok);
            }
            done?.Invoke(written);
        }

        private IEnumerator StartRankedRun(ArenaLevel level, bool auto) {
            RunLog log = _runLog;
            _tokenAsked = true;
            yield return _api.StartRun(LeaderboardService.Collection(level, auto), token => {
                if (_runLog == log) _runToken = token;   // still the same run
            });
        }

        private IEnumerator PushName() {
            yield return _saves.Save(Save, null);
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                if (BestFor(level) > 0) yield return _leaderboard.Rename(level, Save.playerName, null);
                if (DailyBestFor(level) > 0) yield return _leaderboard.Rename(LeaderboardService.DailyCollection(level), Save.playerName, null);
                if (BestFor(level, true) > 0) yield return _leaderboard.Rename(LeaderboardService.AutoCollection(level), Save.playerName, null);
            }
            ForgetBoards();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            _player = FindAnyObjectByType<Player>();
            _input = _player != null ? _player.GetComponent<FrenyInput>() : null;
            _runnerPlayer = FindAnyObjectByType<RPlayer>();
            _ui = FindAnyObjectByType<UIManager>();
            _runnerScore = FindAnyObjectByType<ScoreManager>();
            _sawAlive = false;
            // Campaign stages are not runs: a death there just respawns at the checkpoint.
            _reported = CampaignLevels.IsStage(scene.name);
            _assisted = false;
            // only arena runs are ranked, on the board of the difficulty picked in the menu
            _arenaRun = scene.name == GameMenus.ArenaScene;
            _runLevel = ArenaDifficulty.Current;
            _runAuto = _arenaRun && !ArenaDifficulty.Manual;   // Automático or Manual, as picked in the menu
            _runLog = null;
            _runToken = null;
            _tokenAsked = false;
            // a record nobody clicked "Salvar recorde" for is saved anyway
            if (_pending != null && Record == RecordStatus.CanSave) StartCoroutine(PushRecord(_pending));
        }

        private void Update() {
            if (_reported) return;

            int health;
            if (_player != null) {
                health = _player.Health;
            } else if (_runnerPlayer != null) {
                health = _runnerPlayer.health;
            } else {
                return;
            }

            // Player.Health is only set in Start, so wait until we have seen it alive.
            if (health > 0) {
                if (!_sawAlive && _player != null) {
                    // Player.coins is the persistent total (FrenyProgress), so count this run's share.
                    _coinsAtStart = _player.coins;
                }
                // the log starts with the run even if the login is still going on (a new player who
                // goes straight to the arena); the ticket is asked for as soon as the game is online
                if (!_sawAlive && _arenaRun) {
                    _runLog = new RunLog(Time.time);
                    AntiCheat.Reset();
                    _clockWall = default;
                    _clockOff = 0;
                    if (_player != null) {
                        _runLog.AddVital("max", _player.MaxHealth);
                        _runLog.AddVital("life", health);
                    }
                }
                if (_runLog != null) CheckClock();
                if (_runLog != null && !_tokenAsked && State == Status.Online) {
                    StartCoroutine(StartRankedRun(_runLevel, _runAuto));
                }
                _sawAlive = true;
                if (_input != null && _input.IsAutopilotDriving) {
                    _assisted = true;
                }
            } else if (_sawAlive) {
                _reported = true;
                _runLog?.Seal(AntiCheat.Flags);
                long coins = _player != null ? Math.Max(0, _player.coins - _coinsAtStart) : 0;
                ReportRun(CurrentScore(), coins, _assisted);
            }
        }

        private long CurrentScore() {
            if (_ui != null) return Math.Max(0, _ui.Score);
            if (_runnerScore != null) return _runnerScore.score;
            return 0;
        }

        // Every run counts for the stats, but only an arena run played by hand from start to end
        // goes to the board of its difficulty. The GDD keeps idle play out of the manual "Freny
        // Arena Online" ranking; a run started in Automático goes to the autopilot's own board
        // (José, 2026-11-01), even if the player took control for a while. A run started in Manual
        // where the autopilot then played goes on neither.
        private void ReportRun(long score, long coins, bool assisted) {
            bool auto = _runAuto;
            bool mixed = assisted && !auto;
            string unranked = !_arenaRun ? "não vale para o ranking (só a Arena tem)"
                : mixed ? "não vale para o ranking (o piloto jogou numa corrida manual)"
                : auto ? "ranking do automático"
                : null;
            long previousBest = BestFor(_runLevel, auto);
            // beats the board's best (all-time, or today's), even if it can't go there: the
            // banner says so and why
            bool allTime = score > previousBest;
            bool record = _arenaRun && score > 0 && (allTime || (!auto && score > DailyBestFor(_runLevel)));
            bool newBest = record && allTime && !mixed;
            Save.runsPlayed++;
            Save.totalCoins += coins;
            if (!assisted && score > Save.bestScore) Save.bestScore = score;
            if (newBest) SetBest(_runLevel, score, auto);
            SaveLocal();

            // an older record still waiting for its click goes now, before this one replaces it
            if (_pending != null && Record == RecordStatus.CanSave) StartCoroutine(PushRecord(_pending));
            _pending = null;
            RecordScore = score;
            RecordLevel = _runLevel;
            RecordAuto = auto;
            RecordOnlyToday = !allTime;
            if (!record) {
                SetRecord(RecordStatus.None);
            } else if (mixed) {
                SetRecord(RecordStatus.CannotSave, "o piloto automático jogou numa corrida manual");
            } else if (State != Status.Online) {
                SetRecord(RecordStatus.CannotSave, "sem conexão com o servidor");
            } else if (_runLog == null || _runToken == null) {
                SetRecord(RecordStatus.CannotSave, "sem ticket do servidor");
            } else {
                _pending = new PendingRecord { level = _runLevel, score = score, previousBest = previousBest, allTime = allTime, auto = auto, log = _runLog, token = _runToken };
                if (Auth.IsAnonymous) {
                    // it stays as this guest's best here, so the next worse run isn't a "record"
                    if (!auto) SetDailyBest(_runLevel, LeaderboardService.DailyCollection(_runLevel), score);
                    SetRecord(RecordStatus.NeedsAccount);
                } else {
                    SetRecord(RecordStatus.CanSave);
                }
            }
            if (newBest && Record == RecordStatus.CannotSave) SetBest(_runLevel, previousBest, auto);

            // the Conquistas of a manual run go to the API, which checks them against the run's log
            if (_arenaRun && !assisted && !auto && State == Status.Online && _runLog != null && _runToken != null && Achievements.NeedsClaim) {
                StartCoroutine(_achievements.Claim(_runLog, _runToken, LeaderboardService.Collection(_runLevel), score));
            }

            RunReported?.Invoke(score, newBest && Record == RecordStatus.CanSave, unranked);
            if (State == Status.Online) StartCoroutine(PushRun());
#if UNITY_EDITOR
            if (_arenaRun && _runLog != null) StartCoroutine(EditorCheckRun(score, _runLog, _runToken, Record == RecordStatus.CanSave));
#endif
        }

#if UNITY_EDITOR
        // In the editor every arena run (autopilot ones too) is written to Logs/arena-run.json and,
        // unless it is going to the ranking anyway, checked by the ranking API without writing a
        // score, so the API can be tested and tuned with autopilot runs.
        private IEnumerator EditorCheckRun(long score, RunLog log, string token, bool ranked) {
            string board = LeaderboardService.Collection(_runLevel, _runAuto);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "arena-run.json"),
                log.ToJson(token ?? "", board, score));
            if (ranked || token == null) yield break;
            yield return _api.FinishRun(log.ToJson(token, board, score), (sig, error) =>
                Debug.Log("[Online] API check of this run (" + score + " points): " + (sig != null ? "accepted" : "refused, " + error)));
        }
#endif

        private void SaveLocal() {
            PlayerPrefs.SetString(LocalSavePref, JsonUtility.ToJson(Save));
            PlayerPrefs.Save();
        }

        private static PlayerSave LoadLocal() {
            string json = PlayerPrefs.GetString(LocalSavePref, "");
            PlayerSave save = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PlayerSave>(json);
            if (save == null || string.IsNullOrEmpty(save.playerName)) save = NewSave();
            return save;
        }

        private static PlayerSave NewSave() {
            return new PlayerSave { playerName = "Freny" + UnityEngine.Random.Range(1000, 10000) };
        }

        private static string CleanName(string name) {
            name = (name ?? "").Trim();
            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
        }
    }
}
