using System;
using System.Collections.Generic;
using UnityEngine;

public enum LookSlot { Helmet, Cape, Color }

// Freny's looks (José, 2026-11-01): helmets, capes and the colour of his shirt, picked on the menu's
// Visual screen (VarietyScreens) and worn everywhere (FrenyLookRenderer). Bought with coins,
// unlocked by an achievement or by rebirths (Rebirth), or found only in the Ciclope's golden chest
// (GoldenChest). Purely cosmetic. What is owned and worn is
// part of the save slot (SaveSystem: looks, lookWorn), so the cloud keeps it.
//
// The helmets are sheets cut per body frame (Tools/ArtGen/freny_cosmetics.py ->
// Resources/FrenyLook/helmet_<art>), the shirt is one white mask tinted with the colour, and the
// capes are drawn at runtime (CapeArt).
public static class FrenyLook {

    // GoldenChest: only the Ciclope's golden chest drops it (ChestDrop); once dropped it is kept
    // with the bought ones (BoughtIds), so the save slot and the cloud carry it the same way
    public enum Unlock { Free, Coins, Achievement, Rebirths, GoldenChest }

    // the chance a golden chest also holds a look, one he doesn't have yet (José, 2026-11-01)
    public const float ChestLookChance = 0.25f;

    public class Item {
        public string id;
        public LookSlot slot;
        public string name;
        public Unlock unlock;
        public int price;                // coins, or rebirths needed
        public Achievement achievement;
        public string art;               // helmet sheet (helmet_<art>)
        public string sheet;             // or a sheet from elsewhere in Resources
        public Color color = Color.white;   // shirt tint, or the cape's cloth
        public Color shade = Color.gray;    // the cape's folds
        public Color trim = Color.white;    // the cape's hem
    }

    private static Color C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    // the first item of each slot is "as he is" (free); prices against the Armeiro's ~67,600 coins
    public static readonly Item[] All = {
        new Item { id = "helmet.none", slot = LookSlot.Helmet, name = "Original", unlock = Unlock.Free },
        // José, 2026-11-01: the Berserker helmet just for the look, one horn snapped; with armor on,
        // FrenyMotion draws it whole
        new Item { id = BerserkerId, slot = LookSlot.Helmet, name = "Capacete Berserker", unlock = Unlock.Coins, price = 1000, sheet = "FrenyHeads/berserker_broken" },
        new Item { id = "helmet.gold", slot = LookSlot.Helmet, name = "Elmo dourado", unlock = Unlock.Coins, price = 2500, art = "gold" },
        new Item { id = "helmet.ice", slot = LookSlot.Helmet, name = "Elmo de gelo", unlock = Unlock.Coins, price = 2500, art = "ice" },
        new Item { id = "helmet.viking", slot = LookSlot.Helmet, name = "Elmo viking", unlock = Unlock.Coins, price = 4000, art = "viking" },
        new Item { id = "helmet.shadow", slot = LookSlot.Helmet, name = "Elmo sombrio", unlock = Unlock.Achievement, achievement = Achievement.Cyclops, art = "shadow" },
        new Item { id = "helmet.crown", slot = LookSlot.Helmet, name = "Coroa", unlock = Unlock.Rebirths, price = 1, art = "crown" },
        new Item { id = "helmet.cyclops", slot = LookSlot.Helmet, name = "Elmo do Ciclope", unlock = Unlock.GoldenChest, art = "cyclops" },

        new Item { id = "cape.none", slot = LookSlot.Cape, name = "Sem capa", unlock = Unlock.Free },
        new Item { id = "cape.red", slot = LookSlot.Cape, name = "Capa vermelha", unlock = Unlock.Coins, price = 1500,
            color = C(178, 40, 48), shade = C(118, 24, 36), trim = C(232, 96, 84) },
        new Item { id = "cape.night", slot = LookSlot.Cape, name = "Capa noturna", unlock = Unlock.Coins, price = 2000,
            color = C(44, 56, 116), shade = C(26, 32, 76), trim = C(120, 150, 230) },
        new Item { id = "cape.blood", slot = LookSlot.Cape, name = "Capa carmesim", unlock = Unlock.Achievement, achievement = Achievement.Survivor,
            color = C(110, 16, 30), shade = C(68, 8, 20), trim = C(208, 47, 47) },
        new Item { id = "cape.royal", slot = LookSlot.Cape, name = "Capa real", unlock = Unlock.Rebirths, price = 1,
            color = C(100, 42, 146), shade = C(62, 24, 94), trim = C(255, 214, 74) },
        new Item { id = "cape.gold", slot = LookSlot.Cape, name = "Capa dourada", unlock = Unlock.GoldenChest,
            color = C(240, 196, 70), shade = C(176, 122, 36), trim = C(255, 244, 180) },

        new Item { id = "color.none", slot = LookSlot.Color, name = "Cinza", unlock = Unlock.Free },
        new Item { id = "color.red", slot = LookSlot.Color, name = "Vermelho", unlock = Unlock.Coins, price = 400, color = C(222, 74, 70) },
        new Item { id = "color.blue", slot = LookSlot.Color, name = "Azul", unlock = Unlock.Coins, price = 400, color = C(84, 120, 222) },
        new Item { id = "color.green", slot = LookSlot.Color, name = "Verde", unlock = Unlock.Coins, price = 400, color = C(86, 176, 92) },
        new Item { id = "color.purple", slot = LookSlot.Color, name = "Roxo", unlock = Unlock.Coins, price = 400, color = C(160, 92, 214) },
        new Item { id = "color.black", slot = LookSlot.Color, name = "Preto", unlock = Unlock.Coins, price = 600, color = C(74, 72, 86) },
        new Item { id = "color.white", slot = LookSlot.Color, name = "Branco", unlock = Unlock.Achievement, achievement = Achievement.Combo5, color = C(250, 248, 240) },
        new Item { id = "color.gold", slot = LookSlot.Color, name = "Dourado", unlock = Unlock.Rebirths, price = 2, color = C(240, 196, 70) },
    };

