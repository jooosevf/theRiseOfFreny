using UnityEngine;

// The autopilot's way off the ground floor when the Ciclope floods it. Walking to the spot the
// map climbs from, slowed by the acid, took him 1.6 to 3.4 s and 1 to 3 hits (José's test,
// 2026-11-01). But the arenas' platforms are one-way (a PlatformEffector2D): he can jump up
// through one from right under it, or at it from a little to the side, so the quickest way up is
// a jump at once, at the nearest one-way platform within a jump's height. It is driven every
// frame from the start of the vomit (Cyclops.FloodComing), not at the autopilot's decisions,
// which come too far apart. With no one-way platform in reach, ArenaAutopilot.FleeFlood climbs
// over the map as before.
public class FloodEscape {
    private const float RiseMargin = 0.3f;       // as ArenaMap: a top this close to the jump's apex is too high
    private const float MinRise = 0.3f;
    private const float LandInset = 0.15f;       // lands this far inside the edge (beyond half his body)
    private const float AirReachShare = 0.8f;    // of what the jump could carry him sideways, to be safe
    private const float MaxLook = 10f;           // farther platforms are left to the map
    private const float OnFloor = 0.4f;          // feet this close to the floor's top: he is down there

    private static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    public ArenaMap.Platform Target { get; private set; }
    public string Action { get; private set; } = "";
    private bool hopping;          // in the air on the way up
    private float hopStart;
    private bool airJumped;

    public static bool Danger => Cyclops.FloodComing || AcidPuddle.Flooding;

    // one frame of the escape; false when there is nothing to escape (or he is not on the floor)
    public bool Frame(Player player, ArenaMap map, float feet, ref float nextJumpTime, float jumpCooldown, out PlayerInputFrame frame) {
        frame = default;
        if (!Danger || player == null || map == null || map.Floor == null) {
            Reset();
            return false;
        }
        float x = player.transform.position.x;
        bool grounded = player.IsGrounded;

        if (hopping) {
            bool landed = grounded && Time.time - hopStart > 0.15f && Mathf.Abs(player.VerticalSpeed) < 0.5f;
            if (!landed && Target != null && Time.time - hopStart < 2f) {
                Steer(player, x, feet, ref frame);
                return true;
            }
            hopping = false;
            if (feet > map.Floor.y + OnFloor) {
                Reset();   // up: ArenaAutopilot.FleeFlood waits there for the flood to drain
                return false;
            }
        }
        if (!grounded || Mathf.Abs(feet - map.Floor.y) > OnFloor) {
            Target = null;
            return false;
        }

        Target = Pick(player, map, x, out float aimX, out float distance, out float reach);
        if (Target == null) {
            return false;
        }
        float toward = Mathf.Sign(aimX - x);
        if (distance <= reach) {
            // jump now: straight up when he is under it, else at it
            frame.jumpPressed = true;
            frame.jumpHeld = true;
            frame.move = distance > 0.05f ? toward : 0f;
            frame.runHeld = frame.move != 0f;
            nextJumpTime = Time.time + jumpCooldown;
            hopping = true;
            airJumped = false;
            hopStart = Time.time;
            Action = $"Pulando para fora da enchente em {Target}";
        } else {
            frame.move = toward;
            frame.runHeld = true;
            Action = $"Correndo para pular em {Target}";
        }
        return true;
    }

    private void Reset() {
        Target = null;
        hopping = false;
    }

    // in the air: over the platform before coming down, and the double jump if he falls short
    private void Steer(Player player, float x, float feet, ref PlayerInputFrame frame) {
        Range(player, Target, out float lo, out float hi);
        float aim = Mathf.Clamp(x, lo, hi);
        frame.move = Mathf.Abs(aim - x) > 0.05f ? Mathf.Sign(aim - x) : 0f;
        frame.runHeld = frame.move != 0f;
        frame.jumpHeld = true;
        if (!airJumped && player.VerticalSpeed < 0f && feet < Target.y - 0.1f && player.CanAirJump) {
            frame.jumpPressed = true;
            airJumped = true;
        }
        Action = $"No ar, saindo da enchente para {Target}";
    }

