using UnityEngine;

// What the Ciclope throws up: a flat green puddle on the floor that burns Freny's feet
// (1 heart, then his usual blink) until it dries up.
public class AcidPuddle : MonoBehaviour {
    private const int Damage = 1;
    private const float SlowFactor = 0.45f;
    private static readonly Color Green = new Color(0.45f, 0.85f, 0.2f, 0.85f);   // green (José, 2026-10-29)
    // the Ciclope's own vomit is purple, like in its Vomit sheet (José, 2026-10-30); the green one
    // is kept for an enemy of phase 2
    public static readonly Color Purple = new Color(0.31f, 0.25f, 0.62f, 0.9f);
    private Color colour = Green;
    private static Sprite pixel;
    private static Player freny;

    private float width;
    private float until;
    private float life;
    private SpriteRenderer renderer2d;
    private float nextBubble;

    // the puddles on the floor now (the autopilot steps out of them)
    public static readonly System.Collections.Generic.List<AcidPuddle> Active = new System.Collections.Generic.List<AcidPuddle>();
    public float Width => width;

    private void OnEnable() {
        Active.Add(this);
    }

    private void OnDestroy() {
        if (floodMesh != null) {
            Destroy(floodMesh);
        }
    }

    private void OnDisable() {
        Active.Remove(this);
        Flooding = Active.Exists(p => p != null && p.flood);
    }

    // the Ciclope's flood (José, 2026-10-30): purple vomit runs out both ways from its mouth over
    // the whole ground floor (xMin..xMax) and stays `seconds`. It burns Freny and every other
    // enemy standing in it, but not the Ciclopes
    private const float FloodDepth = 0.45f;
    private const float FloodRunSpeed = 14f;   // units per second each way
    private const float EnemyBurnGap = 1f;
    public static bool Flooding { get; private set; }
    public bool IsFlood => flood;
    private bool flood;
    private float floodLeft, floodRight, floodMin, floodMax, floodY;
    private float nextEnemyBurn;
    private Mesh floodMesh;
    private LineRenderer crest;
    private float level = 1f;
    private const float WaveStep = 0.2f;        // one wave point every this many units
    private const float WaveHeight = 0.07f;
    private const float WaveLength = 1.6f;
    private const float WaveSpeed = 2.2f;       // how fast the ripples run along
    private static Material floodMaterial;

    public static AcidPuddle Flood(float fromX, float floorY, float xMin, float xMax, float seconds) {
        GameObject go = new GameObject("Vomit Flood");
        AcidPuddle puddle = go.AddComponent<AcidPuddle>();
        puddle.flood = true;
        puddle.colour = Purple;
        // the ground floor platform can stop short of the screen edges (a different collider
        // further on): the flood keeps running while there is floor at the same height
        puddle.floodMin = Reach(xMin, floorY, -1f);
        puddle.floodMax = Reach(xMax, floorY, 1f);
        puddle.floodLeft = puddle.floodRight = Mathf.Clamp(fromX, puddle.floodMin, puddle.floodMax);
        puddle.life = seconds;
        puddle.until = Time.time + seconds;
        puddle.floodY = floorY;
        go.transform.position = new Vector3(fromX, floorY, 0f);

        // the body: a strip of quads whose top follows the waves (vertex colours: darker at the bottom)
        GameObject body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        puddle.floodMesh = new Mesh();
        puddle.floodMesh.MarkDynamic();
        body.AddComponent<MeshFilter>().sharedMesh = puddle.floodMesh;
        MeshRenderer meshRenderer = body.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = FloodMaterial();
        meshRenderer.sortingOrder = 44;
        // the bright line along the top of the waves
        GameObject top = new GameObject("Crest");
        top.transform.SetParent(go.transform, false);
        puddle.crest = top.AddComponent<LineRenderer>();
        puddle.crest.useWorldSpace = true;
        puddle.crest.sharedMaterial = FloodMaterial();
        puddle.crest.sortingOrder = 45;
        puddle.crest.widthMultiplier = 0.07f;
        puddle.crest.numCapVertices = 2;
        puddle.FloodShape();
        Flooding = true;
        Fx.Burst(new Vector3(fromX, floorY + 0.2f, 0f), Purple, 30, 3f, 0.1f, 0.5f);
        return puddle;
    }

