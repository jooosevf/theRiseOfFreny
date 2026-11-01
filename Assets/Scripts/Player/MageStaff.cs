using UnityEngine;

// The Cajado do Mago (José, 2026-11-01): now and then (DropChance) a sorcerer that dies in the arena
// leaves its staff on the floor (ItemPickup.Kind.Staff). Picked up, Freny's shots are spell balls for
// Duration seconds: the sorcerers' own hexball (HexballEffect, fromFreny), which hurts enemies and
// never him, with no ammo used. The staff shows on his back with sparks, and the HUD shows how long
// it lasts (CombatHud). Another staff picked up starts the time again.
public class MageStaff : MonoBehaviour {

    public const float DropChance = 0.08f;
    public const float Duration = 20f;
    public const float Cooldown = 0.3f;     // between casts (the revolver's is 0.2)
    public const int Damage = 2;
    private const float BallSpeed = 3.2f;   // x the prefab's speed (3): a little quicker than a sorcerer's
    private const float BlinkFor = 3f;      // the staff blinks before it goes
    private static readonly Color SparkColor = new Color(0.7f, 0.45f, 1f);
    // Freny's balls are green-gold, so they don't read as the sorcerers' purple ones
    public static readonly Color BallTint = new Color(0.6f, 1f, 0.6f);

    // the sorcerers' hexball prefab, taken from the one that dropped the staff
    private static GameObject hexPrefab;
    private static Sprite staffSprite;

    // Freny's staff (one Freny at a time)
    public static MageStaff Current { get; private set; }

    private Player player;
    private float until;
    private SpriteRenderer shown;
    private float nextSpark;

    public static Sprite Icon => staffSprite != null ? staffSprite : (staffSprite = Resources.Load<Sprite>("Fase4/Mage Staff"));
    public bool Active => Time.time < until && player != null && !player.IsDead;
    public float TimeLeft => Mathf.Max(0f, until - Time.time);

    public static bool Held(Player player) {
        return Current != null && Current.player == player && Current.Active;
    }

    // every sorcerer that comes into the scene hands over its ball, so the staff can cast it
    public static void Remember(GameObject hexballPrefab) {
        if (hexballPrefab != null) {
            hexPrefab = hexballPrefab;
        }
    }

    // ArenaDirector, when a sorcerer dies
    public static void RollDrop(Sorcerer sorcerer) {
        if (sorcerer == null) {
            return;
        }
        Remember(sorcerer.hexballEffectPrefab);
        if (!ArenaBounds.Active || hexPrefab == null || Random.value >= DropChance) {
            return;
        }
        Drop(sorcerer.Center);
    }

    // for testing too: a staff tossed up from that point
    public static void Drop(Vector3 at) {
        ItemPickup.Spawn(ItemPickup.Kind.Staff, 1, at, new Vector2(Random.Range(-1f, 1f), Random.Range(4f, 5.5f)));
    }

    // ItemPickup: the staff in his hands for Duration seconds
    public static bool Give(Player player) {
        if (player == null || player.IsDead) {
            return false;
        }
        MageStaff staff = player.GetComponent<MageStaff>();
        if (staff == null) {
            staff = player.gameObject.AddComponent<MageStaff>();
        }
        staff.player = player;
        staff.until = Time.time + Duration;
        Current = staff;
        staff.Show(true);
        Fx.Burst(player.BodyCenter, SparkColor, 24, 3f, 0.1f, 0.6f);
        Sfx.Play("level_up", player.transform.position, 0.8f, 1.3f);
        return true;
    }

    private void OnDestroy() {
        if (Current == this) {
            Current = null;
        }
        if (shown != null) {
            Destroy(shown.gameObject);
        }
    }

    // Player.Shoot, instead of a bullet
    public void Cast(Vector3 from, Vector3 direction) {
        GameObject prefab = hexPrefab;
        if (prefab == null) {
            return;
        }
        GameObject ball = Instantiate(prefab, from, Quaternion.identity);
        HexballEffect effect = ball.GetComponent<HexballEffect>();
        if (effect != null) {
            effect.fromFreny = true;
            effect.caster = null;
            effect.damage = Damage;
            effect.knockback = 0f;
            effect.speedMultiplier = BallSpeed;
            effect.SetDirection(direction);
        }
        foreach (SpriteRenderer renderer in ball.GetComponentsInChildren<SpriteRenderer>()) {
            renderer.color = BallTint;
        }
        Fx.Burst(from, SparkColor, 8, 1.5f, 0.08f, 0.4f);
        Sfx.Play("hexball_launch", from, 0.8f, 1.2f);
    }

    private void Update() {
        if (!Active) {
            Show(false);
            return;
        }
        Show(true);
        // on his back, the orb over the shoulder away from where he faces (followed in world space,
        // so the player's own scale doesn't matter)
        float back = player.FacingRight ? -1f : 1f;
        shown.transform.position = player.BodyCenter + new Vector3(back * 0.28f, -0.45f, 0f);
        shown.transform.rotation = Quaternion.Euler(0f, 0f, back * 15f);
        float left = TimeLeft;
        shown.enabled = left > BlinkFor || Mathf.Repeat(Time.time, 0.3f) < 0.18f;
        if (Time.time >= nextSpark) {
            nextSpark = Time.time + 0.12f;
            Vector3 orb = shown.bounds.center + Vector3.up * (shown.bounds.extents.y * 0.75f);
            Fx.Rise(orb + (Vector3)(Random.insideUnitCircle * 0.15f), SparkColor, Random.Range(0.05f, 0.1f), 0.6f);
        }
    }

    // the staff on his back, the orb over his shoulder
    private void Show(bool on) {
        if (!on) {
            if (shown != null) {
                shown.enabled = false;
            }
            return;
        }
        if (shown == null) {
            GameObject obj = new GameObject("Mage Staff (held)");
            shown = obj.AddComponent<SpriteRenderer>();
            shown.sprite = Icon;
            SpriteRenderer body = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            if (body != null && body != shown) {
                shown.sortingLayerID = body.sortingLayerID;
                shown.sortingOrder = body.sortingOrder - 1;   // behind him: it peeks over his shoulder
            }
        }
        shown.enabled = true;
    }
}
