using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The Ciclope, the Arena's boss (ArenaDirector: the first at 50k, then one every 100k). Slow and huge,
// with the game's boss bar (BossHealth prefab) at the top of the screen instead of the small one. Three attacks:
// - the punch up close: 2 hearts and a big knockback (José: "o knockback do ciclope deve ser grande");
// - the smash: it pounds the floor and a shockwave runs both ways along it (jump over it);
// - the vomit: a puddle in front of it that burns for a few seconds.
// Built at runtime from the Golem prefab (EnemyTemplates) and drawn by FrameAnimator.
public class Cyclops : Enemy {
    public const int Life = 100;   // José, 2026-10-29
    private const float SmashCooldown = 7f;
    private const float VomitCooldown = 12f;
    private const float SmashReach = 9f;
    private const float JetMinDistance = 2.5f;
    private const string Sheets = "Cyclops/Cyclops ";
    // the red Golem's mine, thrown much harder: fast and flat, hard to dodge
    private const float BombCooldown = 8f;
    private const float BombMinDistance = 7f;   // only when Freny is far: thrown hard across the arena
    private const float BombMaxDistance = 14f;
    private const float BombAngle = 28f;
    private const float BombGravity = 4.5f;
    private const float BombMaxSpeed = 26f;
    private const float BombSize = 3f;   // 3 times the red Golem's mine (José, 2026-10-29: 4 was too much)
    private float nextBomb;

    private FrameAnimator frames;
    private bool special;          // smashing or vomiting: stands still
    private float nextSmash;
    private float nextVomit;
    private int maxHealth;
    private const float AfterBlowRest = 3f;   // seconds of calm after any of its blows
    private float quietUntil;
    private bool punching;

    // the eye (in the Idle frame, 0.88 forward and 0.73 up from the sprite's middle): hits there
    // take double (José, 2026-10-29)
    private static readonly Vector2 EyeOffset = new Vector2(0.88f, 0.73f) * Size;
    private const float EyeRadius = 0.45f * Size;

    // José, 2026-10-30: 20% bigger. The art's pivot is its middle and its feet are at the bottom,
    // so the sprite moves up 1.6 per unit of growth; the collider (ArenaDirector) grows with it
    public const float Size = 1.2f;

    public Vector3 EyePoint => Eye;   // the autopilot aims its sword there
    private Vector3 Eye => sprite.bounds.center + new Vector3((FacingRight ? 1f : -1f) * EyeOffset.x, EyeOffset.y, 0f);

    protected override float HitScale(Vector3 from, bool shot) {
        if (HitPoint.HasValue && Vector2.Distance(HitPoint.Value, Eye) <= EyeRadius) {
            Fx.Burst(Eye, new Color(0.6f, 0.4f, 1f), 14, 3f, 0.08f, 0.35f);
            LastHitMark = EyeHit;
            return 2f;
        }
        return 1f;
    }

    // it laughs at Freny when it arrives and when it hurts him (José: "risadas do ciclope"):
    // the Sorcerer Supreme's laugh, much deeper
    private const float LaughGap = 6f;
    private float nextLaugh;
    private bool laughOnArrival;
    private int lastFrenyHealth = -1;

    private void Laugh() {
        Sfx.Play("supreme_laugh", Center, 1f, 0.55f);
        nextLaugh = Time.time + LaughGap;
    }

    private void UpdateLaugh() {
        if (player == null) {
            return;
        }
        if (laughOnArrival && Time.time >= nextLaugh) {
            laughOnArrival = false;
            Laugh();
        }
        bool hurt = lastFrenyHealth >= 0 && player.Health < lastFrenyHealth;
        lastFrenyHealth = player.Health;
        if (hurt && Time.time >= nextLaugh && Vector2.Distance(player.transform.position, transform.position) < 14f) {
            Laugh();
        }
    }

    // resting after a blow: the moment to go in with the sword (the autopilot waits for it)
    public bool Exposed => !isDead && !special && !IsAttacking && Time.time < quietUntil;
    public float RestLeft => Exposed ? quietUntil - Time.time : 0f;

    protected override int HitDamage => 1;   // the punch: 1 heart, but it throws Freny far (the smash and the bomb take 2)
    protected override float HitPush => 0f;   // nothing moves it
    // it jumps up and down the platforms after Freny like the walkers (José, 2026-10-29)
    protected override Color DeathColor => new Color(0.5f, 0.35f, 0.85f);
    protected override string SoundName => "golem";
    protected override float SoundPitch => 0.6f;
    protected override float KnockbackLift => 0.6f;
    public override bool ShowsHealthBar => false;

