using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

// Day and night in the arena (José, 2026-10-28): the moon crosses the sky from left to right,
// then the sun rises, the sky turns blue, the arena gets brighter and the torch flames burn blue;
// at dusk it goes back to night. The clouds drift all the time. Purely visual, nothing in the game
// changes with the time of day. Added at runtime by ArenaBootstrap, so the scene file stays as is.
//
// The painted moon was part of the night sky picture; it is swapped for the moonless copy and
// drawn as its own sprite. Clouds, mountains and the floating castle are drawn with day-coloured
// copies (Sky/Resources/DayNight) tinted down to their painted night colours, so the night looks
// exactly as before. Sky, clouds and mountains are unlit, so the global light only brightens the
// arena itself, and on top of the Brilho setting (BrightnessLights), never instead of it.
public class ArenaDayNight : MonoBehaviour {

    // José, 2026-10-30: 1 minute of day, 4 of night; a run starts at night with the moon a third
    // of the way across. Phase stays 0..0.5 for the day and 0.5..1 for the night
    private const float DaySeconds = 60f;
    private const float NightSeconds = 240f;
    private const float StartSeconds = DaySeconds + NightSeconds / 3f;
    private const float CloudSpeed = 0.25f;   // units per second, to the left

    // where the sun and the moon travel, as a fraction of the screen: an arch from the left
    // horizon to the right one (low down they are hidden behind the ruins and the mountains)
    private const float ArcHalfWidth = 0.42f;
    private const float HorizonY = 0.45f;
    private const float ArcHeight = 0.36f;

    // the painted night colours, as multipliers of the day sprites (same numbers that made them)
    private static readonly Color CloudNight = new Color(0.119f, 0.105f, 0.161f);
    private static readonly Color MountainNight = new Color(0.16f, 0.15f, 0.22f);
    private static readonly Color CastleNight = new Color(0.26f, 0.23f, 0.32f);

    // the day sky, top to bottom, and the sunrise / sunset glow over it
    private static readonly Color DayTop = new Color32(74, 136, 212, 255);
    private static readonly Color DayBottom = new Color32(176, 212, 236, 255);
    private static readonly Color DuskTop = new Color32(96, 84, 160, 255);
    private static readonly Color DuskBottom = new Color32(244, 148, 96, 255);

    // global light: the scene's own colour at night, warm white by day, orange at dawn and dusk.
    // José, 2026-10-29: the day was too bright and the enemies turned blue. The torches' lights
    // reach 10 units, and at full strength in blue they tinted everything near them; by day they
    // now fade to a faint glow (the sun does the lighting), and the day light is lower (was 1.8)
    // and warmer (was 1, 0.97, 0.9): the enemies are painted in navy blues, which a white light
    // shows as plain blue
    private const float DayLightBoost = 1.4f;
    private static readonly Color DayLight = new Color(1f, 0.9f, 0.76f);
    private static readonly Color DuskLight = new Color(1f, 0.68f, 0.55f);
    private static readonly Color BlueTorchLight = new Color(0.45f, 0.7f, 1f);
    private const float DayTorchLight = 0.15f;   // of the torch light's night intensity

    // the sky's layers, behind the clouds (1) and the mountains (2)
    private const int NightSkyOrder = -10;
    private const int DaySkyOrder = -9;
    private const int DuskSkyOrder = -8;
    private const int SunMoonOrder = -7;
    private const int CastleOrder = -6;

    // tests and screenshots: when 0..1, the cycle stays at this point of the day
    public static float PhaseOverride = -1f;

    // the Lua de Sangue (José, 2026-10-30): every BloodEvery-th night the moon turns red, the arena
    // goes red and the enemies get faster, heal and hit harder (Enemy.BloodMoon reads Blood).
    // A run starts in night 1, so the first one is night 3. It lasts only BloodSeconds around
    // midnight (José, 2026-10-30), about 10 min 25 s into a run, then the night goes back to
    // normal. Blood is 0..1 and fades in and out over BloodFade. BloodOverride forces it for tests
    public const int BloodEvery = 3;
    private const float BloodSeconds = 30f;
    private const float BloodFade = 3f;
    public static float Blood { get; private set; }
    public static float BloodOverride = -1f;
    private static readonly Color BloodLight = new Color(1f, 0.32f, 0.28f);
    private static readonly Color BloodTint = new Color(1f, 0.55f, 0.55f);
    private static readonly Color BloodMoonColor = new Color(1f, 0.3f, 0.25f);
    private float bloodAnnounced = -1f;
    private int announcedNight;
    private GUIStyle bloodStyle;

