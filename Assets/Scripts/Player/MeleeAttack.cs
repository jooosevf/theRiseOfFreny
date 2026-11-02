using System.Collections.Generic;
using UnityEngine;

// Hitbox used by Freny's sword (Sword_Hit_Box) and by the enemies' Hit_Box objects; the attack
// animation turns the collider on and off. Every target can be hit once per swing, so one swing
// can hit two enemies standing together, and each hit of Freny's 3-hit combo lands on the same enemy.
public class MeleeAttack : MonoBehaviour
{
    // Freny's sword landed a hit (FrenyMotion's hit-stop and sparks), at the contact point
    public static event System.Action<Vector2> FrenyHit;

    // how hard each combo hit pushes an enemy back (x its HitPush): small, small, big
    private static readonly float[] ComboPush = { 0.7f, 0.7f, 2.2f };

    private readonly Dictionary<IDamageable, int> lastHit = new Dictionary<IDamageable, int>(); // swing that hit it
    private bool ownerIsEnemy;
    private Player owner;
    private FrenyMotion motion;   // Freny's swing: no damage during its wind-up

    private void Awake() {
        ownerIsEnemy = GetComponentInParent<Enemy>() != null;
        owner = GetComponentInParent<Player>();
        if (owner) {
            HitBody.KeepAwake(this); // a sleeping sword body never reached a chest or a vase
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        TryHit(other);
    }

    // the Melee Attack clip keeps the hitbox on longer than the gap between combo hits, so an enemy
    // still inside it never "enters" again: Freny's sword also hits what stays in it (once per swing)
    private void OnTriggerStay2D(Collider2D other) {
        if (owner) {
            TryHit(other);
        }
    }

    private void TryHit(Collider2D other) {
        // enemies deal their damage from Enemy.AttackRoutine (wind-up + reach check), so their
        // hitboxes must not hurt anyone: before, a Licker's tongue could hit the Golem next to it
        if (ownerIsEnemy) {
            return;
        }

        if (owner) {
            if (motion == null) {
                motion = owner.GetComponent<FrenyMotion>();
            }
            if (motion && !motion.SwordLive) {
                return;
            }
        }

        // the sword hitbox overlaps Freny himself, and he is IDamageable too
        if (other.CompareTag("Player") || other.transform.IsChildOf(transform.root)) {
            return;
        }

        IDamageable hit = other.GetComponent<IDamageable>();
        if (hit == null) {
            return;
        }

        int swing = owner ? owner.SwingCount : 0;
        if (lastHit.TryGetValue(hit, out int hitBy) && hitBy == swing) {
            return;
        }
        if (lastHit.Count > 32) {
            lastHit.Clear(); // forget enemies that died long ago
        }
        lastHit[hit] = swing;
        int damage = owner ? owner.MeleeDamage : 1;
        int step = owner ? Mathf.Clamp(owner.ComboStep, 1, 3) : 1;
        if (hit is Enemy enemy) {
            // where the blade meets the body: the middle of the hitbox, pulled onto the enemy
            Collider2D blade = GetComponent<Collider2D>();
            Vector3 at = blade != null ? (Vector3)other.ClosestPoint(blade.bounds.center) : other.bounds.center;
            if (!enemy.IsDead) {
                Enemy.NextHitPower = WeaponLevels.Roll(WeaponLevels.SwordPower, at);   // the Armeira's buffs, crit
                Enemy.NextHitSource = owner && owner.Raging ? PlayerStats.WeaponFury
                    : owner && owner.GreatSwordActive ? PlayerStats.WeaponGreatSword : PlayerStats.WeaponSword;
            }
            enemy.Damage(damage, ComboPush[step - 1], at);
        } else {
            hit.Damage();
        }
        if (owner && hit is Enemy) {
            owner.OnMeleeHit();
        }
        FrenyHit?.Invoke(other.ClosestPoint(transform.position));
        // the finisher lands heavier: lower and louder
        Sfx.Play("sword_hit", other.transform.position, step == 3 ? 1.3f : 1f, step == 3 ? 0.8f : 1f);
    }
}
