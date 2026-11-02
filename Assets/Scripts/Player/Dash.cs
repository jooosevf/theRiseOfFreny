using UnityEngine;

// Dash: a flat, fixed-speed burst in the facing direction, once per jump in the air, and on the ground
// too (José, 2026-11-02). Owns the velocity while it lasts (Player.FixedUpdate leaves the horizontal speed
// alone), then hands it back. Ctrl dashes anywhere; Shift is the sprint on the ground and a dash in
// the air. On a touch screen the sprint's double tap also dashes, as it has no button of its own. Added
// by ArenaBootstrap and CampaignBootstrap.
public class Dash : MonoBehaviour {

    public float dashVelocity = 12f;   // units/s, constant for the whole dash
    public float startDashTime = 0.18f; // seconds
    public float dashCooldown = 1.2f;  // after the dash ends

    private Rigidbody2D rb;
    private Player player;
    private FrenyInput input;
    private PlayerAnimation playerAnim;
    private int direction;             // 0 = not dashing, otherwise -1 or 1
    private float dashUntil;
    private float nextDashTime;
    private bool usedInAir;            // one dash per jump; landing gives it back

    public bool IsDashing => direction != 0;
    public bool CanDash => direction == 0 && Time.time >= nextDashTime && !usedInAir && player;
    // the autopilot only dashes in the air: its dodges on the ground are jumps
    public bool CanAirDash => CanDash && player.IsAirborne;
    // 1 = ready, 0 = just dashed (for the HUD)

    private void Start() {
        rb = GetComponent<Rigidbody2D>();
        player = GetComponent<Player>();
        playerAnim = GetComponent<PlayerAnimation>();
        input = GetComponent<FrenyInput>();
    }

    private void Update() {
        if (player == null || player.IsDead) {
            return;
        }
        if (input == null) {
            // Player adds FrenyInput in its Start when the scene doesn't have one yet
            input = GetComponent<FrenyInput>();
        }

        if (player.IsGrounded && !player.IsAirborne) {
            usedInAir = false;
        }

        if (IsDashing) {
            if (Time.time >= dashUntil) {
                Stop();
            }
            return;
        }

        bool sprintDash = input != null && input.Current.runPressed && (player.IsAirborne || TouchControls.Active);
        if (input != null && (input.Current.dashPressed || sprintDash) && CanDash && !player.IsStunned && !player.IsCrouching) {
            Begin();
        }
    }

    private void FixedUpdate() {
        // rb is destroyed when the player dies
        if (IsDashing && rb) {
            // flat: the fall waits until the dash is over
            rb.SetVel(new Vector2(direction * dashVelocity, 0f));
        }
    }

    private void Begin() {
        direction = player.FacingRight ? 1 : -1;
        usedInAir = player.IsAirborne;
        dashUntil = Time.time + startDashTime;
        nextDashTime = dashUntil + dashCooldown;
        if (playerAnim) {
            playerAnim.Dash();
        }
        Fx.Spray(transform.position, new Vector2(-direction, 0.2f), new Color(1f, 1f, 1f, 0.7f), 10, 3f, 0.5f);
        Sfx.Play("freny_dash", transform.position);
    }

    private void Stop() {
        direction = 0;
        if (rb) {
            rb.SetVel(new Vector2(0f, rb.Vel().y));
        }
    }

    // a knockback interrupts the dash (otherwise Dash kept overriding the shove)
    public void Cancel() {
        if (IsDashing) {
            Stop();
        }
    }
}