    // which night it is (1 = the one the run starts in), or 0 by day
    private int NightNumber {
        get {
            float t = StartSeconds + Time.time - startTime;
            float cycle = DaySeconds + NightSeconds;
            if (Mathf.Repeat(t, cycle) < DaySeconds) {
                return 0;
            }
            return Mathf.FloorToInt(t / cycle) + 1;
        }
    }
    // seconds since the night fell (only meaningful at night)
    private float NightSecond => Mathf.Repeat(StartSeconds + Time.time - startTime, DaySeconds + NightSeconds) - DaySeconds;

    // the blood window sits in the middle of the night, with a short fade at each end
    private static float BloodAt(float nightSecond) {
        float fromMiddle = Mathf.Abs(nightSecond - NightSeconds / 2f);
        return Mathf.Clamp01((BloodSeconds / 2f - fromMiddle) / BloodFade);
    }

    // how much daylight there is right now, 0 (night, or no cycle) to 1 (full day): the hexballs
    // read it to draw their dark rim, which keeps them visible against the bright day
    public static float Daylight { get; private set; }

    private Camera cam;
    private float startTime;
    private Transform clouds;
    private float cloudsX;
    private float cloudPeriod = 16f;
    private Tilemap cloudMap, mountainMap;
    private SpriteRenderer daySky, duskSky, sun, moon, castle;
    private readonly List<Torch> torches = new List<Torch>();
    private readonly Dictionary<string, Sprite> blueFlames = new Dictionary<string, Sprite>();

#if UNITY_5_3_OR_NEWER
    private Light2D globalLight;
    private Color nightLight;
#endif

    private class Torch {
        public SpriteRenderer flame;
        public SpriteRenderer blue;
#if UNITY_5_3_OR_NEWER
        public Light2D light;
        public Color color;
        public float intensity;
#endif
    }

    // 0 = sunrise: the sun is up for the first half of the cycle, the moon for the second
    public float Phase => PhaseOverride >= 0f ? PhaseOverride : PhaseAt(Mathf.Repeat(StartSeconds + Time.time - startTime, DaySeconds + NightSeconds));

    private static float PhaseAt(float seconds) {
        return seconds < DaySeconds ? 0.5f * seconds / DaySeconds : 0.5f + 0.5f * (seconds - DaySeconds) / NightSeconds;
    }

    private void Start() {
        cam = Camera.main;
        startTime = Time.time;
        Sprite[] loaded = Resources.LoadAll<Sprite>("DayNight");
        var sprites = new Dictionary<string, Sprite>();
        foreach (Sprite sprite in loaded) {
            if (sprite.name.StartsWith("Standing Torch Blue_")) {
                blueFlames[sprite.name.Substring("Standing Torch Blue_".Length)] = sprite;
            } else {
                sprites[sprite.name] = sprite;
            }
        }

        Tilemap sky = null;
        foreach (Tilemap map in FindCompat.All<Tilemap>(true)) {
            if (map.gameObject.scene != gameObject.scene) {
                continue;
            }
            switch (map.name) {
                case "Sky_Tilemap": sky = map; break;
                case "Clouds": cloudMap = map; break;
                case "Mountains_Tilemap": mountainMap = map; break;
            }
        }
        if (sky == null || cam == null) {
            enabled = false;
            return;
        }
        TilemapRenderer skyRenderer = sky.GetComponent<TilemapRenderer>();
        Material unlit = skyRenderer.sharedMaterial;
        int layer = skyRenderer.sortingLayerID;
        skyRenderer.sortingOrder = NightSkyOrder;

        SetUpSky(sky, sprites, unlit, layer);
        if (cloudMap != null) {
            UseDaySprites(cloudMap, sprites);
            clouds = cloudMap.transform;
            cloudsX = clouds.localPosition.x;
        }
        if (mountainMap != null) {
            UseDaySprites(mountainMap, sprites);
        }

        daySky = MakeGradient("Day Sky", DayTop, DayBottom, unlit, layer, DaySkyOrder);
        duskSky = MakeGradient("Dusk Sky", DuskTop, DuskBottom, unlit, layer, DuskSkyOrder);
        sun = MakeSprite("Sun", sprites, unlit, layer, SunMoonOrder);
        moon = MakeSprite("Moon", sprites, unlit, layer, SunMoonOrder);

        FindTorches();
#if UNITY_5_3_OR_NEWER
        foreach (Light2D light in FindCompat.All<Light2D>(true)) {
            if (light.lightType == Light2D.LightType.Global && light.gameObject.scene == gameObject.scene) {
                globalLight = light;
                nightLight = light.color;
                break;
            }
        }
#endif
        LateUpdate();
    }

