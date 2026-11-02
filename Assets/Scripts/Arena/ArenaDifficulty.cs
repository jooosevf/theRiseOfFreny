using UnityEngine;

// José, 2026-11-02: three levels. Hard keeps its name in the code and its boards (leaderboard_hard,
// daily_hard_*), so the records stay; the player sees it as "Freny" (Label)
public enum ArenaLevel { Easy, Medium, Hard }

// The difficulty picked before an arena run (the menu sets Current, then loads the arena).
// ArenaDirector reads the numbers: how fast the run ramps up, how many enemies at the peak,
// how far into the run it starts, and how late the strong enemies (Golem, sorcerers) unlock.
public static class ArenaDifficulty {

    private const string Key = "freny.arena.difficulty";
    private const string ManualKey = "freny.arena.manual";

    public static ArenaLevel Current {
        get => (ArenaLevel)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)ArenaLevel.Medium), 0, 2);
        set {
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();
        }
    }

    // picked with the difficulty: Manual starts the run with Auto off, Automático with it on.
    // Only a run played by hand from start to end counts for the ranking (OnlineServices).
    public static bool Manual {
        get => PlayerPrefs.GetInt(ManualKey, 0) == 1;
        set {
            PlayerPrefs.SetInt(ManualKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // one online ranking per level, in the order they are listed: Hard is the official one,
    // Easy comes last
    public static readonly ArenaLevel[] Ranked = { ArenaLevel.Hard, ArenaLevel.Medium, ArenaLevel.Easy };

    public static string Label(ArenaLevel level) {
        switch (level) {
            case ArenaLevel.Easy: return "Fácil";
            case ArenaLevel.Medium: return "Médio";
            default: return "Freny";
        }
    }

    // Medium is what José played and found "normal"; Hard adds enemies and speed, never
    // health (one-hit enemies stay one-hit, the fights keep their pace)
    private static float Pick(float easy, float medium, float hard) {
        switch (Current) {
            case ArenaLevel.Easy: return easy;
            case ArenaLevel.Hard: return hard;
            default: return medium;
        }
    }

    // seconds-to-peak multiplier: Easy ramps up slower
    public static float RampFactor => Pick(1.6f, 1f, 0.7f);

    // enemies alive at the peak
    public static float PeakFactor => Pick(0.6f, 1f, 1.5f);

    // time between spawns
    public static float SpawnIntervalFactor => Pick(1.25f, 1f, 0.8f);

    // where the run starts on the 0..1 difficulty curve
    public static float StartAt => Pick(0f, 0f, 0.15f);

    // added to every enemy's unlock point: on Easy the strong ones come much later
    public static float UnlockShift => Pick(0.15f, 0f, -0.1f);

    // enemy movement speed: the small one-hit enemies (Bug, Bat) speed up the most
    public static float SpeedFactor(bool small) => small ? Pick(0.85f, 1f, 1.25f) : Pick(0.9f, 1f, 1.1f);

    // time between enemy attacks and casts
    public static float CooldownFactor => Pick(1.25f, 1f, 0.9f);
}
