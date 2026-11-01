using UnityEngine;

// What the autopilot (ArenaAutopilot) knows about the Poço de Ácido, kept out of its file: hop
// over the acid pools instead of stopping at them (the old puddles dry up, these never do), and
// get out from under what comes down from above: a Tentáculo's spit, a spike about to fall, a
// Rastejante about to drop. No-ops in the classic arena.
public static class AcidPitPilot {
    private const float HopFrom = 0.9f;       // jumps this far before a pool's edge
    private const float SpitClear = 1.4f;     // keeps this far from where a spit comes down
    private const float SpitWarning = 2.5f;   // ... from this long before it lands
    private const float SpikeClear = 1f;
    private const float CrawlerClear = 1.1f;

    private static float hopDirection;        // != 0 while a hop over a pool is in the air
    private static float hopStarted;

    // every frame he walks on his own (not during a climb or a drop): a pool ahead is jumped, unless
    // the enemy he is after stands in it (a Tentáculo): then he stops at the edge and fights from there
    public static void Hop(Player player, ref PlayerInputFrame frame, Enemy target, ref float nextJumpTime, float jumpCooldown) {
        AcidPitArena pit = AcidPitArena.Instance;
        if (pit == null || player == null) {
            hopDirection = 0f;
            return;
        }
        if (hopDirection != 0f) {
            // in the air over the pool: keep going the same way until he lands
            if (player.IsGrounded && Time.time - hopStarted > 0.2f) {
                hopDirection = 0f;
            } else {
                frame.move = hopDirection;
                frame.runHeld = true;
                return;
            }
        }
        if (frame.move == 0f || !player.IsGrounded) {
            return;
        }
        float me = player.transform.position.x;
        float feet = player.Feet.y;
        float dir = Mathf.Sign(frame.move);
        foreach (AcidPuddle pool in pit.Pools) {
            if (pool == null || Mathf.Abs(feet - pool.transform.position.y) > 0.6f) {
                continue;
            }
            float center = pool.transform.position.x;
            float half = pool.Width / 2f;
            float toEdge = dir > 0f ? center - half - me : me - (center + half);
            if (toEdge < 0f || toEdge > HopFrom) {
                continue;
            }
            if (target != null && !target.IsDead && Mathf.Abs(target.Feet.x - center) < half + 0.3f && Mathf.Abs(target.Feet.y - feet) < 1f) {
                frame.move = 0f;
                frame.runHeld = false;
                return;
            }
            if (Time.time < nextJumpTime) {
                frame.move = 0f;   // the jump is not ready yet: wait at the edge
                return;
            }
            frame.jumpPressed = true;
            frame.jumpHeld = true;
            frame.runHeld = true;
            nextJumpTime = Time.time + jumpCooldown;
            hopDirection = dir;
            hopStarted = Time.time;
            return;
        }
    }

    // at each decision, before fighting: out from under whatever is coming down on him. True (with
    // the frame and what he is doing filled in) when he has to move
    public static bool Dodge(Player player, ref PlayerInputFrame frame, out string what) {
        what = null;
        if (AcidPitArena.Instance == null || player == null) {
            return false;
        }
        float me = player.transform.position.x;
        float headY = player.BodyCenter.y;
        foreach (Tentacle.Spit spit in Tentacle.Incoming) {
            float left = spit.lands - Time.time;
            if (left > -0.2f && left < SpitWarning && Mathf.Abs(me - spit.x) < SpitClear) {
                Away(player, ref frame, me - spit.x);
                what = "Fugindo do cuspe do Tentáculo";
                return true;
            }
        }
        foreach (FallingSpike spike in FallingSpike.Active) {
            if (spike != null && spike.Dangerous && Mathf.Abs(me - spike.X) < SpikeClear && spike.Tip > headY - 0.5f) {
                Away(player, ref frame, me - spike.X);
                what = "Saindo de baixo do espinho";
                return true;
            }
        }
        foreach (Crawler crawler in Crawler.All) {
            if (crawler != null && !crawler.IsDead && crawler.Dropping && Mathf.Abs(me - crawler.transform.position.x) < CrawlerClear
                && crawler.transform.position.y > headY) {
                Away(player, ref frame, me - crawler.transform.position.x);
                what = "Saindo de baixo do Rastejante";
                return true;
            }
        }
        return false;
    }

    private static void Away(Player player, ref PlayerInputFrame frame, float fromThreat) {
        float away = Mathf.Abs(fromThreat) < 0.05f ? (player.FacingRight ? 1f : -1f) : Mathf.Sign(fromThreat);
        // not off the screen's edge: the other way then (he would wrap, but the threat may too)
        if (ArenaBounds.Active && !ArenaBounds.Inside(player.transform.position.x + away * 1.5f, 0.5f)) {
            away = -away;
        }
        frame.move = away;
        frame.runHeld = player.RunReady;
    }
}