    // the moon tile becomes the plain night sky (the moon is drawn on its own), and the castle
    // leaves the tilemap so it can stay in front of the day sky
    private void SetUpSky(Tilemap sky, Dictionary<string, Sprite> sprites, Material unlit, int layer) {
        TileBase plain = null;
        var moonCells = new List<Vector3Int>();
        foreach (Vector3Int cell in sky.cellBounds.allPositionsWithin) {
            Sprite sprite = sky.GetSprite(cell);
            if (sprite == null) {
                continue;
            }
            if (sprite.name == "Night sky") {
                plain = sky.GetTile(cell);
            } else if (sprite.name == "Night sky Moon") {
                moonCells.Add(cell);
            } else if (sprite.name == "Floating Castle" && castle == null && sprites.TryGetValue("Floating Castle Day", out Sprite day)) {
                castle = MakeRenderer("Floating Castle", day, unlit, layer, CastleOrder);
                castle.transform.position = sky.CellToWorld(cell) + Vector3.Scale(sky.tileAnchor, sky.layoutGrid.cellSize);
                sky.SetTile(cell, null);
            }
        }
        if (plain != null) {
            foreach (Vector3Int cell in moonCells) {
                sky.SetTile(cell, plain);
            }
        }
    }

    // every tile whose sprite has a "<name> Day" copy gets a tile with that copy
    private static void UseDaySprites(Tilemap map, Dictionary<string, Sprite> sprites) {
        var tiles = new Dictionary<Sprite, Tile>();
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin) {
            Sprite sprite = map.GetSprite(cell);
            if (sprite == null || !sprites.TryGetValue(sprite.name + " Day", out Sprite day)) {
                continue;
            }
            if (!tiles.TryGetValue(day, out Tile tile)) {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = day;
                tile.colliderType = Tile.ColliderType.None;
                tiles[day] = tile;
            }
            Matrix4x4 matrix = map.GetTransformMatrix(cell);
            map.SetTile(cell, tile);
            map.SetTransformMatrix(cell, matrix);
        }
    }

    private void FindTorches() {
        foreach (SpriteRenderer flame in FindCompat.All<SpriteRenderer>(true)) {
            if (flame.gameObject.scene != gameObject.scene || flame.sprite == null || !flame.sprite.name.StartsWith("Standing Torch_")) {
                continue;
            }
            var torch = new Torch { flame = flame };
            torch.blue = MakeRenderer("Blue Flame", null, flame.sharedMaterial, flame.sortingLayerID, flame.sortingOrder + 1);
            torch.blue.transform.SetParent(flame.transform, false);
#if UNITY_5_3_OR_NEWER
            torch.light = flame.GetComponent<Light2D>();
            if (torch.light != null) {
                torch.color = torch.light.color;
                torch.intensity = torch.light.intensity;
            }
#endif
            torches.Add(torch);
        }
    }

    // leaving the arena (campaign, menu) is always night for the hexballs
    private void OnDisable() {
        Daylight = 0f;
        Blood = 0f;
    }

    private void LateUpdate() {
        float phase = Phase;
        float sunHeight = Mathf.Sin(phase * 2f * Mathf.PI);
        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.2f, sunHeight));
        Daylight = day;
        int night = PhaseOverride >= 0f ? 0 : NightNumber;
        bool bloodNight = night > 0 && night % BloodEvery == 0;
        Blood = BloodOverride >= 0f ? BloodOverride : bloodNight ? BloodAt(NightSecond) : 0f;
        if (bloodNight && night != announcedNight && Blood > 0.5f) {
            announcedNight = night;
            bloodAnnounced = Time.time;
            Sfx.Play("supreme_laugh", cam.transform.position, 0.9f, 0.6f);
        }
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(sunHeight - 0.02f) / 0.22f);

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 center = cam.transform.position;

        foreach (SpriteRenderer sky in new[] { daySky, duskSky }) {
            sky.transform.position = new Vector3(center.x, center.y, 0f);
            sky.transform.localScale = new Vector3(halfWidth * 2f + 2f, (halfHeight * 2f + 2f) / sky.sprite.rect.height, 1f);
        }
        daySky.color = new Color(1f, 1f, 1f, day);
        duskSky.color = new Color(1f, 1f, 1f, dusk * 0.85f);

        PlaceBody(sun, phase, center, halfWidth, halfHeight);
        PlaceBody(moon, phase - 0.5f, center, halfWidth, halfHeight);
        if (moon != null) {
            Color c = moon.color;
            moon.color = Color.Lerp(c, new Color(BloodMoonColor.r, BloodMoonColor.g, BloodMoonColor.b, c.a), Blood);
        }

        if (cloudMap != null) {
            cloudMap.color = Color.Lerp(CloudNight, Color.white, day) * Color.Lerp(Color.white, BloodTint, Blood);
            float drift = PhaseOverride >= 0f ? 0f : Mathf.Repeat((Time.time - startTime) * CloudSpeed, cloudPeriod);
            Vector3 position = clouds.localPosition;
            clouds.localPosition = new Vector3(cloudsX - drift, position.y, position.z);
        }
        if (mountainMap != null) {
            mountainMap.color = Color.Lerp(MountainNight, Color.white, day) * Color.Lerp(Color.white, BloodTint, Blood);
        }
        if (castle != null) {
            castle.color = Color.Lerp(CastleNight, Color.white, day) * Color.Lerp(Color.white, BloodTint, Blood);
        }

        foreach (Torch torch in torches) {
            if (torch.flame == null) {
                continue;
            }
            Sprite blue = null;
            bool show = day > 0.01f && torch.flame.enabled && torch.flame.sprite != null
                        && blueFlames.TryGetValue(FrameOf(torch.flame.sprite), out blue);
            torch.blue.enabled = show;
            if (show) {
                torch.blue.sprite = blue;
                torch.blue.flipX = torch.flame.flipX;
                torch.blue.color = new Color(1f, 1f, 1f, day);
            }
#if UNITY_5_3_OR_NEWER
            if (torch.light != null) {
                torch.light.color = Color.Lerp(torch.color, BlueTorchLight, day);
                torch.light.intensity = torch.intensity * Mathf.Lerp(1f, DayTorchLight, day);
            }
#endif
        }

