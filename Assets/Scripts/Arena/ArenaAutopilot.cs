using System.Collections.Generic;
using UnityEngine;

// Plays Freny in the arena. It only produces a PlayerInputFrame, the same thing the keyboard
// produces, so it moves, jumps and attacks with the player's physics and cooldowns. It re-thinks
// every FrenyProgress.AutopilotReactionTime seconds, which the brain upgrades shorten.
//
// Priorities (GDD, "Arena idle"):
// 1. dodge an incoming projectile or a wind-up next to him
// 2. enemy in sword range -> sword (or step back while it recharges)
// 3. grenade at a group or at an enemy on a platform above
// 4. enemy aligned and far -> shot
// 5. low health and a potion -> drink it
// 6. coins and chests nearby when nobody is close
// 7. otherwise walk to the target; another platform is reached over the ArenaMap, one hop at a
//    time: stand beside the platform's edge, jump straight up, move onto it once the feet are
//    above it (or walk off an edge and fall). The hop is driven frame by frame, not per decision.
[RequireComponent(typeof(Player))]
public class ArenaAutopilot : MonoBehaviour, IPlayerInputSource {

    [SerializeField] private float verticalTolerance = 1.2f;   // "same floor" for sword and gun
    [SerializeField] private float jumpToReachHeight = 1.0f;   // target this much higher -> "above" (grenade, HUD)
    [SerializeField] private float dodgeWindow = 0.55f;        // dodge only when the projectile lands within this many seconds
    [SerializeField] private float dashCloseDistance = 5f;     // farther than this (same floor, sword ready) -> dash in
    [SerializeField] private float pickupRange = 7f;           // coins/chests this far away are worth a detour
    [SerializeField] private float grabRange = 4f;             // coins this close are grabbed even mid-fight
    [SerializeField] private float stuckSeconds = 3f;          // not getting closer to the target this long -> it's unreachable
    [SerializeField] private float grenadeAimError = 0.15f;    // +-15% on the charge: a person doesn't land every grenade on the head
    [SerializeField] private float dangerRange = 3f;
    [SerializeField] private float shotgunRange = 3.5f;        // closer than this the shotgun's fan lands all three pellets           // an enemy this close means no detours
    [SerializeField] private float jumpCooldown = 0.5f;        // no hopping: one jump, then land and think
    [SerializeField] private float unreachableMemory = 4f;     // seconds an enemy with no route is ignored
    [SerializeField] private float routeHopCost = 3f;          // one platform hop weighs this many metres when picking a target
    [SerializeField] private float maneuverTimeout = 3f;       // a climb or drop taking longer than this has failed
    [SerializeField] private int maneuverAttempts = 3;         // failed climbs before the target is given up for a while
    [SerializeField] private int lowHealth = 1;
    [SerializeField] private float scanInterval = 0.25f;

    private Player player;
    private Dash dash;
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<HexballEffect> projectiles = new List<HexballEffect>();
    private readonly Dictionary<Enemy, float> unreachableUntil = new Dictionary<Enemy, float>();
    private float nextScanTime;
    private float nextDecisionTime;
    private bool comboPress;                       // the last decision pressed the next combo hit
    private const float ComboRethink = 0.06f;      // decision gap while a sword combo is going
    private float nextJumpTime;
    private PlayerInputFrame plan;
    private bool planIsFresh;
    private bool planTurnsOnly;      // the move in the plan is a one-frame tap to turn around
    private Enemy currentTarget;     // kept between decisions, so he doesn't flip-flop between two enemies
    private bool retreating;         // decided once per sword cooldown
    private float bestTargetDistance = float.MaxValue; // closest he has been to the current target
    private float lastProgressTime;
    private Enemy progressTarget;
    private LayerMask ground;

    // the map of platforms and where he is on it
    private ArenaMap map;
    private ArenaMap.Platform standingOn;   // the last platform he stood on
    private int[] hops;                     // hops from standingOn to every platform, for this decision
    private bool wantsRoute;                // this decision asked for the current route

    // the hop being executed frame by frame; decisions only choose it
    private enum Phase { None, Walk, Jump, Steer, Fall }
    private Phase phase;
    private ArenaMap.Step route;
    private float phaseStart;
    private int attempts;
    private float walkBestDx;               // closest he has been to the stand spot (the walk is stuck when it stops shrinking)
    private Transform routeLoot;            // the coin/chest this route is for (null: it is for the enemy)
    private readonly Dictionary<Transform, float> lootUnreachableUntil = new Dictionary<Transform, float>();

    public string CurrentAction { get; private set; } = "";  // enemies, target and what he is doing (logged to the console)
    private string action = "";
    private string lastLogged = "";

    private void Awake() {
        player = GetComponent<Player>();
        dash = GetComponent<Dash>();
    }

    private void Start() {
        ground = player.whatIsGround;
        if (ground.value == 0) {
            ground = LayerMask.GetMask("Ground");
        }
    }

    private float FeetY => player.feetPos ? player.feetPos.position.y : transform.position.y;