    // from x, outwards while there is floor at floorY and no wall in the way
    private static float Reach(float x, float floorY, float dir) {
        int ground = LayerMask.GetMask("Ground");
        float limit = ArenaBounds.Active ? (dir > 0f ? ArenaBounds.Right : ArenaBounds.Left) : x;
        for (float next = x + dir * 0.25f; dir > 0f ? next <= limit : next >= limit; next += dir * 0.25f) {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(next, floorY + 0.4f), Vector2.down, 0.8f, ground);
            if (hit.collider == null || Mathf.Abs(hit.point.y - floorY) > 0.2f || Physics2D.OverlapPoint(new Vector2(next, floorY + 0.3f), ground) != null) {
                break;
            }
            x = next;
        }
        return x;
    }

    private float WaveY(float x) {
        float t = Time.time * WaveSpeed;
        float k = 2f * Mathf.PI / WaveLength;
        float wave = Mathf.Sin(x * k - t) * WaveHeight + Mathf.Sin(x * k * 0.47f + t * 0.6f) * WaveHeight * 0.6f;
        // a wave front where it is still running out
        return floodY + FloodDepth * level + wave * level;
    }

    // the liquid between floodLeft and floodRight, `level` (0..1) of its full depth, its bottom on the floor
    private void FloodShape() {
        width = Mathf.Max(0.01f, floodRight - floodLeft);
        transform.position = new Vector3((floodLeft + floodRight) / 2f, floodY, 0f);
        int columns = Mathf.Max(2, Mathf.CeilToInt(width / WaveStep) + 1);
        Vector3[] vertices = new Vector3[columns * 2];
        Color[] colors = new Color[columns * 2];
        int[] triangles = new int[(columns - 1) * 6];
        Vector3 origin = transform.position;
        Color dark = new Color(colour.r * 0.6f, colour.g * 0.5f, colour.b * 0.75f, alpha);
        Color light = new Color(colour.r, colour.g, colour.b, alpha);
        Vector3[] line = new Vector3[columns];
        for (int i = 0; i < columns; i++) {
            float x = Mathf.Lerp(floodLeft, floodRight, i / (float)(columns - 1));
            float y = WaveY(x);
            vertices[i * 2] = new Vector3(x, floodY - 0.02f, 0f) - origin;
            vertices[i * 2 + 1] = new Vector3(x, y, 0f) - origin;
            colors[i * 2] = dark;
            colors[i * 2 + 1] = light;
            line[i] = new Vector3(x, y, 0f);
            if (i < columns - 1) {
                int v = i * 2, k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 1;
                triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 1;
                triangles[k + 4] = v + 3;
                triangles[k + 5] = v + 2;
            }
        }
        floodMesh.Clear();
        floodMesh.vertices = vertices;
        floodMesh.colors = colors;
        floodMesh.triangles = triangles;
        floodMesh.RecalculateBounds();
        crest.positionCount = columns;
        crest.SetPositions(line);
        crest.startColor = crest.endColor = new Color(0.66f, 0.56f, 0.95f, alpha);
    }

    private float alpha = 0.85f;

    private static Material FloodMaterial() {
        if (floodMaterial == null) {
            Material unlit = ItemPickup.UnlitMaterial;
            floodMaterial = unlit != null ? new Material(unlit) : new Material(Shader.Find("Sprites/Default"));
            floodMaterial.mainTexture = Texture2D.whiteTexture;
        }
        return floodMaterial;
    }

    private void UpdateFlood() {
        float left = until - Time.time;
        if (left <= 0f) {
            Destroy(gameObject);
            return;
        }
        float floorY = floodY;
        float step = FloodRunSpeed * Time.deltaTime;
        floodLeft = Mathf.Max(floodMin, floodLeft - step);
        floodRight = Mathf.Min(floodMax, floodRight + step);
        level = Mathf.Clamp01(left / 0.6f);   // it drains in the last moments
        alpha = 0.85f * Mathf.Clamp01(left / 0.6f + 0.3f);
        FloodShape();
        if (Time.time >= nextBubble) {
            nextBubble = Time.time + 0.05f;
            Fx.Burst(new Vector3(Random.Range(floodLeft, floodRight), floorY + FloodDepth * level, 0f), colour, 1, 1f, 0.05f, 0.4f);
        }
        float top = floorY + FloodDepth * level + 0.1f;
        if (freny == null) {
            freny = FindAnyObjectByType<Player>();
        }
        if (freny != null && !freny.IsDead) {
            Vector3 feet = freny.Feet;
            if (feet.x > floodLeft && feet.x < floodRight && feet.y >= floorY - 0.3f && feet.y <= top) {
                freny.Slow(SlowFactor, 0.3f);
                freny.TryDamage(Damage);
            }
        }
        if (Time.time >= nextEnemyBurn) {
            nextEnemyBurn = Time.time + EnemyBurnGap;
            for (int i = Enemy.Active.Count - 1; i >= 0; i--) {
                Enemy enemy = Enemy.Active[i];
                if (enemy == null || enemy.IsDead || enemy is Cyclops) {
                    continue;
                }
                Vector3 feet = enemy.Feet;
                if (feet.x > floodLeft && feet.x < floodRight && feet.y >= floorY - 0.3f && feet.y <= top) {
                    enemy.HitBy(null, Damage);   // not Freny's hit: no score for him
                }
            }
        }
    }

    // kept for an enemy of phase 2 (José, 2026-10-30), the Ciclope no longer uses it.
    // Thrown up high from the mouth: a jet that comes down on targetX from above, through every
    // platform on the way, to the arena floor, where it spreads (VomitStream)
    public static void Pour(Vector3 mouth, float targetX, float standingY, Player player, float width, float seconds) {
        GameObject go = new GameObject("Vomit Stream");
        go.transform.position = mouth;
        go.AddComponent<VomitStream>().Setup(mouth, targetX, standingY, player, width, seconds);
    }

    public static AcidPuddle Create(Vector3 floorPoint, float width, float seconds, float spread = 0f) {
        GameObject go = new GameObject("Acid Puddle");
        go.transform.position = floorPoint + Vector3.up * 0.06f;
        Sprite[] art = Frames();
        // the drawn puddle is ArtWidth wide; a flat strip when the art is missing
        go.transform.localScale = art != null ? new Vector3(width / ArtWidth, 1f, 1f) : new Vector3(width, 0.14f, 1f);
        AcidPuddle puddle = go.AddComponent<AcidPuddle>();
        puddle.fullWidth = width;
        puddle.width = spread > 0f ? Mathf.Min(0.4f, width) : width;   // it lands as a splash and runs out sideways
        puddle.spreadSpeed = spread > 0f ? (width - puddle.width) / spread : 0f;
        puddle.scaleY = go.transform.localScale.y;
        puddle.artScale = art != null;
        puddle.Resize();
        puddle.life = seconds;
        puddle.until = Time.time + seconds;
        puddle.renderer2d = go.AddComponent<SpriteRenderer>();
        puddle.renderer2d.sprite = art != null ? art[0] : Pixel();
        puddle.renderer2d.color = art != null ? Color.white : puddle.colour;
        puddle.frames = art;
        puddle.renderer2d.sortingOrder = 44;
        if (ItemPickup.UnlitMaterial != null) {
            puddle.renderer2d.sharedMaterial = ItemPickup.UnlitMaterial;
        }
        Fx.Burst(floorPoint, puddle.colour, 30, 3f, 0.1f, 0.5f);
        return puddle;
    }

    private float fullWidth;
    private float spreadSpeed;
    private float scaleY;
    private bool artScale;

    private void Resize() {
        transform.localScale = new Vector3(artScale ? width / ArtWidth : width, scaleY, 1f);
    }

    // more poured on it while it is still running out
    public void Feed() {
        until = Mathf.Max(until, Time.time + life * 0.5f);
    }

    private void Update() {
        if (flood) {
            UpdateFlood();
            return;
        }
        float left = until - Time.time;
        if (left <= 0f) {
            Destroy(gameObject);
            return;
        }
        if (width < fullWidth) {
            width = Mathf.Min(fullWidth, width + spreadSpeed * Time.deltaTime);
            Resize();
        }
        Color c = frames != null ? Color.white : colour;
        c.a *= Mathf.Clamp01(left / Mathf.Min(1f, life));
        renderer2d.color = c;
        if (frames != null) {
            renderer2d.sprite = frames[(int)(Time.time * FrameRate) % frames.Length];   // it seethes
        }
        if (Time.time >= nextBubble) {
            nextBubble = Time.time + 0.15f;
            Fx.Burst(transform.position + Vector3.right * Random.Range(-width / 2f, width / 2f), colour, 2, 1f, 0.05f, 0.4f);
        }
        if (freny == null) {
            freny = FindAnyObjectByType<Player>();
        }
        if (freny != null && !freny.IsDead) {
            Vector3 feet = freny.Feet;
            Vector3 at = transform.position;
            if (Mathf.Abs(feet.x - at.x) < width / 2f && feet.y >= at.y - 0.3f && feet.y <= at.y + 0.35f) {
                freny.Slow(SlowFactor, 0.3f);   // wading through it: slow while in it and a moment after
                freny.TryDamage(Damage);        // and it burns (his blink after a hit sets the pace)
            }
        }
    }

    // Cyclops/Vomit Puddle_0.._2: pixel art, 96x20 at 32 px a unit (Tools/ArtGen/vomit_puddle.py)
    private const float ArtWidth = 3f;
    private const float FrameRate = 6f;
    private static Sprite[] artFrames;
    private Sprite[] frames;

    private static Sprite[] Frames() {
        if (artFrames == null) {
            Sprite[] loaded = new Sprite[3];
            for (int i = 0; i < loaded.Length; i++) {
                loaded[i] = Resources.Load<Sprite>("Cyclops/Vomit Puddle_" + i);
                if (loaded[i] == null) {
                    return null;
                }
            }
            artFrames = loaded;
        }
        return artFrames;
    }

    private static Sprite Pixel() {
        if (pixel == null) {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return pixel;
    }
}