#if UNITY_5_3_OR_NEWER
        if (globalLight != null) {
            float boost = Mathf.Lerp(1f, DayLightBoost, day);
            globalLight.intensity = BrightnessLights.BaseIntensity(globalLight) * GameSettings.BrightnessFactor * boost;
            Color light = Color.Lerp(nightLight, DayLight, day);
            globalLight.color = Color.Lerp(Color.Lerp(light, DuskLight, dusk * 0.5f), BloodLight, Blood * 0.7f);
        }
#endif
    }

    // "LUA DE SANGUE" across the screen for a few seconds when it begins
    private void OnGUI() {
        if (bloodAnnounced < 0f || Time.time - bloodAnnounced > 4f || GameMenus.Paused) {
            return;
        }
        float t = Time.time - bloodAnnounced;
        float alpha = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((4f - t) / 0.8f);
        if (bloodStyle == null) {
            bloodStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        float scale = Screen.width / 1920f;
        bloodStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(64f * scale));
        Rect r = new Rect(0f, Screen.height * 0.28f, Screen.width, 90f * scale);
        bloodStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), "LUA DE SANGUE", bloodStyle);
        bloodStyle.normal.textColor = new Color(1f, 0.2f, 0.15f, alpha);
        GUI.Label(r, "LUA DE SANGUE", bloodStyle);
    }

    // half a turn above the horizon from left to right, the other half below it, out of sight
    private static void PlaceBody(SpriteRenderer body, float phase, Vector3 center, float halfWidth, float halfHeight) {
        if (body == null) {
            return;
        }
        float angle = phase * 2f * Mathf.PI;
        float height = Mathf.Sin(angle);
        body.transform.position = new Vector3(
            center.x - Mathf.Cos(angle) * ArcHalfWidth * halfWidth * 2f,
            center.y - halfHeight + (HorizonY + height * ArcHeight) * halfHeight * 2f,
            0f);
        body.color = new Color(1f, 1f, 1f, Mathf.InverseLerp(-0.05f, 0.08f, height));
    }

    private static string FrameOf(Sprite sprite) {
        string name = sprite.name;
        int underscore = name.LastIndexOf('_');
        return underscore >= 0 ? name.Substring(underscore + 1) : name;
    }

    private SpriteRenderer MakeSprite(string name, Dictionary<string, Sprite> sprites, Material material, int layer, int order) {
        return sprites.TryGetValue(name, out Sprite sprite) ? MakeRenderer(name, sprite, material, layer, order) : null;
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite, Material material, int layer, int order) {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    // a vertical gradient in a few flat bands, like the pixel art around it; it covers the whole
    // screen, but the sky only shows in the upper half, so the change happens up there
    private SpriteRenderer MakeGradient(string name, Color top, Color bottom, Material material, int layer, int order) {
        const int bands = 20;
        var texture = new Texture2D(1, bands, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int i = 0; i < bands; i++) {
            texture.SetPixel(0, i, Color.Lerp(bottom, top, Mathf.InverseLerp(bands * 0.4f, bands - 1f, i)));
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, bands), new Vector2(0.5f, 0.5f), 1f);
        return MakeRenderer(name, sprite, material, layer, order);
    }
}
