using System.Collections.Generic;
using UnityEngine;

// The Rastejante of the Poço de Ácido: an eyeball on six legs that crawls over everything solid,
// floors, walls, the undersides of platforms and ceilings, following the outline of the level.
// It heads for Freny along whatever it is on; from a ceiling right above him it lets go (a short
// shake first: the warning) and drops on him, and on the ground it bites. Then it climbs the next
// wall it finds and does it again. Moving itself (no physics): it keeps the "down" of the surface
// it clings to, turns up a wall it walks into and round an edge it walks off. Built at runtime from
// the Golem prefab (EnemyTemplates); the art is Tools/ArtGen/acid_crawler.py.
public class Crawler : Enemy {
    private const int Life = 2;
    private const float Cling = 0.5f;          // from its middle to the surface (the legs' reach, 1 unit art)
    private const float TellSeconds = 0.4f;    // shaking on the ceiling before it lets go
    private const float DropGap = 4f;          // between two drops
    private const float DropAbove = 10f;       // drops on him from at most this high
    private const float DropAlign = 0.5f;      // and only this close over him
    private const float Gravity = 28f;
    private const float BiteReach = 0.85f;
    private const float BiteWindup = 0.3f;
    private const float BiteGap = 1.2f;
    private const float Rethink = 0.4f;        // how often it picks a way along its surface
    private const float WalkFps = 12f;
    private const string Sheets = "Crawler/Crawler ";

    private static readonly Color WindupTint = new Color(1f, 0.5f, 0.5f);

    // the ones in the arena now (the autopilot steps from under one about to drop)
    public static readonly List<Crawler> All = new List<Crawler>();

    private enum State { Crawl, Tell, Fall }
    private State state = State.Fall;   // it comes out of the portal and lands first
    private float stateStart;
    private Vector2 down = Vector2.down;
    private Vector2 along = Vector2.right;
    private float fallSpeed;
    private bool fallHit;
    private float nextDrop;
    private float nextBite;
    private float biteAt = -1f;
    private float nextRethink;
    private float walked;
    private Sprite[] walk, drop;

    protected override bool WalksOnGround => false;   // it has its own way of keeping to surfaces
    protected override string SoundName => "bug";
    protected override float SoundPitch => 0.7f;
    protected override float SpeedFactor => 1.1f;
    protected override Color DeathColor => new Color(0.85f, 0.85f, 0.7f);

    public bool OnCeiling => state == State.Crawl && down.y > 0.5f;
    // about to drop or dropping, over x
    public bool Dropping => state == State.Tell || state == State.Fall;

    protected override void OnEnable() {
        base.OnEnable();
        All.Add(this);
    }

    protected override void OnDisable() {
        base.OnDisable();
        All.Remove(this);
    }

    public override void Init() {
        health = Life;
        speed = 2;
        coins = 2;
        scoreToAdd = 200;
        attackCooldown = BiteGap;
        base.Init();
        if (rb) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        walk = FrameAnimator.Load(Sheets + "Walk");
        drop = FrameAnimator.Load(Sheets + "Drop");
        if (sprite != null) {
            sprite.transform.localPosition = Vector3.zero;
            sprite.transform.localScale = Vector3.one;
            if (walk.Length > 0) {
                sprite.sprite = walk[0];
            }
        }
        transform.localScale = Vector3.one;
        stateStart = Time.time;
        nextDrop = Time.time + 2f;
    }

    public override void Update() {
        if (isDead || player == null || FellOutOfWorld()) {
            return;
        }
        switch (state) {
            case State.Crawl:
                Crawl();
                break;
            case State.Tell:
                if (Time.time - stateStart >= TellSeconds) {
                    LetGo();
                }
                break;
            case State.Fall:
                Fall();
                break;
        }
        Draw();
    }

    // ---- crawling ----

    private void Crawl() {
        if (Time.time >= nextRethink) {
            nextRethink = Time.time + Rethink;
            PickWay();
        }
        // right over him on a ceiling: shake, then drop
        Vector3 body = player.BodyCenter;
        if (OnCeiling && Time.time >= nextDrop && !player.IsDead && Mathf.Abs(body.x - transform.position.x) < DropAlign
            && body.y < transform.position.y && transform.position.y - body.y < DropAbove && ClearBelow(body.y)) {
            state = State.Tell;
            stateStart = Time.time;
            PlaySound("attack");
            return;
        }
        if (Bite()) {
            return;
        }
        float step = MoveSpeed * Time.deltaTime;
        while (step > 0f) {
            float part = Mathf.Min(step, 0.15f);
            step -= part;
            if (!Step(part)) {
                StartFall();
                return;
            }
        }
        walked += MoveSpeed * Time.deltaTime;
        KeepOnScreen();
    }

    // which way along its surface: towards Freny's side of it; when he is straight across (under
    // its ceiling, beside its wall) it keeps going, so it ends up somewhere it can reach him from
    private void PickWay() {
        Vector2 axis = along.x != 0f ? Vector2.right : Vector2.up;
        Vector2 toHim = (Vector2)player.BodyCenter - (Vector2)transform.position;
        float onAxis = Vector2.Dot(toHim, axis);
        // on the ground with him a floor up, nothing to gain by standing under him
        bool under = down.y < -0.5f && toHim.y > 1.5f && Mathf.Abs(toHim.x) < 2f;
        if (Mathf.Abs(onAxis) > 0.3f && !under) {
            along = axis * Mathf.Sign(onAxis);
        }
    }

