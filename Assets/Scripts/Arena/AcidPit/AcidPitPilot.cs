using System.Collections.Generic;
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

    // ---- the platforms that come and go (CrumbleBlock, MovingBlock) on the autopilot's map ----

    private const float CrumbleCost = 4f;     // a solid crumbling platform: only when no other way is near

    private static ArenaMap tracked;
    private static readonly Dictionary<ArenaMap.Platform, Slide> slides = new Dictionary<ArenaMap.Platform, Slide>();

    private struct Slide {
        public MovingBlock block;
        public float homeMin, homeMax;
    }

    // ArenaMap.LandingCost in the Poço (AcidPitArena sets it): a crumbled platform, or one already
    // cracking, can't be landed on, so no route goes there or through it; a solid one costs a few
    // hops (it gives way 0.7 s after he steps on it, too soon to walk on to the next hop)
    public static float LandingCost(ArenaMap.Platform platform) {
        CrumbleBlock block = CrumbleOf(platform);
        if (block == null) {
            return 0f;
        }
        return !block.Solid || block.Cracking ? float.PositiveInfinity : CrumbleCost;
    }

    // he stands on a crumbling platform that is giving way: off it now
    public static bool Cracking(ArenaMap.Platform platform) {
        CrumbleBlock block = CrumbleOf(platform);
        return block != null && block.Cracking;
    }

    private static CrumbleBlock CrumbleOf(ArenaMap.Platform platform) {
        if (platform == null || platform.collider == null) {
            return null;
        }
        foreach (CrumbleBlock block in CrumbleBlock.Active) {
            if (block != null && block.gameObject == platform.collider.gameObject) {
                return block;
            }
        }
        return null;
    }

    // every frame (AcidPitArena): the sliding platforms' edges on the map follow them, so where he
    // stands, where he lands and the edge he jumps for are where the platform is now
    public static void Track(ArenaMap map) {
        if (map == null) {
            return;
        }
        if (tracked != map) {
            tracked = map;
            slides.Clear();
        }
        foreach (MovingBlock block in MovingBlock.Active) {
            if (block == null) {
                continue;
            }
            ArenaMap.Platform platform = null;
            foreach (KeyValuePair<ArenaMap.Platform, Slide> known in slides) {
                if (known.Value.block == block) {
                    platform = known.Key;
                    break;
                }
            }
            if (platform == null) {
                platform = map.Platforms.Find(p => p.collider != null && p.collider.gameObject == block.gameObject);
                if (platform == null) {
                    continue;
                }
                // the map was built before it started sliding: its extents are the home ones
                slides[platform] = new Slide { block = block, homeMin = platform.xMin, homeMax = platform.xMax };
                platform.sliding = true;
            }
            Slide slide = slides[platform];
            platform.xMin = slide.homeMin + block.Offset;
            platform.xMax = slide.homeMax + block.Offset;
        }
    }

    // how far a sliding platform is from where the map's hops were planned (0 for the others): the
    // spot beside it he jumps from moves with it
    public static float Shift(ArenaMap.Platform platform) {
        return platform != null && slides.TryGetValue(platform, out Slide slide) ? platform.xMin - slide.homeMin : 0f;
    }
}
