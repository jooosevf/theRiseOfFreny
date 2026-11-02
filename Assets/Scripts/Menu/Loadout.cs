using UnityEngine;

// What Freny takes into a stage or an arena run. Moves bought with coins are FrenyUpgrades (equipped
// or not: FrenyProgress.IsEquipped). The gear that was not an upgrade (the Berserker helmet) is no
// longer sold; what is left here reads old saves.
// Drops picked up in a stage (great sword, helmet, ammo) still last only for that stage.
public static class Loadout {

    public const string Helmet = "Helmet";
    // the Berserker helmet just for the look is one of the FrenyLook helmets now; this key only marks
    // who bought it before that (FrenyLook.MigrateBerserker)
    public const string CosmeticHelmet = "CosmeticHelmet";
    public const string GearKeyPrefix = "freny.gear.";
    public const int HelmetCost = 6000;

    // 2026-11-01: the Armeira no longer sells the helmet (it only drops); a bought one was refunded
    // (FrenyProgress.MigrateShop). The list is empty so saves and rebirths leave it alone.
    public static readonly string[] GearIds = { };

    // the cosmetic is on, or the suit that comes with it: Freny always wears the Berserker helmet
    // (FrenyMotion, CombatHud)
    public static bool WearsCosmeticHelmet => FrenyLook.WearsBerserker || FrenyLook.WearsTraje;

    public static bool Owns(string gear) {
        return PlayerPrefs.GetInt(GearKeyPrefix + gear, 0) > 0;
    }

    public static string DisplayName(string gear) {
        return "Capacete Berserker";
    }}
