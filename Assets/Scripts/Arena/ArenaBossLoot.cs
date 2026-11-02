using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Ciclope's treasures and mood in the Arena (José, 2026-11-01). Added at runtime by
// ArenaBootstrap, so the scene file stays as is.
// - The key: a fallen Ciclope can drop its key (BossKey) on top of the chest ArenaDirector already
//   leaves. The first one of a run always does, so every player who beats it sees the golden
//   chest; the next ones drop it KeyChance of the time, so it stays a prize (a long run meets a
//   Ciclope every 150k points and would pile up keys).
// - The golden chest (GoldenChest) comes with each key, ChestDelay seconds later, through a gold
//   portal on one of the highest platforms Freny can reach. The key belongs to the run, not to the
//   inventory: the Arena stays self-contained, nothing new goes into the save, the cloud save or the
//   ranked run's log, and a key can't be farmed in one run and spent in another.
// - The music: the Ciclope's own track ("ciclope", Tools/MusicGen/dark_fantasy.py) while one is alive,
//   crossfaded by MusicPlayer, then back to the Arena's.
// - The torches flicker faster and burn redder while it is out (BossTorches).
// It also draws the short texts over the loot (Say) and the key next to the weapons (CombatHud
// calls DrawKeyIcon).
public class ArenaBossLoot : MonoBehaviour {

    public const float KeyChance = 0.25f;    // from the second Ciclope of a run on
    private const float ChestDelay = 1.5f;
    private const float PortalSeconds = 2f;
    private const string BossTrack = "ciclope";
    private static readonly Color ChestPortalColor = new Color(1f, 0.8f, 0.25f);

    private int cyclopsKilled;      // this run
    private bool bossMusic;
    private Player hero;

    private void Awake() {
        BossKey.Carried = 0;   // a new run (the scene reloads for each one) starts with no key
        gameObject.AddComponent<BossTorches>();
    }

    private void OnEnable() {
        Enemy.Died += OnEnemyDied;
    }

    private void OnDisable() {
        Enemy.Died -= OnEnemyDied;
    }

    // (the music needs nothing here: MusicPlayer picks the next scene's track when it loads)
    private void OnDestroy() {
        BossKey.Carried = 0;
    }

    private void Start() {
        GameObject heroObj = GameObject.FindGameObjectWithTag("Player");
        hero = heroObj ? heroObj.GetComponent<Player>() : null;
    }

    // a Ciclope alive in the Arena (a dying one stays in Cyclops.Alive through its death animation)
    public static bool BossOut {
        get {
            foreach (Cyclops cyclops in Cyclops.Alive) {
                if (cyclops != null && !cyclops.IsDead) {
                    return true;
                }
            }
            return false;
        }
    }

    private void Update() {
        bool boss = BossOut;
        if (boss != bossMusic && MusicPlayer.Instance != null) {
            bossMusic = boss;
            MusicPlayer.Instance.Play(boss ? BossTrack : null);   // null: the scene's own track again
        }
    }