    // where on a platform he can land, body and all
    private static void Range(Player player, ArenaMap.Platform platform, out float lo, out float hi) {
        float inset = player.BodyHalfWidth + LandInset;
        lo = platform.xMin + inset;
        hi = platform.xMax - inset;
    }

    // the one-way platform he gets onto soonest: the jump's flight time, plus the run to where a
    // jump from there carries him onto it
    private static ArenaMap.Platform Pick(Player player, ArenaMap map, float x, out float aimX, out float distance, out float reach) {
        aimX = x;
        distance = reach = 0f;
        float jump = player.JumpHeight;
        float rise0 = Mathf.Sqrt(2f * player.RiseGravity * jump);    // take-off speed
        float apexTime = rise0 / player.RiseGravity;
        float speed = Mathf.Max(0.5f, player.GroundSpeed);           // now: slowed by the acid, maybe sprinting
        float runSpeed = Mathf.Max(speed, player.MaxSpeed * 0.6f);   // on the way to the jump spot
        ArenaMap.Platform best = null;
        float bestTime = float.MaxValue;
        foreach (ArenaMap.Platform platform in map.Platforms) {
            if (platform == map.Floor || platform.tower || (map.LandingCost != null && map.LandingCost(platform) > 0f)) {
                continue;   // (a crumbling one of the Poço would drop him back into it)
            }
            float rise = platform.y - map.Floor.y;
            if (rise < MinRise || rise > jump - RiseMargin) {
                continue;
            }
            Range(player, platform, out float lo, out float hi);
            if (lo > hi) {
                continue;
            }
            float aim = Mathf.Clamp(x, lo, hi);
            float gap = Mathf.Abs(aim - x);
            if (gap > MaxLook || !OneWayAt(map, aim, platform.y) || !ClearAbove(map, x, map.Floor.y, platform.y)
                || !ClearAbove(map, aim, map.Floor.y, platform.y)) {
                continue;
            }
            // from the take-off until his feet come back down to its top
            float above = Mathf.Max(0.05f, jump - rise - 0.15f);
            float flight = apexTime + Mathf.Sqrt(2f * above / player.FallGravity);
            float carry = speed * flight * AirReachShare;
            float time = flight + Mathf.Max(0f, gap - carry) / runSpeed;
            if (time < bestTime) {
                bestTime = time;
                best = platform;
                aimX = aim;
                distance = gap;
                reach = carry;
            }
        }
        return best;
    }

    // the top at x is a one-way platform's (jumped through from below)
    private static bool OneWayAt(ArenaMap map, float x, float top) {
        int n = Physics2D.Raycast(new Vector2(x, top + 0.3f), Vector2.down, Filter(map), hits, 0.6f);
        for (int i = 0; i < n; i++) {
            if (Mathf.Abs(hits[i].point.y - top) <= 0.1f && OneWay(hits[i].collider)) {
                return true;
            }
        }
        return false;
    }

    // nothing solid (only one-way platforms) between the floor and that top along x
    private static bool ClearAbove(ArenaMap map, float x, float floorY, float top) {
        float from = floorY + 0.3f;
        int n = Physics2D.Raycast(new Vector2(x, from), Vector2.up, Filter(map), hits, top - from + 0.2f);
        for (int i = 0; i < n; i++) {
            if (!OneWay(hits[i].collider)) {
                return false;
            }
        }
        return true;
    }

    private static bool OneWay(Collider2D collider) {
        if (collider == null || !collider.usedByEffector) {
            return false;
        }
        PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>();
        return effector != null && effector.useOneWay;
    }

    private static ContactFilter2D Filter(ArenaMap map) {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(map.Ground);
        return filter;
    }
}
