using System.Collections.Generic;
using UnityEngine;
#if UNITY_5_3_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

// While a Ciclope is in the Arena the torches flicker faster and burn redder (José, 2026-11-01):
// the flame animation runs faster and is tinted red, and its light flickers and turns red. It
// fades in when the boss arrives and out when it falls (Mood, 0..1). Lives next to ArenaBossLoot.
// ArenaDayNight sets the torch lights every LateUpdate (blue by day), so this runs after it and
// works on top of what it set; without a day and night cycle it keeps the lights' own values.
// The flames are the scene's "Standing Torch_" sprites, the same ones ArenaDayNight finds.
[DefaultExecutionOrder(100)]
public class BossTorches : MonoBehaviour {

    private const float FadeSeconds = 1.5f;
    private const float FlameSpeed = 1.9f;      // the flame animation's speed at full mood
    private static readonly Color FlameTint = new Color(1f, 0.55f, 0.45f);
    private static readonly Color RedLight = new Color(1f, 0.12f, 0.05f);

    // 0 calm, 1 a Ciclope is out
    public static float Mood { get; private set; }

    private class Torch {
        public SpriteRenderer flame;
        public Animator animator;
        public float seed;
#if UNITY_5_3_OR_NEWER
        public Light2D light;
        public Color color;
        public float intensity;
#endif
    }

    private readonly List<Torch> torches = new List<Torch>();
    private ArenaDayNight dayNight;
    private bool applied;

    private void Start() {
        foreach (SpriteRenderer flame in FindCompat.All<SpriteRenderer>(true)) {
            if (flame.gameObject.scene != gameObject.scene || flame.sprite == null || !flame.sprite.name.StartsWith("Standing Torch_")) {
                continue;
            }
            Torch torch = new Torch { flame = flame, animator = flame.GetComponent<Animator>(), seed = Random.Range(0f, 100f) };
#if UNITY_5_3_OR_NEWER
            torch.light = flame.GetComponent<Light2D>();
            if (torch.light != null) {
                torch.color = torch.light.color;
                torch.intensity = torch.light.intensity;
            }
#endif
            torches.Add(torch);
        }
    }

    private void OnDisable() {
        Mood = 0f;
    }

    private void LateUpdate() {
        float target = ArenaBossLoot.BossOut ? 1f : 0f;
        Mood = Mathf.MoveTowards(Mood, target, Time.deltaTime / FadeSeconds);
        if (Mood <= 0f && !applied) {
            return;
        }
        if (dayNight == null) {
            dayNight = FindAnyObjectByType<ArenaDayNight>();
        }
        // ArenaDayNight rewrote the lights this frame: work on top of that; otherwise on their own values
        bool rewritten = dayNight != null && dayNight.isActiveAndEnabled;
        foreach (Torch torch in torches) {
            if (torch.flame == null) {
                continue;
            }
            torch.flame.color = Color.Lerp(Color.white, FlameTint, Mood);
            if (torch.animator != null) {
                torch.animator.speed = Mathf.Lerp(1f, FlameSpeed, Mood);
            }
#if UNITY_5_3_OR_NEWER
            if (torch.light != null) {
                Color color = rewritten ? torch.light.color : torch.color;
                float intensity = rewritten ? torch.light.intensity : torch.intensity;
                // two quick waves and a little noise: a nervous flicker, never fully out
                float t = Time.time + torch.seed;
                float flicker = 0.55f * Mathf.PerlinNoise(t * 9f, torch.seed) + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 23f)) + 0.45f;
                torch.light.color = Color.Lerp(color, RedLight, Mood * 0.8f);
                torch.light.intensity = intensity * Mathf.Lerp(1f, flicker * 1.15f, Mood);
            }
#endif
        }
        applied = Mood > 0f;
    }
}
