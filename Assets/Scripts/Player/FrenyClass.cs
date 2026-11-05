using System.Collections.Generic;
using UnityEngine;

// The Flagelante's numbers, from Resources/FrenyPoses/flagelante_kit.json (design/gdd/classes.md).
[System.Serializable]
public class FlagelanteKit {
    public int finisherDamage;
    public float gapFactor;           // x attackCooldown: after hits 1 and 2
    public float finisherRecovery;    // x attackCooldown: after hit 3
    public float reach;               // x the sword's
    public int greatSwings;
    public int repairsStart;          // spare repairs: each one brings the whole shield back
    public int repairsMax;
    public float shieldWalk;
    public float blockGrace;          // the same flurry only wears the shield once
    public int thornDamage;
    public float pavesCooldown;
    public int pavesDamage;
    public float pavesPush;
    public float pavesAhead;
    public float pavesRadius;
    public float pavesWideRadius;
    public float incenseFuse;
    public float incenseRadius;       // x the grenade's
    public int incenseDamageDown;     // under the grenade's
    public float incenseThrow;        // x the grenade's speed
    public float dragSeconds;
    public float dragSpeed;
    public float dragGap;
}

// The classes (José, 2026-11-05): the same Freny, the same buttons and the same slots, with another
// set of weapons. design/gdd/classes.md has the numbers and why. Bought at the Armeira as a
// FrenyUpgrade, picked in the Inventário, read by Player once when a run starts. The autopilot only
// knows the Sobrevivente's weapons, so a run it starts is always his.
public enum FrenyClass { Sobrevivente, Flagelante }

public static class FrenyClasses {

    private const string SelectedKey = "freny.class";

    private static FlagelanteKit kit;

    public static FlagelanteKit Kit {
        get {
            if (kit == null) {
                kit = JsonUtility.FromJson<FlagelanteKit>(Resources.Load<TextAsset>("FrenyPoses/flagelante_kit").text);
            }
            return kit;
        }
    }

    public static string Name(FrenyClass kit) {
        return kit == FrenyClass.Flagelante ? "Flagelante" : "Sobrevivente";
    }

    public static bool Owned(FrenyClass kit) {
        switch (kit) {
            case FrenyClass.Flagelante: return Features.ClassFlagelante && FrenyProgress.GetLevel(FrenyUpgrade.ClassFlagelante) > 0;
            default: return true;
        }
    }

    // a class switched off, or one the save no longer has, falls back to the Sobrevivente
    public static FrenyClass Selected {
        get {
            FrenyClass kit = (FrenyClass)PlayerPrefs.GetInt(SelectedKey, (int)FrenyClass.Sobrevivente);
            return Owned(kit) ? kit : FrenyClass.Sobrevivente;
        }
        set {
            PlayerPrefs.SetInt(SelectedKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static List<FrenyClass> Available() {
        List<FrenyClass> kits = new List<FrenyClass> { FrenyClass.Sobrevivente };
        if (Owned(FrenyClass.Flagelante)) {
            kits.Add(FrenyClass.Flagelante);
        }
        return kits;
    }

    // what this run is played with. Read by Player.Start; the drops and the HUD follow it
    public static FrenyClass Run { get; private set; }

    public static FrenyClass BeginRun(bool autopilotRun) {
        Run = autopilotRun ? FrenyClass.Sobrevivente : Selected;
        return Run;
    }

    public static bool Flagelante => Run == FrenyClass.Flagelante;

    // ---- art: Resources/FrenyPoses/flagelante, the icons of the Flagelante's weapons ----

    private static Dictionary<string, Sprite> icons;

    public static Sprite Icon(string name) {
        if (icons == null) {
            icons = new Dictionary<string, Sprite>();
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("FrenyPoses/flagelante")) {
                icons[sprite.name] = sprite;
            }
        }
        return icons.TryGetValue(name, out Sprite found) ? found : null;
    }

    // this run's icon for a slot, or the Sobrevivente's own
    public static Sprite Slot(string name, Sprite survivor) {
        Sprite own = Flagelante ? Icon(name) : null;
        return own != null ? own : survivor;
    }

    // the item on the floor, as this run's class calls it
    public static Sprite Drop(ItemPickup.Kind kind, Sprite survivor) {
        switch (kind) {
            case ItemPickup.Kind.Bullets: return Slot("Reparo", survivor);
            case ItemPickup.Kind.Shells: return Slot("Carga", survivor);
            case ItemPickup.Kind.Grenades: return Slot("Incensario", survivor);
            case ItemPickup.Kind.Shotgun: return Slot("Paves", survivor);
            case ItemPickup.Kind.GreatSword: return Slot("Mangual tres", survivor);
            default: return survivor;
        }
    }

    public static string ItemName(ItemPickup.Kind kind, string survivor) {
        if (!Flagelante) {
            return survivor;
        }
        switch (kind) {
            case ItemPickup.Kind.Bullets: return "os reparos";
            case ItemPickup.Kind.Shells: return "as cargas";
            case ItemPickup.Kind.Grenades: return "os incensários";
            case ItemPickup.Kind.Shotgun: return "o pavês";
            case ItemPickup.Kind.GreatSword: return "o mangual de três cabeças";
            default: return survivor;
        }
    }
}