    public PlayerInputFrame ReadInput() {
        if (player == null || player.IsDead) {
            CurrentAction = "";
            phase = Phase.None;
            return default;
        }

        if (dash == null) {
            dash = GetComponent<Dash>();
        }
        if (map == null && !BuildMap()) {
            return default;
        }
        TrackPlatform();

        if (Time.time >= nextScanTime) {
            Scan();
            nextScanTime = Time.time + scanInterval;
        }

        // in the air during a hop nothing is re-decided: the hop steers him until he lands
        bool airborne = phase == Phase.Jump || phase == Phase.Steer || phase == Phase.Fall;
        if (!airborne && Time.time >= nextDecisionTime) {
            planTurnsOnly = false;
            wantsRoute = false;
            plan = Decide();
            if (!wantsRoute) {
                phase = Phase.None; // the decision went another way: the route is dropped
                routeLoot = null;
            }
            CheckStuck();
            Announce();
            if (plan.jumpPressed) {
                if (Time.time < nextJumpTime) {
                    plan.jumpPressed = false; // landed a moment ago: walk this tick, jump on the next
                } else {
                    nextJumpTime = Time.time + jumpCooldown;
                }
            }
            planIsFresh = true;
            nextDecisionTime = Time.time + FrenyProgress.AutopilotReactionTime * Tuning.AutoReaction.Value;
            if (comboPress || player.ComboContinues) {
                // a combo is a rhythm, not a new decision: look again soon so the next hit isn't late
                nextDecisionTime = Mathf.Min(nextDecisionTime, Time.time + ComboRethink);
                comboPress = false;
            }
        }

        // reflexes, every frame and not only at decisions: the Ciclope's wave crosses his jump
        // window (about 0.35 s) faster than he re-thinks
        if (WaveReflex(out PlayerInputFrame jump)) {
            return jump;
        }
        // and the swing at the Ciclope's eye at the top of a jump (in the air his feet are off its
        // floor, so the decision's "same floor" test never let it through)
        if (EyeReflex(out PlayerInputFrame swing)) {
            return swing;
        }

        if (phase != Phase.None) {
            return ManeuverFrame();
        }

        PlayerInputFrame frame = plan;
        KeepOutOfAcid(ref frame);
        if (!planIsFresh) {
            // buttons are pressed once per decision, like a person tapping; movement and held jump continue
            frame.jumpPressed = false;
            frame.meleePressed = false;
            frame.rangedPressed = false;
            frame.dashPressed = false;
            frame.potionPressed = false;
            frame.grenadePressed = false;
            if (planTurnsOnly) {
                // turning around takes one frame; keeping the stick held walked him past the enemy
                frame.move = 0f;
            }
        }
        planIsFresh = false;
        return frame;
    }

    // the console keeps a readable history of every decision change (the HUD line was too small)
    private void Announce() {
        CurrentAction = Describe();
        if (CurrentAction != lastLogged) {
            Debug.Log($"[Autopiloto {Time.time:0.0}s] {CurrentAction}");
            lastLogged = CurrentAction;
        }
    }

    private bool BuildMap() {
        if (!ArenaBounds.Active) {
            return false;
        }
        map = ArenaMap.Shared(player);
        return map != null;
    }

    private void TrackPlatform() {
        if (!player.IsGrounded) {
            return;
        }
        ArenaMap.Platform under = map.PlatformUnder(new Vector2(transform.position.x, FeetY + 0.1f), 0.5f);
        if (under != null) {
            standingOn = under;
        }
    }

    // where he is for route planning: the platform he stands on, or the one he will land on
    private ArenaMap.Platform MyPlatform() {
        if (player.IsGrounded && standingOn != null) {
            return standingOn;
        }
        ArenaMap.Platform below = map.PlatformUnder(new Vector2(transform.position.x, FeetY + 0.1f), 100f);
        return below ?? standingOn;
    }

    private void Scan() {
        enemies.Clear();
        foreach (Enemy enemy in Enemy.Active) {
            if (enemy && !enemy.IsDead) {
                enemies.Add(enemy);
            }
        }

        projectiles.Clear();
        projectiles.AddRange(HexballEffect.Active);
    }

    private const float MaxMeleeReach = 1.8f;  // centre to centre, the normal sword

    // the shotgun up close while it has shells, the revolver farther away; false when neither can fire
    private bool Shoot(ref PlayerInputFrame frame, float dx, bool facingTarget, float towardTarget) {
        if (currentTarget is Shielded shield && shield.Guards(player.Feet)) {
            return false;   // a sixth of a point a shot: get behind it instead
        }
        bool shotgun = Mathf.Abs(dx) <= shotgunRange && player.GunReadyFor(true);
        bool revolver = !shotgun && player.GunReadyFor(false);
        if (!shotgun && !revolver) {
            return false;
        }
        if (!facingTarget) {
            Turn(ref frame, towardTarget);
        } else {
            frame.selectShotgun = shotgun;
            frame.selectPistol = !shotgun;
            frame.rangedPressed = true;
            action = shotgun ? "Tiro de escopeta" : "Tiro";
        }
        return true;
    }

