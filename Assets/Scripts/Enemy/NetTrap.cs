using UnityEngine;

// An enemy caught in Freny's Rede (NetProjectile): it stands where it was caught for Seconds, with
// the net drawn over its body, and can't start an attack (Enemy.Netted). A walker caught mid-air
// still falls; a bat hangs where it was. The Ciclope is too big to hold: it only slows down for
// CyclopsSeconds. Kept on the enemy as a component, so no enemy script needs to know about the pin.
public class NetTrap : MonoBehaviour {

    public const float Seconds = 4f;
    public const float CyclopsSeconds = 3f;
    public const float CyclopsSpeed = 0.45f;
    private const float FadeTime = 0.6f;

    private static Sprite netSprite;

    private Enemy enemy;
    private SpriteRenderer net;
    private Vector3 pin;
    private bool pinned;
    private bool hangs;      // flyers keep their height too
    private float until;

    public static Sprite NetSprite => netSprite != null ? netSprite : (netSprite = Resources.Load<Sprite>("Fase4/Net Open"));

    public static void Catch(Enemy enemy) {
        NetTrap trap = enemy.GetComponent<NetTrap>();
        if (trap == null) {
            trap = enemy.gameObject.AddComponent<NetTrap>();
        }
        trap.Hold(enemy);
    }

    private void Hold(Enemy target) {
        enemy = target;
        pinned = !(enemy is Cyclops);
        hangs = enemy is Bat;
        until = Time.time + (pinned ? Seconds : CyclopsSeconds);
        pin = enemy.transform.position;
        enemy.Netted = pinned;
        enemy.NetSpeed = pinned ? 1f : CyclopsSpeed;
        if (net == null) {
            GameObject obj = new GameObject("Net Over");
            obj.transform.SetParent(transform, false);
            net = obj.AddComponent<SpriteRenderer>();
            net.sprite = NetSprite;
            SpriteRenderer body = GetComponentInChildren<SpriteRenderer>();
            if (body != null && body != net) {
                net.sortingLayerID = body.sortingLayerID;
                net.sortingOrder = body.sortingOrder + 5;
            } else {
                net.sortingOrder = 45;
            }
        }
        net.enabled = true;
        Fit();
    }

    // the net covers the body, whatever its size (the Ciclope's too) and its facing
    private void Fit() {
        if (net == null || net.sprite == null) {
            return;
        }
        Bounds b = enemy.BodyBounds;
        Vector2 art = net.sprite.bounds.size;
        float size = Mathf.Max(b.size.x, b.size.y) * 1.25f;
        Vector3 parentScale = transform.lossyScale;
        net.transform.localScale = new Vector3(size / art.x / Mathf.Abs(parentScale.x), size / art.y / Mathf.Abs(parentScale.y), 1f);
        net.transform.position = b.center;
    }

    // after the enemy's own Update has moved it: back to where it was caught
    private void LateUpdate() {
        if (enemy == null) {
            Destroy(this);
            return;
        }
        if (enemy.IsDead || Time.time >= until) {
            Release();
            return;
        }
        if (pinned) {
            Vector3 p = enemy.transform.position;
            float y = hangs ? pin.y : Mathf.Min(p.y, pin.y);   // walkers still fall
            pin = new Vector3(pin.x, y, p.z);
            enemy.transform.position = pin;
        }
        Fit();
        float left = until - Time.time;
        net.color = new Color(1f, 1f, 1f, left < FadeTime ? left / FadeTime : 1f);
    }

    private void Release() {
        if (enemy != null) {
            enemy.Netted = false;
            enemy.NetSpeed = 1f;
        }
        if (net != null) {
            Destroy(net.gameObject);
        }
        Destroy(this);
    }

    // a net that caught nobody: it lies on the floor a moment and fades
    public static void DropEmpty(Vector3 at) {
        GameObject obj = new GameObject("Net (empty)");
        obj.transform.position = at;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = NetSprite;
        renderer.sortingOrder = 45;
        if (renderer.sprite != null) {
            obj.transform.localScale = Vector3.one * (1.6f / Mathf.Max(0.01f, renderer.sprite.bounds.size.x));
        }
        obj.AddComponent<Fade>().seconds = 1.2f;
    }

    private class Fade : MonoBehaviour {
        public float seconds = 1f;
        private float start;
        private SpriteRenderer renderer;

        private void Start() {
            start = Time.time;
            renderer = GetComponent<SpriteRenderer>();
        }

        private void Update() {
            float t = (Time.time - start) / seconds;
            if (t >= 1f) {
                Destroy(gameObject);
                return;
            }
            renderer.color = new Color(1f, 1f, 1f, 1f - t);
        }
    }
}
