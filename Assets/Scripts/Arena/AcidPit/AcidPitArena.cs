using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

// The Poço de Ácido (phase 2 of the arena, José 2026-11-01): the same "ZE Arena" scene, turned
// toxic when it loads (ArenaBootstrap adds this when ArenaVariant.Current is AcidPit). The ruins go
// green, a green haze hangs over the sky and along the floor, the light turns sickly, and acid pools
// are cut into the ground floor: they burn and slow Freny like the Ciclope's puddles (AcidPuddle,
// made permanent), and the Tentáculos come out of them. Two of the floating platforms crumble a
// moment after Freny steps on them (CrumbleBlock) and come back later, and spikes break off the
// ceilings over his head (FallingSpike). The director gets its own enemy table (EnemyTable): the
// classic arena's minus the Licker, plus the Tentáculo and the Rastejante; the pace (MaxAlive,
// spawn gaps) is the classic one, so the ranking API's limits hold for both arenas.
[DefaultExecutionOrder(1000)]   // after ArenaDayNight, whose colours it tints every frame
public class AcidPitArena : MonoBehaviour {

    public static AcidPitArena Instance { get; private set; }
    public static bool Running => Instance != null;

    // the names the run's kill log sends to the ranking API (api/_lib/rules.js)
    public const string TentacleName = "Tentacle_Enemy";
    public const string CrawlerName = "Crawler_Enemy";
    public const float CrawlerUnlockAt = 0.15f;   // a little into the run, like the Licker it replaces

    // pools on the ground floor, in whole tiles (the acid art is 1 unit a tile)
    private static readonly int[] PoolWidths = { 2, 3, 3 };
    private const float PoolGap = 2.5f;           // floor left between two pools
    private const float KeepClearOfEdges = 1.2f;  // from where a route leaves or reaches the floor
    private const float KeepClearOfMiddle = 2.5f; // the Ciclope comes out in the middle
    // the acid's top over the floor line. The surface tile's liquid starts 44 px (0.44) under its top
    // edge, so this lifts the tile until the liquid itself shows over the floor (it sat under it)
    private const float SurfaceLift = 0.6f;
    private const float PoolFrameSeconds = 0.25f;

    private const int CrumblingPlatforms = 2;
    private const int MovingPlatforms = 2;

    // a spike now and then, more often as the run heats up
    private const float SpikeFirst = 8f;
    private const float SpikeSlow = 8f;
    private const float SpikeFast = 3.5f;

    // the toxic look
    private static readonly Color GroundTint = new Color(0.7f, 0.9f, 0.58f);
    private static readonly Color BackTint = new Color(0.52f, 0.74f, 0.46f);
    private static readonly Color FarTint = new Color(0.6f, 0.88f, 0.52f);   // mountains and clouds, over ArenaDayNight's colours
    private static readonly Color LightTint = new Color(0.8f, 1f, 0.7f);
    private static readonly Color TorchGreen = new Color(0.55f, 1f, 0.35f);
    private static readonly Color Haze = new Color(0.35f, 0.75f, 0.2f);
    public static readonly Color Acid = new Color(0.55f, 0.9f, 0.25f);

    public readonly List<AcidPuddle> Pools = new List<AcidPuddle>();
    private readonly List<SpriteRenderer[]> poolTiles = new List<SpriteRenderer[]>();
    private Sprite[] acidTiles;
    private Player hero;
    private ArenaMap map;
    private float floorY;
    private bool built;
    private float nextSpike;
    private float started;
    private Tilemap mountains, clouds;
#if UNITY_5_3_OR_NEWER
    private Light2D globalLight;
    private readonly List<Light2D> torches = new List<Light2D>();
#endif

