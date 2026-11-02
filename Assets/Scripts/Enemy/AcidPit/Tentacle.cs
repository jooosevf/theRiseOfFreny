using System.Collections.Generic;
using UnityEngine;

// The Tentáculo of the Poço de Ácido: it rises out of an acid pool (the director puts it there,
// AcidPitArena.SpawnSpot) and stays put. It watches Freny, and when he stands still for StillSeconds
// it spits at him: a green jet thrown high that comes down where he stood (AcidPuddle.Pour, the
// VomitStream kept from the Ciclope) and leaves a puddle on the floor. Moving keeps him safe. A hit
// that doesn't kill it sends it back under the acid, and it comes up again out of another pool a
// moment later; left alone, it sinks for good after a while. Two hits kill it (a combo does).
// Built at runtime from the Golem prefab (EnemyTemplates); the art is the project's unused
// Tentacle sheets, played here frame by frame.
public class Tentacle : Enemy {
    private const int Life = 2;
    private const float StillSeconds = 1f;      // Freny standing this long gets spat at
    private const float StillRadius = 0.5f;     // "still": his feet stayed this close to one spot
    private const float SpitRange = 16f;        // across most of the arena
    private const float SpitCooldown = 3f;      // x the arena's cooldown factors
    private const float PuddleWidth = 1.6f;
    private const float PuddleSeconds = 3f;
    private const float FlightGuess = 1.7f;     // the jet's time in the air, for the autopilot
    private const float StayMin = 10f, StayMax = 14f;   // above the acid before it sinks away
    private const float UnderSeconds = 2.5f;    // after a hit, under the acid before it comes up elsewhere
    private const float SpriteLift = 1.05f;     // the sheet's base is its bottom row: 1.2 under the middle, sunk a little
    private const float MouthHeight = 1.6f;
    private const string Sheets = "Tentacle/Tentacle ";

    private static readonly Color WindupTint = new Color(0.7f, 1f, 0.55f);

    // where spits are coming down, and when (the autopilot gets out of the way)
    public struct Spit {
        public float x;
        public float lands;
    }
    public static readonly List<Spit> Incoming = new List<Spit>();

    private enum State { Rising, Up, Windup, Spitting, Diving }
    private State state;
    private float stateStart;
    private bool leaving;       // this dive is for good
    private float leaveAt;
    private float nextSpit;
    private float spitX;
    private int lastHealth;
    private Vector3 stillAnchor;
    private float stillSince;
    private Sprite[] spawn, idle, attack, hit;

    protected override bool WalksOnGround => false;   // it never moves: no floor-following
    protected override string SoundName => "licker";
    protected override float SoundPitch => 0.8f;
    protected override Color DeathColor => new Color(0.35f, 0.3f, 0.65f);
    public override bool ShowsHealthBar => false;      // it dives at the first hit anyway

    // the top of the tentacle, where the jet leaves from
    public Vector3 Mouth => transform.position + Vector3.up * MouthHeight;

    public override void Init() {
        health = Life;
        speed = 0;
        coins = 2;
        scoreToAdd = 250;
        base.Init();
        if (rb) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        lastHealth = Health;
        if (sprite != null) {
            sprite.transform.localPosition = new Vector3(0f, SpriteLift, 0f);
            sprite.transform.localScale = Vector3.one;
            sprite.transform.localRotation = Quaternion.identity;
            sprite.sortingOrder = 43;   // just behind the pool's acid (44): its base is under the surface
        }
        spawn = FrameAnimator.Load(Sheets + "Spawn");
        idle = FrameAnimator.Load(Sheets + "Idle");
        attack = FrameAnimator.Load(Sheets + "Attack");
        hit = FrameAnimator.Load(Sheets + "Hit");
        Surface();
    }

    // up out of the acid (also when it comes back after a dive, AcidPitArena.Resurface)
    public void Surface() {
        SetState(State.Rising);
        leaving = false;
        leaveAt = Time.time + Random.Range(StayMin, StayMax);
        nextSpit = Time.time + 1.2f;
        lastHealth = Health;
        if (player != null) {
            stillAnchor = player.Feet;
            stillSince = Time.time;
        }
        Vector3 at = transform.position;
        Fx.Burst(at + Vector3.up * 0.1f, AcidPitArena.Acid, 14, 2.5f, 0.08f, 0.5f);
        Sfx.Play("potion", at, 0.8f, 0.6f);
    }

    private void SetState(State next) {
        state = next;
        stateStart = Time.time;
    }