// the vomit as a liquid: one green stream from the mouth along its fall (an arc out of the mouth,
// then straight down). The front falls with gravity, the tail lets go when the pouring stops and
// falls after it, so the stream stretches, thins and drains. It splashes where it passes the
// Ciclope's own floor and, at the bottom, runs out into a puddle that spreads sideways.
public class VomitStream : MonoBehaviour {
    private const float Gravity = 30f;
    private const float PourSeconds = 0.7f;
    private const float Thickness = 0.26f;
    private const float SpreadSeconds = 0.5f;
    private const float Step = 0.02f;          // seconds between two points of the path
    private const float TopMargin = 0.8f;      // the jet peaks this far under the top of the screen
    private const float LowestPeak = 4f;       // and at least this high over the mouth
    private static readonly Color Green = new Color(0.45f, 0.85f, 0.2f, 1f);
    private static readonly Color DarkGreen = new Color(0.25f, 0.55f, 0.1f, 1f);
    private static Material material;
    private static Player freny;

    // the jet's flight, one point every Step seconds: up out of the mouth, over and down
    private readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>();
    private int peak;   // from this point on it is falling (only the falling part burns)
    private readonly System.Collections.Generic.List<int> crossings = new System.Collections.Generic.List<int>();   // where it goes through a platform
    private Vector3 bottom;
    private float width;
    private float seconds;
    private float started;
    private float nextSplash;
    private AcidPuddle puddle;
    private LineRenderer line;

