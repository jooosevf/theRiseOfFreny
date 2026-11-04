using System.Collections;
using UnityEngine;

// Thin wrapper over Freny's Animator, so Player and Dash never spell parameter names.
// Player_Animator parameters in use:
//   bools     Moving, isJumping, Stunned
//   triggers  Melee, Ranged, Dash, Hit, Death
public class PlayerAnimation : MonoBehaviour {

    private Animator _anim;
    private FrenyMotion _motion;   // procedural motion on top of the frames

    private void Awake() {
        _anim = GetComponentInChildren<Animator>();
        _motion = GetComponent<FrenyMotion>();
        if (_motion == null) {
            _motion = gameObject.AddComponent<FrenyMotion>();
        }
    }

    public void Move(bool moving) {
        _anim.SetBool("Moving", moving);
    }

    // true while airborne (Jump state), false on the ground (Landing -> Idle/Walk)
    public void Jump(bool inAir) {
        _anim.SetBool("isJumping", inAir);
    }

    // knocked down: Any State -> Duck while true, Duck -> Idle when it clears
    public void Stunned(bool stunned) {
        if (stunned) {
            _anim.ResetTrigger("Hit"); // the crouch replaces the flinch
        }
        _anim.SetBool("Stunned", stunned);
    }

    // the sword's hitbox is switched on by the Melee Attack clip, and only Idle, Walk, Jump and
    // Landing have a Melee transition: a swing during the 0.8 s Hit flinch, a shot or the previous
    // swing lost its trigger, so the sword did nothing after he was hurt. The state plays directly.
    private static readonly int MeleeState = Animator.StringToHash("Base Layer.Melee Attack");

    // step: the hit of the 3-hit combo (1, 2 or 3), each drawn its own way
    public void Melee(int step = 1) {
        if (_anim.HasState(0, MeleeState)) {
            _anim.ResetTrigger("Melee");
            _anim.Play(MeleeState, 0, 0f);
        } else {
            Fire("Melee");
        }
        _motion.Slash(step);
    }

    public void Ranged() {
        Fire("Ranged");
        _motion.Shot();
    }

    public void Dash() {
        Fire("Dash");
        _motion.DashStart();
    }

    public void Hit() {
        _anim.SetTrigger("Hit");
        _motion.Hurt();
    }

    public void Death() {
        _anim.SetTrigger("Death");
        _motion.Die();
    }

    // co-op: back from the Death state, as at the start of a run
    public void Revive() {
        _anim.Rebind();
        _anim.Update(0f);
        _motion.Revive();
    }

    // a trigger stays armed until some state has a transition for it; if the current state has
    // none, it used to fire the animation seconds later (a pistol draw right after landing)
    private void Fire(string trigger) {
        _anim.SetTrigger(trigger);
        StartCoroutine(ClearTrigger(trigger));
    }

    private IEnumerator ClearTrigger(string trigger) {
        yield return new WaitForSeconds(0.15f);
        if (_anim) {
            _anim.ResetTrigger(trigger);
        }
    }
}
