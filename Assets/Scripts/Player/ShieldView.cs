using UnityEngine;

// The Flagelante's Escudo de Espinhos in his hand while he holds it up (Player.ShieldUp): in front of
// him, redder and shakier as it wears out. Added by Player.Start for that class only.
public class ShieldView : MonoBehaviour {

    private static readonly Vector2 Front = new Vector2(0.42f, -0.5f);
    private const float ShakeBelow = 0.5f;

    private Player player;
    private SpriteRenderer body;
    private SpriteRenderer shield;

    private void Start() {
        player = GetComponent<Player>();
        body = GetComponentInChildren<SpriteRenderer>();
        shield = new GameObject("Shield").AddComponent<SpriteRenderer>();
        shield.transform.SetParent(body.transform, false);
        shield.sprite = FrenyClasses.Icon("Escudo");
        shield.sharedMaterial = body.sharedMaterial;
        shield.sortingLayerID = body.sortingLayerID;
        shield.sortingOrder = body.sortingOrder + 3;
        shield.enabled = false;
    }

    private void LateUpdate() {
        if (shield == null || player == null) {
            return;
        }
        shield.enabled = player.ShieldUp && body.enabled;
        if (!shield.enabled) {
            return;
        }
        float left = player.RevolverBullets / (float)Mathf.Max(1, player.RevolverCapacity);
        Vector2 at = Front;
        if (left <= ShakeBelow) {
            at += Random.insideUnitCircle * 0.015f;
        }
        at.x = body.flipX ? -at.x : at.x;
        shield.flipX = body.flipX;
        shield.transform.localPosition = at;
        shield.color = Color.Lerp(new Color(1f, 0.55f, 0.45f), Color.white, left);
    }
}