    private PlayerInputFrame Decide() {
        PlayerInputFrame frame = new PlayerInputFrame();
        Vector2 me = transform.position;
        float myFeet = FeetY;
        ArenaMap.Platform myPlatform = MyPlatform();
        hops = map.HopsFrom(myPlatform);

        // 1. dodge, only when a projectile is about to land (before, any hexball nearby kept him
        // hopping forever instead of fighting)
        HexballEffect threat = IncomingProjectile(me);
        if (threat) {
            float awayFromThreat = Mathf.Sign(me.x - threat.transform.position.x);
            if (Dodge(ref frame, awayFromThreat, "o projétil")) {
                return frame;
            }
        }

        // 1a. the Ciclope's flood on the ground floor: up onto a platform until it drains
        if (FleeFlood(ref frame, me, myPlatform)) {
            return frame;
        }

        // 1b. the red Golem's mines, the Ciclope's shockwave and acid: get away or jump over
        if (AvoidHazards(ref frame, me, myFeet)) {
            return frame;
        }

        Enemy target = PickTarget(me, myFeet);
        if (target == null) {
            if (GoToPickup(ref frame, me, myFeet, myPlatform, pickupRange * 3f)) {
                return frame;
            }
            action = enemies.Count > 0 ? "Sem caminho até os inimigos" : "Esperando inimigos";
            return frame;
        }

        Vector2 targetPos = target.transform.position;
        float dx = targetPos.x - me.x;
        float dy = target.Feet.y - myFeet;
        float towardTarget = Mathf.Sign(dx);
        bool facingTarget = (dx > 0) == player.FacingRight;
        // mid-combo a target a little above (a Bat knocked up by the hit) is still worth the next hit
        bool sameFloor = dy <= (player.ComboContinues ? Mathf.Max(verticalTolerance, 1f) : verticalTolerance) && dy >= -0.7f;
        // never beyond what the sword really reaches (a saved 3.0 had him swinging at the air)
        float meleeRange = Mathf.Min(Tuning.AutoMeleeRange.Value, MaxMeleeReach) * (player.GreatSwordActive ? 1.4f : 1f);
        float nearestEnemy = NearestEnemyDistance(me);

        // 1c. the Ciclope: keep out of its reach and shoot, go in with the sword only while it
        // rests after a blow and there is time for a few hits and the way out
        if (target is Cyclops boss && boss.RestLeft < CyclopsEngageTime && sameFloor) {
            if (Mathf.Abs(dx) < CyclopsSafeDistance) {
                frame.move = -towardTarget;
                frame.runHeld = player.RunReady;
                action = "Saindo do alcance do Ciclope";
                return frame;
            }
            // waits just out of reach, so its rest leaves time to get to the eye (from far away he
            // never got there in time)
            if (Mathf.Abs(dx) > CyclopsSafeDistance + CyclopsWaitBand) {
                frame.move = towardTarget;
                frame.runHeld = Mathf.Abs(dx) > 6f && player.RunReady;
                action = "Chegando perto do Ciclope";
                return frame;
            }
            if (player.IsGrounded && Mathf.Abs(dx) <= Tuning.AutoShootRange.Value && Shoot(ref frame, dx, facingTarget, towardTarget)) {
                return frame;
            }
            action = "Esperando o Ciclope abrir a guarda";
            return frame;
        }

        // 1c'. it rests: jump at its eye and swing up there (double damage, José: "porrada no olho")
        if (target is Cyclops resting && sameFloor && Mathf.Abs(resting.EyePoint.x - me.x) <= meleeRange + EyeJumpReach) {
            float eyeY = resting.EyePoint.y;
            float bladeY = player.BodyCenter.y;
            // the eye is on its front face: its centre is about a metre farther in, behind the body
            float toEye = resting.EyePoint.x - me.x;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            bool falling = rb != null && rb.Vel().y <= 0.5f;
            if (player.IsGrounded && eyeY - bladeY > EyeTolerance && player.AttackReady && Time.time >= nextJumpTime) {
                frame.jumpPressed = true;
                frame.jumpHeld = true;
                frame.move = towardTarget;
                nextJumpTime = Time.time + jumpCooldown;
                action = "Pulando no olho do Ciclope";
                return frame;
            }
            if (!player.IsGrounded) {
                frame.jumpHeld = bladeY < eyeY;
                if (!facingTarget) {
                    Turn(ref frame, towardTarget);
                } else if ((bladeY >= eyeY - EyeTolerance || falling) && Mathf.Abs(toEye) <= meleeRange + 0.5f
                           && (player.AttackReady || player.ComboContinues)) {
                    // at the eye's height, or at the top of the jump if it falls short
                    frame.meleePressed = true;
                    action = "Espada no olho do Ciclope";
                } else {
                    frame.move = Mathf.Abs(toEye) > meleeRange * 0.7f ? Mathf.Sign(toEye) : 0f;
                    action = "No ar, mirando o olho";
                }
                return frame;
            }
        }

        // 1d. the Escudeiro's shield faces him: jump over it to hit its back (from above counts too)
        if (target is Shielded shield && sameFloor && Mathf.Abs(dx) <= ShieldJumpDistance
            && shield.Guards(player.Feet) && player.IsGrounded && Time.time >= nextJumpTime) {
            frame.jumpPressed = true;
            frame.jumpHeld = true;
            frame.move = towardTarget;
            action = "Pulando o escudo do Escudeiro";
            return frame;
        }

        // 1b. an enemy next to him is winding up a hit: get out of the way (a person would)
        Enemy striker = WindingUpNearby(me, myFeet, meleeRange + 0.6f);
        if (striker && (!player.AttackReady || player.Health <= lowHealth)) {
            float away = Mathf.Sign(me.x - striker.transform.position.x);
            if (Dodge(ref frame, away, "o golpe")) {
                return frame;
            }
        }

        // 2. sword
        if (sameFloor && Mathf.Abs(dx) <= meleeRange) {
            if (player.AttackReady) {
                retreating = false;
                if (!facingTarget) {
                    Turn(ref frame, towardTarget);
                } else {
                    frame.meleePressed = true;
                    action = "Golpe de espada";
                }
                return frame;
            }

            // mid-combo: press again just before the sword is ready (the swing buffer fires it), so
            // the last hit has landed and the target is known to be still alive
            if (player.ComboContinues && facingTarget) {
                frame.meleePressed = player.AttackReadyIn <= 0.08f; // otherwise wait a moment, no shot
                action = "Combo de espada";
                comboPress = true;
                return frame;
            }

            // the sword is recharging: the gun has its own cooldown, so shoot meanwhile
            if (player.IsGrounded && Shoot(ref frame, dx, facingTarget, towardTarget)) {
                return frame;
            }
            // nothing to shoot with: decide once whether to step back or hold ground
            if (!retreating && Random.value < Tuning.AutoKeepDistance.Value) {
                retreating = true;
            }
            if (retreating || player.Health <= lowHealth) {
                frame.move = -towardTarget;
                action = "Recuando na recarga";
            } else {
                action = "Esperando a recarga";
            }
            return frame;
        }
        retreating = false;

        // 2b. a flyer right over his head: shoot straight up
        if (target is Bat && Mathf.Abs(target.Center.x - me.x) <= 0.7f && target.Center.y - me.y > 0.9f
            && target.Center.y - me.y <= Tuning.AutoShootRange.Value && player.GunReadyFor(false)) {
            frame.aimUpHeld = true;
            frame.selectPistol = true;
            frame.rangedPressed = true;
            action = "Tiro para cima";
            return frame;
        }

        // 3. grenade (they are counted now: found in chests or bought): a group, an enemy on a
        // platform above, or one he can't reach at all (a sorcerer on a tower), lobbed with the
        // charge that lands there
        if (player.CanThrowGrenade && player.IsGrounded) {
            Enemy lob = GrenadeTarget(me, target, out string why);
            if (lob != null) {
                float lobDx = lob.transform.position.x - me.x;
                float charge = player.GrenadeChargeFor(lobDx, lob.Center.y - (me.y + 0.6f));
                if ((lobDx > 0) != player.FacingRight) {
                    Turn(ref frame, Mathf.Sign(lobDx));
                } else {
                    frame.grenadePressed = true;
                    frame.grenadeCharge = Mathf.Clamp01(charge * Random.Range(1f - grenadeAimError, 1f + grenadeAimError));
                    action = why;
                }
                return frame;
            }
        }

        // 4. gun (only from the ground): the shotgun up close while it has shells, the revolver
        // farther away; with no ammo at all it is the sword only
        if (sameFloor && player.IsGrounded && Mathf.Abs(dx) <= Tuning.AutoShootRange.Value
            && Shoot(ref frame, dx, facingTarget, towardTarget)) {
            return frame;
        }

        // 5. potion
        if (FrenyProgress.HasAutoPotion && player.Health <= lowHealth && player.HasPotion) {
            frame.potionPressed = true;
            action = "Bebendo poção";
            return frame;
        }

        // 6. loot: a coin within arm's reach is grabbed even mid-fight; farther loot only when nobody is close
        // (with a Ciclope out only what is within arm's reach: he lost its rests to crates far away)
        float lootRange = Cyclops.Current != null ? grabRange : nearestEnemy > dangerRange ? pickupRange : grabRange;
        if (GoToPickup(ref frame, me, myFeet, myPlatform, lootRange)) {
            return frame;
        }

        // 7. go to the target: walk when it stands on his platform (or flies at his height), else hop
        // towards its platform over the map
        ArenaMap.Platform targetPlatform = PlatformOf(target);
        bool flying = targetPlatform == null;
        bool atBodyHeight = Mathf.Abs(target.Center.y - (me.y + 0.5f)) <= verticalTolerance;
        bool walkable = flying ? atBodyHeight : targetPlatform == myPlatform;
        if (walkable) {
            bool dashIn = Mathf.Abs(dx) > dashCloseDistance && player.AttackReady && striker == null && dash && dash.CanDash; // CanDash: only in the air
            if (dashIn) {
                if (!facingTarget) {
                    Turn(ref frame, towardTarget);
                } else {
                    frame.dashPressed = true;
                    frame.move = towardTarget;
                    action = "Dash até o inimigo";
                }
                return frame;
            }
            frame.move = towardTarget;
            frame.runHeld = Mathf.Abs(dx) > 4f && player.RunReady;
            action = frame.runHeld ? "Correndo até o inimigo" : "Indo até o inimigo";
            return frame;
        }

        if (flying) {
            // a bat above or below him comes down by itself: no route, and never "unreachable"
            action = "Esperando o inimigo voador";
            return frame;
        }
        routeLoot = null;
        if (RequestRoute(myPlatform, targetPlatform, me.x)) {
            return frame; // the hop drives the frame
        }

        // no route there: forget this enemy for a while and fight whoever else is around
        unreachableUntil[target] = Time.time + unreachableMemory;
        currentTarget = null;
        action = "Sem caminho, trocando de alvo";
        return frame;
    }

