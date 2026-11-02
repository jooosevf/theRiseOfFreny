using System;
using System.Collections.Generic;
using UnityEngine;

public enum Blessing { ExtraHeart, Strength, CoinMagnet, QuickDash, FullFlask, LightFeet, QuickHands, Grenadier, StoneSkin, Vampire }

// The Bênçãos (José, 2026-11-01, "cada corrida diferente"): at the start of every Arena run the player
// picks any of them and keeps it for the whole run (ArenaRunVariety's picker; the autopilot takes
// AutopilotChoice). None of them touches the score, the combo or the spawns (a longer combo would
// need the ranking API's COMBO_SECONDS to change), so blessed runs go to the normal ranking; the
// run log names the blessing (RunLog.ToJson) for the record.
// The numbers live in the effects: RunModifiers (stats), ArenaRunVariety (the run's extras).
public static class Blessings {

    public class Info {
        public Blessing id;
        public string key;     // the run log's name
        public string name;
        public string what;
        public Func<MenuArt, Sprite> icon;
    }

    public static readonly Info[] All = {
        new Info { id = Blessing.ExtraHeart, key = "extra_heart", name = "Coração extra",
            what = "+1 coração de vida máxima.", icon = a => a.heart },
        new Info { id = Blessing.Strength, key = "strength", name = "Força",
            what = "+25% de dano na espada, nos tiros e nas granadas.", icon = a => a.greatSword },
        new Info { id = Blessing.CoinMagnet, key = "coin_magnet", name = "Ímã de moedas",
            what = "As moedas por perto voam até o Freny.", icon = a => a.coin },
        new Info { id = Blessing.QuickDash, key = "quick_dash", name = "Dash veloz",
            what = "Dash 30% mais longo, e recarrega na metade do tempo.", icon = a => a.jump },
        new Info { id = Blessing.FullFlask, key = "full_flask", name = "Frasco cheio",
            what = "Começa com uma poção guardada e ganha outra 45 s depois de beber.", icon = a => a.potion },
        new Info { id = Blessing.LightFeet, key = "light_feet", name = "Pés leves",
            what = "+15% de velocidade.", icon = a => a.run },
        new Info { id = Blessing.QuickHands, key = "quick_hands", name = "Mãos rápidas",
            what = "A espada recarrega 20% mais rápido.", icon = a => a.sword },
        new Info { id = Blessing.Grenadier, key = "grenadier", name = "Granadeiro",
            what = "Começa com 3 granadas a mais.", icon = a => a.grenade },
        new Info { id = Blessing.StoneSkin, key = "stone_skin", name = "Pele de pedra",
            what = "Começa com a armadura cheia, e ela volta cheia a cada 2 minutos.", icon = a => a.berserkerHelmet },
        new Info { id = Blessing.Vampire, key = "vampire", name = "Vampiro",
            what = "A cada 20 inimigos derrotados, recupera 1 coração.", icon = a => a.rageHelmet },
    };

    // José, 2026-11-01: no luck in a run: the player picks any of them. The autopilot always takes
    // this one, armor that keeps coming back suits how it plays
    public const Blessing AutopilotChoice = Blessing.StoneSkin;

    // the blessing of the run being played (null before the pick and outside the arena)
    public static Blessing? Current { get; private set; }

    public static event Action<Blessing> Chosen;

    public static Info Of(Blessing b) => All[(int)b];

    public static bool Has(Blessing b) => Current.HasValue && Current.Value == b;

    public static string RunKey => Current.HasValue ? Of(Current.Value).key : "";

    public static Sprite Icon(Blessing b) {
        MenuArt art = MenuArt.Get;
        return art != null ? MenuArt.Trim(Of(b).icon(art)) : null;
    }

    // a new arena run (or no run at all): nothing picked yet
    public static void Clear() {
        Current = null;
    }

    public static void Choose(Blessing b) {
        Current = b;
        Chosen?.Invoke(b);
    }
}