    private void OnEnemyDied(Enemy enemy) {
        if (!(enemy is Cyclops)) {
            return;
        }
        cyclopsKilled++;
        if (Features.EnemyToughening) {
            RunModifiers.CyclopsSlain++;
            Camera cam = Camera.main;
            Vector3 banner = (cam != null ? cam.transform.position : enemy.Center) + Vector3.up * 3f;
            Color red = new Color(1f, 0.35f, 0.3f);
            Say("Os inimigos ficaram mais fortes", banner, red, 3.5f);
            Say("Inimigos: vida x" + RunModifiers.HealthMultiplier(RunModifiers.CyclopsSlain).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','), banner + Vector3.down * 0.9f, red, 3.5f);
        }
        if (Features.GoldenChest && (cyclopsKilled == 1 || Random.value < KeyChance)) {
            DropKey(enemy.Center, enemy.coinPrefab);
        }
    }

    // out of the boss's body, up in an arc, then the golden chest on a high platform; reflection
    // can call this for a test without fighting a Ciclope (a GameObject coin prefab may be null)
    private void DropKey(Vector3 from, GameObject coinPrefab) {
        float toward = ArenaBounds.Active ? Mathf.Sign((ArenaBounds.Left + ArenaBounds.Right) * 0.5f - from.x) : 1f;
        BossKey.Spawn(from + Vector3.up * 0.5f, new Vector2(toward * Random.Range(0.5f, 1.5f), 6f));
        StartCoroutine(SpawnChest(coinPrefab));
    }

    private IEnumerator SpawnChest(GameObject coinPrefab) {
        yield return new WaitForSeconds(ChestDelay);
        if (!HighSpot(out Vector3 floor)) {
            yield break;
        }
        Fx.Portal(floor + Vector3.up * 0.6f, PortalSeconds, ChestPortalColor);
        Sfx.Play("portal", floor, 1f, 1.4f);
        yield return new WaitForSeconds(PortalSeconds);
        GoldenChest.Spawn(floor, coinPrefab);
        Fx.Burst(floor + Vector3.up * 0.5f, BossKey.Gold, 30, 3.5f, 0.1f, 0.8f);
        Sfx.Play("stage_clear", floor, 0.7f, 1.2f);
        Say("Baú dourado!", floor + Vector3.up * 1.4f, BossKey.Gold, 2f);
    }

    // a free spot on one of the highest platforms Freny can reach (the towers' pockets are out of
    // his reach): the upper third of their heights, each as likely as the next; the floor itself
    // only when there is nothing else
    private bool HighSpot(out Vector3 floor) {
        floor = Vector3.zero;
        ArenaMap map = ArenaMap.Shared(hero);
        if (map == null || map.Floor == null) {
            return false;
        }
        List<ArenaMap.Platform> reachable = map.ReachableFrom(map.Floor);
        List<ArenaMap.Platform> up = new List<ArenaMap.Platform>();
        float low = float.MaxValue, high = float.MinValue;
        foreach (ArenaMap.Platform p in reachable) {
            if (p != map.Floor && !p.tower && p.Width >= 1.5f) {
                low = Mathf.Min(low, p.y);
                high = Mathf.Max(high, p.y);
            }
        }
        foreach (ArenaMap.Platform p in reachable) {
            if (p != map.Floor && !p.tower && p.Width >= 1.5f && p.y >= Mathf.Lerp(low, high, 0.66f)) {
                up.Add(p);
            }
        }
        if (up.Count == 0) {
            up.Add(map.Floor);
        }
        for (int attempt = 0; attempt < 16; attempt++) {
            ArenaMap.Platform pick = up[Random.Range(0, up.Count)];
            float min = Mathf.Max(pick.xMin + 0.6f, ArenaBounds.Active ? ArenaBounds.Left + 1f : float.MinValue);
            float max = Mathf.Min(pick.xMax - 0.6f, ArenaBounds.Active ? ArenaBounds.Right - 1f : float.MaxValue);
            if (max < min) {
                continue;
            }
            float x = Random.Range(min, max);
            if (map.HeadroomAt(x, pick.y) < 1.4f || ChestNear(x, pick.y)) {
                continue;
            }
            floor = new Vector3(x, pick.y, 0f);
            return true;
        }
        return false;
    }

    private static bool ChestNear(float x, float y) {
        foreach (GoldenChest chest in GoldenChest.Active) {
            if (chest && Vector2.Distance(chest.transform.position, new Vector2(x, y + 0.5f)) < 1.5f) {
                return true;
            }
        }
        return false;
    }

    // ---- the key on the HUD ----

    // after the weapons row (CombatHud): the key, and how many when more than one; returns where it ends
    public static float DrawKeyIcon(float x, float y, float size, float scale, GUIStyle label) {
        if (BossKey.Carried <= 0) {
            return x;
        }
        Sprite sprite = BossKey.FrameAt(Time.time);
        if (sprite == null) {
            return x;
        }
        Rect tr = sprite.textureRect;
        Texture tex = sprite.texture;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float w = size * 0.9f;
        float h = w * tr.height / Mathf.Max(1f, tr.width);
        GUI.DrawTextureWithTexCoords(new Rect(x + (size - w) / 2f, y + (size - h) / 2f, w, h), tex, uv);
        if (BossKey.Carried > 1 && label != null) {
            Rect r = new Rect(x + size - 14f * scale, y + size + 10f * scale, 60f * scale, 26f * scale);
            Color old = label.normal.textColor;
            label.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), "x" + BossKey.Carried, label);
            label.normal.textColor = old;
            GUI.Label(r, "x" + BossKey.Carried, label);
        }
        return x + size + 10f * scale;
    }

    // ---- short texts over the loot ----

    private struct Line {
        public string text;
        public Vector3 at;
        public Color color;
        public float born, life;
    }

    private static readonly List<Line> lines = new List<Line>();
    private GUIStyle sayStyle;

    // a line that floats up from a point in the world for `seconds` ("Precisa da chave do Ciclope")
    public static void Say(string text, Vector3 world, Color color, float seconds) {
        lines.RemoveAll(l => l.text == text);
        lines.Add(new Line { text = text, at = world, color = color, born = Time.time, life = seconds });
    }

    private void OnGUI() {
        Camera cam = Camera.main;
        if (cam == null || lines.Count == 0 || Event.current.type != EventType.Repaint || GameMenus.Paused || ArenaRunVariety.PickerOpen) {
            return;
        }
        if (sayStyle == null) {
            sayStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }
        float scale = Screen.height / 1080f;
        sayStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(30f * scale));
        for (int i = lines.Count - 1; i >= 0; i--) {
            Line l = lines[i];
            float age = Time.time - l.born;
            if (age >= l.life) {
                lines.RemoveAt(i);
                continue;
            }
            Vector3 screen = cam.WorldToScreenPoint(l.at + Vector3.up * (0.6f * age / l.life));
            if (screen.z < 0f) {
                continue;
            }
            float alpha = Mathf.Clamp01((l.life - age) / 0.4f);
            // as wide as the text, and whole on screen even when it was said near an edge
            float width = sayStyle.CalcSize(new GUIContent(l.text)).x + 8f;
            float left = Mathf.Clamp(screen.x - width / 2f, 0f, Mathf.Max(0f, Screen.width - width));
            Rect r = new Rect(left, Screen.height - screen.y - 30f, width, 60f);
            float o = Mathf.Max(1f, 2f * scale);
            sayStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), l.text, sayStyle);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), l.text, sayStyle);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), l.text, sayStyle);
            GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), l.text, sayStyle);
            sayStyle.normal.textColor = new Color(l.color.r, l.color.g, l.color.b, alpha);
            GUI.Label(r, l.text, sayStyle);
        }
    }

    // ---- shared art ----

    private static Sprite haloSprite;

    // a soft round glow, bright in the middle and fading to nothing at the edge (1 unit wide)
    public static Sprite HaloSprite() {
        if (haloSprite != null) {
            return haloSprite;
        }
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, t * t));
            }
        }
        texture.Apply();
        haloSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return haloSprite;
    }
}
