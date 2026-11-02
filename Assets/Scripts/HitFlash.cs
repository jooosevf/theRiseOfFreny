using System.Collections.Generic;
using UnityEngine;

// A hit lights the enemy up white for a few frames and holds its animation still for the
// hit-stop, the way Hollow Knight sells a nail hit: HitFlash.Hit(enemy, flash, freeze). Every
// SpriteRenderer under the object swaps to the Freny/SpriteFlash material (Resources/Shaders)
// and gets its own back when the flash ends; a renderer with no sprite is left alone.
public class HitFlash : MonoBehaviour {
    private static Material flashMaterial;

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Animator> animators = new List<Animator>();
    private readonly List<float> speeds = new List<float>();
    private float flashUntil, freezeUntil;
    private bool flashing, frozen;

    private static Material FlashMaterial {
        get {
            if (flashMaterial == null) {
                Shader shader = Resources.Load<Shader>("SpriteFlash");
                if (shader != null) {
                    flashMaterial = new Material(shader);
                    flashMaterial.SetColor("_FlashColor", Color.white);
                    flashMaterial.SetFloat("_Flash", 1f);
                }
            }
            return flashMaterial;
        }
    }

    public static void Hit(Component target, float flash, float freeze) {
        if (target == null) {
            return;
        }
        HitFlash fx = target.GetComponent<HitFlash>();
        if (fx == null) {
            fx = target.gameObject.AddComponent<HitFlash>();
        }
        fx.Begin(flash, freeze);
    }

    private void Begin(float flash, float freeze) {
        float now = Time.time;
        if (flash > 0f && FlashMaterial != null) {
            if (!flashing) {
                renderers.Clear();
                materials.Clear();
                foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>()) {
                    if (sr.sprite == null || sr.sharedMaterial == FlashMaterial) {
                        continue;
                    }
                    renderers.Add(sr);
                    materials.Add(sr.sharedMaterial);
                    sr.sharedMaterial = FlashMaterial;
                }
                flashing = true;
            }
            flashUntil = Mathf.Max(flashUntil, now + flash);
        }
        if (freeze > 0f) {
            if (!frozen) {
                animators.Clear();
                speeds.Clear();
                foreach (Animator a in GetComponentsInChildren<Animator>()) {
                    animators.Add(a);
                    speeds.Add(a.speed);
                    a.speed = 0f;
                }
                frozen = true;
            }
            freezeUntil = Mathf.Max(freezeUntil, now + freeze);
        }
    }

    private void LateUpdate() {
        if (flashing && Time.time >= flashUntil) {
            EndFlash();
        }
        if (frozen && Time.time >= freezeUntil) {
            EndFreeze();
        }
    }

    private void EndFlash() {
        for (int i = 0; i < renderers.Count; i++) {
            if (renderers[i] != null && renderers[i].sharedMaterial == FlashMaterial) {
                renderers[i].sharedMaterial = materials[i];
            }
        }
        renderers.Clear();
        materials.Clear();
        flashing = false;
    }

    private void EndFreeze() {
        for (int i = 0; i < animators.Count; i++) {
            if (animators[i] != null && animators[i].speed == 0f) {
                animators[i].speed = speeds[i];
            }
        }
        animators.Clear();
        speeds.Clear();
        frozen = false;
    }

    private void OnDisable() {
        if (flashing) {
            EndFlash();
        }
        if (frozen) {
            EndFreeze();
        }
    }
}
