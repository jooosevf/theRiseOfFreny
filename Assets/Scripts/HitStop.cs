using UnityEngine;

// The game stands still for a moment on a hard hit (Hollow Knight's nail freeze): HitStop.Freeze(seconds)
// zeroes Time.timeScale and brings it back after that many real seconds. Everything stops together
// (Freny, the enemy, the crescent, particles), so the hit reads as one thing. A pause (GameMenus)
// wins: a freeze never starts while paused, and never unpauses the game when it ends.
public class HitStop : MonoBehaviour {
    private static HitStop instance;
    private float until = -1f;
    private bool frozen;

    public static void Freeze(float seconds) {
        if (seconds <= 0f || CoopWorld.Active || GameMenus.Paused || Time.timeScale == 0f && !(instance != null && instance.frozen)) {
            return;
        }
        if (instance == null) {
            instance = new GameObject("HitStop").AddComponent<HitStop>();
            DontDestroyOnLoad(instance.gameObject);
        }
        float end = Time.unscaledTime + seconds;
        if (!instance.frozen) {
            instance.frozen = true;
            Time.timeScale = 0f;
        }
        instance.until = Mathf.Max(instance.until, end);
    }

    private void Update() {
        if (!frozen || Time.unscaledTime < until) {
            return;
        }
        frozen = false;
        if (!GameMenus.Paused) {
            Time.timeScale = GameSpeed.Value;
        }
    }

    private void OnDestroy() {
        if (frozen && !GameMenus.Paused) {
            Time.timeScale = GameSpeed.Value;
        }
        frozen = false;
    }
}
