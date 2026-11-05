using System.Collections.Generic;
using UnityEngine;

// An item lying on the floor: a potion, revolver bullets, shotgun shells, grenades, a shotgun, a
// great sword (a few big swings), the Berserker helmet (armor) or a diamond (25 coins). Enemies drop
// one now and then where they die; chests and vases hold them too. Built at runtime (no prefab): a
// sprite, a soft halo behind it, a trigger and the same small arc as the coins, falling to rest
// just above the floor. Freny collects it by touching it; an item he has no use for (a potion at
// full health, a full reserve, full armor) stays where it is until it fades. The three rare
// power-ups are items too; ArenaPowerUps rolls them, spins them and does what they do.
public class ItemPickup : MonoBehaviour {

    public enum Kind { Potion, Bullets, Shells, Grenades, Shotgun, GreatSword, Armor, Diamond, Staff, InstaKill, Nuke, Magnet }

    // enemy drops, rolled once per death on top of its coin. These are the chances when Freny is
    // half short of that item; each one is scaled by what he needs right now (Need: none at full
    // health or a full reserve, double when empty) and by what's already lying around (Crowding)
    private const float PotionChance = 0.15f;
    private const float BulletsChance = 0.12f;
    private const float ShellsChance = 0.10f;   // only when he has the shotgun
    private const float ShotgunChance = 0.03f;  // only when he doesn't
    private const float GrenadeChance = 0.06f;
    private const float GreatSwordChance = 0.04f; // only when he can use one
    private const float ArmorChance = 0.04f;
    private const float DiamondChance = 0.04f;
    private const float MaxDropChance = 0.55f;    // all chances together, however short he is
    private const float SameKindFactor = 0.3f;    // per item of that kind already on the floor
    private const int CalmItems = 3;              // items on the floor before drops start to thin out
    private const int BusyItems = 10;             // ... down to BusyFactor of the chance at this many
    private const float BusyFactor = 0.2f;
    private const int ShotgunShellsInDrop = 6;
    public const int DiamondValue = 25;           // coins
    private const float DiamondFrameTime = 0.08f;
    private const int GrenadesPerDrop = 1;
    private const int BulletsPerDrop = 4;
    private const int ShellsPerDrop = 3;

    private const float TossGravity = -18f;
    private const float Lifetime = 29f;   // 25 s steady, then blinks for the last 4
    private const float BlinkFor = 4f;
    private const float WorldSize = 0.75f;       // the drawn art's longest side, the shotgun a bit bigger (WorldSizeFor)
    private const float PickupRadius = 0.45f;    // world units, whatever the art's size
    private const float HaloSize = 1.6f;         // halo diameter, relative to the art's longest side
    private static readonly Color HaloColor = new Color(1f, 0.88f, 0.5f, 0.5f);
    private const float BobHeight = 0.06f;       // bobs up from its resting spot, never into the floor
    private const float RestGap = 0.08f;         // how far the art's bottom rests above the floor
    private const float MaxFallSpeed = 14f;

    public static readonly List<ItemPickup> Active = new List<ItemPickup>();

    public Kind kind;
    public int amount = 1;

    private Vector2 velocity;
    private bool tossing;
    private bool pulled;   // on its way to Freny (the Ímã, ArenaPowerUps)
    private float hoverY;
    private float restLift = 0.25f;  // from the floor to the pivot, so the art's bottom sits just above it
    private float bobPhase;
    private float dieAt;
    private SpriteRenderer sprite;
    private SpriteRenderer halo;
    private static int groundMask = -1;
    private static int GroundMask => groundMask >= 0 ? groundMask : (groundMask = LayerMask.GetMask("Ground"));
    private static Sprite fallbackSprite;
    private static Sprite haloSprite;
    private static Material unlitMaterial;

    public bool Landed => !tossing;

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDisable() {
        Active.Remove(this);
    }

    // ---- making them -------------------------------------------------------------------------

