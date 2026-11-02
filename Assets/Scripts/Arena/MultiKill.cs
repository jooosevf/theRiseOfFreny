using UnityEngine;

// The multi-kill banners (José, 2026-11-02): one attack of Freny's that kills 2, 3, 4 or 5 or more
// enemies shows DOUBLE, TRIPLE, QUADRA or PENTA KILL across the screen. One attack is one grenade
// blast, one swing of the sword (of the Espadão and the Fúria too) or one shotgun shot; a swing or
// a shot lands its kills a few frames apart, so the banner climbs as they come. The Nuke is a drop,
// not an attack, so it doesn't count. It is only the banner: no points, nothing in the run's log, so
// the ranking API never hears of it. Added at runtime by ArenaBootstrap, so the scene stays as is.
public class MultiKill : MonoBehaviour {

    private const float Seconds = 1.8f;
    private const float PopSeconds = 0.15f;
    private const float FadeSeconds = 0.4f;
    private const float ScreenY = 0.37f;   // under "LUA DE SANGUE" and the achievement banner

    // from bone to blood, and bigger each step
    private static readonly string[] Names = { "DOUBLE KILL", "TRIPLE KILL", "QUADRA KILL", "PENTA KILL" };
    private static readonly int[] Sizes = { 54, 62, 72, 86 };
    private static readonly float[] Pitches = { 1f, 1.12f, 1.26f, 1.5f };
    public static readonly Color[] Colors = {
        new Color(0.91f, 0.85f, 0.72f),
        new Color(0.95f, 0.72f, 0.25f),
        new Color(1f, 0.45f, 0.15f),
        new Color(0.9f, 0.12f, 0.1f),
    };

    private static int swing, swingKills;
    private static int shot, shotKills;
    private static int tier = -1;      // which banner is up
    private static float shownAt;
    private GUIStyle style;

    private void Awake() {
        // a new run starts counting from nothing (the scene reloads for each one)
        swing = shot = swingKills = shotKills = 0;
        tier = -1;
    }

    // Grenade.Explode: all the enemies one blast killed
    public static void BlastKills(int kills) {
        Show(kills);
    }

    // MeleeAttack: a swing (Player.SwingCount) killed an enemy
    public static void SwordKill(int swingId) {
        if (swingId != swing) {
            swing = swingId;
            swingKills = 0;
        }
        Show(++swingKills);
    }

    // RangedAttack: a pellet of that shot (Achievements.NextShot) killed an enemy
    public static void ShotKill(int shotId) {
        if (shotId != shot) {
            shot = shotId;
            shotKills = 0;
        }
        Show(++shotKills);
    }

    private static void Show(int kills) {
        if (!Features.MultiKill || kills < 2) {
            return;
        }
        tier = Mathf.Min(kills, Names.Length + 1) - 2;
        shownAt = Time.unscaledTime;
        Sfx.Play("multi_kill", 1f, Pitches[tier]);
    }

    private void OnGUI() {
        float t = Time.unscaledTime - shownAt;
        if (tier < 0 || t > Seconds || GameMenus.Paused) {
            return;
        }
        if (style == null) {
            style = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        float scale = Screen.width / 1920f;
        // it lands big and settles, then fades
        float pop = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / PopSeconds));
        float alpha = Mathf.Clamp01((Seconds - t) / FadeSeconds);
        style.fontSize = Mathf.Max(16, Mathf.RoundToInt(Sizes[tier] * pop * scale));
        Rect r = new Rect(0f, Screen.height * ScreenY, Screen.width, 130f * scale);
        style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.85f);
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), Names[tier], style);
        Color c = Colors[tier];
        style.normal.textColor = new Color(c.r, c.g, c.b, alpha);
        GUI.Label(r, Names[tier], style);
    }
}
