using UnityEngine;

// Global game speed (Time.timeScale). José, 2026-10-30: no longer a setting; it is locked at the
// normal speed for everyone (an old saved value, from the slider that used to be in Configurações,
// is ignored), which also keeps every run comparable for the ranking.
public static class GameSpeed {

    public const float Default = 1f;

    public static float Value => Default;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Apply() {
        Time.timeScale = Value;
        Time.fixedDeltaTime = 0.02f * Value;
    }
}
