using UnityEngine;

// Small ground enemy that walks its patrol and bites when Freny is next to it.
public class Bug : Enemy {

    protected override bool Small => true;
    public override bool WeakKill => true;
    protected override float SwarmTopSpeed => 3f;   // Freny's walk
    protected override Color DeathColor => new Color(0.5f, 0.75f, 0.3f);
    protected override string SoundName => "bug";
}