    // ---- hazards ----

    private const float CyclopsSafeDistance = 3.2f;   // its punch and the start of the smash
    private const float CyclopsEngageTime = 0.9f;     // its rest must last this long to go in
    private const float EyeJumpReach = 1.2f;          // how far out he jumps for the eye
    private const float EyeTolerance = 0.45f;         // the eye's radius: the blade must be about there
    // while a Ciclope is out it comes first (José, 2026-10-29: he went after everything else); the
    // others only when they are right on him (the bat and bug that keep his combo alive)
    private const float BossFirst = 25f;
    private const float OthersDuringBoss = 15f;
    private const float CloseEnough = 3f;
    private const float CyclopsWaitBand = 1f;         // waits between SafeDistance and this much farther
    private const float ShieldJumpDistance = 2.4f;    // close enough to jump over the Escudeiro
    private const float MineKeepOut = 0.8f;           // beyond the blast radius
    private const float WaveJumpAt = 3.2f;            // a shockwave this close: jump now

    private const float WaveReactTime = 0.3f;   // jump when the wave will reach him this soon

    private const float HexReactTime = 0.25f;   // a hexball this close in time: jump it now

    private bool EyeReflex(out PlayerInputFrame frame) {
        frame = default;
        if (player.IsGrounded || !(currentTarget is Cyclops boss) || boss.IsDead || boss.RestLeft <= 0f) {
            return false;
        }
        Vector2 me = transform.position;
        Vector3 eye = boss.EyePoint;
        float toEye = eye.x - me.x;
        float meleeRange = Mathf.Min(Tuning.AutoMeleeRange.Value, MaxMeleeReach) * (player.GreatSwordActive ? 1.4f : 1f);
        if (Mathf.Abs(toEye) > meleeRange + EyeJumpReach) {
            return false;
        }
        float bladeY = player.BodyCenter.y;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        bool falling = rb != null && rb.Vel().y <= 0.5f;
        frame.jumpHeld = bladeY < eye.y;
        frame.move = Mathf.Abs(toEye) > meleeRange * 0.7f ? Mathf.Sign(toEye) : 0f;
        bool facing = (toEye > 0f) == player.FacingRight;
        if (!facing) {
            frame.move = Mathf.Sign(toEye);
            action = "No ar, virando para o olho";
            return true;
        }
        if ((bladeY >= eye.y - EyeTolerance || falling) && Mathf.Abs(toEye) <= meleeRange + 0.5f && player.AttackReady) {
            frame.meleePressed = true;
            action = "Espada no olho do Ciclope";
        } else {
            action = "No ar, mirando o olho";
        }
        return true;
    }

    private bool WaveReflex(out PlayerInputFrame frame) {
        frame = default;
        if (!player.IsGrounded) {
            return false;
        }
        Vector2 me = transform.position;
        float myFeet = FeetY;
        // the sorcerers' hexballs (the Ciclope's helpers took 2/3 of his life): a jump away at the
        // last moment, since the decisions come too far apart to catch them
        Vector2 body = me + Vector2.up * 0.5f;
        foreach (HexballEffect ball in HexballEffect.Active) {
            if (!ball) {
                continue;
            }
            Vector2 pos = ball.transform.position;
            Vector2 dir = ((Vector2)ball.Direction).normalized;
            Vector2 toMe = body - pos;
            float along = Vector2.Dot(toMe, dir);
            float miss = Mathf.Abs(toMe.x * dir.y - toMe.y * dir.x);
            if (along > 0f && miss <= 0.8f && along / Mathf.Max(0.1f, ball.Speed) <= HexReactTime && Time.time >= nextJumpTime) {
                if (Mathf.Abs(dir.y) < 0.5f) {
                    // a level shot: jump straight over it
                    frame.jumpPressed = true;
                    frame.jumpHeld = true;
                    nextJumpTime = Time.time + jumpCooldown;
                    action = "Pulando o feitiço";
                } else {
                    // from above (a tower): run out from under it
                    float away = Mathf.Sign(me.x - pos.x);
                    frame.move = away == 0f ? (player.FacingRight ? 1f : -1f) : away;
                    frame.runHeld = player.RunReady;
                    action = "Saindo de baixo do feitiço";
                }
                return true;
            }
        }
        foreach (Shockwave wave in Shockwave.Active) {
            if (wave == null) {
                continue;
            }
            float toMe = me.x - wave.transform.position.x;
            bool coming = Mathf.Sign(toMe) == Mathf.Sign(wave.Direction);
            if (coming && Mathf.Abs(toMe) <= Shockwave.Speed * WaveReactTime + 0.6f && Mathf.Abs(myFeet - wave.FloorY) < 0.6f) {
                frame.jumpPressed = true;
                frame.jumpHeld = true;
                nextJumpTime = Time.time + jumpCooldown;
                action = "Pulando a onda do Ciclope";
                return true;
            }
        }
        return false;
    }

