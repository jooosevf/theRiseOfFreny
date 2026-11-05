using UnityEngine;

// The Rede (José, 2026-11-01): bought at the Armeira, Freny starts each stage with NetsPerStage
// nets and throws one with the Rede button (F; a pad button can be set in Controles; "Rede" on a
// touch screen). It flies in an arc like the grenade, opens where it lands or on the first enemy it
// touches, and holds every enemy under it for NetTrap.Seconds (the Ciclope is only slowed down).
// Added to Freny at runtime by Player.Start, so the Player prefab is untouched.
public class NetThrower : MonoBehaviour {

    public const int NetsPerStage = 3;
    public const int MaxNets = 5;
    public const float TapCharge = 0.45f;   // a tap of the key: about 5 m ahead
    public const float Angle = 40f;
    public const float MinSpeed = 5f;
    public const float MaxSpeed = 12f;
    public const float Gravity = 1.5f;
    private const float Cooldown = 0.5f;

    private Player player;
    private FrenyInput input;
    private GuardedInt nets;
    private bool granted;
    private float readyAt;

    public int Nets { get => nets; private set => nets = value; }
    public bool CanThrow => Nets > 0 && Time.time >= readyAt && player != null && !player.IsDead && !player.IsStunned;

    private static Sprite ballSprite;
    public static Sprite Icon => ballSprite != null ? ballSprite : (ballSprite = Resources.Load<Sprite>("Fase4/Net Ball"));

    public static NetThrower Attach(Player player) {
        NetThrower thrower = player.GetComponent<NetThrower>();
        return thrower != null ? thrower : player.gameObject.AddComponent<NetThrower>();
    }

    // what he carried into this room of the test dungeon (CampaignWorld)
    public void SetNets(int count) {
        Nets = Mathf.Clamp(count, 0, MaxNets);
    }

    private void Start() {
        player = GetComponent<Player>();
        input = GetComponent<FrenyInput>();
        Grant();
        FrenyProgress.Changed += Grant;   // bought mid-run in the arena shop
    }

    private void OnDestroy() {
        FrenyProgress.Changed -= Grant;
    }

    private void Grant() {
        if (!granted && FrenyProgress.HasNet) {
            granted = true;
            Nets = Mathf.Min(MaxNets, Nets + NetsPerStage);
        }
    }

    // a net picked up (or given by a test): false when he is full
    public bool AddNets(int amount) {
        if (Nets >= MaxNets) {
            return false;
        }
        Nets = Mathf.Min(MaxNets, Nets + amount);
        return true;
    }

    private void Update() {
        if (input == null || player == null || !player.enabled || GameMenus.Paused) {   // not in the shop
            return;
        }
        PlayerInputFrame frame = input.Current;
        if (frame.netPressed && CanThrow) {
            Throw(frame.netCharge);
        }
    }

    public void Throw(float charge) {
        Nets--;
        readyAt = Time.time + Cooldown;
        float facing = player.FacingRight ? 1f : -1f;
        float speed = Mathf.Lerp(MinSpeed, MaxSpeed, Mathf.Clamp01(charge));
        float rad = Angle * Mathf.Deg2Rad;
        Vector2 velocity = new Vector2(facing * Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        Vector3 from = transform.position + Vector3.up * 0.6f;
        NetProjectile.Launch(from, velocity, facing, player.Feet.y);
    }

    // the charge (0..1) that lands a net `dx` away and `dy` higher; -1 out of reach (as Player.GrenadeChargeFor)
    public static float ChargeFor(float dx, float dy) {
        float rad = Angle * Mathf.Deg2Rad;
        float g = Mathf.Abs(Physics2D.gravity.y) * Gravity;
        float x = Mathf.Abs(dx);
        float denom = 2f * Mathf.Cos(rad) * Mathf.Cos(rad) * (x * Mathf.Tan(rad) - dy);
        if (denom <= 0.01f) {
            return -1f;
        }
        float speed = Mathf.Sqrt(g * x * x / denom);
        if (speed > MaxSpeed + 0.5f) {
            return -1f;
        }
        return Mathf.Clamp01(Mathf.InverseLerp(MinSpeed, MaxSpeed, speed));
    }
}
