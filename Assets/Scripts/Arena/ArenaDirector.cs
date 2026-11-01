using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Decides what spawns in the arena and when, so a run starts calm and gets busier.
// Difficulty climbs from 0 to 1 over Tuning.RampSeconds (5 minutes by default): more enemies
// alive at once, shorter gaps between spawns, and tougher enemy types unlocking over time.
// Where things appear is random: any platform Freny can reach (ArenaMap), each platform as likely
// as the next so the corners and the small ledges get their share, never close to him and away from
// the enemies already there. Sorcerers take the towers first (ledges against the side walls, out of
// his reach, so they leave after a few casts), one per tower so both sides get one; with both towers
// taken they appear on the platforms like everyone else. How many sorcerers (either kind) can be
// around at once goes with the score: one up to 50 000 points, two past that, three past 100 000.
// The scene's EnemySpawners are only a fallback. The Escudeiro joins the list at runtime, and every
// FirstBossAt points (then AfterBossKill points after each one falls) the Ciclope is called onto the floor; while it lives nobody else comes.
public class ArenaDirector : MonoBehaviour {

    [System.Serializable]
    public class Entry {
        public GameObject prefab;
        public float unlockAt = 0f;   // difficulty (0..1) at which this enemy starts appearing
        public float weight = 1f;     // relative chance once unlocked
        public int unlockScore = 0;   // and not before the run has this many points (the red Golem: 50 000)
        public bool flying = false;   // spawns in the air above a spawner instead of on it
    }

    [SerializeField] private Entry[] enemies;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private float chestInterval = 30f;
    [SerializeField] private float vaseInterval = 12f;   // a breakable vase now and then (ArenaVase)
    [SerializeField] private int maxVases = 3;
    [SerializeField] private float portalSeconds = 1f;
    [SerializeField] private float firstSpawnDelay = 2f;
    [SerializeField] private float minDistanceFromHero = 7f;   // about a quarter of the screen
    [SerializeField] private int towerSorcererCasts = 8;
    [SerializeField] private int sorcerersPerTower = 1;        // one more each once the run is past its peak

    private readonly List<EnemySpawner> spawners = new List<EnemySpawner>();
    private float runStart;
    private float nextSpawnTime;
    private float nextChestTime;
    private float nextVaseTime;
    private int pendingSpawns; // portals already open
    private int pendingSorcerers; // the ones of those portals a sorcerer comes out of
    // a sorcerer that dies keeps its place for this long before another one can come (José, 2026-10-29)
    private const float SorcererRespawnDelay = 15f;
    private readonly List<float> sorcererDeaths = new List<float>();
    private readonly List<Vector3> pendingPoints = new List<Vector3>(); // where those portals are
    private readonly int[] pendingTower = new int[2];                  // tower portals open per side (0 left, 1 right)
    private Player hero;
    private readonly Dictionary<ArenaMap.Platform, Transform[]> patrols = new Dictionary<ArenaMap.Platform, Transform[]>();

    public float Elapsed => Time.time - runStart;
    // the arena difficulty picked in the menu (ArenaDifficulty) speeds the ramp up or down,
    // changes the peak and can start the run part-way up the curve
    // the running arena's difficulty (0 outside the arena), for things spawned without a director
    // reference: the Sorcerer's hexball speeds up with it
    public static float CurrentDifficulty { get; private set; }
    // minutes past the peak: the run keeps getting harder after the curve tops out, so a long
    // run (20k+ points) ends up really hard instead of staying at the peak forever
    public static float CurrentOvertime { get; private set; }