    private void Awake() {
        Instance = this;
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Start() {
        started = Time.time;
        nextSpike = Time.time + SpikeFirst;
        hero = FindAnyObjectByType<Player>();
        Recolour();
    }

    private void Update() {
        if (!built) {
            TryBuild();
            return;
        }
        AnimatePools();
        AcidPitPilot.Track(map);   // the sliding platforms' edges on the autopilot's map
        if (hero != null && !hero.IsDead && Time.time >= nextSpike) {
            nextSpike = Time.time + Mathf.Lerp(SpikeSlow, SpikeFast, ArenaDirector.CurrentDifficulty);
            DropSpike();
        }
    }

    // the map is built from the scene's colliders: once Freny is there
    private void TryBuild() {
        if (hero == null) {
            hero = FindAnyObjectByType<Player>();
        }
        map = ArenaMap.Shared(hero);
        if (map == null || map.Floor == null) {
            return;
        }
        built = true;
        floorY = map.Floor.y;
        MakePools();
        MakeCrumblingPlatforms();
        map.LandingCost = AcidPitPilot.LandingCost;   // no route onto a crumbled or cracking one
        MakeFog();
    }

    // ---- the enemies (ArenaDirector) ----

    // the Poço's list: the classic arena's (with its unlock points, which the ranking API checks)
    // minus the Licker, fewer Bugs, plus the Tentáculo from the start and the Rastejante a little
    // later. Built from the Golem prefab like the Escudeiro (EnemyTemplates)
    public static ArenaDirector.Entry[] EnemyTable(ArenaDirector.Entry[] enemies, GameObject golem) {
        if (!Running || golem == null || enemies == null) {
            return enemies;
        }
        List<ArenaDirector.Entry> list = new List<ArenaDirector.Entry>();
        foreach (ArenaDirector.Entry entry in enemies) {
            if (entry.prefab == null || entry.prefab.name == "Licker_Enemy") {
                continue;
            }
            if (entry.prefab.name == "Bug_Enemy") {
                entry.weight *= 0.7f;
            }
            list.Add(entry);
        }
        GameObject tentacle = EnemyTemplates.Make<Tentacle>(golem, TentacleName, new Vector2(0.5f, 1.5f), new Vector2(0f, 0.75f));
        if (tentacle != null) {
            list.Add(new ArenaDirector.Entry { prefab = tentacle, unlockAt = 0f, weight = 1.2f });
        }
        GameObject crawler = EnemyTemplates.Make<Crawler>(golem, CrawlerName, new Vector2(0.7f, 0.7f), Vector2.zero);
        if (crawler != null) {
            list.Add(new ArenaDirector.Entry { prefab = crawler, unlockAt = CrawlerUnlockAt, weight = 1f });
        }
        return list.ToArray();
    }

    // a Tentáculo comes out of a pool, not off a platform: the director asks here where
    public static void SpawnSpot(GameObject prefab, ref Vector3 floorPoint, ref Vector3 position) {
        if (!Running || prefab == null || prefab.GetComponent<Tentacle>() == null) {
            return;
        }
        if (Instance.PoolSpot(null, out Vector3 at)) {
            floorPoint = at;
            position = at;
        }
    }

    // a spot in the pool with the fewest Tentáculos (not `leaving`'s own pool when there is
    // another), the ones away from Freny first
    public bool PoolSpot(Tentacle leaving, out Vector3 at) {
        at = Vector3.zero;
        if (Pools.Count == 0) {
            return false;
        }
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < Pools.Count; i++) {
            AcidPuddle pool = Pools[i];
            float x = pool.transform.position.x;
            float half = pool.Width / 2f;
            int count = 0;
            foreach (Enemy enemy in Enemy.Active) {
                if (enemy is Tentacle t && t != leaving && !t.IsDead && Mathf.Abs(t.Feet.x - x) < half + 0.2f) {
                    count++;
                }
            }
            bool own = leaving != null && Mathf.Abs(leaving.Feet.x - x) < half + 0.2f;
            bool near = hero != null && Mathf.Abs(hero.transform.position.x - x) < 3f;
            float score = count * 10f + (own ? 5f : 0f) + (near ? 3f : 0f) + Random.Range(0f, 2f);
            if (score < bestScore) {
                bestScore = score;
                best = i;
            }
        }
        AcidPuddle pick = Pools[best];
        float spread = Mathf.Max(0f, pick.Width / 2f - 0.6f);
        at = new Vector3(pick.transform.position.x + Random.Range(-spread, spread), floorY, 0f);
        return true;
    }

    // brings a Tentáculo that dived back up out of another pool after `seconds` (it is switched off
    // meanwhile, so it can't run anything itself)
    public void Resurface(Tentacle tentacle, float seconds) {
        StartCoroutine(ResurfaceLater(tentacle, seconds));
    }

    private IEnumerator ResurfaceLater(Tentacle tentacle, float seconds) {
        yield return new WaitForSeconds(seconds);
        if (tentacle == null || tentacle.IsDead) {
            yield break;
        }
        if (PoolSpot(tentacle, out Vector3 at)) {
            tentacle.transform.position = at;   // the root is its feet
        }
        tentacle.gameObject.SetActive(true);
        tentacle.Surface();
    }

    // the pool under x at floor height (the autopilot and the Tentáculos ask)
    public float FloorY => floorY;

    // one of the Poço's own pools (not a puddle someone threw up)
    public static bool IsPool(AcidPuddle puddle) {
        return Instance != null && Instance.Pools.Contains(puddle);
    }

