using System.Collections;
using UnityEngine;

// Particle effects built at runtime, so no prefab or material has to be set up in the Editor:
// bursts (hits, deaths, muzzle flashes), dust (walking, landing) and spawn portals.
// Everything lives on one hidden GameObject that is rebuilt after each scene load.
public static class Fx {

    private static ParticleSystem burst;   // gravity, fades out: debris, blood, sparks
    private static ParticleSystem dust;    // no gravity, grows and fades: dust and smoke
    private static FxRunner runner;

    private static readonly ParticleSystem.MinMaxGradient White = new ParticleSystem.MinMaxGradient(Color.white);

    // ---- public API ----

    // small puff at the feet
    public static void Dust(Vector3 position, int count = 3) {
        Ensure();
        for (int i = 0; i < count; i++) {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = position + new Vector3(Random.Range(-0.15f, 0.15f), 0.02f, 0f);
            p.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 0.8f), 0f);
            p.startColor = new Color(0.85f, 0.8f, 0.7f, 0.6f);
            p.startSize = Random.Range(0.08f, 0.16f);
            p.startLifetime = Random.Range(0.3f, 0.5f);
            dust.Emit(p, 1);
        }
    }

    // specks floating up and fading, with no gravity (a big enemy crumbling away)
    public static void Rise(Vector3 position, Color color, float size, float life) {
        Ensure();
        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
        p.position = position;
        p.velocity = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.6f, 1.6f), 0f);
        p.startColor = color;
        p.startSize = size;
        p.startLifetime = life;
        dust.Emit(p, 1);
    }

    // debris flying out in every direction (hits, deaths, impacts)
    public static void Burst(Vector3 position, Color color, int count, float speed = 3f, float size = 0.1f, float life = 0.6f) {
        Ensure();
        for (int i = 0; i < count; i++) {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float s = speed * Random.Range(0.4f, 1.2f);
            p.position = position;
            p.velocity = new Vector3(Mathf.Cos(angle) * s, Mathf.Sin(angle) * s + speed * 0.3f, 0f);
            p.startColor = color;
            p.startSize = size * Random.Range(0.6f, 1.4f);
            p.startLifetime = life * Random.Range(0.7f, 1.3f);
            burst.Emit(p, 1);
        }
    }

    // sparks in a direction (muzzle flash, dash trail)
    public static void Spray(Vector3 position, Vector2 direction, Color color, int count, float speed = 4f, float spread = 0.4f) {
        Ensure();
        Vector2 dir = direction.normalized;
        for (int i = 0; i < count; i++) {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            Vector2 v = (dir + Vector2.Perpendicular(dir) * Random.Range(-spread, spread)) * speed * Random.Range(0.5f, 1.2f);
            p.position = position;
            p.velocity = v;
            p.startColor = color;
            p.startSize = Random.Range(0.05f, 0.12f);
            p.startLifetime = Random.Range(0.15f, 0.35f);
            burst.Emit(p, 1);
        }
    }

    // swirling ring where an enemy is about to appear; lasts `duration` seconds
    public static void Portal(Vector3 position, float duration, Color color) {
        Ensure();
        runner.StartCoroutine(PortalRoutine(position, duration, color));
    }

    private static IEnumerator PortalRoutine(Vector3 position, float duration, Color color) {
        float end = Time.time + duration;
        while (Time.time < end && dust != null) {
            float t = 1f - (end - Time.time) / duration; // 0 -> 1, the ring tightens as the enemy arrives
            for (int i = 0; i < 2; i++) {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Lerp(0.9f, 0.3f, t);
                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
                p.position = position + new Vector3(Mathf.Cos(angle) * radius, 0.4f + Mathf.Sin(angle) * radius, 0f);
                // orbit around the centre
                p.velocity = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f) * 1.5f;
                p.startColor = color;
                p.startSize = Random.Range(0.08f, 0.14f);
                p.startLifetime = 0.4f;
                dust.Emit(p, 1);
            }
            yield return null;
        }
        if (dust != null) {
            Burst(position + Vector3.up * 0.4f, color, 14, 2.5f, 0.1f, 0.5f);
        }
    }

    // ---- setup ----

    private static void Ensure() {
        if (burst != null && dust != null) {
            return;
        }
        GameObject root = new GameObject("Fx");
        runner = root.AddComponent<FxRunner>();
        burst = Make(root, "Burst", 1.2f);
        dust = Make(root, "Dust", -0.05f);
    }

    private static ParticleSystem Make(GameObject root, string name, float gravity) {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root.transform, false);
        ParticleSystem ps = obj.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        main.gravityModifier = gravity;
        main.startColor = White;
        ps.Stop();

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        // Sprites/Default is always in the build (every sprite in the scene uses it) and tints by vertex colour
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            renderer.material = new Material(shader);
        }
        renderer.sortingOrder = 100;
        return ps;
    }

    // hosts the portal coroutines; its GameObject dies with the scene, which resets the cache
    private class FxRunner : MonoBehaviour {
        private void OnDestroy() {
            burst = null;
            dust = null;
            runner = null;
        }
    }
}