    // never walk into the acid (he used to step out, then run back in after the Ciclope)
    private void KeepOutOfAcid(ref PlayerInputFrame frame) {
        if (frame.move == 0f || !player.IsGrounded) {
            return;
        }
        float me = transform.position.x;
        float myFeet = FeetY;
        foreach (AcidPuddle puddle in AcidPuddle.Active) {
            if (puddle == null || puddle.IsFlood) {
                continue;
            }
            Vector2 at = puddle.transform.position;
            float half = puddle.Width / 2f;
            bool inside = Mathf.Abs(me - at.x) < half;
            bool ahead = Mathf.Sign(at.x - me) == Mathf.Sign(frame.move) && Mathf.Abs(me - at.x) < half + 0.8f;
            if (!inside && ahead && Mathf.Abs(myFeet - at.y) < 0.6f) {
                frame.move = 0f;
                frame.runHeld = false;
                return;
            }
        }
    }

    // the whole ground floor is flooded, or the Ciclope has started the vomit that floods it:
    // climb to the nearest platform; already up on one, wait there instead of dropping back into it
    private bool FleeFlood(ref PlayerInputFrame frame, Vector2 me, ArenaMap.Platform myPlatform) {
        if (!(AcidPuddle.Flooding || Cyclops.FloodComing) || map.Floor == null || myPlatform == null) {
            return false;
        }
        if (myPlatform != map.Floor) {
            if (!player.IsGrounded) {
                return false;
            }
            action = "Esperando a enchente baixar";
            return true;
        }
        ArenaMap.Platform best = null;
        float bestScore = float.PositiveInfinity;
        foreach (ArenaMap.Platform platform in map.Platforms) {
            int count = HopsTo(platform);
            if (platform == map.Floor || platform.tower || count == int.MaxValue) {
                continue;
            }
            float score = count * 10f + Mathf.Abs((platform.xMin + platform.xMax) / 2f - me.x);
            if (score < bestScore) {
                bestScore = score;
                best = platform;
            }
        }
        if (best != null && RequestRoute(myPlatform, best, me.x)) {
            action = "Fugindo da enchente: " + action;
            return true;
        }
        return false;
    }

    private bool AvoidHazards(ref PlayerInputFrame frame, Vector2 me, float myFeet) {
        // a mine near him: run out of its blast (it goes off 0.9 s after it lands, or on touch)
        foreach (GolemMine mine in GolemMine.Active) {
            if (mine == null) {
                continue;
            }
            Vector2 at = mine.transform.position;
            float dx = me.x - at.x;
            if (Mathf.Abs(dx) < mine.Blast + MineKeepOut && Mathf.Abs(myFeet - at.y) < mine.Blast) {
                float away = Mathf.Abs(dx) < 0.05f ? (player.FacingRight ? -1f : 1f) : Mathf.Sign(dx);
                frame.move = away;
                frame.runHeld = player.RunReady;
                // right on top of it: jump away instead of walking through the blast
                if (Mathf.Abs(dx) < 1f && player.IsGrounded && Time.time >= nextJumpTime) {
                    frame.jumpPressed = true;
                    frame.jumpHeld = true;
                }
                action = mine.Landed ? "Fugindo da mina" : "Saindo de onde a mina vai cair";
                return true;
            }
        }
        // a shockwave coming along his floor: jump it at the last moment
        foreach (Shockwave wave in Shockwave.Active) {
            if (wave == null) {
                continue;
            }
            float toMe = me.x - wave.transform.position.x;
            bool coming = Mathf.Sign(toMe) == Mathf.Sign(wave.Direction);
            if (coming && Mathf.Abs(toMe) < WaveJumpAt && Mathf.Abs(myFeet - wave.FloorY) < 0.6f
                && player.IsGrounded && Time.time >= nextJumpTime) {
                frame.jumpPressed = true;
                frame.jumpHeld = true;
                action = "Pulando a onda do Ciclope";
                return true;
            }
        }
        // standing in (or at the edge of) the acid: step out the shorter way
        foreach (AcidPuddle puddle in AcidPuddle.Active) {
            if (puddle == null || puddle.IsFlood) {
                continue;
            }
            Vector2 at = puddle.transform.position;
            float dx = me.x - at.x;
            if (Mathf.Abs(dx) < puddle.Width / 2f + 0.4f && Mathf.Abs(myFeet - at.y) < 0.6f) {
                frame.move = dx >= 0f ? 1f : -1f;
                frame.runHeld = player.RunReady;
                action = "Saindo do ácido";
                return true;
            }
        }
        return false;
    }

    // ---- actions ----

    private void Turn(ref PlayerInputFrame frame, float direction) {
        frame.move = direction;
        planTurnsOnly = true;
        action = "Virando";
    }

    private bool Dodge(ref PlayerInputFrame frame, float away, string what) {
        bool facingAway = (away > 0) == player.FacingRight;
        if (FrenyProgress.HasDashDodge && facingAway && dash && dash.CanDash) {
            frame.dashPressed = true;
            action = "Esquivando com dash de " + what;
        } else if ((player.IsGrounded && Time.time >= nextJumpTime) || player.CanAirJump) {
            frame.jumpPressed = true;
            frame.jumpHeld = true;
            frame.move = away;
            action = "Pulando " + what;
        } else {
            return false;
        }
        return true;
    }

    // ---- hops between platforms ----