    // ---- pools ----

    private void MakePools() {
        ArenaMap.Platform floor = map.Floor;
        // keep away from where the routes leave and reach the floor (where Freny stands to jump up
        // to a platform, where he lands dropping off one), so the autopilot's hops stay dry, from the
        // middle (the Ciclope) and from where Freny starts
        List<Vector2> blocked = new List<Vector2>();
        // what is never given up: the middle, Freny's start and the pools already made
        List<Vector2> hard = new List<Vector2>();
        float middle = (ArenaBounds.Left + ArenaBounds.Right) * 0.5f;
        hard.Add(new Vector2(middle - KeepClearOfMiddle, middle + KeepClearOfMiddle));
        if (hero != null) {
            float x = hero.transform.position.x;
            hard.Add(new Vector2(x - 2f, x + 2f));
        }
        blocked.AddRange(hard);
        foreach (ArenaMap.Platform p in map.Platforms) {
            if (p == floor) {
                continue;
            }
            foreach (float fromX in new[] { floor.xMin + 1f, (floor.xMin + floor.xMax) * 0.5f, floor.xMax - 1f }) {
                if (map.FirstStep(floor, p, fromX, out ArenaMap.Step up) && up.from == floor) {
                    blocked.Add(new Vector2(up.x - KeepClearOfEdges, up.x + KeepClearOfEdges));
                }
            }
            foreach (float fromX in new[] { p.xMin, p.xMax }) {
                if (map.FirstStep(p, floor, fromX, out ArenaMap.Step down) && down.to == floor && down.kind == ArenaMap.StepKind.Drop) {
                    float landing = down.x + down.direction * 0.6f;
                    blocked.Add(new Vector2(landing - KeepClearOfEdges, landing + KeepClearOfEdges));
                }
            }
        }
        float min = Mathf.Max(floor.xMin, ArenaBounds.Left) + 2f;
        float max = Mathf.Min(floor.xMax, ArenaBounds.Right) - 2f;
        acidTiles = FrameAnimator.Load("Liquids/Acid 100x100");
        foreach (int wanted in PoolWidths) {
            // the wide one first; where it doesn't fit, a narrower one. The routes' landing spots
            // left room for only two pools (José, 2026-11-01: three): the last try lets a pool sit
            // near a route's foot, never in the middle, at Freny's start or on another pool
            bool placed = false;
            for (int pass = 0; pass < 2 && !placed; pass++) {
                List<Vector2> avoid = pass == 0 ? blocked : hard;
                for (int width = wanted; width >= 2 && !placed; width--) {
                    for (int attempt = 0; attempt < 60 && !placed; attempt++) {
                        float center = Mathf.Round(Random.Range(min + width / 2f, max - width / 2f) * 2f) / 2f;
                        float left = center - width / 2f, right = center + width / 2f;
                        bool clear = true;
                        foreach (Vector2 b in avoid) {
                            clear &= right <= b.x || left >= b.y;
                        }
                        if (clear) {
                            MakePool(center, width);
                            Vector2 taken = new Vector2(left - PoolGap, right + PoolGap);
                            blocked.Add(taken);
                            hard.Add(taken);
                            placed = true;
                        }
                    }
                }
            }
        }
#if UNITY_EDITOR
        Debug.Log($"[Arena] Poço de Ácido: {Pools.Count} poças");
#endif
    }

    private void MakePool(float center, int width) {
        AcidPuddle pool = AcidPuddle.Create(new Vector3(center, floorY, 0f), width, float.PositiveInfinity);
        SpriteRenderer flat = pool.GetComponent<SpriteRenderer>();
        if (flat != null && acidTiles != null && acidTiles.Length >= 12) {
            flat.enabled = false;   // the acid tiles draw it, sunk into the floor
        }
        Pools.Add(pool);
        if (acidTiles == null || acidTiles.Length < 12) {
            poolTiles.Add(new SpriteRenderer[0]);
            return;
        }
        // two rows of the acid tiles: the surface (sheet row 0, 6 pieces) and the skulls under it
        // (row 1), the surface line SurfaceLift over the floor, covering the floor's top tiles
        GameObject holder = new GameObject("Acid Pool");
        holder.transform.SetParent(transform, false);
        SpriteRenderer[] tiles = new SpriteRenderer[width * 2];
        for (int row = 0; row < 2; row++) {
            for (int i = 0; i < width; i++) {
                SpriteRenderer tile = new GameObject("Acid").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(holder.transform, false);
                tile.transform.position = new Vector3(center - width / 2f + 0.5f + i, floorY + SurfaceLift - 0.5f - row, 0f);
                tile.sprite = acidTiles[row * 6 + (i + Random.Range(0, 6)) % 6];
                tile.sortingOrder = 44;
                if (ItemPickup.UnlitMaterial != null) {
                    tile.sharedMaterial = ItemPickup.UnlitMaterial;
                }
                tiles[row * width + i] = tile;
            }
        }
        poolTiles.Add(tiles);
    }