    public static ItemPickup Spawn(Kind kind, int amount, Vector3 position, Vector2 toss) {
        GameObject obj = new GameObject("Item " + kind);
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteFor(kind);
        renderer.color = TintFor(kind);
        renderer.sortingOrder = 40;
        // unlit, so it stays bright in the dark corners the torches don't reach
        if (UnlitMaterial != null) {
            renderer.sharedMaterial = UnlitMaterial;
        }
        // the icons sit small in the middle of 100x100 cells, so size by the drawn part, not the cell
        Bounds art = ArtBounds(renderer.sprite);
        float artSize = Mathf.Max(art.size.x, art.size.y);
        float scale = artSize > 0.001f ? WorldSizeFor(kind) / artSize : 1f;
        obj.transform.localScale = Vector3.one * scale;

        GameObject haloObj = new GameObject("Halo");
        haloObj.transform.SetParent(obj.transform, false);
        haloObj.transform.localPosition = art.center;
        SpriteRenderer haloRenderer = haloObj.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = HaloSprite();
        haloRenderer.color = HaloFor(kind);
        haloRenderer.sortingOrder = 39;
        if (UnlitMaterial != null) {
            haloRenderer.sharedMaterial = UnlitMaterial;
        }
        haloObj.transform.localScale = Vector3.one * (artSize > 0.001f ? artSize * HaloSize : 1f);

        CircleCollider2D trigger = obj.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.offset = art.center;
        trigger.radius = PickupRadius / scale;
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic; // triggers need a body; the arc moves it by hand

        ItemPickup pickup = obj.AddComponent<ItemPickup>();
        pickup.kind = kind;
        pickup.amount = amount;
        pickup.sprite = renderer;
        pickup.halo = haloRenderer;
        pickup.velocity = toss;
        pickup.restLift = RestGap - art.min.y * scale;
        pickup.tossing = true;
        pickup.dieAt = Time.time + Lifetime;
        return pickup;
    }

