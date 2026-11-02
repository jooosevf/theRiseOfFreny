using UnityEngine;

// Freny in a pool (Liquid). Water: he floats up to the surface and bobs there, moves slower, and
// a jump pushes him up and out. Acid: he sinks slowly, can barely move and loses a heart every
// second until he dies (a slow death instead of an instant one). Runs after Player so its
// vertical speed wins in the same physics step.
[DefaultExecutionOrder(110)]
public class LiquidSwimmer : MonoBehaviour {

    [SerializeField] private float waterSideFactor = 0.65f;
    [SerializeField] private float floatSpeed = 2f;       // rising towards the surface
    [SerializeField] private float swimJumpSpeed = 7.5f;
    [SerializeField] private float acidSideFactor = 0.25f;
    [SerializeField] private float acidSinkSpeed = 0.45f;
    [SerializeField] private float acidHurtInterval = 0.8f; // with the hurt blink, about a heart a second

    private Player player;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Liquid pool;
    private float savedGravity;
    private float nextAcidHurt;
    private bool jumpQueued;


    private void Start() {
        player = GetComponent<Player>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void Update() {
        if (player == null || body == null || bodyCollider == null || player.IsDead) {
            Leave();
            return;
        }
        Bounds bounds = bodyCollider.bounds;
        // in a pool once the middle of the body is in it (acid: once the feet are)
        Liquid here = Liquid.At(bounds.center);
        Liquid underFeet = Liquid.At(new Vector2(bounds.center.x, bounds.min.y + 0.1f));
        if (here == null && underFeet != null && underFeet.IsAcid) {
            here = underFeet;
        }
        if (here != pool) {
            Leave();
            if (here != null) {
                Enter(here);
            }
        }
        if (pool == null) {
            return;
        }
        if (pool.IsAcid) {
            if (Time.time >= nextAcidHurt) {
                nextAcidHurt = Time.time + acidHurtInterval;
                player.TryDamage(1);
            }
        } else if (!InputBindings.GameplayBlocked && InputBindings.GetDown(GameAction.Jump)) {
            jumpQueued = true;
        }
    }

    private void FixedUpdate() {
        if (pool == null || body == null) {
            return;
        }
        Vector2 velocity = body.Vel();
        if (pool.IsAcid) {
            velocity.x *= acidSideFactor;
            velocity.y = -acidSinkSpeed;
        } else {
            velocity.x *= waterSideFactor;
            float surface = pool.SurfaceAt(body.position.x);
            float middle = bodyCollider.bounds.center.y;
            if (jumpQueued) {
                velocity.y = swimJumpSpeed;
            } else if (middle < surface - 0.15f) {
                velocity.y = Mathf.MoveTowards(velocity.y, floatSpeed, 20f * Time.fixedDeltaTime);
            } else {
                velocity.y = Mathf.MoveTowards(velocity.y, 0f, 12f * Time.fixedDeltaTime); // bobbing at the top
            }
            jumpQueued = false;
        }
        body.SetVel(velocity);
    }

    private void Enter(Liquid liquid) {
        pool = liquid;
        savedGravity = body.gravityScale;
        body.gravityScale = 0f;
        Vector2 velocity = body.Vel();
        body.SetVel(new Vector2(velocity.x, Mathf.Max(velocity.y, -2f))); // the splash stops the fall
        Fx.Burst(new Vector3(body.position.x, liquid.SurfaceAt(body.position.x), 0f),
            liquid.IsAcid ? new Color(0.5f, 0.9f, 0.2f) : new Color(0.4f, 0.6f, 1f), 10, 2.5f, 0.06f, 0.5f);
        // the first acid burn comes a moment after the splash
        nextAcidHurt = Time.time + 0.5f;
    }

    private void Leave() {
        if (pool == null) {
            return;
        }
        pool = null;
        jumpQueued = false;
        if (body != null) {
            body.gravityScale = savedGravity;
        }
    }

    private void OnDisable() {
        Leave();
    }
}
