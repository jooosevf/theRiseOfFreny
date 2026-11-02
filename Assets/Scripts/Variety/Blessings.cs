using System;
using System.Collections.Generic;
using UnityEngine;

public enum Blessing { ExtraHeart, Strength, CoinMagnet, QuickDash, FullFlask, LightFeet, QuickHands, Grenadier, StoneSkin, Vampire }

// The Bênçãos (José, 2026-11-02): each golden chest (GoldenChest) offers three drawn among those
// he doesn't hold yet, and the one picked adds to the others until the run ends (the autopilot
// picks by AutopilotOrder). None of them touches the score, the combo or the spawns (a longer combo would
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
            what = "Dá uma poção na hora e outra 45 s depois de beber.", icon = a => a.potion },
        new Info { id = Blessing.LightFeet, key = "light_feet", name = "Pés leves",
            what = "+15% de velocidade.", icon = a => a.run },
        new Info { id = Blessing.QuickHands, key = "quick_hands", name = "Mãos rápidas",
            what = "A espada recarrega 20% mais rápido.", icon = a => a.sword },
        new Info { id = Blessing.Grenadier, key = "grenadier", name = "Granadeiro",
            what = "Dá 3 granadas na hora.", icon = a => a.grenade },
        new Info { id = Blessing.StoneSkin, key = "stone_skin", name = "Pele de pedra",
            what = "Enche a armadura na hora, e ela volta cheia a cada 2 minutos.", icon = a => a.berserkerHelmet },
        new Info { id = Blessing.Vampire, key = "vampire", name = "Vampiro",
            what = "A cada 20 inimigos derrotados, recupera 1 coração.", icon = a => a.rageHelmet },
    };

    public const int Choices = 3;

    // what the autopilot takes first among those offered: staying alive, then hitting harder
    public static readonly Blessing[] AutopilotOrder = {
        Blessing.StoneSkin, Blessing.ExtraHeart, Blessing.Strength, Blessing.Vampire, Blessing.FullFlask,
        Blessing.QuickHands, Blessing.LightFeet, Blessing.QuickDash, Blessing.Grenadier, Blessing.CoinMagnet,
    };

    // the blessings of the run being played, in the order they were taken (empty outside the arena)
    public static readonly List<Blessing> Held = new List<Blessing>();

    // each one taken (the run log writes it down with the time)
    public static event Action<Blessing> Chosen;

    public static Info Of(Blessing b) => All[(int)b];

    public static bool Has(Blessing b) => Held.Contains(b);

    public static Sprite Icon(Blessing b) {
        MenuArt art = MenuArt.Get;
        return art != null ? MenuArt.Trim(Of(b).icon(art)) : null;
    }

    // a new arena run (or no run at all): none yet
    public static void Clear() {
        Held.Clear();
    }

    // up to Choices different ones he doesn't hold yet, at random; empty once he holds them all
    public static Blessing[] Draw() {
        List<Blessing> pool = new List<Blessing>();
        foreach (Info info in All) {
            if (!Has(info.id)) {
                pool.Add(info.id);
            }
        }
        Blessing[] drawn = new Blessing[Mathf.Min(Choices, pool.Count)];
        for (int i = 0; i < drawn.Length; i++) {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            drawn[i] = pool[pick];
            pool.RemoveAt(pick);
        }
        return drawn;
    }

    public static void Choose(Blessing b) {
        if (Has(b)) {
            return;
        }
        Held.Add(b);
        Chosen?.Invoke(b);
    }
}