    // what a dying enemy leaves besides its coin (most of the time nothing)
    public static void RollEnemyDrop(Vector3 position) {
        Vector2 toss = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(4f, 5.5f));
        RollDrop(position, toss, 1f,
            (Kind.Potion, PotionChance), (Kind.Bullets, BulletsChance), (Kind.Grenades, GrenadeChance),
            (Kind.Shells, ShellsChance), (Kind.Shotgun, ShotgunChance), (Kind.GreatSword, GreatSwordChance),
            (Kind.Armor, ArmorChance), (Kind.Diamond, DiamondChance));
    }

    // a broken vase: often nothing, sometimes a small reward
    public static void RollVaseDrop(Vector3 position) {
        Vector2 toss = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(4.5f, 5.5f));
        RollDrop(position, toss, 1f,
            (Kind.Diamond, 0.12f), (Kind.Potion, 0.18f), (Kind.Bullets, 0.10f), (Kind.Grenades, 0.08f), (Kind.Armor, 0.03f));
    }

    // one roll over the weighted kinds: each chance times what Freny needs of it and how many of
    // it are already on the floor, all of it thinned out when the floor is full of drops
    private static void RollDrop(Vector3 position, Vector2 toss, float scale, params (Kind kind, float chance)[] table) {
        Player player = FindPlayer();
        float[] chances = new float[table.Length];
        float total = 0f;
        for (int i = 0; i < table.Length; i++) {
            chances[i] = table[i].chance * Need(table[i].kind, player) * Crowding(table[i].kind);
            total += chances[i];
        }
        float squeeze = (total > MaxDropChance ? MaxDropChance / total : 1f) * Clutter() * scale;
        float roll = Random.value;
        for (int i = 0; i < table.Length; i++) {
            if ((roll -= chances[i] * squeeze) < 0f) {
                Spawn(table[i].kind, AmountFor(table[i].kind), position, toss);
                return;
            }
        }
    }

    private static int AmountFor(Kind kind) {
        switch (kind) {
            case Kind.Bullets: return BulletsPerDrop;
            case Kind.Shells: return ShellsPerDrop;
            case Kind.Grenades: return GrenadesPerDrop;
            case Kind.Shotgun: return ShotgunShellsInDrop;
            default: return 1;
        }
    }

    // how much Freny needs it, 0 (no use) .. 2 (out of it), 1 when half short
    public static float Need(Kind kind, Player player) {
        if (player == null) {
            return 1f;
        }
        switch (kind) {
            case Kind.Potion:
                if (!player.WantsPotion) return 0f;
                // the last heart counts as fully out of health
                return player.Health <= 1 ? 2f : 2f * (player.MaxHealth - player.Health) / Mathf.Max(1, player.MaxHealth);
            case Kind.Bullets: return 2f * (1f - player.BulletsFill);
            case Kind.Shells: return player.ShotgunOwned ? 2f * (1f - player.ShellsFill) : 0f;
            case Kind.Shotgun: return player.ShotgunOwned ? 0f : 2f;
            case Kind.Grenades: return 2f * (1f - player.GrenadesFill);
            case Kind.Armor: return 2f * (Player.ArmorMax - player.Armor) / (float)Player.ArmorMax;
            case Kind.GreatSword: return player.WantsGreatSword ? 1f : 0f;
            case Kind.Diamond: return FrenyProgress.CoinShare;   // always welcome, less so for the autopilot
            default: return 1f;
        }
    }

    // the same item already waiting on the floor makes another one much less likely
    private static float Crowding(Kind kind) {
        int lying = 0;
        foreach (ItemPickup item in Active) {
            if (item != null && item.kind == kind) {
                lying++;
            }
        }
        return Mathf.Pow(SameKindFactor, lying);
    }

    // in a frantic fight the floor fills with drops: past a few items, fewer new ones
    private static float Clutter() {
        int count = Active.Count;
        if (count <= CalmItems) {
            return 1f;
        }
        return Mathf.Lerp(1f, BusyFactor, (count - CalmItems) / (float)(BusyItems - CalmItems));
    }

    private static Player cachedPlayer;

    private static Player FindPlayer() {
        if (cachedPlayer == null) {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            cachedPlayer = obj ? obj.GetComponent<Player>() : null;
        }
        return cachedPlayer;
    }

    private static float WorldSizeFor(Kind kind) {
        switch (kind) {
            case Kind.Shotgun: return WorldSize * 1.3f;
            case Kind.GreatSword: return WorldSize * 1.4f;
            case Kind.Diamond: return WorldSize * 0.8f;
            case Kind.Potion: return WorldSize * 0.7f;
            case Kind.Staff: return WorldSize * 1.4f;   // the Cajado do Mago (MageStaff), long and thin
            case Kind.InstaKill: case Kind.Nuke: case Kind.Magnet: return WorldSize * 1.25f;   // rare: they stand out
            default: return WorldSize;
        }
    }

    private static Sprite SpriteFor(Kind kind) {
        ArenaAssets assets = ArenaAssets.Instance;
        Sprite sprite = null;
        if (assets != null) {
            switch (kind) {
                case Kind.Potion: sprite = assets.potionIcon; break;
                case Kind.Bullets: sprite = assets.ammoIcon != null ? assets.ammoIcon : assets.revolverIcon; break;
                case Kind.Shells: sprite = assets.shellsIcon != null ? assets.shellsIcon : assets.ammoIcon != null ? assets.ammoIcon : assets.shotgunIcon; break;
                case Kind.Grenades: sprite = assets.grenadeIcon; break;
                case Kind.Shotgun: sprite = assets.shotgunIcon; break;
                case Kind.GreatSword: sprite = assets.greatSwordIcon; break;
                case Kind.Armor: sprite = assets.armorHelmetIcon; break;
                case Kind.Diamond: sprite = assets.diamondFrames != null && assets.diamondFrames.Length > 0 ? assets.diamondFrames[0] : null; break;
            }
        }
        sprite = FrenyClasses.Drop(kind, sprite);
        if (kind == Kind.Staff) {
            sprite = MageStaff.Icon;
        }
        if (ArenaPowerUps.IsPowerUp(kind)) {
            sprite = ArenaPowerUps.Icon(kind);
        }
        return sprite != null ? sprite : FallbackSprite();
    }

    private static Color HaloFor(Kind kind) {
        return ArenaPowerUps.IsPowerUp(kind) ? ArenaPowerUps.ColorOf(kind) : HaloColor;
    }

    // icons reused for several items get a tint so they read apart (bullets gold, shells red...)
    private static Color TintFor(Kind kind) {
        ArenaAssets assets = ArenaAssets.Instance;
        switch (kind) {
            case Kind.Potion: return assets != null && assets.potionIcon != null ? Color.white : new Color(0.95f, 0.25f, 0.3f);
            case Kind.Bullets: return assets != null && assets.ammoIcon != null ? Color.white : new Color(1f, 0.85f, 0.35f);
            case Kind.Shells: return assets != null && assets.shellsIcon != null ? Color.white : new Color(1f, 0.55f, 0.45f);
            case Kind.Diamond: return assets != null && assets.diamondFrames != null && assets.diamondFrames.Length > 0 ? Color.white : new Color(0.4f, 0.65f, 1f);
            case Kind.Armor: return assets != null && assets.armorHelmetIcon != null ? Color.white : new Color(0.6f, 0.6f, 0.65f);
            default: return Color.white;
        }
    }

    // the opaque part of a sprite in its own units: the Tight mesh hugs the art, the rect doesn't
    private static Bounds ArtBounds(Sprite sprite) {
        if (sprite == null) {
            return new Bounds(Vector3.zero, Vector3.one * 0.6f);
        }
        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0) {
            return sprite.bounds;
        }
        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Length; i++) {
            bounds.Encapsulate(vertices[i]);
        }
        return bounds;
    }

    // URP's unlit sprite shader ignores the 2D lights (Sprites/Default draws the icons as pale blocks
    // under the 2D renderer); it is in Always Included Shaders, and without it they stay lit
    public static Material UnlitMaterial {
        get {
            if (unlitMaterial == null) {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null) {
                    unlitMaterial = new Material(shader);
                }
            }
            return unlitMaterial;
        }
    }

    // a soft round glow, bright in the middle and fading to nothing at the edge (1 unit wide)
    private static Sprite HaloSprite() {
        if (haloSprite != null) {
            return haloSprite;
        }
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, t * t));
            }
        }
        texture.Apply();
        haloSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return haloSprite;
    }

    // a plain disc, for an item whose icon is not set (the campaign scenes have no ArenaAssets)
    private static Sprite FallbackSprite() {
        if (fallbackSprite != null) {
            return fallbackSprite;
        }
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - size * 0.5f + 0.5f, dy = y - size * 0.5f + 0.5f;
                texture.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        }
        texture.Apply();
        fallbackSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return fallbackSprite;
    }

    // ---- life on the floor -------------------------------------------------------------------

    private void Update() {
        if (Time.time >= dieAt) {
            Destroy(gameObject);
            return;
        }
        if (sprite) {
            // blinks for its last seconds, so it is clear it is about to go
            sprite.enabled = dieAt - Time.time > BlinkFor || Mathf.Repeat(Time.time, 0.3f) < 0.18f;
            if (halo) {
                // breathes gently, so it catches the eye without looking like a hit flash
                halo.enabled = sprite.enabled;
                Color c = HaloFor(kind);
                c.a *= 0.7f + 0.3f * Mathf.Sin(Time.time * 4f + bobPhase);
                halo.color = c;
            }
            if (kind == Kind.Diamond) {
                AnimateDiamond();
            } else if (ArenaPowerUps.IsPowerUp(kind)) {
                sprite.sprite = ArenaPowerUps.FrameAt(kind, Time.time + bobPhase);
            }
        }
        // the Ímã brings what he can use; the rest stays where it is
        if (ArenaPowerUps.PullTarget(out Vector3 pullTo) && UsefulTo(FindPlayer())) {
            pulled = true;
            tossing = false;
            transform.position = ArenaPowerUps.PulledFrom(transform.position, pullTo);
            return;
        }
        if (pulled) {
            // the magnet ran out with it in the air: it falls from there
            pulled = false;
            tossing = true;
            velocity = Vector2.zero;
        }
        if (!tossing) {
            // rests just above the floor, bobbing a little like the coins
            Vector3 p = transform.position;
            p.y = hoverY + (Mathf.Sin(Time.time * 3f + bobPhase) + 1f) * 0.5f * BobHeight;
            transform.position = p;
            return;
        }

        velocity.y = Mathf.Max(velocity.y + TossGravity * Time.deltaTime, -MaxFallSpeed);
        Vector3 pos = transform.position;
        Vector3 next = pos + (Vector3)(velocity * Time.deltaTime);
        if (velocity.y < 0f) {
            // falls until the first floor under it
            float drop = pos.y - next.y;
            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.down, drop + restLift, GroundMask);
            if (hit.collider != null) {
                next.y = hit.point.y + restLift;
                Hover(next.y);
            }
        }
        if (ArenaBounds.Active) {
            next.x = ArenaBounds.ClampX(next.x, 0.3f);
        }
        if (next.y < -50f || (ArenaBounds.Active && next.y < ArenaBounds.Bottom - 3f)) {
            Destroy(gameObject); // fell out of the world
            return;
        }
        transform.position = next;
    }

    // the diamond sparkles like the coin spins (same frames as its sheet, the size set at Spawn)
    private void AnimateDiamond() {
        ArenaAssets assets = ArenaAssets.Instance;
        if (assets == null || assets.diamondFrames == null || assets.diamondFrames.Length < 2) {
            return;
        }
        int frame = (int)((Time.time + bobPhase) / DiamondFrameTime) % assets.diamondFrames.Length;
        sprite.sprite = assets.diamondFrames[frame];
    }

    private void Hover(float y) {
        tossing = false;
        hoverY = y;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // would Freny take it right now (the autopilot skips the ones he can't use)
    public bool UsefulTo(Player player) {
        if (player == null) {
            return false;
        }
        switch (kind) {
            case Kind.Potion: return player.WantsPotion;
            case Kind.Bullets: return player.WantsBullets;
            case Kind.Shells: return player.WantsShells;
            case Kind.Grenades: return player.WantsGrenades;
            case Kind.Shotgun: return !player.ShotgunOwned || player.WantsShells;
            case Kind.GreatSword: return player.WantsGreatSword;
            case Kind.Armor: return player.WantsArmor;
            case Kind.Diamond: return true;
            case Kind.Staff: return true;
            case Kind.InstaKill: case Kind.Nuke: case Kind.Magnet: return true;
            default: return false;
        }
    }

    public string Describe() {
        switch (kind) {
            case Kind.Potion: return "a poção";
            case Kind.Bullets: return FrenyClasses.ItemName(kind, "as balas");
            case Kind.Shells: return FrenyClasses.ItemName(kind, "os cartuchos");
            case Kind.Grenades: return FrenyClasses.ItemName(kind, "as granadas");
            case Kind.Shotgun: return FrenyClasses.ItemName(kind, "a escopeta");
            case Kind.GreatSword: return FrenyClasses.ItemName(kind, "o espadão");
            case Kind.Armor: return "a armadura";
            case Kind.Diamond: return "o diamante";
            case Kind.Staff: return "o cajado";
            case Kind.InstaKill: return "o Insta-kill";
            case Kind.Nuke: return "o Nuke";
            case Kind.Magnet: return "o Ímã";
            default: return "o item";
        }
    }

    // Stay, not Enter: a potion touched at full health is taken as soon as he gets hurt
    private void OnTriggerStay2D(Collider2D other) {
        if (!other.CompareTag("Player")) {
            return;
        }
        Player player = other.GetComponent<Player>();
        if (player == null || !Apply(player)) {
            return;
        }
        Fx.Burst(transform.position, TintFor(kind) == Color.white ? new Color(1f, 0.95f, 0.7f) : TintFor(kind), 10, 2f, 0.07f, 0.35f);
        Sfx.Play(PickupSound(kind), transform.position);
        Destroy(gameObject);
    }

    private bool Apply(Player player) {
        switch (kind) {
            case Kind.Potion: return player.CollectPotion(amount);
            case Kind.Bullets: return player.AddBullets(amount);
            case Kind.Shells: if (!player.WantsShells) return false; player.AddShells(amount); return true;
            case Kind.Grenades: return player.AddGrenades(amount);
            case Kind.Shotgun: return player.GiveShotgun(amount);
            case Kind.GreatSword: return player.GiveGreatSword();
            case Kind.Armor: return player.GiveArmor();
            case Kind.Diamond: player.AddCoins(RunModifiers.ScaleCoins(DiamondValue * amount)); return true;
            case Kind.Staff: return MageStaff.Give(player);
            case Kind.InstaKill: case Kind.Nuke: case Kind.Magnet: return ArenaPowerUps.Take(kind);
            default: return false;
        }
    }

    private static string PickupSound(Kind kind) {
        switch (kind) {
            case Kind.Potion: return "potion";
            case Kind.Shotgun: return "pistol_reload";
            case Kind.GreatSword: return "weapon_switch";
            case Kind.Armor: return "upgrade";
            case Kind.Staff: return "hexball_launch";
            case Kind.Diamond: return "coin";
            case Kind.InstaKill: case Kind.Nuke: case Kind.Magnet: return "upgrade";
            default: return "pickup_ammo";
        }
    }
}