    // from 150k points two come at once (José, 2026-10-29), each with its own bar
    public static readonly System.Collections.Generic.List<Cyclops> Alive = new System.Collections.Generic.List<Cyclops>();
    public static Cyclops Current => Alive.Count > 0 ? Alive[0] : null;

    public override void Init() {
        health = Life;
        speed = 1;
        coins = 30;
        scoreToAdd = 5000;
        knockback = 24f;   // José, 2026-10-29: Freny flies "beeeem mais longe" (was 14)
        attackDistance = 0.6f;
        attackWindup = 0.5f;
        attackCooldown = 3.5f;
        radius = 16f;
        sightHeight = 4f;
        base.Init();
        maxHealth = Health;
        sprite.transform.localScale = Vector3.one * Size;
        sprite.transform.localPosition = new Vector3(0f, 1.6f * (Size - 1f), 0f);
        frames = sprite.GetComponent<FrameAnimator>();
        if (frames == null) {
            frames = sprite.gameObject.AddComponent<FrameAnimator>();
        }
        frames.Setup(this, sprite);
        frames.Add("idle", Sheets + "Idle", 3f);
        frames.Add("walk", Sheets + "Walk", 7f);
        frames.Add("attack", Sheets + "Melee", 6f, false);
        frames.Add("hit", Sheets + "Hit", 10f, false);
        frames.Add("smash", Sheets + "Smash", 12f, false);
        frames.Add("vomit", Sheets + "Vomit", 10f, false);
        nextSmash = Time.time + 4f;
        nextVomit = Time.time + 7f;
        nextBomb = Time.time + 5f;
        if (!Alive.Contains(this)) {
            Alive.Add(this);
        }
        ShowBar(Alive.IndexOf(this));
        nextLaugh = Time.time + 1.2f;   // a laugh as it comes out of the portal
        laughOnArrival = true;
    }

    protected override void OnDisable() {
        base.OnDisable();
        Alive.Remove(this);
        if (bar != null) {
            Destroy(bar);
        }
    }

    public override void Update() {
        if (isDead) {
            return;
        }
        if (special) {
            if (!hopping) {
                StickToGround();
                ClampToArena();
            }
            return;
        }
        UpdateLaugh();
        base.Update();
        KeepApart();
        // the punch ends inside its coroutine, so it is caught here, a frame later
        if (punching && !IsAttacking) {
            quietUntil = Time.time + AfterBlowRest;   // after a punch, nothing right away
        }
        punching = IsAttacking;
        TrackBloodMoon();
        if (isDead || IsAttacking || player == null || player.Health <= 0 || Time.time < quietUntil) {
            return;
        }
        Vector3 feet = Feet;
        float dx = Mathf.Abs(player.Feet.x - feet.x);
        float dy = player.Feet.y - feet.y;
        if (CanBeam) {
            StartCoroutine(Beam());   // the Lua de Sangue: a red beam out of its eye
        } else if (Time.time >= nextBomb && dx >= BombMinDistance && dx <= BombMaxDistance && Mathf.Abs(dy) < 6f) {
            ThrowBomb();
        } else if (Time.time >= nextSmash && dx < SmashReach && Mathf.Abs(dy) < 2.5f) {
            StartCoroutine(Smash());
        } else if (Time.time >= nextVomit && dx >= JetMinDistance) {
            StartCoroutine(Vomit());   // a few steps away: it floods the floor under him
        }
    }

    // up close it punches; 2 times in 5 it throws up instead (José, 2026-10-29)
    private const float VomitShare = 0.4f;

    protected override bool CanStartAttack {
        get {
            if (Time.time < quietUntil || special) {
                return false;
            }
            if (Time.time >= nextVomit && Random.value < VomitShare) {
                StartCoroutine(Vomit());
                return false;
            }
            return true;
        }
    }

    // two Ciclopes chasing Freny ended up on top of each other, looking like one: the second
    // keeps this far from the first
    private const float TwinSpacing = 2.6f;
    private const float TwinPush = 3f;   // units per second

    private void KeepApart() {
        foreach (Cyclops other in Alive) {
            if (other == this || other.isDead) {
                continue;
            }
            float dx = transform.position.x - other.transform.position.x;
            if (Mathf.Abs(dx) >= TwinSpacing || Mathf.Abs(Feet.y - other.Feet.y) > 1f) {
                continue;
            }
            float away = dx == 0f ? (Alive.IndexOf(this) % 2 == 0 ? -1f : 1f) : Mathf.Sign(dx);
            if (!Blocked(away) && GroundAhead(away)) {
                transform.position += Vector3.right * (away * TwinPush * Time.deltaTime);
            }
        }
    }

