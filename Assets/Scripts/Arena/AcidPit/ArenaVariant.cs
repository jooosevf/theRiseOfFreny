using UnityEngine;

public enum ArenaKind { Classic, AcidPit }

// Which arena a run is played in. The Poço de Ácido is not a scene of its own: it is "ZE Arena"
// set up differently at runtime (AcidPitArena), so it keeps every fix the arena gets and needs no
// scene to merge. It opens once the player has made AcidPitScore points in one arena run, and its
// runs go to the same rankings as the classic arena (same difficulty boards, same rules: the API
// only learns its two enemies' names).
public static class ArenaVariant {

    private const string Key = "freny.arena.variant";
    // the best score of any arena run on this save, the autopilot's too (the arena is idle play)
    public const string BestScoreKey = "freny.arena.bestScore";
    public const int AcidPitScore = 50000;

    // the probe of the final test can open the Poço without 50 000 points (by reflection: Force)
    private static bool forced;

    public static ArenaKind Current {
        get => Unlocked && PlayerPrefs.GetInt(Key, 0) == 1 ? ArenaKind.AcidPit : ArenaKind.Classic;
        set {
            PlayerPrefs.SetInt(Key, value == ArenaKind.AcidPit ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // the best arena score this player has: the save's own record, and the online ones for a
    // player who got there before this record existed
    public static long BestScore {
        get {
            long best = PlayerPrefs.GetInt(BestScoreKey, 0);
            Online.OnlineServices online = Online.OnlineServices.Instance;
            if (online != null) {
                if (online.Save != null) {
                    best = System.Math.Max(best, online.Save.bestScore);
                }
                foreach (ArenaLevel level in ArenaDifficulty.Ranked) {
                    best = System.Math.Max(best, online.BestFor(level));
                }
            }
            return best;
        }
    }

    public static bool Unlocked => Features.AcidPit && (forced || BestScore >= AcidPitScore);

    // the end of every arena run (ArenaRunManager)
    public static void NoteScore(int score) {
        if (score > PlayerPrefs.GetInt(BestScoreKey, 0)) {
            PlayerPrefs.SetInt(BestScoreKey, score);
        }
    }

    public static string Label(ArenaKind kind) {
        return kind == ArenaKind.AcidPit ? "Poço de Ácido" : "Clássica";
    }

    // the arena picker of the menus (the main menu's Arena page and the pause menu's Nova corrida)
    public static void AddChoice(MenuPage page) {
        if (!Features.AcidPit) {
            return;
        }
        if (!Unlocked) {
            page.Label("Poço de Ácido: faça " + AcidPitScore / 1000 + " mil pontos numa corrida da Arena para abrir.");
            return;
        }
        page.Choice("Arena", new[] { Label(ArenaKind.Classic), Label(ArenaKind.AcidPit) }, Current == ArenaKind.AcidPit ? 1 : 0,
            i => Current = i == 1 ? ArenaKind.AcidPit : ArenaKind.Classic);
    }

    // for the final test's probe (reflection): opens the Poço and picks it for the next run
    private static void Force(bool on) {
        forced = on;
        Current = on ? ArenaKind.AcidPit : ArenaKind.Classic;
    }
}
