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
    private static readonly Color SparkColor = new Color(0.45f, 1f, 0.35f);
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
        if (!Features.MageStaff || !ArenaBounds.Active || hexPrefab == null || Random.value >= DropChance) {
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
        // the sorcerers' ball art is purple, and a green tint over purple only came out dark blue:
        // Freny's balls hide it and draw a green orb of their own in its place
        foreach (SpriteRenderer renderer in ball.GetComponentsInChildren<SpriteRenderer>()) {
            renderer.color = new Color(1f, 1f, 1f, 0f);
            AddGreenOrb(renderer);
        }
        // and its purple glow (the prefab's two particle systems) turns green too
        foreach (ParticleSystem glow in ball.GetComponentsInChildren<ParticleSystem>()) {
            ParticleSystem.MainModule main = glow.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(BallTint.r, BallTint.g, BallTint.b, main.startColor.color.a));
            glow.Clear();
        }
        // and the purple light it casts on the walls (URP is not in the CI compile check, like ArenaDayNight)
#if UNITY_5_3_OR_NEWER
        foreach (UnityEngine.Rendering.Universal.Light2D light in ball.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>()) {
            light.color = new Color(0.25f, 0.9f, 0.3f);
        }
#endif
        Fx.Burst(from, SparkColor, 8, 1.5f, 0.08f, 0.4f);
        Sfx.Play("hexball_launch", from, 0.8f, 1.2f);
    }

    // a soft green orb over the ball's sprite, the same size
    private static Sprite orbSprite;

    private static void AddGreenOrb(SpriteRenderer over) {
        if (orbSprite == null) {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    Color c = d < 0.35f ? new Color(0.85f, 1f, 0.75f) : d < 0.7f ? BallTint : new Color(0.2f, 0.75f, 0.25f);
                    c.a = d < 0.7f ? 1f : Mathf.Clamp01((1f - d) / 0.3f) * 0.8f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            orbSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        GameObject go = new GameObject("Green Orb");
        go.transform.SetParent(over.transform, false);
        SpriteRenderer orb = go.AddComponent<SpriteRenderer>();
        orb.sprite = orbSprite;
        orb.sortingLayerID = over.sortingLayerID;
        orb.sortingOrder = over.sortingOrder + 1;
        if (ItemPickup.UnlitMaterial != null) {
            orb.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        // as big as the ball looks: the orb is 1 unit across, the ball's sprite this many units in the
        // ball's own space (its scale applies to both)
        float across = over.sprite != null ? Mathf.Min(over.sprite.bounds.size.x, over.sprite.bounds.size.y) * 0.8f : 0.4f;
        go.transform.localScale = new Vector3(across, across, 1f);
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