    private void FacePlayer() {
        transform.localScale = new Vector3(player.transform.position.x > transform.position.x ? -1 : 1, 1, 1);
    }

    private float Facing => FacingRight ? 1f : -1f;

    // José, 2026-10-29: he stays on the floor; Freny up on a platform gets the bomb and the vomit
    protected override bool Climbs => false;

    // José, 2026-10-30: he likes to stand under the first platform in the middle, where Freny can
    // jump at his eye; off in a corner he was hard to reach. He still follows Freny, but only
    // part of the way and never further than HomeLeash from that spot
    private const float HomePull = 0.5f;
    private const float HomeLeash = 4f;
    private float homeX = float.NaN;

    protected override float ChaseX(float targetX) {
        if (!ArenaBounds.Active) {
            return targetX;
        }
        if (float.IsNaN(homeX)) {
            homeX = FindHome();
        }
        float x = Mathf.Lerp(targetX, homeX, HomePull);
        return ArenaBounds.ClampX(Mathf.Clamp(x, homeX - HomeLeash, homeX + HomeLeash), 0.5f);
    }

    // the middle of the lowest platform above the floor in the central half of the arena
    private float FindHome() {
        float middle = (ArenaBounds.Left + ArenaBounds.Right) / 2f;
        float quarter = (ArenaBounds.Right - ArenaBounds.Left) / 4f;
        ArenaMap map = player != null ? ArenaMap.Shared(player) : null;
        if (map == null || map.Floor == null) {
            return middle;
        }
        ArenaMap.Platform best = null;
        foreach (ArenaMap.Platform platform in map.Platforms) {
            float centre = (platform.xMin + platform.xMax) / 2f;
            if (platform.tower || platform.y < map.Floor.y + 1f || Mathf.Abs(centre - middle) > quarter) {
                continue;
            }
            if (best == null || platform.y < best.y - 0.1f
                || (Mathf.Abs(platform.y - best.y) <= 0.1f && Mathf.Abs(centre - middle) < Mathf.Abs((best.xMin + best.xMax) / 2f - middle))) {
                best = platform;
            }
        }
        return best != null ? (best.xMin + best.xMax) / 2f : middle;
    }

    private void ThrowBomb() {
        FacePlayer();
        nextBomb = Time.time + BombCooldown * ScoreCooldown;
        quietUntil = Time.time + 0.8f;
        frames.Play("attack", 0.4f);
        Vector3 from = Center + Vector3.up * (HalfHeight * 0.7f) + Vector3.right * (Facing * HalfWidth);
        GolemMine.ThrowAt(this, from, player.transform.position, BombAngle, BombGravity, BombMaxSpeed, BombSize);
    }

    // José, 2026-10-30: each blow holds its pose a moment. The smash: it jumps with its arms up
    // (frames 5-6), hits the ground on frame 7 and stays down on frame 8 before getting up
    private const int SmashFrames = 12;
    private const int SmashImpactFrame = 7;
    private const float SmashHold = 0.6f;
    private const float SmashShake = 0.35f;
    private const int SmashHopFrame = 4;      // it leaves the ground here and lands on the impact frame
    private const float SmashHopHeight = 0.9f;
    private bool hopping;
    private const float SmashShakeSeconds = 0.5f;

    private IEnumerator Smash() {
        special = true;
        FacePlayer();
        float length = frames.Length("smash");
        float frame = length / SmashFrames;
        frames.Play("smash", length);
        PlaySound("windup");
        yield return new WaitForSeconds(frame * SmashHopFrame);
        // a little hop with the arms up, coming down on the impact frame
        hopping = true;
        Vector3 ground = transform.position;
        float air = frame * (SmashImpactFrame - SmashHopFrame);
        for (float t = 0f; t < air && !isDead; t += Time.deltaTime) {
            float u = t / air;
            transform.position = ground + Vector3.up * (SmashHopHeight * 4f * u * (1f - u));
            yield return null;
        }
        transform.position = ground;
        hopping = false;
        if (!isDead) {
            Vector3 feet = Feet;
            Fx.Dust(feet, 24);
            Sfx.Play("grenade_explode", feet, 0.8f, 0.6f);
            Shockwave.Launch(feet, 1f);
            Shockwave.Launch(feet, -1f);
            ScreenKick.Kick(SmashShake, SmashShakeSeconds);   // the whole screen shakes (José, 2026-10-30)
            yield return new WaitForSeconds(frame * 1.1f);
            frames.Hold(SmashHold);   // on the ground, fist down
            yield return new WaitForSeconds(SmashHold);
        }
        yield return new WaitForSeconds(Mathf.Max(0f, length - frame * (SmashImpactFrame + 1.1f)));
        nextSmash = Time.time + SmashCooldown * ScoreCooldown;
        quietUntil = Time.time + AfterBlowRest;
        special = false;
    }