    // the surface pieces shift along now and then: the acid seethes
    private int poolFrame = -1;

    private void AnimatePools() {
        if (acidTiles == null || acidTiles.Length < 6) {
            return;
        }
        int frame = (int)(Time.time / PoolFrameSeconds);
        if (frame == poolFrame) {
            return;
        }
        poolFrame = frame;
        foreach (SpriteRenderer[] tiles in poolTiles) {
            int width = tiles.Length / 2;
            for (int i = 0; i < width; i++) {
                tiles[i].sprite = acidTiles[(i * 2 + frame) % 6];
            }
        }
    }

    // ---- crumbling platforms ----

    // two of the floating platforms (a one-way collider with three tiles under it), picked at random
    private void MakeCrumblingPlatforms() {
        List<BoxCollider2D> floating = new List<BoxCollider2D>();
        int ground = LayerMask.NameToLayer("Ground");
        foreach (PlatformEffector2D effector in FindCompat.All<PlatformEffector2D>()) {
            if (effector.gameObject.layer == ground && effector.gameObject.scene == gameObject.scene
                && effector.GetComponent<BoxCollider2D>() is BoxCollider2D box && box.usedByEffector
                && effector.name.Contains("Platform") && effector.GetComponentInChildren<SpriteRenderer>() != null) {
                floating.Add(box);
            }
        }
        for (int i = 0; i < CrumblingPlatforms && floating.Count > 0; i++) {
            int pick = Random.Range(0, floating.Count);
            floating[pick].gameObject.AddComponent<CrumbleBlock>();
            floating.RemoveAt(pick);
        }
        // and the moving ones (José, 2026-11-01): of the rest, those with room to slide
        int moving = 0;
        while (moving < MovingPlatforms && floating.Count > 0) {
            int pick = Random.Range(0, floating.Count);
            BoxCollider2D box = floating[pick];
            floating.RemoveAt(pick);
            if (MovingBlock.PathClear(box)) {
                box.gameObject.AddComponent<MovingBlock>();
                moving++;
            }
        }
    }

    // ---- falling spikes ----

    // over where Freny is going: a ceiling (a platform's underside, a tower roof) 2 to 9 units above
    private void DropSpike() {
        Rigidbody2D body = hero.GetComponent<Rigidbody2D>();
        float ahead = body != null ? Mathf.Clamp(body.Vel().x * 0.6f, -2f, 2f) : 0f;
        int ground = LayerMask.GetMask("Ground");
        for (int attempt = 0; attempt < 6; attempt++) {
            float x = hero.BodyCenter.x + ahead + Random.Range(-1.5f, 1.5f);
            if (!ArenaBounds.Inside(x, 0.8f)) {
                continue;
            }
            Vector2 from = new Vector2(x, hero.BodyCenter.y);
            RaycastHit2D up = Physics2D.Raycast(from, Vector2.up, 9f, ground);
            if (up.collider == null || up.point.y - hero.Feet.y < 2f || up.point.y > ArenaBounds.Top - 0.3f) {
                continue;
            }
            FallingSpike.Hang(new Vector3(x, up.point.y, 0f));
            return;
        }
    }

    // ---- the look ----

    private void Recolour() {
        foreach (Tilemap tilemap in FindCompat.All<Tilemap>(true)) {
            if (tilemap.gameObject.scene != gameObject.scene) {
                continue;
            }
            switch (tilemap.name) {
                case "Ground_Tilemap":
                case "EXTRA Ground_Tilemap":
                case "Foreground_Tilemap":
                case "Foreground OFFSET_Tilemap":
                    tilemap.color *= GroundTint;
                    break;
                case "Background_Tilemap":
                case "EXTRA Background_Tilemap":
                case "Farground_Tilemap":
                case "Tilemap":
                    tilemap.color *= BackTint;
                    break;
                case "Mountains_Tilemap":
                    mountains = tilemap;
                    break;
                case "Clouds":
                    clouds = tilemap;
                    break;
            }
        }
        int groundLayer = LayerMask.NameToLayer("Ground");
        foreach (SpriteRenderer renderer in FindCompat.All<SpriteRenderer>(true)) {
            if (renderer.gameObject.scene == gameObject.scene && renderer.gameObject.layer == groundLayer && renderer.name.StartsWith("Platform")) {
                renderer.color *= GroundTint;
            }
        }
#if UNITY_5_3_OR_NEWER
        foreach (Light2D light in FindCompat.All<Light2D>(true)) {
            if (light.gameObject.scene != gameObject.scene) {
                continue;
            }
            if (light.lightType == Light2D.LightType.Global) {
                globalLight = light;
            } else {
                torches.Add(light);
            }
        }
#endif
        MakeSkyHaze();
    }