    public override void Update() {
        if (isDead || player == null) {
            return;
        }
        PruneIncoming();
        float t = Time.time - stateStart;

        // a hit that didn't kill it: back under the acid (the Lua de Sangue's healing raises the mark)
        if (Health > lastHealth) {
            lastHealth = Health;
        }
        if (Health < lastHealth) {
            lastHealth = Health;
            if (state != State.Diving) {
                leaving = false;
                SetState(State.Diving);
                t = 0f;
            }
        }

        // keeps an eye on him (the sheets look left: scale -1 looks right)
        if (state != State.Diving) {
            transform.localScale = new Vector3(player.transform.position.x > transform.position.x ? -1f : 1f, 1f, 1f);
        }
        TrackStillness();

        switch (state) {
            case State.Rising:
                Show(spawn, t, 10f, false);
                if (t >= spawn.Length / 10f) {
                    SetState(State.Up);
                }
                break;
            case State.Up:
                Show(Time.time - LastHitTime < 0.2f ? hit : idle, t, 6f, true);
                if (Time.time >= leaveAt) {
                    leaving = true;
                    SetState(State.Diving);
                } else if (Time.time >= nextSpit && StandingStill && Mathf.Abs(player.transform.position.x - transform.position.x) <= SpitRange) {
                    spitX = player.Feet.x;
                    Incoming.Add(new Spit { x = spitX, lands = Time.time + WindupSeconds + FlightGuess });
                    PlaySound("attack");
                    SetState(State.Windup);
                }
                break;
            case State.Windup:
                ShowRange(attack, 0, 5, t, 8f);
                Flash(true);
                if (t >= WindupSeconds) {
                    Flash(false);
                    AcidPuddle.Pour(Mouth, spitX, player.Feet.y, player, PuddleWidth, PuddleSeconds);
                    Sfx.Play("potion", Mouth, 1f, 0.75f);
                    Fx.Burst(Mouth, AcidPitArena.Acid, 8, 2f, 0.07f, 0.3f);
                    SetState(State.Spitting);
                }
                break;
            case State.Spitting:
                ShowRange(attack, 5, 5, t, 10f);
                if (t >= 0.5f) {
                    nextSpit = Time.time + SpitCooldown * Tuning.EnemyCooldown.Value * DifficultyCooldown;
                    SetState(State.Up);
                }
                break;
            case State.Diving:
                Flash(false);
                // the rise backwards
                int frame = spawn.Length - 1 - (int)(t * 12f);
                if (spawn.Length > 0) {
                    sprite.sprite = spawn[Mathf.Clamp(frame, 0, spawn.Length - 1)];
                }
                if (frame < 0) {
                    Sink();
                }
                break;
        }
    }

    private float WindupSeconds => 5f / 8f;

    // gone under: for good (no points, no coins), or to come up out of another pool
    private void Sink() {
        Fx.Burst(transform.position + Vector3.up * 0.1f, AcidPitArena.Acid, 10, 2f, 0.08f, 0.4f);
        Sfx.Play("potion", transform.position, 0.6f, 1.2f);
        if (leaving || AcidPitArena.Instance == null) {
            Despawn();
            return;
        }
        AcidPitArena.Instance.Resurface(this, UnderSeconds);
        gameObject.SetActive(false);
    }

    private bool StandingStill => Time.time - stillSince >= StillSeconds;

    private void TrackStillness() {
        Vector3 feet = player.Feet;
        if (player.IsDead || Vector2.Distance(feet, stillAnchor) > StillRadius) {
            stillAnchor = feet;
            stillSince = Time.time;
        }
    }

    private static void PruneIncoming() {
        Incoming.RemoveAll(s => Time.time > s.lands + 0.3f);
    }

    private void Show(Sprite[] clip, float t, float fps, bool loop) {
        if (sprite == null || clip == null || clip.Length == 0) {
            return;
        }
        int frame = (int)(t * fps);
        sprite.sprite = clip[loop ? frame % clip.Length : Mathf.Min(frame, clip.Length - 1)];
    }

    private void ShowRange(Sprite[] clip, int first, int count, float t, float fps) {
        if (sprite == null || clip == null || clip.Length < first + count) {
            return;
        }
        sprite.sprite = clip[first + Mathf.Min((int)(t * fps), count - 1)];
    }

    // flashing while it gathers the spit, like the Bat's wind-up
    private void Flash(bool on) {
        if (sprite != null) {
            sprite.color = on && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f ? WindupTint : Color.white;
        }
    }
}