    // José, 2026-10-30: it throws up where it stands (its Vomit sheet: frame 4 starts it, frame 5 is
    // held while it pours), enough to flood the whole ground floor for a while. Everything down
    // there gets burned but the Ciclopes, so Freny has to keep off the floor (AcidPuddle.Flood)
    private const int VomitFrames = 9;
    private const int VomitStartFrame = 4;
    private const float FloodHold = 1.2f;
    private const float FloodSeconds = 4f;
    private const float MouthForward = 2.07f;   // the stream in frame 4, from the sprite's middle

    // a flood is on its way (from the start of the vomit): the autopilot climbs right away
    public static bool FloodComing => Alive.Exists(c => c != null && !c.isDead && c.vomiting);
    private bool vomiting;

    private IEnumerator Vomit() {
        special = true;
        vomiting = true;
        FacePlayer();
        float length = frames.Length("vomit");
        float frame = length / VomitFrames;
        frames.Play("vomit", length);
        Sfx.Play("potion", Center, 1f, 0.5f);   // a deep, slow gurgle
        yield return new WaitForSeconds(frame * VomitStartFrame);
        if (!isDead) {
            float mouthX = transform.position.x + Facing * MouthForward * Size;
            ArenaMap map = player != null && ArenaBounds.Active ? ArenaMap.Shared(player) : null;
            if (map != null && map.Floor != null) {
                AcidPuddle.Flood(mouthX, map.Floor.y, map.Floor.xMin, map.Floor.xMax, FloodSeconds);
            } else {
                AcidPuddle.Flood(mouthX, Feet.y, ArenaBounds.Left, ArenaBounds.Right, FloodSeconds);
            }
            yield return new WaitForSeconds(frame * 1.1f);
            frames.Hold(FloodHold);   // head down, pouring
            yield return new WaitForSeconds(FloodHold);
        }
        yield return new WaitForSeconds(Mathf.Max(0f, length - frame * (VomitStartFrame + 1.1f)));
        nextVomit = Time.time + VomitCooldown * ScoreCooldown;
        quietUntil = Time.time + AfterBlowRest;
        vomiting = false;
        special = false;
    }

    // José, 2026-10-30: in the Lua de Sangue its eye turns red and it fires a red beam at Freny
    // (BloodBeam), 2 or 3 times a Blood Moon at most: the first BeamFirst seconds into it, then one
    // every BeamGap seconds. It charges the eye, locks on where Freny is and fires
    private const int BeamsPerMoon = 3;
    private const float BeamFirst = 2f;
    private const float BeamGap = 8f;
    private const float BeamCharge = 0.9f;
    private const float BeamSeconds = 0.8f;
    private int beamsThisMoon;
    private float nextBeam;
    private bool inBloodMoon;

    private bool CanBeam => inBloodMoon && beamsThisMoon < BeamsPerMoon && Time.time >= nextBeam;

    private void TrackBloodMoon() {
        bool blood = BloodMoon > 0.5f;
        if (blood && !inBloodMoon) {
            beamsThisMoon = 0;
            nextBeam = Time.time + BeamFirst;
        }
        inBloodMoon = blood;
    }

    // where the beam leaves the eye: a little over the middle of the eye (José: it looked like it
    // came from behind it)
    private Vector3 BeamEye => Eye + Vector3.up * (0.18f * Size);
    private const float GlowSize = 1.3f;