    // ArenaDayNight sets these every frame; this runs after it and tints what it set
    // (only what it set again this frame: a colour still the one tinted last frame is left alone,
    // or it would darken frame after frame if ArenaDayNight ever stops)
    private Color mountainsSet, cloudsSet, lightSet;
    private readonly Dictionary<Object, Color> torchSet = new Dictionary<Object, Color>();

    private void LateUpdate() {
        if (mountains != null && mountains.color != mountainsSet) {
            mountains.color *= FarTint;
            mountainsSet = mountains.color;
        }
        if (clouds != null && clouds.color != cloudsSet) {
            clouds.color *= FarTint;
            cloudsSet = clouds.color;
        }
#if UNITY_5_3_OR_NEWER
        if (globalLight != null && globalLight.color != lightSet) {
            globalLight.color *= LightTint;
            lightSet = globalLight.color;
        }
        foreach (Light2D torch in torches) {
            if (torch != null && (!torchSet.TryGetValue(torch, out Color set) || torch.color != set)) {
                torch.color = Color.Lerp(torch.color, TorchGreen, 0.6f);
                torchSet[torch] = torch.color;
            }
        }
#endif
    }

    // a green veil over the sky (in front of the sun, the moon and the castle, behind the clouds'
    // own tint and the arena)
    private void MakeSkyHaze() {
        Camera cam = Camera.main;
        if (cam == null) {
            return;
        }
        SpriteRenderer haze = Gradient("Toxic Haze", new Color(Haze.r, Haze.g, Haze.b, 0.18f), new Color(Haze.r, Haze.g, Haze.b, 0.45f), -5);
        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        haze.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        haze.transform.localScale = new Vector3(halfWidth * 2f + 2f, (halfHeight * 2f + 2f) / haze.sprite.rect.height, 1f);
    }

    // low fog along the ground floor, behind the characters
    private void MakeFog() {
        if (!ArenaBounds.Active) {
            return;
        }
        SpriteRenderer fog = Gradient("Acid Fog", new Color(Haze.r, Haze.g, Haze.b, 0f), new Color(Haze.r, Haze.g, Haze.b, 0.3f), 30);
        const float Height = 3f;
        fog.transform.position = new Vector3((ArenaBounds.Left + ArenaBounds.Right) * 0.5f, floorY + Height * 0.5f - 0.3f, 0f);
        fog.transform.localScale = new Vector3(ArenaBounds.Right - ArenaBounds.Left + 2f, Height / fog.sprite.rect.height, 1f);
    }

    // a vertical gradient in flat bands, `top` colour at the top, like ArenaDayNight's skies
    private SpriteRenderer Gradient(string name, Color top, Color bottom, int order) {
        const int Bands = 12;
        Texture2D texture = new Texture2D(1, Bands, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int i = 0; i < Bands; i++) {
            texture.SetPixel(0, i, Color.Lerp(bottom, top, i / (Bands - 1f)));
        }
        texture.Apply();
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform, false);
        renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, Bands), new Vector2(0.5f, 0.5f), 1f);
        renderer.sortingOrder = order;
        if (ItemPickup.UnlitMaterial != null) {
            renderer.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        return renderer;
    }

    // "POÇO DE ÁCIDO" across the screen as the run starts
    private GUIStyle titleStyle;

    private void OnGUI() {
        float t = Time.time - started;
        if (t > 3.5f || GameMenus.Paused) {
            return;
        }
        float alpha = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((3.5f - t) / 0.8f);
        if (titleStyle == null) {
            titleStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        float scale = Screen.width / 1920f;
        titleStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(64f * scale));
        // under the blessing / mutator lines that ArenaRunVariety shows at the same time (they sit at
        // about a quarter of the screen); it covered them
        Rect r = new Rect(0f, Screen.height * 0.40f, Screen.width, 90f * scale);
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), "POÇO DE ÁCIDO", titleStyle);
        titleStyle.normal.textColor = new Color(0.6f, 1f, 0.3f, alpha);
        GUI.Label(r, "POÇO DE ÁCIDO", titleStyle);
    }
}
