using UnityEngine;

// Something in a room Freny uses with the Interact key, standing in front of it: a save point or a lever.
[RequireComponent(typeof(Collider2D))]
public abstract class WorldInteract : MonoBehaviour {

    // the one Freny is standing at, for the HUD hint
    public static WorldInteract Current { get; private set; }

    public abstract string Hint { get; }
    protected abstract void Use(Player player);

    private Player inside;

    private void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            inside = other.GetComponent<Player>();
            Current = this;
        }
    }

    private void OnTriggerExit2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            inside = null;
            if (Current == this) {
                Current = null;
            }
        }
    }

    private void OnDisable() {
        if (Current == this) {
            Current = null;
        }
    }

    private void Update() {
        if (inside != null && !inside.IsDead && !InputBindings.GameplayBlocked && InputBindings.GetDown(GameAction.Interact)) {
            Use(inside);
        }
    }
}