    private float RampTime => Tuning.RampSeconds.Value * ArenaDifficulty.RampFactor;
    public float Difficulty => Mathf.Clamp01(ArenaDifficulty.StartAt + rampElapsed / RampTime);
    public float Overtime => Mathf.Max(0f, rampElapsed - RampTime * (1f - ArenaDifficulty.StartAt)) / 60f;
    // the time the difficulty ramp counts: it stops at the first Ciclope and starts again at a
    // million (José, 2026-10-29: after the first Ciclope it got too hard too fast; a million should
    // be reachable, but hard)
    private float rampElapsed;
    public const int RampPausedFrom = FirstBossAt;
    public const int RampResumesAt = 1000000;
    // a busy arena from the start (3), the peak at the end of the curve, then one more per
    // minute of overtime, up to MaxEnemies
    private const int MaxEnemies = 24;
    public int MaxAlive => Mathf.Min(MaxEnemies, Mathf.Max(1, Mathf.RoundToInt(
        Mathf.Lerp(3f * Mathf.Min(1f, ArenaDifficulty.PeakFactor), Tuning.PeakEnemies.Value * ArenaDifficulty.PeakFactor, Difficulty)
        + Overtime)));
    public float SpawnInterval => Mathf.Lerp(3f, 0.8f, Difficulty) * ArenaDifficulty.SpawnIntervalFactor / (1f + 0.15f * Overtime);

    // how many enemies go after Freny from anywhere in the arena (Enemy.JoinChase); the ones that
    // see him up close always fight, the rest patrol until a place frees up
    public static int MaxChasers => Mathf.RoundToInt(Mathf.Lerp(4f, 6f, CurrentDifficulty) + Mathf.Min(3f, CurrentOvertime * 0.5f));
    // enemy pace over the run, on top of the menu level (ArenaDifficulty): slower at the start,
    // faster at the peak and still faster in overtime
    public static float SpeedRamp => Mathf.Lerp(0.95f, 1.15f, CurrentDifficulty) * Mathf.Min(1.4f, 1f + 0.04f * CurrentOvertime);
    public static float CooldownRamp => Mathf.Lerp(1.1f, 0.8f, CurrentDifficulty) / Mathf.Min(1.6f, 1f + 0.06f * CurrentOvertime);

    private static readonly Color PortalColor = new Color(0.6f, 0.3f, 1f);
    private static readonly Color ChestPortalColor = new Color(1f, 0.85f, 0.2f);

    private void OnEnable() {
        Enemy.Died += OnEnemyDied;
    }

    private void OnDisable() {
        Enemy.Died -= OnEnemyDied;
    }

    private void OnEnemyDied(Enemy enemy) {
        if (enemy is Sorcerer) {
            sorcererDeaths.Add(Time.time);
        }
        if (enemy is Cyclops && chestPrefab != null) {
            // the boss leaves a chest where it fell
            Instantiate(chestPrefab, enemy.Feet + Vector3.up * 0.3f, Quaternion.identity);
        }
    }

    // ---- the Escudeiro and the Ciclope (EnemyTemplates) ----

    // José: the first Ciclope at 50k, then each one AfterBossKill points after the last one fell
    // (2026-10-30; it was every 100k); two at once only from a million. Each one brings more enemies with it (BossHelpers, BossCompany)
    public const int FirstBossAt = 50000;
    public const int AfterBossKill = 150000;   // José, 2026-10-30: the next one comes this many points after the last one falls
    public const int TwoBossesAt = 1000000;
    private const float TwinGap = 4f;
    private GameObject cyclopsTemplate;
    private int nextBossScore = FirstBossAt;
    private int bossesCalled;   // Ciclope fights so far this run (1 = the 50k one)
    private bool bossPending;

    private void AddRuntimeEnemies() {
        GameObject golem = null;
        foreach (Entry entry in enemies) {
            if (entry.prefab != null && entry.prefab.GetComponent<Golem>() != null && entry.unlockScore == 0) {
                golem = entry.prefab;
                break;
            }
        }
        if (golem == null) {
            return;
        }
        GameObject shielded = EnemyTemplates.Make<Shielded>(golem, "Shielded_Enemy", new Vector2(0.9f, 1.35f), new Vector2(0f, -0.33f));
        if (shielded != null) {
            List<Entry> list = new List<Entry>(enemies);
            list.Add(new Entry { prefab = shielded, unlockAt = 0.4f, weight = 1f });
            enemies = list.ToArray();
        }
        cyclopsTemplate = EnemyTemplates.Make<Cyclops>(golem, "Cyclops_Boss", new Vector2(1.9f * Cyclops.Size, 2.45f * Cyclops.Size), new Vector2(0f, Cyclops.Size * 1.225f - 1.585f));
    }

