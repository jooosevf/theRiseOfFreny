using UnityEngine;

// A lever: pulled once, it stays down until a death goes back to before it was pulled. It opens
// the gates that name it, in this room or another.
public class WorldLever : WorldInteract {

    private string key;

    public override string Hint => IsOn ? "A alavanca já foi puxada" : "W: puxar a alavanca";

    private bool IsOn => CampaignWorld.State != null && CampaignWorld.State.Has(key);

    public void Init(string id) {
        key = "switch/" + id;
        Refresh();
    }

    protected override void Use(Player player) {
        if (IsOn) {
            return;
        }
        CampaignWorld.Mark(key);
        Refresh();
        Sfx.Play("door", transform.position);
        ScreenKick.Kick(0.1f, 0.15f);
        WorldGate.RefreshAll();
        CampaignWorld.ShowToast("Algo se abriu ao longe");
    }

    private void Refresh() {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Sprite sprite = Resources.Load<Sprite>(IsOn ? "World/Lever On" : "World/Lever Off");
        if (renderer != null && sprite != null) {
            renderer.sprite = sprite;
        }
    }
}
