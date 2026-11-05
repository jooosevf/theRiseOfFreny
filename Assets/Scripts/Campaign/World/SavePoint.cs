using UnityEngine;

// The only place that writes the world to disk. It also counts as a checkpoint, and what was in
// the bag goes into the account (design/gdd/estado-do-mundo.md).
public class SavePoint : WorldInteract {

    public override string Hint => "W: salvar";

    protected override void Use(Player player) {
        CampaignWorld.Save(player, transform.position);
        Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.75f, 0.2f, 0.25f), 18, 2f, 0.08f, 0.6f);
        Sfx.Play("checkpoint", transform.position);
        CampaignWorld.ShowToast("Jogo salvo");
    }
}