    // asks the map for the first hop towards `destination` and keeps (or starts) that hop
    private bool RequestRoute(ArenaMap.Platform from, ArenaMap.Platform destination, float fromX) {
        if (!map.FirstStep(from, destination, fromX, out ArenaMap.Step step)) {
            return false;
        }
        wantsRoute = true;
        if (phase == Phase.None || !route.Same(step)) {
            route = step;
            attempts = 0;
            SetPhase(Phase.Walk);
            Debug.Log($"[Autopiloto {Time.time:0.0}s] Rota: {step}");
        }
        action = route.kind == ArenaMap.StepKind.Climb
            ? $"Indo até a beirada para subir em {route.to}"
            : $"Indo até a beirada para descer para {route.to}";
        return true;
    }

    private void SetPhase(Phase next) {
        phase = next;
        phaseStart = Time.time;
        lastProgressTime = Time.time;
        if (next == Phase.Walk) {
            walkBestDx = float.MaxValue;
        }
    }

    private void FinishRoute(string how) {
        Debug.Log($"[Autopiloto {Time.time:0.0}s] {how} em {route.to}");
        phase = Phase.None;
        routeLoot = null;
        plan = default;
        nextDecisionTime = Time.time; // think again right away
    }

    // the thing the route was for is forgotten for a while: the loot when he was after loot,
    // else the enemy (before, a failed coin route blamed an enemy standing right next to him)
    private void FailRoute(string why) {
        Debug.Log($"[Autopiloto {Time.time:0.0}s] Rota abandonada ({why}): {route}");
        if (routeLoot != null) {
            lootUnreachableUntil[routeLoot] = Time.time + unreachableMemory;
        } else if (currentTarget != null) {
            unreachableUntil[currentTarget] = Time.time + unreachableMemory;
            currentTarget = null;
        }
        routeLoot = null;
        phase = Phase.None;
        plan = default;
        nextDecisionTime = Time.time;
    }

    // one frame of the current hop. The jump is straight up beside the platform and he only moves
    // onto it once his feet are above it: jumping diagonally at the edge bumped the body into the
    // platform's side and the jump died there (the cause of the endless pacing under a platform)
    private PlayerInputFrame ManeuverFrame() {
        PlayerInputFrame frame = new PlayerInputFrame();
        float x = transform.position.x;
        float feet = FeetY;
        bool grounded = player.IsGrounded;
        float elapsed = Time.time - phaseStart;

        switch (phase) {
            case Phase.Walk: {
                float dx = route.x - x;
                if (route.kind == ArenaMap.StepKind.Climb) {
                    if (Mathf.Abs(dx) > 0.08f) {
                        frame.move = Mathf.Sign(dx); // no sprint here: it is saved for the move onto the platform
                        action = $"Indo até a beirada para subir em {route.to}";
                    } else if (grounded && Time.time >= nextJumpTime) {
                        frame.jumpPressed = true;
                        nextJumpTime = Time.time + jumpCooldown;
                        SetPhase(Phase.Jump);
                        action = $"Pulando reto ao lado de {route.to}";
                    } else {
                        action = "Na beirada, esperando para pular";
                    }
                } else {
                    frame.move = route.direction;
                    action = $"Indo até a beirada para descer para {route.to}";
                    if (!grounded) {
                        SetPhase(Phase.Fall);
                        action = $"Caindo para {route.to}";
                    }
                }
                // stuck only when he stops getting closer to the spot (a far edge takes longer than any fixed time)
                float remaining = Mathf.Abs(route.x - x);
                if (remaining < walkBestDx - 0.25f) {
                    walkBestDx = remaining;
                    phaseStart = Time.time;
                    elapsed = 0f;
                }
                if (elapsed > maneuverTimeout) {
                    FailRoute("não chegou à beirada");
                }
                break;
            }
            case Phase.Jump: {
                frame.move = 0f; // straight up
                action = $"Pulando reto ao lado de {route.to}";
                // move onto the platform as soon as the feet will be above it by the time the body's
                // side reaches its edge (waiting until they are above it already lost too much of the
                // jump: on the floor-to-platform step he landed on the very edge)
                float edgeX = route.direction > 0f ? route.to.xMin : route.to.xMax;
                float sideGap = Mathf.Max(0f, route.direction > 0f ? edgeX - (x + player.BodyHalfWidth) : (x - player.BodyHalfWidth) - edgeX);
                float predictedFeet = feet + Mathf.Max(0f, player.VerticalSpeed) * (sideGap / Mathf.Max(0.1f, player.MaxSpeed));
                if (predictedFeet > route.to.y + 0.03f) {
                    SetPhase(Phase.Steer);
                } else if (elapsed > 0.35f && grounded) {
                    // the jump didn't happen (landed at the same moment, cooldown...): try again
                    attempts++;
                    if (attempts >= maneuverAttempts) {
                        FailRoute("o pulo não saiu");
                    } else {
                        SetPhase(Phase.Walk);
                    }
                } else if (elapsed > 1.5f) {
                    FailRoute("o pulo não chegou lá");
                }
                break;
            }
            case Phase.Steer: {
                frame.move = route.direction;
                frame.runHeld = true; // the sprint, when ready, buys room over the edge before he drops
                action = $"Entrando em {route.to}";
                // the feet circle touches the top a little before he rests on it, so landing also
                // means the body stopped falling (else he could turn and walk off the edge in mid-air)
                bool resting = grounded && Mathf.Abs(player.VerticalSpeed) < 0.5f && feet <= route.to.y + 0.1f;
                ArenaMap.Platform landed = resting ? map.PlatformUnder(new Vector2(x, feet + 0.1f), 0.5f) : null;
                if (landed != null && elapsed > 0.1f) {
                    if (landed == route.to) {
                        FinishRoute("Subiu");
                    } else {
                        attempts++;
                        if (attempts >= maneuverAttempts) {
                            FailRoute("não conseguiu entrar na plataforma");
                        } else {
                            SetPhase(Phase.Walk);
                        }
                    }
                } else if (!grounded && player.VerticalSpeed < 0f && feet < route.to.y - 0.2f && player.CanAirJump) {
                    frame.jumpPressed = true; // falling short: the double jump saves the climb
                } else if (elapsed > maneuverTimeout) {
                    FailRoute("demorou demais para entrar");
                }
                break;
            }
            case Phase.Fall: {
                frame.move = 0f; // straight down, onto the platform the map promised
                action = $"Caindo para {route.to}";
                bool resting = grounded && Mathf.Abs(player.VerticalSpeed) < 0.5f;
                ArenaMap.Platform landed = resting ? map.PlatformUnder(new Vector2(x, feet + 0.1f), 0.5f) : null;
                if (landed != null && elapsed > 0.1f) {
                    FinishRoute("Desceu");
                } else if (elapsed > maneuverTimeout) {
                    FailRoute("a queda não terminou");
                }
                break;
            }
        }

        Announce();
        return frame;
    }