    public const string OwnedKey = "freny.look.owned";          // ids bought with coins, comma separated
    public const string WornKeyPrefix = "freny.look.";          // + helmet / cape / color: the id worn

    public static event Action Changed;

    public static Item Find(string id) => Array.Find(All, i => i.id == id);

    public static List<Item> InSlot(LookSlot slot) => new List<Item>(Array.FindAll(All, i => i.slot == slot && Reachable(i)));

    // a look only a mechanic switched off (Features) gives is not shown
    public static bool Reachable(Item item) {
        return (item.unlock != Unlock.Rebirths || Features.Rebirth) && (item.unlock != Unlock.GoldenChest || Features.GoldenChest);
    }

    // ---- owned ----

    public static List<string> BoughtIds {
        get {
            string raw = PlayerPrefs.GetString(OwnedKey, "");
            return new List<string>(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public static void SetBoughtIds(IEnumerable<string> ids) {
        string raw = ids != null ? string.Join(",", ids) : "";
        if (raw.Length > 0) PlayerPrefs.SetString(OwnedKey, raw); else PlayerPrefs.DeleteKey(OwnedKey);
    }

    public static bool Owns(Item item) {
        if (item == null) {
            return false;
        }
        switch (item.unlock) {
            case Unlock.Free: return true;
            case Unlock.Coins:
            case Unlock.GoldenChest: return BoughtIds.Contains(item.id);
            case Unlock.Achievement: return Achievements.Has(item.achievement);
            default: return Rebirth.Count >= item.price;
        }
    }

    // "2500 moedas", "Conquista: Duro de Matar", "1 renascimento"
    public static string HowToGet(Item item) {
        switch (item.unlock) {
            case Unlock.Free: return "";
            case Unlock.Coins: return item.price + " moedas";
            case Unlock.Achievement: return "Conquista: " + Achievements.Of(item.achievement).name;
            case Unlock.GoldenChest: return "Só no baú dourado do Ciclope";
            default: return item.price == 1 ? "1 renascimento" : item.price + " renascimentos";
        }
    }

    // the golden chest's look (GoldenChest.Open): ChestLookChance of one he doesn't have yet, kept
    // at once like a bought one; null when the roll fails or he has them all
    public static Item ChestDrop() => GrantChestLook(UnityEngine.Random.value < ChestLookChance);

    // `lucky` skips the roll (the final test calls this by reflection with true)
    private static Item GrantChestLook(bool lucky) {
        if (!lucky) {
            return null;
        }
        List<Item> missing = new List<Item>(Array.FindAll(All, i => i.unlock == Unlock.GoldenChest && !Owns(i)));
        if (missing.Count == 0) {
            return null;
        }
        Item item = missing[UnityEngine.Random.Range(0, missing.Count)];
        List<string> owned = BoughtIds;
        owned.Add(item.id);
        SetBoughtIds(owned);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return item;
    }

    public static bool TryBuy(Item item) {
        if (item == null || item.unlock != Unlock.Coins || Owns(item) || FrenyProgress.Coins < item.price) {
            return false;
        }
        FrenyProgress.Coins -= item.price;
        List<string> bought = BoughtIds;
        bought.Add(item.id);
        SetBoughtIds(bought);
        PlayerPrefs.Save();
        Sfx.Ui("shop_buy");
        Changed?.Invoke();
        return true;
    }

    // ---- worn ----

    private static string Key(LookSlot slot) => WornKeyPrefix + slot.ToString().ToLowerInvariant();

    public static string WornId(LookSlot slot) => PlayerPrefs.GetString(Key(slot), "");

    public static void SetWornId(LookSlot slot, string id) {
        if (!string.IsNullOrEmpty(id)) PlayerPrefs.SetString(Key(slot), id); else PlayerPrefs.DeleteKey(Key(slot));
    }

    // what he wears in that slot; an item no longer owned (another slot loaded, a reset) is not worn
    public static Item Worn(LookSlot slot) {
        Item item = Find(WornId(slot));
        return item != null && item.slot == slot && item.unlock != Unlock.Free && Reachable(item) && Owns(item) ? item : null;
    }

    public const string BerserkerId = "helmet.berserker";
    public static bool WearsBerserker => Features.Looks && WornId(LookSlot.Helmet) == BerserkerId;

    // whoever bought the cosmetic helmet when it was gear of the Armeiro (Loadout.CosmeticHelmet)
    // keeps it, and keeps it on unless it was taken off or another helmet is on
    public static void MigrateBerserker() {
        string key = Loadout.GearKeyPrefix + Loadout.CosmeticHelmet;
        string off = FrenyProgress.UnequippedKeyPrefix + Loadout.CosmeticHelmet;
        if (PlayerPrefs.GetInt(key, 0) <= 0) {
            return;
        }
        List<string> bought = BoughtIds;
        if (!bought.Contains(BerserkerId)) {
            bought.Add(BerserkerId);
            SetBoughtIds(bought);
        }
        if (PlayerPrefs.GetInt(off, 0) == 0 && WornId(LookSlot.Helmet) == "") {
            SetWornId(LookSlot.Helmet, BerserkerId);
        }
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(off);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static bool Wear(Item item) {
        if (item == null || !Owns(item)) {
            return false;
        }
        SetWornId(item.slot, item.unlock == Unlock.Free ? "" : item.id);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    // ---- art ----

    private static readonly Dictionary<string, Dictionary<string, Sprite>> sheets = new Dictionary<string, Dictionary<string, Sprite>>();

    // a sprite sheet in Resources (FrenyLook/helmet_<art>, FrenyLook/shirt), by body frame name
    public static Dictionary<string, Sprite> Sheet(string path) {
        if (!sheets.TryGetValue(path, out Dictionary<string, Sprite> sheet) || sheet == null) {
            sheet = new Dictionary<string, Sprite>();
            foreach (Sprite s in Resources.LoadAll<Sprite>(path)) {
                sheet[s.name] = s;
            }
            sheets[path] = sheet;
        }
        return sheet;
    }

    public static Dictionary<string, Sprite> HelmetSheet(Item helmet) =>
        helmet == null ? null : helmet.sheet != null ? Sheet(helmet.sheet) : helmet.art != null ? Sheet("FrenyLook/helmet_" + helmet.art) : null;

    public static Dictionary<string, Sprite> ShirtSheet => Sheet("FrenyLook/shirt");

    // the plain helmet of each body frame (FrenyHeads/berserker has one per frame), which the
    // capes hang from: its bottom is his neck, its back edge the back of his head
    public static Dictionary<string, Sprite> NeckSheet => Sheet("FrenyHeads/berserker");

    // ---- save (SaveSystem) ----

    public static List<string> WornIds() {
        List<string> worn = new List<string>();
        foreach (LookSlot slot in Enum.GetValues(typeof(LookSlot))) {
            string id = WornId(slot);
            if (!string.IsNullOrEmpty(id)) {
                worn.Add(id);
            }
        }
        return worn;
    }

    public static void Load(List<string> bought, List<string> worn) {
        SetBoughtIds(bought);
        foreach (LookSlot slot in Enum.GetValues(typeof(LookSlot))) {
            Item item = worn != null ? Array.Find(All, i => i.slot == slot && worn.Contains(i.id)) : null;
            SetWornId(slot, item != null ? item.id : "");
        }
        Changed?.Invoke();
    }
}
