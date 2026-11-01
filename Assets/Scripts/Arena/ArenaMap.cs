using System.Collections.Generic;
using UnityEngine;

// The arena's standable surfaces, found once by casting rays down through the screen on the Ground
// layer: the floor and every platform become segments (top y, xMin..xMax). The autopilot plans its
// way to another platform over this map: beside which edge to stand and jump, or off which edge to
// walk, instead of probing the ground around Freny on every decision (that found edges he could
// not climb and never saw the platform two steps away).
public class ArenaMap {

    public class Platform {
        public int index;
        public float y;              // the top surface
        public float xMin, xMax;
        public Collider2D collider;  // the collider the samples came from (edges are refined on it)
        public bool tower;           // the pocket ledge up in a side tower, out of Freny's reach (MarkTowers)

        public float Width => xMax - xMin;

        public bool Contains(float x, float margin = 0f) {
            return x >= xMin + margin && x <= xMax - margin;
        }

        public override string ToString() {
            return $"P{index} (y {y:0.0}, x {xMin:0.0} a {xMax:0.0}{(tower ? ", torre" : "")})";
        }
    }

    public enum StepKind { Climb, Drop }

    // one hop of a route: where to stand and which way to go
    public struct Step {
        public Platform from, to;
        public StepKind kind;
        public float x;          // Climb: where to stand and jump; Drop: past this x the body is off the edge
        public float direction;  // Climb: towards the platform once above it; Drop: which way to walk off

        public bool Same(Step other) {
            return from == other.from && to == other.to && kind == other.kind && Mathf.Abs(x - other.x) < 0.01f;
        }

        public override string ToString() {
            string side = direction > 0f ? "esquerda" : "direita";
            return kind == StepKind.Climb
                ? $"subir de {from} para {to} pela borda {side} (x {x:0.0})"
                : $"descer de {from} para {to} pela borda {(direction > 0f ? "direita" : "esquerda")} (x {x:0.0})";
        }
    }

    public readonly List<Platform> Platforms = new List<Platform>();
    private readonly List<Step>[] steps;   // outgoing steps per platform
    private readonly LayerMask ground;
    public LayerMask Ground => ground;

    // what landing on a platform costs on top of the hop, for the ones that come and go (the Poço
    // de Ácido's crumbling platforms: AcidPitPilot.LandingCost); PositiveInfinity while it can't be
    // stood on, so no route goes there or through it. Null: every platform is always there
    public System.Func<Platform, float> LandingCost;

    private float CostOf(Platform platform) => LandingCost != null ? LandingCost(platform) : 0f;

    public bool Landable(Platform platform) => platform != null && !float.IsPositiveInfinity(CostOf(platform));

    // the hops that leave a platform (the autopilot picks one itself to get off a crumbling one)
    public IReadOnlyList<Step> StepsFrom(Platform from) {
        return from != null && from.index < steps.Length ? steps[from.index] : (IReadOnlyList<Step>)System.Array.Empty<Step>();
    }
    private readonly float halfWidth, height, jumpHeight;
    private readonly float left, right;

    private const float SampleStep = 0.2f;
    private const float EdgeClearance = 0.18f; // gap between the body and the platform's side while rising (the feet circle must not graze it)
    private const float DropOvershoot = 0.25f; // how far past the edge the body centre goes before it falls
    private const float MinRise = 0.3f;        // lower than this is a step, not a platform to climb
    private const float RiseMargin = 0.3f;     // a platform this close to the jump's apex is not climbed (no time left to move onto it)
    private const float MinHeadroom = 0.6f;    // free height above a top for it to count as a surface
    private const float SteerRoom = 0.35f;     // the feet rise this much above the platform before he moves onto it (headroom checked up to there)