    private bool BossAround => bossPending || Cyclops.Current != null;
    // after a Ciclope falls the arena fills up again at the usual pace, not all at once
    private const float AfterBossCalm = 20f;
    private float calmUntil;
    private bool bossWasAround;

    private void UpdateBoss() {
        if (cyclopsTemplate == null || BossAround || Score < nextBossScore) {
            return;
        }
        nextBossScore = int.MaxValue;   // set again when it falls: AfterBossKill points later
        bossesCalled++;
        StartCoroutine(SpawnBoss());
    }

    // on the floor, as far from Freny as it goes, through a bigger portal
    // while the Ciclope is out, nobody else comes but BossHelpers plain sorcerers that help it
    // from the towers (José, 2026-10-29)
    // 2 plain sorcerers with the first Ciclope, one more with each next one, up to 5; from the
    // second Ciclope (150k) the usual enemies keep coming too, 2 more at once with each fight
    private int BossHelpers => Mathf.Min(5, 1 + bossesCalled);
    private int BossCompany => Mathf.Max(0, 2 * (bossesCalled - 1));
    private const float BossHelperGap = 4f;
    private float nextHelperTime;

    private void UpdateBossHelpers() {
        if (Cyclops.Current == null || Time.time < nextHelperTime) {
            return;
        }
        int helpers = pendingSorcerers;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy is Sorcerer sorcerer && !sorcerer.IsDead && !sorcerer.IsSupreme) {
                helpers++;
            }
        }
        if (helpers >= BossHelpers) {
            return;
        }
        foreach (Entry entry in enemies) {
            Sorcerer plain = entry.prefab != null ? entry.prefab.GetComponent<Sorcerer>() : null;
            if (plain != null && !plain.IsSupreme) {
                StartCoroutine(SpawnWithPortal(entry));
                nextHelperTime = Time.time + BossHelperGap;
                return;
            }
        }
    }

    private IEnumerator SpawnBoss() {
        ArenaMap map = Map;
        if (map == null || map.Floor == null) {
            yield break;
        }
        bossPending = true;
        ArenaMap.Platform floor = map.Floor;
        // right in the middle of the arena floor (José, 2026-10-29), announced by a long portal
        float left = Mathf.Max(floor.xMin + 2f, ArenaBounds.Left + 2f);
        float right = Mathf.Min(floor.xMax - 2f, ArenaBounds.Right - 2f);
        float middle = (ArenaBounds.Left + ArenaBounds.Right) * 0.5f;
        float x = float.IsNaN(middle) || float.IsInfinity(middle) ? (floor.xMin + floor.xMax) * 0.5f : Mathf.Clamp(middle, left, right);
        Vector3 floorPoint = new Vector3(x, floor.y, 0f);
        Fx.Portal(floorPoint + Vector3.up * 1.2f, portalSeconds * 2f, new Color(0.75f, 0.2f, 0.85f));
        Sfx.Play("portal", floorPoint, 1f, 0.6f);
        yield return new WaitForSeconds(portalSeconds * 2f);
        // from TwoBossesAt points two come, one on each side of the middle
        int count = Score >= TwoBossesAt ? 2 : 1;
        for (int i = 0; i < count; i++) {
            Vector3 at = count == 1 ? floorPoint
                : new Vector3(Mathf.Clamp(x + (i == 0 ? -TwinGap : TwinGap), left, right), floor.y, 0f);
            GameObject spawned = Instantiate(cyclopsTemplate, at, Quaternion.identity);
            Enemy boss = spawned.GetComponent<Enemy>();
            if (boss is Cyclops cyclops) {
                cyclops.Paired = count == 2;
            }
            if (boss != null) {
                Transform[] ends = PatrolFor(floor);
                boss.pointA = ends[0];
                boss.pointB = ends[1];
                boss.currentTarget = ends[0].position;
                boss.PlaceFeetAt(at);
            }
        }
        bossPending = false;
    }

    private void Start() {
        foreach (EnemySpawner spawner in FindCompat.All<EnemySpawner>()) {
            spawner.SetDirected();
            spawners.Add(spawner);
        }
        GameObject heroObj = GameObject.FindGameObjectWithTag("Player");
        hero = heroObj ? heroObj.GetComponent<Player>() : null;
        runStart = Time.time;
        nextSpawnTime = Time.time + firstSpawnDelay;
        nextChestTime = Time.time + chestInterval;
        nextVaseTime = Time.time + firstSpawnDelay;
        AddRuntimeEnemies();
    }

    private void Update() {
        CurrentDifficulty = Difficulty;
        CurrentOvertime = Overtime;
        if (enemies == null || enemies.Length == 0 || (spawners.Count == 0 && Map == null)) {
            return;
        }

        if (Score < RampPausedFrom || Score >= RampResumesAt) {
            rampElapsed += Time.deltaTime;
        }
        UpdateBoss();
        UpdateBossHelpers();
        UpdateBossFodder();
        int present = AliveEnemies() + pendingSpawns;
        if (bossWasAround && !BossAround) {
            calmUntil = Time.time + AfterBossCalm;
            nextBossScore = Score + AfterBossKill;
        }
        bossWasAround = BossAround;
        bool spawnNow = BossAround ? Company() < BossCompany && !bossPending : present < MaxAlive;
        if (Time.time >= nextSpawnTime && spawnNow) {
            Entry pick = Pick();
            if (pick != null && pick.prefab != null) {
                StartCoroutine(SpawnWithPortal(pick));
            }
            // a half-empty arena fills up fast: no more long stretches with two enemies around
            bool sparse = present + 1 < MaxAlive / 2 && Time.time >= calmUntil;
            nextSpawnTime = Time.time + SpawnInterval * (sparse ? 0.35f : 1f);
        }

        if (chestPrefab != null && Time.time >= nextChestTime) {
            StartCoroutine(SpawnChest());
            nextChestTime = Time.time + chestInterval;
        }

        if (Time.time >= nextVaseTime) {
            nextVaseTime = Time.time + vaseInterval;
            if (ArenaVase.Active.Count < maxVases && RandomSpot(Reachable(Map), 1f, out Vector3 floorPoint, out ArenaMap.Platform _)) {
                ArenaVase.Create(floorPoint);
            }
        }
    }

    private void OnDestroy() {
        CurrentDifficulty = 0f;
        CurrentOvertime = 0f;
    }

    // the usual enemies out during a Ciclope fight (not the Ciclopes, not the sorcerers)
    // (the one bat and one bug that keep after Freny don't count: BossFodder)
    private int Company() {
        int count = pendingSpawns;
        int bats = 0, bugs = 0;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead && !(enemy is Cyclops) && !(enemy is Sorcerer)) {
                count++;
                bats += enemy is Bat ? 1 : 0;
                bugs += enemy is Bug ? 1 : 0;
            }
        }
        return count - Mathf.Min(1, bats) - Mathf.Min(1, bugs);
    }

    // José, 2026-10-29: in a Ciclope fight Freny has little to kill and his combo would die, so one
    // bat and one bug are always coming after him; a new one comes when one dies
    private const float FodderGap = 1.5f;
    private float nextBat, nextBug;

    private void UpdateBossFodder() {
        if (Cyclops.Current == null) {
            return;
        }
        int bats = 0, bugs = 0;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead) {
                bats += enemy is Bat ? 1 : 0;
                bugs += enemy is Bug ? 1 : 0;
            }
        }
        if (bats == 0 && Time.time >= nextBat && SpawnKind<Bat>()) {
            nextBat = Time.time + portalSeconds + FodderGap;   // its portal is still opening
        }
        if (bugs == 0 && Time.time >= nextBug && SpawnKind<Bug>()) {
            nextBug = Time.time + portalSeconds + FodderGap;
        }
    }

    private bool SpawnKind<T>() where T : Enemy {
        foreach (Entry entry in enemies) {
            if (entry.prefab != null && entry.prefab.GetComponent<T>() != null) {
                StartCoroutine(SpawnWithPortal(entry));
                return true;
            }
        }
        return false;
    }

    private int AliveEnemies() {
        int alive = 0;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead) {
                alive++;
            }
        }
        return alive;
    }

    private static int Score => UIManager.Instance ? UIManager.Instance.Score : 0;

    public const int SupremeAt = 100000;

    // sorcerers (plain and Supreme) at once, by score (José, 2026-10-29)
    public static int SorcererLimit(int score) {
        return score >= RampResumesAt ? 3 : score >= 50000 ? 2 : 1;   // the third only from a million
    }

    // alive ones, the portals they are coming out of, and the places of the ones that died in the
    // last SorcererRespawnDelay seconds
    private int Sorcerers() {
        sorcererDeaths.RemoveAll(t => Time.time - t >= SorcererRespawnDelay);
        int count = pendingSorcerers + sorcererDeaths.Count;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy is Sorcerer && !enemy.IsDead) {
                count++;
            }
        }
        return count;
    }

    private static bool IsSorcerer(Entry entry) {
        return entry.prefab != null && entry.prefab.GetComponent<Sorcerer>() != null;
    }

    // the weakest enemies (unlockAt 0) are always there; the rest move with the arena difficulty,
    // and some also wait for a score
    private bool Unlocked(Entry entry) {
        if (entry.unlockScore > 0 && Score < entry.unlockScore) {
            return false;
        }
        if (entry.prefab != null && entry.prefab.GetComponent<Golem>() is Golem golem && golem.Armored && Golem.RedAlive >= Golem.MaxRed) {
            return false;   // José, 2026-10-30: at most 3 red Golems at once
        }
        if (Score < SupremeAt && entry.prefab != null && entry.prefab.GetComponent<Sorcerer>() is Sorcerer sorcerer && sorcerer.IsSupreme) {
            return false;   // José, 2026-10-29: the Feiticeiro Supremo only from 100k
        }
        return entry.unlockAt <= 0f || Difficulty >= entry.unlockAt + ArenaDifficulty.UnlockShift;
    }

    private Entry Pick() {
        bool sorcerersFull = Sorcerers() >= SorcererLimit(Score);
        float total = 0f;
        foreach (Entry entry in enemies) {
            if (entry.prefab != null && Unlocked(entry) && !(sorcerersFull && IsSorcerer(entry))) {
                total += entry.weight;
            }
        }
        if (total <= 0f) {
            return null;
        }
        float roll = Random.Range(0f, total);
        foreach (Entry entry in enemies) {
            if (entry.prefab == null || !Unlocked(entry) || (sorcerersFull && IsSorcerer(entry))) {
                continue;
            }
            roll -= entry.weight;
            if (roll <= 0f) {
                return entry;
            }
        }
        return null;
    }

    // the spawners float above their platforms; the portal and the enemy go on the floor itself. The
    // point returned is ON the floor (the enemy's feet are placed there, see Enemy.PlaceFeetAt). A random
    // x over a gap (or past the screen edge) is rolled again, so nothing spawns in the air.
    private static Vector3 SpawnPoint(EnemySpawner spawner) {
        int ground = LayerMask.GetMask("Ground");
        for (int attempt = 0; attempt < 8; attempt++) {
            Vector2 position = attempt < 7 ? spawner.RandomSpawnPosition() : (Vector2)spawner.transform.position;
            position.x = ArenaBounds.ClampX(position.x, 1f);
            RaycastHit2D hit = Physics2D.Raycast(position + Vector2.up * 0.5f, Vector2.down, 6f, ground);
            if (hit.collider != null) {
                return new Vector3(position.x, hit.point.y, 0f);
            }
        }
        return spawner.transform.position;
    }

    private ArenaMap Map => ArenaMap.Shared(hero);

    // how far a spot is from Freny; he wraps around the screen edges, so the two edges are
    // neighbours and the distance goes the short way round
    private float HeroDistance(Vector2 spot) {
        if (!hero) {
            return float.MaxValue;
        }
        Vector2 heroPos = hero.transform.position;
        float dx = Mathf.Abs(spot.x - heroPos.x);
        if (ArenaBounds.Active) {
            dx = Mathf.Min(dx, ArenaBounds.Right - ArenaBounds.Left - dx);
        }
        return new Vector2(dx, spot.y - heroPos.y).magnitude;
    }

    // distance to the nearest enemy already there or about to come out of a portal
    private float CrowdDistance(Vector2 spot) {
        float nearest = float.MaxValue;
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead) {
                nearest = Mathf.Min(nearest, Vector2.Distance(spot, enemy.Feet));
            }
        }
        foreach (Vector3 pending in pendingPoints) {
            nearest = Mathf.Min(nearest, Vector2.Distance(spot, pending));
        }
        return nearest;
    }

    // ---- towers ----

    private static float MidX => (ArenaBounds.Left + ArenaBounds.Right) * 0.5f;

    private static int SideOf(float x) {
        return x < MidX ? 0 : 1;
    }

    // the lowest tower ledges on one side (0 left, 1 right): the pocket in that side's tower, where
    // the sorcerers appear. Each side is looked at on its own, so a pocket a little higher than the
    // other one still counts (both used to be measured against the lowest of the two)
    private static List<ArenaMap.Platform> TowerLedges(ArenaMap map, int side) {
        List<ArenaMap.Platform> ledges = new List<ArenaMap.Platform>();
        float lowest = float.MaxValue;
        foreach (ArenaMap.Platform p in map.Platforms) {
            if (p.tower && SideOf((p.xMin + p.xMax) * 0.5f) == side) {
                lowest = Mathf.Min(lowest, p.y);
            }
        }
        foreach (ArenaMap.Platform p in map.Platforms) {
            if (p.tower && SideOf((p.xMin + p.xMax) * 0.5f) == side && p.y <= lowest + 0.5f) {
                ledges.Add(p);
            }
        }
        return ledges;
    }

    private int TowerCapacity => sorcerersPerTower + (Overtime > 0f ? 1 : 0);

    // tower sorcerers on each side right now, portals included (the ones that teleport are on a tower)
    private int[] TowerSorcerers() {
        int[] count = { pendingTower[0], pendingTower[1] };
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy is Sorcerer caster && !caster.IsDead && caster.teleportTo != null) {
                count[SideOf(caster.Feet.x)]++;
            }
        }
        return count;
    }

    // the tower a new sorcerer goes to: the emptier one (a coin flip when even), -1 when both are full
    private int TowerForNewSorcerer(ArenaMap map) {
        int[] count = TowerSorcerers();
        int best = -1;
        for (int side = 0; side < 2; side++) {
            if (count[side] >= TowerCapacity || TowerLedges(map, side).Count == 0) {
                continue;
            }
            if (best < 0 || count[side] < count[best] || (count[side] == count[best] && Random.value < 0.5f)) {
                best = side;
            }
        }
        return best;
    }

    // where a tower sorcerer jumps to every few casts: the other tower when it has room, so Freny has
    // to keep turning round; otherwise another spot on its own tower
    private Vector3? TeleportSpot(Sorcerer caster) {
        ArenaMap map = Map;
        if (map == null || caster == null) {
            return null;
        }
        int here = SideOf(caster.Feet.x);
        int[] count = TowerSorcerers();
        List<ArenaMap.Platform> ledges = TowerLedges(map, 1 - here);
        if (ledges.Count == 0 || count[1 - here] >= TowerCapacity) {
            ledges = TowerLedges(map, here);
        }
        for (int attempt = 0; attempt < 8 && ledges.Count > 0; attempt++) {
            ArenaMap.Platform pick = ledges[Random.Range(0, ledges.Count)];
            float min = Mathf.Max(pick.xMin + 0.4f, ArenaBounds.Left + 0.6f);
            float max = Mathf.Min(pick.xMax - 0.4f, ArenaBounds.Right - 0.6f);
            if (max < min) {
                continue;
            }
            float x = Random.Range(min, max);
            if (Mathf.Abs(x - caster.Feet.x) < 1f || map.HeadroomAt(x, pick.y) < 1.6f) {
                continue;
            }
            return new Vector3(x, pick.y, 0f);
        }
        return null;
    }

    // ---- spots ----

    // A random clear spot on one of `candidates`: a few samples (a platform picked at random, each as
    // likely as the next, then a random x on it with room above), the ones too close to Freny thrown
    // away, and of the rest the one farthest from the other enemies, so they come from everywhere
    // instead of piling up. With nothing far enough, the farthest sample still goes if it is at least
    // half the distance. False when nothing fits (the spawners are used then).
    private bool RandomSpot(List<ArenaMap.Platform> candidates, float headroom, out Vector3 point, out ArenaMap.Platform platform) {
        point = Vector3.zero;
        platform = null;
        ArenaMap map = Map;
        if (map == null || candidates == null || candidates.Count == 0) {
            return false;
        }
        const int Wanted = 6;
        int found = 0;
        float bestScore = float.MinValue;
        float farthest = 0f;
        Vector3 farthestPoint = Vector3.zero;
        ArenaMap.Platform farthestPlatform = null;
        for (int attempt = 0; attempt < 32 && found < Wanted; attempt++) {
            ArenaMap.Platform pick = candidates[Random.Range(0, candidates.Count)];
            float min = Mathf.Max(pick.xMin + 0.5f, ArenaBounds.Left + 1f);
            float max = Mathf.Min(pick.xMax - 0.5f, ArenaBounds.Right - 1f);
            if (max < min) {
                continue;
            }
            float x = Random.Range(min, max);
            Vector2 spot = new Vector2(x, pick.y);
            if (map.HeadroomAt(x, pick.y) < headroom) {
                continue;
            }
            float fromHero = HeroDistance(spot);
            if (fromHero < minDistanceFromHero) {
                if (fromHero > farthest) {
                    farthest = fromHero;
                    farthestPoint = spot;
                    farthestPlatform = pick;
                }
                continue;
            }
            found++;
            // past 8 units every spot is spread enough: the jitter keeps the pick random among them
            float score = Mathf.Min(CrowdDistance(spot), 8f) + Random.Range(0f, 2f);
            if (score > bestScore) {
                bestScore = score;
                point = spot;
                platform = pick;
            }
        }
        if (found > 0) {
            return true;
        }
        if (farthestPlatform != null && farthest >= minDistanceFromHero * 0.5f) {
            point = farthestPoint;
            platform = farthestPlatform;
            return true;
        }
        return false;
    }

    // platforms a walker, a flyer, a chest or a vase can go to: the ones Freny can reach (a flyer over
    // a closed-off ledge or a scrap under the floor would be stuck there)
    private List<ArenaMap.Platform> Reachable(ArenaMap map) {
        return map == null || map.Floor == null ? null : map.ReachableFrom(map.Floor);
    }

    // two points at the ends of the platform: the enemy patrols the whole of it
    private Transform[] PatrolFor(ArenaMap.Platform platform) {
        if (patrols.TryGetValue(platform, out Transform[] ends)) {
            return ends;
        }
        ends = new Transform[2];
        for (int i = 0; i < 2; i++) {
            Transform end = new GameObject(i == 0 ? "PatrolA " + platform.index : "PatrolB " + platform.index).transform;
            end.SetParent(transform);
            end.position = new Vector3(i == 0 ? platform.xMin + 0.5f : platform.xMax - 0.5f, platform.y, 0f);
            ends[i] = end;
        }
        patrols[platform] = ends;
        return ends;
    }

    private IEnumerator SpawnWithPortal(Entry entry) {
        ArenaMap map = Map;
        bool sorcerer = entry.prefab.GetComponent<Sorcerer>() != null;
        float headroom = entry.flying ? 3.2f : sorcerer ? 1.6f : 2.2f;
        int tower = sorcerer && map != null ? TowerForNewSorcerer(map) : -1;
        List<ArenaMap.Platform> candidates = tower >= 0 ? TowerLedges(map, tower)
            : Reachable(map);
        if (!RandomSpot(candidates, headroom, out Vector3 floorPoint, out ArenaMap.Platform platform)
            && (tower < 0 || !RandomSpot(Reachable(map), headroom, out floorPoint, out platform))) {
            if (spawners.Count > 0) {
                yield return SpawnAtSpawner(entry);
            }
            yield break;
        }
        if (!platform.tower) {
            tower = -1; // the tower had no room: on a platform like the rest
        }
        Vector3 position = floorPoint;
        if (entry.flying) {
            position += Vector3.up * Random.Range(1.5f, 2.5f);
        }

        pendingSpawns++;
        pendingPoints.Add(floorPoint);
        if (sorcerer) {
            pendingSorcerers++;
        }
        if (tower >= 0) {
            pendingTower[tower]++;
        }
        Fx.Portal(entry.flying ? position : floorPoint + Vector3.up * 0.6f, portalSeconds, PortalColor);
        yield return new WaitForSeconds(portalSeconds);
        pendingSpawns--;
        pendingPoints.Remove(floorPoint);
        if (sorcerer) {
            pendingSorcerers--;
        }
        if (tower >= 0) {
            pendingTower[tower]--;
        }
#if UNITY_EDITOR
        Debug.Log($"[Arena] {entry.prefab.name} em {platform} x {floorPoint.x:0.0}, a {HeroDistance(floorPoint):0.0} do Freny");
#endif

        GameObject spawned = Instantiate(entry.prefab, position, Quaternion.identity);
        Enemy enemy = spawned.GetComponent<Enemy>();
        if (enemy == null) {
            yield break;
        }
        Transform[] ends = PatrolFor(platform);
        enemy.pointA = ends[0];
        enemy.pointB = ends[1];
        enemy.currentTarget = (Random.value < 0.5f ? ends[0] : ends[1]).position;
        if (!entry.flying) {
            enemy.PlaceFeetAt(floorPoint);
        }
        if (enemy is Sorcerer caster && platform.tower) {
            caster.castsBeforeLeaving = towerSorcererCasts;
            caster.teleportTo = () => TeleportSpot(caster);
        }
    }

    // the old way, when the map found nothing: on a scene spawner's platform
    private IEnumerator SpawnAtSpawner(Entry entry) {
        EnemySpawner spawner = FarSpawner();
        Vector3 position = SpawnPoint(spawner);
        if (entry.flying) {
            position += Vector3.up * Random.Range(1.5f, 2.5f);
        }

        bool sorcerer = IsSorcerer(entry);
        pendingSpawns++;
        if (sorcerer) {
            pendingSorcerers++;
        }
        Fx.Portal(position, portalSeconds, PortalColor);
        Sfx.Play("portal", position);
        yield return new WaitForSeconds(portalSeconds);
        pendingSpawns--;
        if (sorcerer) {
            pendingSorcerers--;
        }

        if (spawner == null) {
            yield break;
        }
        if (entry.flying) {
            GameObject spawned = Instantiate(entry.prefab, position, Quaternion.identity);
            Enemy enemy = spawned.GetComponent<Enemy>();
            if (enemy) {
                enemy.spawnerUuid = spawner.uuid;
                spawner.countEnemies++;
            }
        } else {
            spawner.SpawnDirected(entry.prefab, position); // feet on the floor point
        }
    }

    // a random spawner far enough from Freny (the farthest one when none is)
    private EnemySpawner FarSpawner() {
        EnemySpawner farthest = spawners[0];
        List<EnemySpawner> far = new List<EnemySpawner>();
        foreach (EnemySpawner spawner in spawners) {
            float distance = HeroDistance(spawner.transform.position);
            if (distance >= minDistanceFromHero) {
                far.Add(spawner);
            }
            if (distance > HeroDistance(farthest.transform.position)) {
                farthest = spawner;
            }
        }
        return far.Count > 0 ? far[Random.Range(0, far.Count)] : farthest;
    }

    private IEnumerator SpawnChest() {
        Vector3 position;
        if (RandomSpot(Reachable(Map), 1.2f, out Vector3 floorPoint, out ArenaMap.Platform _)) {
            position = floorPoint;
        } else if (spawners.Count > 0) {
            position = SpawnPoint(FarSpawner());
        } else {
            yield break;
        }
        Fx.Portal(position, portalSeconds, ChestPortalColor);
        Sfx.Play("portal", position, 1f, 1.3f);
        yield return new WaitForSeconds(portalSeconds);
        // the chest sprite is centred on its root: lift it so it sits on the floor instead of sinking in
        Instantiate(chestPrefab, position + Vector3.up * 0.3f, Quaternion.identity);
    }
}
