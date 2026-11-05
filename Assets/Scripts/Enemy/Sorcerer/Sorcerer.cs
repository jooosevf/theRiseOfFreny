using UnityEngine;

// Ranged enemy: stands still and lobs hexballs at Freny wherever he is (any height, any side).
// The Attack animation calls Attack() through SorcererAnimationEvent when the cast hand opens.
public class Sorcerer : Enemy {
    public GameObject hexballEffectPrefab;
    [SerializeField] private float castRange = 100f;  // the sorcerer always knows where Freny is and keeps casting
    // seconds between hexballs, at any difficulty (shorter with the score, Enemy.ScoreCooldown). José, 2026-10-29: 1.6 s scaled by the
    // difficulty was too strong, 10 s too weak; the Supreme casts more often
    private const float CastCooldownPlain = 5f;
    private const float CastCooldownSupreme = 3.5f;
    // only the Supreme's hexballs home in on Freny in the Arena (turning at most HomingTurn degrees a
    // second); José, 2026-10-29 (it was every sorcerer from 50k)
    private const float HomingTurn = 70f;
    // "Sorcerer Supreme": aims where Freny will be when the hexball arrives, not where he is
    [SerializeField] private bool predictive = false;
    private float CastCooldown => predictive ? CastCooldownSupreme : CastCooldownPlain;
    public bool IsSupreme => predictive;
    private float nextCastTime;
    private Rigidbody2D playerRb;
    // set by the arena director for a sorcerer on a tower, which Freny can't climb to: after this
    // many casts it vanishes in a portal (0 = stays until killed)
    [System.NonSerialized] public int castsBeforeLeaving;
    // also set for a tower sorcerer: where to blink to every few casts (null = stay)
    [System.NonSerialized] public System.Func<Vector3?> teleportTo;
    // it used to blink after every cast and was too hard to hit (José, 2026-10-29)
    private const int CastsPerTeleport = 3;
    [SerializeField] private float teleportDelay = 0.5f;  // after the hexball leaves
    [SerializeField] private float castAfterTeleport = 0.9f;
    private int casts;
    private bool laughed;

    private Transform SorcererCastPosition;

    public override void Init() {
        base.Init();
        SorcererCastPosition = FindCastPosition();
        MageStaff.Remember(hexballEffectPrefab);   // the Cajado do Mago casts the same ball
    }

    protected override Color DeathColor => predictive ? new Color(1f, 0.3f, 0.3f) : new Color(0.45f, 0.3f, 0.7f);
    protected override string SoundName => predictive ? "supreme" : "sorcerer";

    // the sorcerer stands still and casts on a steady rhythm; the Fire animation event calls Attack()
    // (before, the animator looped the cast forever, even with nobody around)
    public override void Update() {
        if (isDead || FellOutOfWorld()) {
            return;
        }
        UpdatePush(); // Freny's hits shove it back a little like everyone else
        StickToGround(); // stands still, but on the floor (one spawned over a gap floated there)
        ClampToArena();
        AimInCoop();
        UpdateRival(); // an enemy that hit it gets the hexballs until the fight ends
        if (Rival == null && (player_t == null || player == null || TargetHealth <= 0)) {
            return;
        }

        Vector2 offset = AimPoint - transform.position;
        if (Rival == null && offset.magnitude > castRange * Tuning.EnemyAggro.Value) {
            return;
        }

        // sprites face left by default, so scale.x = -1 means looking right
        transform.localScale = new Vector3(offset.x > 0 ? -1 : 1, 1, 1);

        if (Time.time >= nextCastTime && !Netted) {   // caught in Freny's net: no casting
            anim.SetTrigger("Attack");
            PlaySound("cast");
            if (predictive && !laughed) {
                laughed = true; // the Sorcerer Supreme laughs at Freny when the fight starts
                PlaySound("laugh");
            }
            nextCastTime = Time.time + CastCooldown * ScoreCooldown;
        }
    }

    // Freny's head (crouching dodges a level shot), or the enemy it is fighting
    private Vector3 AimPoint => Rival != null ? (Vector3)Rival.Center : targetsPeer ? player_t.position : player != null ? player.HeadPoint : player_t.position;

    public void Attack() {
        if (isDead || hexballEffectPrefab == null) {
            return;
        }

        casts++;
        Vector3 castPosition = SorcererCastPosition ? SorcererCastPosition.position : transform.position;
        GameObject hexball = Instantiate(hexballEffectPrefab, castPosition, Quaternion.identity);
        Fx.Burst(castPosition, DeathColor, 8, 1.5f, 0.08f, 0.4f);
        Sfx.Play("hexball_launch", castPosition);

        // aim at Freny wherever he is (below, above, diagonal), not only left or right
        HexballEffect effect = hexball.GetComponent<HexballEffect>();
        if (effect) {
            // quick from the start of an arena run, and faster with the difficulty and the overtime
            effect.speedMultiplier = ArenaBounds.Active
                ? Mathf.Lerp(1.4f, 2.6f, ArenaDirector.CurrentDifficulty) * Mathf.Min(1.3f, 1f + 0.03f * ArenaDirector.CurrentOvertime)
                : 1.4f;
            effect.caster = this; // it hits any other enemy in its way, and they blame this sorcerer
            if (Rival == null && ArenaBounds.Active && IsSupreme) {
                effect.homingTarget = targetsPeer ? null : player;
                effect.homingTurn = HomingTurn;
            }
        }
        if (effect && (player_t || Rival != null)) {
            Vector3 target = AimPoint;
            if (predictive && Rival == null) {
                if (playerRb == null) {
                    playerRb = player_t.GetComponent<Rigidbody2D>();
                }
                if (playerRb) {
                    float flightTime = Vector2.Distance(castPosition, target) / Mathf.Max(0.1f, effect.Speed);
                    target += (Vector3)(playerRb.Vel() * flightTime);
                }
            }
            effect.SetDirection(target - castPosition);
            if (Rival == null) {
                CoopWorld.HexCast(castPosition, effect, peerTarget, IsSupreme);
            }
        }
        if (castsBeforeLeaving > 0 && casts >= castsBeforeLeaving) {
            Fx.Portal(Feet + Vector3.up * 0.6f, 0.6f, DeathColor);
            Sfx.Play("portal", Feet);
            Despawn();
        } else if (teleportTo != null && casts % CastsPerTeleport == 0) {
            StartCoroutine(TeleportAfterCast());
        }
    }

    // appear, cast, vanish in a portal and come out somewhere else: hard to keep track of
    private System.Collections.IEnumerator TeleportAfterCast() {
        yield return new WaitForSeconds(teleportDelay);
        if (isDead || teleportTo == null) {
            yield break;
        }
        Vector3? to = teleportTo();
        if (!to.HasValue) {
            yield break;
        }
        Fx.Portal(Feet + Vector3.up * 0.6f, 0.4f, DeathColor);
        Sfx.Play("portal", Feet);
        PlaceFeetAt(to.Value);
        Fx.Portal(to.Value + Vector3.up * 0.6f, 0.4f, DeathColor);
        nextCastTime = Mathf.Max(nextCastTime, Time.time + castAfterTeleport);
    }

    // each sorcerer uses its own cast point; the tag search alone made every sorcerer cast from the first one found
    private Transform FindCastPosition() {
        foreach (Transform child in GetComponentsInChildren<Transform>()) {
            if (child.CompareTag("SorcererCastPosition")) {
                return child;
            }
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("SorcererCastPosition");
        return tagged ? tagged.transform : transform;
    }
}