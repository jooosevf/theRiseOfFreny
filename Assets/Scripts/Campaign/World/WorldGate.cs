using UnityEngine;

// A portcullis that blocks the way until all its levers are pulled.
public class WorldGate : MonoBehaviour {

    private string[] levers;

    public void Init(string[] leverIds) {
        levers = leverIds;
        Refresh(false);
    }

    public static void RefreshAll() {
        foreach (WorldGate gate in FindCompat.All<WorldGate>()) {
            gate.Refresh(true);
        }
    }

    private void Refresh(bool announce) {
        bool open = true;
        foreach (string lever in levers) {
            open &= CampaignWorld.State != null && CampaignWorld.State.Has("switch/" + lever);
        }
        Collider2D wall = GetComponent<Collider2D>();
        if (announce && open && wall.enabled) {
            Fx.Dust(transform.position + Vector3.down * 1.2f, 8);
            Sfx.Play("door", transform.position);
        }
        wall.enabled = !open;
        GetComponent<SpriteRenderer>().enabled = !open;
    }
}
