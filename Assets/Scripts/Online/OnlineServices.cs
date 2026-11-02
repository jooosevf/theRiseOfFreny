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

        public static OnlineServices Instance { get; private set; }

        public Status State { get; private set; } = Status.Disabled;
        public string LastError { get; private set; }
        public FirebaseAuthService Auth { get; private set; }
        public FirestoreClient Db => _db;   // for CloudProgressSync (save slots)
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
        public enum RecordStatus { None, CanSave, Saving, Saved, CannotSave, Refused, InReview }
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
            Blessings.Chosen += OnBlessing;
        }

        private void OnDestroy() {
            Player.Vital -= OnVital;
            Blessings.Chosen -= OnBlessing;
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

        private void OnBlessing(Blessing blessing) {
            if (_runLog != null && !_reported) _runLog.AddBlessing(Blessings.Of(blessing).key);
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

            Auth = new FirebaseAuthService(config.apiKey);
            _db = new FirestoreClient(config.projectId, Auth);
            _leaderboard = new LeaderboardService(_db, Auth);
            _api = new ScoreApi(config.apiUrl, Auth);
            _saves = new CloudSaveService(_db, Auth);
            _achievements = new AchievementService(_db, _api, Auth);
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
            }
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

        public void SetPlayerName(string newName) {
            newName = CleanName(newName);
            if (newName.Length == 0 || newName == Save.playerName) return;
            Save.playerName = newName;
            SaveLocal();
            if (State == Status.Online) StartCoroutine(PushName());
        }

        // Renascer: the boards with a score of his get the new rebirth count (the star) right away,
        // the same write as a rename (LeaderboardService.Rename sends it)
        public void PushRebirths() {
            if (State == Status.Online) StartCoroutine(PushName());
        }

        // Email account actions, used by the HUD. done(errorOrNull).
        public void LinkEmail(string email, string password, Action<string> done) {
            StartCoroutine(Auth.LinkEmail(email, password, (ok, error) => done?.Invoke(ok ? null : error)));
        }

        public void SignInWithEmail(string email, string password, Action<string> done) {
            StartCoroutine(SignInWithEmailRoutine(email, password, done));
        }

        public void SignOut() {
            Auth.SignOut();
            Save = NewSave();
            ClearBests();
            SaveLocal();
            StartCoroutine(Connect());
        }

        private IEnumerator SignInWithEmailRoutine(string email, string password, Action<string> done) {
            string error = null;
            yield return Auth.SignInWithEmail(email, password, (ok, e) => error = ok ? null : e);
            if (error == null) {
                // This device's local progress belonged to another account; take the cloud one.
                Save = NewSave();
                ClearBests();
                yield return SyncSave();
                ForgetBoards();
            }
            done?.Invoke(error);
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

            // each ranked board: the server's best is the one that counts. A score only gets there
            // at the end of the run that made it (firestore.rules checks it against the run's ticket),
            // so a better local best (an offline run, or an edited save) is not uploaded.
            foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                long? server = null;
                yield return _leaderboard.GetOwn(level, s => server = s);
                if (server != null) SetBest(level, server.Value);
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
                SetRecord(RecordStatus.CanSave);
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