    // the platform an enemy stands on (null for one in the air: a bat, or one still falling)
    private ArenaMap.Platform PlatformOf(Enemy enemy) {
        Vector3 feet = enemy.Feet;
        return map.PlatformUnder(new Vector2(feet.x, feet.y + 0.1f), 1.5f);
    }

    private bool GoToPickup(ref PlayerInputFrame frame, Vector2 me, float myFeet, ArenaMap.Platform myPlatform, float range) {
        Vector2 best = Vector2.zero;
        Transform bestItem = null;
        ArenaMap.Platform bestPlatform = null;
        float bestDist = float.MaxValue;
        string what = "";
        foreach (Coin coin in Coin.Active) {
            Consider(coin ? coin.transform : null, me, myPlatform, range, ref best, ref bestItem, ref bestPlatform, ref bestDist, ref what, "a moeda");
        }
        foreach (ArenaChest chest in ArenaChest.Active) {
            Consider(chest ? chest.transform : null, me, myPlatform, range, ref best, ref bestItem, ref bestPlatform, ref bestDist, ref what, "o baú");
        }
        foreach (ArenaVase vase in ArenaVase.Active) {
            Consider(vase ? vase.transform : null, me, myPlatform, range, ref best, ref bestItem, ref bestPlatform, ref bestDist, ref what, "o vaso");
        }
        foreach (Breakable prop in Breakable.Active) {
            if (prop && !prop.Broken) {
                Consider(prop.transform, me, myPlatform, range, ref best, ref bestItem, ref bestPlatform, ref bestDist, ref what, prop.Describe());
            }
        }
        foreach (ItemPickup item in ItemPickup.Active) {
            if (item && item.Landed && item.UsefulTo(player)) {
                // items count as closer than coins: a potion or ammo matters more than a coin
                Consider(item.transform, me, myPlatform, range + 2f, ref best, ref bestItem, ref bestPlatform, ref bestDist, ref what, item.Describe());
            }
        }
        if (bestDist == float.MaxValue) {
            return false;
        }
        bool isChest = what == "o baú" || what == "o vaso" || what == "a caixa" || what == "o barril";   // they open only when hit
        float dx = best.x - me.x;

        if (bestPlatform != myPlatform) {
            routeLoot = bestItem;
            if (RequestRoute(myPlatform, bestPlatform, me.x)) {
                action = "Pegando " + what + ": " + action;
                return true;
            }
            return false;
        }
        if (isChest && Mathf.Abs(dx) <= Tuning.AutoMeleeRange.Value) {
            // the chest only opens when hit
            if ((dx > 0) != player.FacingRight) {
                Turn(ref frame, Mathf.Sign(dx));
            } else if (player.AttackReady) {
                frame.meleePressed = true;
            }
            action = "Quebrando " + what;
            return true;
        }
        if (Mathf.Abs(dx) < 0.15f) {
            return false;
        }
        frame.move = Mathf.Sign(dx);
        action = "Pegando " + what;
        return true;
    }

    // Travelling towards the target without ever getting closer for stuckSeconds means the route is
    // blocked (a wall, a ledge he can't reach, pacing above it): forget that target for a while.
    // Pacing moves him, so progress is measured as distance to the target, not as movement. A hop in
    // progress has its own timeouts, so it never counts as being stuck.
    private void CheckStuck() {
        if (currentTarget != progressTarget) {
            progressTarget = currentTarget;
            bestTargetDistance = float.MaxValue;
            lastProgressTime = Time.time;
        }
        if (currentTarget == null) {
            return;
        }
        if (phase != Phase.None) {
            lastProgressTime = Time.time;
            return;
        }
        bool travelling = action.StartsWith("Indo") || action.StartsWith("Correndo");
        float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
        if (!travelling || distance < bestTargetDistance - 0.3f) {
            bestTargetDistance = Mathf.Min(bestTargetDistance, distance);
            lastProgressTime = Time.time;
            return;
        }
        if (Time.time - lastProgressTime > stuckSeconds) {
            unreachableUntil[currentTarget] = Time.time + unreachableMemory;
            currentTarget = null;
            action = "Sem chegar perto, trocando de alvo";
        }
    }

    // "3 inimigos, alvo Golem (acima, 4 m à direita) · Indo até a beirada para subir em P3 (y 8,2, x 20,6 a 23,6)"
    private string Describe() {
        string status = enemies.Count == 1 ? "1 inimigo" : enemies.Count + " inimigos";
        if (currentTarget) {
            float dx = currentTarget.transform.position.x - transform.position.x;
            float dy = currentTarget.Feet.y - FeetY;
            string level = dy > jumpToReachHeight ? "acima" : dy < -verticalTolerance ? "abaixo" : "mesmo andar";
            string side = dx >= 0f ? "à direita" : "à esquerda";
            status += $", alvo {currentTarget.GetType().Name} ({level}, {Mathf.Abs(dx):0} m {side})";
        }
        if (standingOn != null) {
            status += $", em {standingOn}";
        }
        return status + " · " + action;
    }

    private void Consider(Transform item, Vector2 me, ArenaMap.Platform myPlatform, float range, ref Vector2 best, ref Transform bestItem,
                          ref ArenaMap.Platform bestPlatform, ref float bestDist, ref string what, string name) {
        if (item == null) {
            return;
        }
        if (lootUnreachableUntil.TryGetValue(item, out float until) && Time.time < until) {
            return; // a route to it failed a moment ago
        }
        Vector2 pos = item.position;
        ArenaMap.Platform platform = map.PlatformUnder(pos, 3f);
        if (platform == null) {
            return; // still in the air (a tossed coin): wait for it to land
        }
        int hopCount = platform == myPlatform ? 0 : HopsTo(platform);
        if (hopCount == int.MaxValue) {
            return;
        }
        float dist = Mathf.Abs(pos.x - me.x) + routeHopCost * hopCount;
        if (dist <= range && dist < bestDist) {
            bestDist = dist;
            best = pos;
            bestItem = item;
            bestPlatform = platform;
            what = name;
        }
    }