    public void Setup(Vector3 mouth, float targetX, float standingY, Player player, float puddleWidth, float puddleSeconds) {
        width = puddleWidth;
        seconds = puddleSeconds;
        started = Time.time;
        freny = player != null ? player : freny;
        Trace(mouth, targetX, standingY, player);
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.sortingOrder = 45;
        line.sharedMaterial = Material();
        line.startColor = DarkGreen;   // the tail (it thins and darkens as it drains)
        line.endColor = Green;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.85f));
        line.widthMultiplier = Thickness;
        line.positionCount = 0;
    }

    // José, 2026-10-30: straight up near the top of the screen, then down onto targetX and on to
    // the floor (over the void at the sides it drops off the screen)
    private void Trace(Vector3 mouth, float targetX, float standingY, Player player) {
        ArenaMap map = player != null ? ArenaMap.Shared(player) : null;
        float top = ArenaBounds.Active ? ArenaBounds.Top - TopMargin : mouth.y + 8f;
        float apex = Mathf.Max(top, mouth.y + LowestPeak);
        float floor = float.NaN;
        if (map != null && map.Floor != null) {
            if (map.Floor.Contains(targetX)) {
                floor = map.Floor.y;
            }
        } else {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(targetX, standingY + 0.1f), Vector2.down, 30f, LayerMask.GetMask("Ground"));
            floor = hit.collider != null ? hit.point.y : standingY;
        }
        float end = float.IsNaN(floor) ? standingY - 20f : floor;
        float vy = Mathf.Sqrt(2f * Gravity * (apex - mouth.y));
        float flight = vy / Gravity + Mathf.Sqrt(2f * (apex - end) / Gravity);
        float vx = (targetX - mouth.x) / flight;
        bottom = new Vector3(targetX, floor, 0f);
        Vector3 p = mouth;
        path.Add(p);
        peak = -1;
        for (float t = Step; ; t += Step) {
            float y = mouth.y + vy * t - 0.5f * Gravity * t * t;
            Vector3 next = new Vector3(mouth.x + vx * t, Mathf.Max(y, end), 0f);
            if (peak < 0 && vy - Gravity * t < 0f) {
                peak = path.Count;
            }
            if (map != null && peak >= 0) {
                foreach (ArenaMap.Platform platform in map.Platforms) {
                    if (platform != map.Floor && p.y > platform.y && next.y <= platform.y && platform.Contains(next.x)) {
                        crossings.Add(path.Count);
                    }
                }
            }
            path.Add(next);
            p = next;
            if (y <= end) {
                break;
            }
        }
    }

    private float Elapsed => Time.time - started;
    private float Head => Mathf.Min(path.Count - 1, Elapsed / Step);
    private float Tail => Mathf.Min(Head, Mathf.Max(0f, (Elapsed - PourSeconds) / Step));

    private Vector3 At(float index) {
        int i = Mathf.Clamp(Mathf.FloorToInt(index), 0, path.Count - 1);
        int j = Mathf.Min(i + 1, path.Count - 1);
        return Vector3.Lerp(path[i], path[j], index - i);
    }

    private void Update() {
        Draw();
        Splash();
        Burn();
        if (Tail >= path.Count - 1.01f) {
            Destroy(gameObject);
        }
    }

    // the stretch between tail and head, wobbling a little as it flies
    private void Draw() {
        float head = Head, tail = Tail;
        int count = Mathf.Clamp(Mathf.CeilToInt((head - tail) / 2f) + 1, 2, 120);
        line.positionCount = count;
        for (int i = 0; i < count; i++) {
            float s = Mathf.Lerp(tail, head, i / (float)(count - 1));
            Vector3 p = At(s);
            p.x += Mathf.Sin(Time.time * 22f + s * 0.4f) * 0.035f;
            line.SetPosition(i, p);
        }
        // it thins out as it drains
        float left = 1f - Mathf.Clamp01((Elapsed - PourSeconds) / 0.6f);
        line.widthMultiplier = Thickness * Mathf.Lerp(0.5f, 1f, left);
    }

    private void Splash() {
        float head = Head, tail = Tail;
        if (tail >= head || Time.time < nextSplash) {
            return;
        }
        nextSplash = Time.time + 0.06f;
        // going through a platform: drops fly off where it passes
        foreach (int crossing in crossings) {
            if (head >= crossing && tail <= crossing) {
                Fx.Burst(path[crossing], Green, 3, 1.6f, 0.1f, 0.3f);
            }
        }
        // the bottom: it lands, the puddle spreads and keeps being fed while it still pours
        if (head >= path.Count - 1.01f && !float.IsNaN(bottom.y)) {
            Fx.Burst(bottom, Green, 4, 2.2f, 0.1f, 0.35f);
            if (puddle == null) {
                puddle = AcidPuddle.Create(bottom, width, seconds, SpreadSeconds);
            } else {
                puddle.Feed();
            }
        }
    }

    // the falling part of the jet burns whoever it comes down on
    private void Burn() {
        float head = Head, from = Mathf.Max(Tail, peak);
        if (freny == null || freny.IsDead || peak < 0 || from >= head) {
            return;
        }
        Vector3 body = freny.BodyCenter;
        for (float s = from; s <= head; s += 1f) {
            Vector3 p = At(s);
            if (Mathf.Abs(p.x - body.x) < 0.4f && Mathf.Abs(p.y - body.y) < 0.6f) {
                freny.TryDamage(1);
                return;
            }
        }
    }

    private static Material Material() {
        if (material == null) {
            Material unlit = ItemPickup.UnlitMaterial;
            material = unlit != null ? new Material(unlit) : new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = Texture2D.whiteTexture;
        }
        return material;
    }
}

