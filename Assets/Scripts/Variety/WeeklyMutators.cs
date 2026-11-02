using System;
using UnityEngine;

public enum Mutator { OnlySword, FastEnemies, EternalNight, CoinRain, Giants, RedMoon, Arsenal, SwiftFeet }

// The Mutador da semana (José, 2026-11-01, "cada corrida diferente"): one extra rule for every Arena
// run of the week, the same for everyone, so it counts on the normal ranking. The week is the ISO
// week by Brasília time (Monday 00:00, UTC-3, like the daily boards). None of the rules changes what
// a kill is worth, the combo or how fast the enemies spawn, so the ranking API's checks hold
// (freny-online api/_lib/validate.js: KILL_POOL and PEAK_SPAWN_SECONDS are about spawns, not about
// how fast enemies walk). The API works out the same week (rules.js weeklyMutator) and the run's
// log names the one the game used (RunLog.ToJson).
// The rule is fixed when the arena loads (ArenaVariety), so a run that crosses Monday keeps its own.
public static class WeeklyMutators {

    public class Info {
        public Mutator id;
        public string key;    // the run log's and the API's name
        public string name;
        public string what;
    }

    public static readonly Info[] All = {
        new Info { id = Mutator.OnlySword, key = "only_sword", name = "Só espada",
            what = "Revólver e escopeta ficam no coldre: é espada e granada." },
        new Info { id = Mutator.FastEnemies, key = "fast_enemies", name = "Inimigos rápidos",
            what = "Todos os inimigos andam 30% mais rápido." },
        new Info { id = Mutator.EternalNight, key = "eternal_night", name = "Noite eterna",
            what = "O dia não chega: a Arena fica sempre de noite, e a Lua de Sangue vem mais cedo." },
        new Info { id = Mutator.CoinRain, key = "coin_rain", name = "Chuva de moedas",
            what = "Moedas e diamantes valem o dobro." },
        new Info { id = Mutator.Giants, key = "giants", name = "Gigantes",
            what = "Os inimigos ficam 30% maiores e com 50% mais vida (menos o Ciclope e os de 1 de vida)." },
        new Info { id = Mutator.RedMoon, key = "red_moon", name = "Lua vermelha",
            what = "Toda noite tem Lua de Sangue, não só a cada 3." },
        new Info { id = Mutator.Arsenal, key = "arsenal", name = "Arsenal",
            what = "O Freny começa com a escopeta, 12 cartuchos e 4 granadas." },
        new Info { id = Mutator.SwiftFeet, key = "swift_feet", name = "Pés de vento",
            what = "O Freny corre 20% mais rápido e o dash recarrega na metade do tempo." },
    };

    // the weeks walk the pool in steps of 5 (coprime with 8): all eight come before one repeats,
    // and two weeks in a row never share a rule. Week 0 is the one starting Monday 2026-01-05.
    // freny-online api/_lib/rules.js (weeklyMutator) does the same sum: change both together.
    private static readonly DateTime FirstMonday = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Unspecified);
    private const int Step = 5, Offset = 3;

    // tests: 0..7 forces a rule for the next arena run (-1: the week's)
    public static int Override = -1;

    // the rule of the run being played (set when the arena loads), null outside the arena
    public static Mutator? ThisRun { get; private set; }

    public static int WeekIndex(DateTime utc) {
        DateTime brasilia = utc.AddHours(-3);
        int days = (int)Math.Floor((brasilia.Date - FirstMonday).TotalDays);
        return (int)Math.Floor(days / 7.0);
    }

    public static Mutator ForWeek(DateTime utc) {
        int n = All.Length;
        return (Mutator)(((WeekIndex(utc) * Step + Offset) % n + n) % n);
    }

    // this week's rule (the menu shows it)
    public static Mutator ThisWeek => Override >= 0 ? (Mutator)Mathf.Clamp(Override, 0, All.Length - 1) : ForWeek(DateTime.UtcNow);

    public static Info Of(Mutator m) => All[(int)m];

    // ArenaVariety: an arena run starts (or the scene is something else: inRun false)
    public static void BeginRun(bool inRun) {
        ThisRun = inRun && Features.WeeklyMutators ? ThisWeek : (Mutator?)null;
    }

    public static bool Active(Mutator m) => ThisRun.HasValue && ThisRun.Value == m;

    // what the run log sends ("" outside a run)
    public static string RunKey => ThisRun.HasValue ? Of(ThisRun.Value).key : "";

    // days until the next rule, counted from Brasília's midnight
    public static int DaysLeft {
        get {
            DateTime brasilia = DateTime.UtcNow.AddHours(-3);
            int days = (int)Math.Floor((brasilia.Date - FirstMonday).TotalDays);
            return 7 - ((days % 7) + 7) % 7;
        }
    }

    // "Mutador da semana: Gigantes" and what it does, for the menu and the start of a run
    public static string Title(Mutator m) => "Mutador da semana: " + Of(m).name;

    public static string MenuText {
        get {
            Info info = Of(ThisWeek);
            int left = DaysLeft;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(MenuKit.TitleColor) + ">" + Title(ThisWeek) + "</color>\n" +
                   info.what + " Muda em " + (left == 1 ? "1 dia." : left + " dias.");
        }
    }
}