    private IEnumerator Beam() {
        special = true;
        beamsThisMoon++;
        FacePlayer();
        frames.Play("idle", BeamCharge + BeamSeconds);
        PlaySound("windup");
        Sfx.Play("supreme_laugh", Center, 0.8f, 0.5f);
        SpriteRenderer glow = BloodBeam.Orb("Eye Glow", sprite);
        for (float t = 0f; t < BeamCharge && !isDead; t += Time.deltaTime) {
            float u = t / BeamCharge;
            glow.transform.position = BeamEye;
            glow.transform.localScale = Vector3.one * (GlowSize * (0.3f + 0.7f * u) * (1f + Mathf.Sin(Time.time * 40f) * 0.08f));
            if (Random.value < 0.4f) {
                Fx.Burst(BeamEye, new Color(1f, 0.1f, 0.1f), 1, 1.5f * (1f - u) + 0.3f, 0.06f, 0.2f);
            }
            yield return null;
        }
        if (!isDead && player != null) {
            BloodBeam.Fire(BeamEye, player.BodyCenter, BeamSeconds, player, sprite);
            Sfx.Play("grenade_explode", Eye, 0.9f, 0.4f);
            for (float t = 0f; t < BeamSeconds && !isDead; t += Time.deltaTime) {
                glow.transform.position = BeamEye;
                glow.transform.localScale = Vector3.one * (GlowSize * 1.3f * (1f + Mathf.Sin(Time.time * 50f) * 0.12f));
                yield return null;
            }
        }
        Destroy(glow.gameObject);
        nextBeam = Time.time + BeamGap;
        quietUntil = Time.time + AfterBlowRest;
        special = false;
    }

    // the game's boss bar (Prefabs/HUD/Resources/BossHealth, taken from stage 1-9): the Boss
    // Health frame with two sliders, the life and a darker trail that follows it down
    private GameObject bar;
    private const float BarSpacing = 95f;   // canvas units between two bars (each is 132 x 0.75 tall)
    private const float BarBottom = 12f;
    private const float SingleBarBottom = 12f;   // canvas units: the version label is in the corner now
    private const float TouchBarScale = 0.6f;
    private const float TouchBarBottom = 12f;
    private const float TouchBarStep = 62f;     // a bar is 132 x 0.75 x 0.6 tall
    // set by the ArenaDirector right after it brings two in at once (Init and the bar come a frame later)
    [System.NonSerialized] public bool Paired;
    private Slider life;
    private Slider trail;

    private void ShowBar(int slot) {
        GameObject prefab = Resources.Load<GameObject>("BossHealth");
        Canvas canvas = null;
        if (UIManager.Instance != null && UIManager.Instance.healthBars != null && UIManager.Instance.healthBars.Length > 0 && UIManager.Instance.healthBars[0] != null) {
            canvas = UIManager.Instance.healthBars[0].canvas;
        }
        if (prefab == null || canvas == null) {
            return;
        }
        bar = Instantiate(prefab, canvas.rootCanvas.transform, false);
        bar.name = "BossHealth";
        RectTransform rect = bar.GetComponent<RectTransform>();
        if (rect != null && Paired) {
            // two at once: their bars go down at the bottom, one on the left and one on the right,
            // so the top stays clear (José, 2026-10-29)
            float side = slot % 2 == 0 ? 0.25f : 0.75f;
            rect.pivot = new Vector2(0.5f, 0f);
            if (TouchControls.Active) {
                // on a phone the corners are the d-pad and the buttons: smaller, one over the other
                // in the middle
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.localScale *= TouchBarScale;
                rect.anchoredPosition = new Vector2(0f, TouchBarBottom + TouchBarStep * (slot % 2));
            } else {
                rect.anchorMin = rect.anchorMax = new Vector2(side, 0f);
                rect.anchoredPosition = new Vector2(0f, BarBottom);
            }
        } else if (rect != null) {
            // one: at the bottom, over the arena floor (José,
            // 2026-10-29: the top is full of HUD); one more while another is out goes over it
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, SingleBarBottom + BarSpacing * slot);
        }
        foreach (Slider slider in bar.GetComponentsInChildren<Slider>(true)) {
            if (slider.name == "HealthBar") {
                life = slider;
            } else {
                trail = slider;
            }
            slider.interactable = false;
            slider.value = 1f;
        }
    }

    private void UpdateBar() {
        if (bar == null) {
            return;
        }
        bar.SetActive(!GameMenus.Paused);
        float value = Mathf.Clamp01((float)Health / Mathf.Max(1, maxHealth));
        if (life != null) {
            life.value = value;
        }
        if (trail != null) {
            trail.value = trail.value > value ? Mathf.MoveTowards(trail.value, value, 0.4f * Time.deltaTime) : value;
        }
    }

    protected override void LateUpdate() {
        base.LateUpdate();
        if (isDead && bar != null) {
            Destroy(bar);
        }
        UpdateBar();
    }
}