    // one bit of the way; false when it lost the surface (it falls then)
    private bool Step(float distance) {
        int ground = GroundMask;
        Vector2 p = transform.position;
        // a wall (or floor) across its way: it turns and climbs it
        RaycastHit2D wall = Physics2D.Raycast(p, along, Cling + distance, ground);
        if (wall.collider != null) {
            Vector2 oldDown = down;
            down = along;
            along = -oldDown;
            p = wall.point - down * Cling;
            transform.position = p;
            return true;
        }
        p += along * distance;
        RaycastHit2D under = Physics2D.Raycast(p, down, Cling + 0.25f, ground);
        if (under.collider != null) {
            transform.position = under.point - down * Cling;
            return true;
        }
        // it walked off an edge: round the corner, onto the face below the edge
        Vector2 turnedDown = -along;
        Vector2 turnedAlong = down;
        Vector2 round = p + along * Cling + down * (Cling + 0.05f);
        RaycastHit2D face = Physics2D.Raycast(round, turnedDown, Cling + 0.3f, ground);
        if (face.collider == null) {
            transform.position = p;
            return false;
        }
        down = turnedDown;
        along = turnedAlong;
        transform.position = face.point - down * Cling;
        return true;
    }

    // the screen is the arena: it turns back at the sides and the top
    private void KeepOnScreen() {
        if (!ArenaBounds.Active) {
            return;
        }
        Vector3 p = transform.position;
        if ((p.x < ArenaBounds.Left + 0.6f && along.x < 0f) || (p.x > ArenaBounds.Right - 0.6f && along.x > 0f)
            || (p.y > ArenaBounds.Top - 0.6f && along.y > 0f)) {
            along = -along;
            nextRethink = Time.time + Rethink * 3f;   // and keeps that way a while
        }
    }

    // nothing solid between it and him (it would land on that instead)
    private bool ClearBelow(float bodyY) {
        Vector2 from = (Vector2)transform.position + Vector2.down * (Cling + 0.1f);
        float length = from.y - bodyY;
        return length <= 0f || Physics2D.Raycast(from, Vector2.down, length, GroundMask).collider == null;
    }

    // on the ground next to him: a short wind-up (it flashes), then a bite
    private bool Bite() {
        if (down.y > -0.5f || player.IsDead) {
            biteAt = -1f;
            return false;
        }
        float reach = Vector2.Distance(transform.position, player.BodyCenter);
        if (biteAt < 0f) {
            if (reach <= BiteReach && Time.time >= nextBite) {
                biteAt = Time.time + BiteWindup * Tuning.EnemyWindup.Value;
                PlaySound("attack");
            }
            return biteAt >= 0f;
        }
        if (Time.time < biteAt) {
            return true;
        }
        biteAt = -1f;
        nextBite = Time.time + BiteGap * Tuning.EnemyCooldown.Value * DifficultyCooldown;
        if (reach <= BiteReach + 0.3f) {
            HitPlayer();
        }
        return false;
    }

    // ---- dropping ----

    private void LetGo() {
        StartFall();
        nextDrop = Time.time + DropGap * DifficultyCooldown;
    }

    private void StartFall() {
        state = State.Fall;
        stateStart = Time.time;
        fallSpeed = 0f;
        fallHit = false;
        down = Vector2.down;
    }

    private void Fall() {
        fallSpeed = Mathf.Min(22f, fallSpeed + Gravity * Time.deltaTime);
        float move = fallSpeed * Time.deltaTime;
        Vector2 p = transform.position;
        RaycastHit2D land = Physics2D.Raycast(p, Vector2.down, Cling + move, GroundMask);
        // it lands on him on the way down
        Vector3 body = player.BodyCenter;
        if (!fallHit && !player.IsDead && Mathf.Abs(body.x - p.x) < 0.55f && Mathf.Abs(body.y - p.y) < player.BodyHeight * 0.5f + 0.3f) {
            fallHit = true;
            HitPlayer();
        }
        if (land.collider != null) {
            transform.position = land.point + Vector2.up * Cling;
            state = State.Crawl;
            stateStart = Time.time;
            down = Vector2.down;
            along = body.x >= land.point.x ? Vector2.right : Vector2.left;
            nextRethink = Time.time + Rethink;
            Fx.Dust(land.point, 3);
            return;
        }
        transform.position = p + Vector2.down * move;
    }

    // ---- looks ----

    // the sheets stand on the floor facing right: turned so the legs point at its surface
    private void Draw() {
        if (sprite == null) {
            return;
        }
        Vector2 d = state == State.Crawl || state == State.Tell ? down : Vector2.down;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f;
        sprite.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 localRight = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
        if (state == State.Crawl) {
            sprite.flipX = Vector2.Dot(along, localRight) < 0f;
        } else {
            sprite.flipX = player.transform.position.x < transform.position.x;
        }
        Vector3 shake = Vector3.zero;
        if (state == State.Tell && drop.Length > 0) {
            sprite.sprite = drop[0];
            shake = (Vector3)(Random.insideUnitCircle * 0.05f);
        } else if (state == State.Fall && drop.Length > 1) {
            sprite.sprite = drop[1];
        } else if (walk.Length > 0) {
            sprite.sprite = walk[(int)(walked * WalkFps / Mathf.Max(0.5f, MoveSpeed)) % walk.Length];
        }
        sprite.transform.localPosition = shake;
        bool flashing = biteAt >= 0f || state == State.Tell;
        sprite.color = flashing && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? WindupTint : Color.white;
    }
}