    private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];

    public static ArenaMap Build(LayerMask ground, float left, float right, float bottom, float top,
                                 float bodyHalfWidth, float bodyHeight, float jumpHeight) {
        return new ArenaMap(ground, left, right, bottom, top, bodyHalfWidth, bodyHeight, jumpHeight);
    }

    // one map per arena scene, shared by the autopilot and the director (built the first time either asks)
    private static ArenaMap shared;
    private static UnityEngine.SceneManagement.Scene sharedScene;

    public static ArenaMap Shared(Player player) {
        if (!ArenaBounds.Active || player == null) {
            return null;
        }
        UnityEngine.SceneManagement.Scene scene = player.gameObject.scene;
        if (shared == null || sharedScene != scene) {
            LayerMask ground = player.whatIsGround.value != 0 ? player.whatIsGround : (LayerMask)LayerMask.GetMask("Ground");
            shared = Build(ground, ArenaBounds.Left, ArenaBounds.Right, ArenaBounds.Bottom, ArenaBounds.Top,
                player.BodyHalfWidth, player.BodyHeight, player.JumpHeight);
            sharedScene = scene;
            Debug.Log($"[Arena] Mapa da arena: {shared.Describe()}");
        }
        return shared;
    }

    public Platform Floor { get; private set; }

    private ArenaMap(LayerMask ground, float left, float right, float bottom, float top,
                     float bodyHalfWidth, float bodyHeight, float jumpHeight) {
        this.ground = ground;
        this.left = left;
        this.right = right;
        halfWidth = bodyHalfWidth;
        height = bodyHeight;
        this.jumpHeight = jumpHeight;

        FindPlatforms(bottom, top);
        steps = new List<Step>[Platforms.Count];
        for (int i = 0; i < Platforms.Count; i++) {
            steps[i] = new List<Step>();
        }
        FindSteps();
        MarkTowers();
    }

    // ---- surfaces ----

    private ContactFilter2D GroundFilter() {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(ground);
        return filter;
    }

    private struct Surface {
        public float y;
        public Collider2D collider;
    }

    private static readonly List<Surface> surfaces = new List<Surface>();
    private static readonly List<float> crossingY = new List<float>();
    private static readonly List<Collider2D> crossingCol = new List<Collider2D>();

    // Every surface someone can stand on along the vertical at x. A ray reports each collider once,
    // so it is cast again just below every hit: the tilemap composites are hollow outlines, which
    // lets the next ray find the same collider's next edge (a tower's pocket ledge under its roof,
    // the floor under a tower). Along one collider the crossings alternate going down: into the
    // solid (a top), out of it (a ceiling). A solid polygon only reports its top, since a ray that
    // starts inside it ignores it. A top with less than MinHeadroom free above it is not a surface.
    private void SurfacesAt(float x, float castTop, float bottom) {
        surfaces.Clear();
        crossingY.Clear();
        crossingCol.Clear();
        ContactFilter2D filter = GroundFilter();
        float y0 = castTop;
        for (int guard = 0; guard < 32 && y0 > bottom; guard++) {
            int n = Physics2D.Raycast(new Vector2(x, y0), Vector2.down, filter, hits, y0 - bottom + 0.5f);
            if (n == 0) {
                break;
            }
            RaycastHit2D nearest = hits[0];
            for (int i = 1; i < n; i++) {
                if (hits[i].distance < nearest.distance) {
                    nearest = hits[i];
                }
            }
            float y = nearest.point.y;
            int before = 0;
            for (int i = 0; i < crossingCol.Count; i++) {
                if (crossingCol[i] == nearest.collider) {
                    before++;
                }
            }
            float above = crossingY.Count > 0 ? crossingY[crossingY.Count - 1] : castTop;
            if (before % 2 == 0 && above - y >= MinHeadroom && y >= bottom + 0.3f) {
                surfaces.Add(new Surface { y = y, collider = nearest.collider });
            }
            crossingY.Add(y);
            crossingCol.Add(nearest.collider);
            y0 = y - 0.01f;
        }
    }

    private void FindPlatforms(float bottom, float top) {
        float castTop = top + 1f;
        List<Platform> open = new List<Platform>();
        List<bool> continued = new List<bool>();

        for (float x = left + 0.05f; x <= right - 0.05f + 0.0001f; x += SampleStep) {
            for (int k = 0; k < continued.Count; k++) {
                continued[k] = false;
            }
            SurfacesAt(x, castTop, bottom);
            foreach (Surface surface in surfaces) {
                int found = -1;
                for (int k = 0; k < open.Count; k++) {
                    if (!continued[k] && open[k].collider == surface.collider && Mathf.Abs(open[k].y - surface.y) <= 0.08f) {
                        found = k;
                        break;
                    }
                }
                if (found >= 0) {
                    open[found].xMax = x;
                    continued[found] = true;
                } else {
                    open.Add(new Platform { y = surface.y, xMin = x, xMax = x, collider = surface.collider });
                    continued.Add(true);
                }
            }
            for (int k = open.Count - 1; k >= 0; k--) {
                if (!continued[k]) {
                    Platforms.Add(open[k]);
                    open.RemoveAt(k);
                    continued.RemoveAt(k);
                }
            }
        }
        Platforms.AddRange(open);

        foreach (Platform platform in Platforms) {
            RefineEdges(platform);
        }
        MergeNeighbours();
        // too narrow to stand on
        Platforms.RemoveAll(p => p.Width < halfWidth * 2f + 0.1f);
        Platforms.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.xMin.CompareTo(b.xMin));
        for (int i = 0; i < Platforms.Count; i++) {
            Platforms[i].index = i;
        }

        // the floor is the widest surface (the lowest one when two are as wide). It used to be the
        // widest of the LOWEST ones, but the floor tilemap has short tops under it (y 5 under the
        // y 6 floor in ZE Arena), so a scrap under the floor was taken for it: nothing was reachable
        // from there and every walker came out of the old scene spawners.
        foreach (Platform platform in Platforms) {
            if (Floor == null || platform.Width > Floor.Width + 0.5f || (platform.Width > Floor.Width - 0.5f && platform.y < Floor.y)) {
                Floor = platform;
            }
        }
    }

    private const float TowerBand = 5f; // how far from a side edge a tower's pocket can be

    // the towers' pockets: on each side, the highest ledge near that edge that Freny can't get to
    // (and the ones level with it). Touching the screen edge is not required: the right tower's
    // pocket in ZE Arena stops a bit short of it, so only the left tower used to be found.
    private void MarkTowers() {
        if (Floor == null) {
            return;
        }
        int[] hops = HopsFrom(Floor);
        for (int side = 0; side < 2; side++) {
            Platform highest = null;
            foreach (Platform platform in Platforms) {
                if (IsTowerCandidate(platform, side, hops) && (highest == null || platform.y > highest.y)) {
                    highest = platform;
                }
            }
            if (highest == null) {
                continue;
            }
            foreach (Platform platform in Platforms) {
                if (IsTowerCandidate(platform, side, hops) && platform.y >= highest.y - 0.3f) {
                    platform.tower = true;
                }
            }
        }
    }

    private bool IsTowerCandidate(Platform platform, int side, int[] hops) {
        float center = (platform.xMin + platform.xMax) * 0.5f;
        bool nearEdge = side == 0 ? center <= left + TowerBand : center >= right - TowerBand;
        return nearEdge && platform != Floor && hops[platform.index] == int.MaxValue && platform.y >= Floor.y + 3f;
    }

    // the samples are SampleStep apart: find the real edges between the last hit and the first miss
    private void RefineEdges(Platform platform) {
        float lo = Mathf.Max(left, platform.xMin - SampleStep), hi = platform.xMin; // lo misses (or is the wall), hi hits
        if (lo < hi && !HitsSurface(lo, platform)) {
            for (int i = 0; i < 8; i++) {
                float mid = (lo + hi) * 0.5f;
                if (HitsSurface(mid, platform)) hi = mid; else lo = mid;
            }
            platform.xMin = (lo + hi) * 0.5f;
        } else {
            platform.xMin = lo;
        }
        lo = platform.xMax;
        hi = Mathf.Min(right, platform.xMax + SampleStep); // lo hits, hi misses (or is the wall)
        if (lo < hi && !HitsSurface(hi, platform)) {
            for (int i = 0; i < 8; i++) {
                float mid = (lo + hi) * 0.5f;
                if (HitsSurface(mid, platform)) lo = mid; else hi = mid;
            }
            platform.xMax = (lo + hi) * 0.5f;
        } else {
            platform.xMax = hi;
        }
    }

    private bool HitsSurface(float x, Platform platform) {
        int n = Physics2D.Raycast(new Vector2(x, platform.y + 0.3f), Vector2.down, GroundFilter(), hits, 0.6f);
        for (int i = 0; i < n; i++) {
            if (hits[i].collider == platform.collider && Mathf.Abs(hits[i].point.y - platform.y) <= 0.08f) {
                return true;
            }
        }
        return false;
    }

    // two colliders meeting at the same height (the floor's tilemaps) are one surface
    // (both ways: tops a few millimetres apart sort out of x order, so "b starts before a ends" alone
    // joined platforms with a hole between them, and Freny dashed into the hole over and over)
    private void MergeNeighbours() {
        bool merged = true;
        while (merged) {
            merged = false;
            for (int i = 0; i < Platforms.Count && !merged; i++) {
                for (int j = i + 1; j < Platforms.Count; j++) {
                    Platform a = Platforms[i], b = Platforms[j];
                    if (Mathf.Abs(a.y - b.y) <= 0.08f && b.xMin <= a.xMax + 0.1f && a.xMin <= b.xMax + 0.1f) {
                        a.xMax = Mathf.Max(a.xMax, b.xMax);
                        a.xMin = Mathf.Min(a.xMin, b.xMin);
                        a.y = Mathf.Max(a.y, b.y);
                        Platforms.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }
        }
    }

    // ---- routes ----

    private void FindSteps() {
        foreach (Platform from in Platforms) {
            foreach (Platform to in Platforms) {
                if (to != from) {
                    AddClimbs(from, to);
                }
            }
            AddDrops(from);
        }
    }

    // stand beside an edge of `to` on `from`, jump straight up, move onto it once above it
    private void AddClimbs(Platform from, Platform to) {
        float rise = to.y - from.y;
        if (rise < MinRise || rise > jumpHeight - RiseMargin) {
            return;
        }
        for (int side = 0; side < 2; side++) {
            float direction = side == 0 ? 1f : -1f; // 0: stand left of the platform, move right onto it
            float standX = side == 0 ? to.xMin - (halfWidth + EdgeClearance) : to.xMax + (halfWidth + EdgeClearance);
            if (!from.Contains(standX, 0.1f) || !Inside(standX, halfWidth)) {
                continue;
            }
            if (!Headroom(standX, from.y, rise + SteerRoom)) {
                continue;
            }
            steps[from.index].Add(new Step { from = from, to = to, kind = StepKind.Climb, x = standX, direction = direction });
        }
    }

    // walk off an edge of `from` and fall straight down onto whatever is below
    private void AddDrops(Platform from) {
        for (int side = 0; side < 2; side++) {
            float direction = side == 0 ? -1f : 1f;
            float offX = side == 0 ? from.xMin - (halfWidth + DropOvershoot) : from.xMax + (halfWidth + DropOvershoot);
            if (!Inside(offX, 0.1f)) {
                continue; // this edge is the arena wall
            }
            Platform landing = null;
            foreach (Platform candidate in Platforms) {
                if (candidate == from || candidate.y >= from.y - 0.2f) {
                    continue;
                }
                // anything the body's width could touch on the way down counts as the landing
                if (candidate.xMax < offX - halfWidth + 0.05f || candidate.xMin > offX + halfWidth - 0.05f) {
                    continue;
                }
                if (landing == null || candidate.y > landing.y) {
                    landing = candidate;
                }
            }
            if (landing == null || !landing.Contains(offX, 0.2f)) {
                continue; // nothing below, or he would land on an edge
            }
            steps[from.index].Add(new Step { from = from, to = landing, kind = StepKind.Drop, x = offX, direction = direction });
        }
    }

    // nothing above the body standing at x up to `rise` higher (the platform he climbs is beside, not above)
    private bool Headroom(float x, float floorY, float rise) {
        Vector2 center = new Vector2(x, floorY + height * 0.5f + 0.03f);
        Vector2 size = new Vector2(halfWidth * 1.8f, height * 0.9f);
        return Physics2D.BoxCast(center, size, 0f, Vector2.up, rise, ground).collider == null;
    }

    private bool Inside(float x, float margin) {
        return x >= left + margin && x <= right - margin;
    }

    // the platform under a point: the highest one whose top is at or below it (within maxDrop)
    public Platform PlatformUnder(Vector2 point, float maxDrop) {
        Platform best = null;
        foreach (Platform platform in Platforms) {
            if (platform.y > point.y + 0.15f || point.y - platform.y > maxDrop || !platform.Contains(point.x, -0.05f)) {
                continue;
            }
            if (platform.collider != null && !platform.collider.enabled) {
                continue;   // a crumbled platform of the Poço (CrumbleBlock): not there until it grows back
            }
            if (best == null || platform.y > best.y) {
                best = platform;
            }
        }
        return best;
    }

    // hops from `from` to every platform (int.MaxValue = no route)
    public int[] HopsFrom(Platform from) {
        int[] hops = new int[Platforms.Count];
        for (int i = 0; i < hops.Length; i++) {
            hops[i] = int.MaxValue;
        }
        if (from == null) {
            return hops;
        }
        Queue<Platform> queue = new Queue<Platform>();
        hops[from.index] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0) {
            Platform current = queue.Dequeue();
            foreach (Step step in steps[current.index]) {
                if (hops[step.to.index] == int.MaxValue && Landable(step.to)) {
                    hops[step.to.index] = hops[current.index] + 1;
                    queue.Enqueue(step.to);
                }
            }
        }
        return hops;
    }

    // the first hop of the shortest route from `from` to `to`, fewest hops first and the least
    // walking among those (from x `fromX`); false when there is no route or he is already there
    public bool FirstStep(Platform from, Platform to, float fromX, out Step first) {
        first = default;
        if (from == null || to == null || from == to) {
            return false;
        }
        int n = Platforms.Count;
        float[] cost = new float[n];
        float[] arriveX = new float[n];
        Step[] via = new Step[n];
        bool[] done = new bool[n];
        for (int i = 0; i < n; i++) {
            cost[i] = float.MaxValue;
        }
        cost[from.index] = 0f;
        arriveX[from.index] = fromX;

        for (int round = 0; round < n; round++) {
            int best = -1;
            for (int i = 0; i < n; i++) {
                if (!done[i] && cost[i] < float.MaxValue && (best < 0 || cost[i] < cost[best])) {
                    best = i;
                }
            }
            if (best < 0 || best == to.index) {
                break;
            }
            done[best] = true;
            foreach (Step step in steps[best]) {
                // a hop costs 1; walking to its edge costs a little, so the nearer edge wins ties
                float stepCost = 1f + Mathf.Abs(step.x - arriveX[best]) / 40f;
                if (step.kind == StepKind.Drop && step.from.y - step.to.y > Player.SafeFallHeight) {
                    stepCost += 6f; // a drop that costs a heart: only when there is no other way down
                }
                float landing = CostOf(step.to);
                if (float.IsPositiveInfinity(landing)) {
                    continue;       // crumbled, or about to
                }
                stepCost += landing;
                float total = cost[best] + stepCost;
                if (total < cost[step.to.index]) {
                    cost[step.to.index] = total;
                    via[step.to.index] = step;
                    arriveX[step.to.index] = step.kind == StepKind.Climb ? step.x + step.direction * (halfWidth + EdgeClearance + 0.4f) : step.x;
                }
            }
        }
        if (cost[to.index] == float.MaxValue) {
            return false;
        }
        // walk the chain back to the hop that leaves `from`
        Step hop = via[to.index];
        while (hop.from != from) {
            hop = via[hop.from.index];
        }
        first = hop;
        return true;
    }

    // free height above the surface at x (up to `limit`)
    public float HeadroomAt(float x, float y, float limit = 6f) {
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, y + 0.05f), Vector2.up, limit, ground);
        return hit.collider != null ? hit.point.y - y : limit;
    }

    // platforms Freny can get to from `from` (the director only puts walkers and chests there)
    public List<Platform> ReachableFrom(Platform from) {
        List<Platform> reachable = new List<Platform>();
        int[] hopCounts = HopsFrom(from);
        foreach (Platform platform in Platforms) {
            if (hopCounts[platform.index] != int.MaxValue) {
                reachable.Add(platform);
            }
        }
        return reachable;
    }

    public string Describe() {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append(Platforms.Count).Append(" plataformas:");
        foreach (Platform platform in Platforms) {
            text.Append(' ').Append(platform.ToString()).Append(" com ").Append(steps[platform.index].Count).Append(" saídas;");
        }
        return text.ToString();
    }
}