    private int HopsTo(ArenaMap.Platform platform) {
        return hops != null && platform != null && platform.index < hops.Length ? hops[platform.index] : int.MaxValue;
    }

    // ---- perception ----

    // a hexball that will pass within a body's width of Freny in the next dodgeWindow seconds
    private HexballEffect IncomingProjectile(Vector2 me) {
        Vector2 body = me + Vector2.up * 0.5f;
        foreach (HexballEffect projectile in projectiles) {
            if (!projectile) {
                continue;
            }
            Vector2 pos = projectile.transform.position;
            Vector2 dir = ((Vector2)projectile.Direction).normalized;
            Vector2 toMe = body - pos;
            float along = Vector2.Dot(toMe, dir);           // distance still to travel towards him
            if (along < 0f || along > Tuning.AutoDodgeRange.Value) {
                continue;                                    // moving away, or still far
            }
            float miss = Mathf.Abs(toMe.x * dir.y - toMe.y * dir.x); // closest approach to his body
            float timeToHit = along / Mathf.Max(0.1f, projectile.Speed);
            if (miss <= 0.8f && timeToHit <= dodgeWindow) {
                return projectile;
            }
        }
        return null;
    }

    private Enemy WindingUpNearby(Vector2 me, float myFeet, float range) {
        foreach (Enemy enemy in enemies) {
            if (!enemy || !enemy.IsAttacking) {
                continue;
            }
            if (Mathf.Abs(enemy.transform.position.x - me.x) <= range && Mathf.Abs(enemy.Feet.y - myFeet) <= verticalTolerance) {
                return enemy;
            }
        }
        return null;
    }

    private float NearestEnemyDistance(Vector2 me) {
        float nearest = float.MaxValue;
        foreach (Enemy enemy in enemies) {
            if (enemy) {
                nearest = Mathf.Min(nearest, Vector2.Distance(enemy.transform.position, me));
            }
        }
        return nearest;
    }

    private int CountEnemiesNear(Vector2 point, float range) {
        int count = 0;
        foreach (Enemy enemy in enemies) {
            if (enemy && Vector2.Distance(enemy.transform.position, point) <= range) {
                count++;
            }
        }
        return count;
    }

    // who deserves a grenade: the current target when it stands in a group or above him, else an
    // enemy he has no route to (it would never be fought otherwise). Only within a lob's reach.
    private Enemy GrenadeTarget(Vector2 me, Enemy target, out string why) {
        why = "";
        if (target != null && InLobRange(me, target)) {
            if (CountEnemiesNear(target.transform.position, 2.5f) >= 2) {
                why = "Granada no grupo";
                return target;
            }
            if (target.Feet.y - FeetY > jumpToReachHeight) {
                why = "Granada na plataforma";
                return target;
            }
        }
        Enemy best = null;
        float bestDistance = float.MaxValue;
        foreach (Enemy enemy in enemies) {
            if (!enemy || enemy.IsDead || !InLobRange(me, enemy)) {
                continue;
            }
            ArenaMap.Platform platform = PlatformOf(enemy);
            if (platform == null || HopsTo(platform) != int.MaxValue) {
                continue; // reachable (or flying): the sword and the gun will do
            }
            float distance = Mathf.Abs(enemy.transform.position.x - me.x);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = enemy;
            }
        }
        if (best != null) {
            why = TargetLabel(best) + ": granada";
        }
        return best;
    }

    private string TargetLabel(Enemy enemy) {
        ArenaMap.Platform platform = PlatformOf(enemy);
        return platform != null && platform.tower ? $"{enemy.GetType().Name} na torre" : $"{enemy.GetType().Name} fora de alcance";
    }

    private bool InLobRange(Vector2 me, Enemy enemy) {
        float dx = enemy.transform.position.x - me.x;
        // never so close that Freny would be caught in his own blast
        if (Mathf.Abs(dx) < Grenade.BlastRadius + 1f || Mathf.Abs(dx) > 8f) {
            return false;
        }
        return player.GrenadeChargeFor(dx, enemy.Center.y - (me.y + 0.6f)) >= 0f;
    }

    // Enemies on his own platform come first; each hop over the map to another platform costs
    // routeHopCost metres, and a platform with no route is only a target when nothing else is
    // around. The current target is kept unless another is clearly better, so he doesn't
    // flip-flop between two. Enemies recently found unreachable are skipped. Smart targeting also
    // prefers enemies that are almost dead.
    private Enemy PickTarget(Vector2 me, float myFeet) {
        Enemy best = null;
        float bestScore = float.MaxValue;
        float currentScore = float.MaxValue;

        foreach (Enemy enemy in enemies) {
            if (!enemy || enemy.IsDead) {
                continue;
            }
            if (unreachableUntil.TryGetValue(enemy, out float until) && Time.time < until) {
                continue;
            }
            Vector2 offset = (Vector2)enemy.transform.position - me;
            float dy = enemy.Feet.y - myFeet;
            float score = Mathf.Abs(offset.x);
            ArenaMap.Platform platform = PlatformOf(enemy);
            if (platform == null) {
                // in the air (a bat): worth it only at his height, it comes to him anyway
                score += Mathf.Abs(dy) <= verticalTolerance ? 1f : 50f;
            } else {
                int hopCount = HopsTo(platform);
                score += hopCount == int.MaxValue ? 50f : routeHopCost * hopCount;
            }

            if (FrenyProgress.HasSmartTarget && !(enemy is Cyclops)) {
                score += 1.5f * enemy.Health; // prefers the ones that die fastest
            }
            if (Cyclops.Current != null) {
                if (enemy is Cyclops) {
                    score -= BossFirst;
                } else if (offset.magnitude > CloseEnough) {
                    score += OthersDuringBoss;
                }
            }

            if (enemy == currentTarget) {
                currentScore = score;
            }
            if (score < bestScore) {
                bestScore = score;
                best = enemy;
            }
        }

        if (currentTarget != null && !currentTarget.IsDead && currentScore < float.MaxValue && currentScore <= bestScore + 2f) {
            return currentTarget;
        }
        currentTarget = best;
        return best;
    }
}
