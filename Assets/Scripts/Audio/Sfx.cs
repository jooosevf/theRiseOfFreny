using System.Collections.Generic;
using UnityEngine;

// Sound effects, played by name from Assets/Audio/Resources/Sfx (see LEIA-ME.md there): Sfx.Play("coin").
// Variations sit next to each other as name_1, name_2...; one is picked at random, with a slightly
// different pitch each time, so repeated sounds don't feel like a loop. A missing file is silence,
// so code can ask for sounds nobody has made yet. Volume follows Configurações > Efeitos.
// One hidden GameObject with a pool of AudioSources, kept across scenes (like the music).
public static class Sfx {

    private const int Voices = 24;          // sounds playing at the same time, at most
    private const int SameSoundVoices = 3;  // copies of one sound at the same time, at most
    private const float PanWidth = 0.6f;    // how far left/right a sound at the screen edge goes

    // per sound: volume (0..1) and the shortest gap between two plays, so a sound that fires every
    // frame (or at high game speed) doesn't pile up; anything not listed plays at 1 with no gap
    private static readonly Dictionary<string, (float volume, float gap)> Settings = new Dictionary<string, (float, float)> {
        { "freny_step", (0.25f, 0.25f) },
        { "freny_land", (0.35f, 0.1f) },
        { "freny_jump", (0.5f, 0.05f) },
        { "freny_jump_air", (0.55f, 0.05f) },
        { "freny_dash", (0.6f, 0.05f) },
        { "sword_swing", (0.55f, 0.05f) },
        { "sword_combo1", (0.55f, 0.05f) },
        { "sword_combo2", (0.55f, 0.05f) },
        { "sword_combo3", (0.6f, 0.05f) },
        { "sword_hit", (0.7f, 0.04f) },
        { "pistol_shot", (0.6f, 0.04f) },
        { "shotgun_shot", (0.75f, 0.04f) },
        { "pistol_empty", (0.5f, 0.2f) },
        { "pistol_reload", (0.5f, 0.2f) },
        { "grenade_throw", (0.5f, 0.05f) },
        { "grenade_explode", (0.85f, 0.05f) },
        { "freny_hurt", (0.7f, 0.1f) },
        { "freny_death", (0.8f, 0.5f) },
        { "freny_rage", (0.85f, 0.5f) },
        { "blood_moon", (0.9f, 5f) },
        { "multi_kill", (0.6f, 0.05f) },
        { "narrator_welcome", (1f, 1f) },
        { "freny_stun", (0.6f, 0.2f) },
        { "potion", (0.6f, 0.1f) },
        { "pickup_ammo", (0.45f, 0.1f) },
        { "weapon_switch", (0.4f, 0.1f) },
        { "arena_wrap", (0.3f, 0.1f) },
        { "golem_step", (0.12f, 0.45f) },
        { "coin", (0.5f, 0.03f) },
        { "combo", (0.35f, 0.05f) },
        { "portal", (0.35f, 0.1f) },
        { "hexball_launch", (0.45f, 0.05f) },
        { "chest_break", (0.7f, 0.1f) },
        { "ui_move", (0.35f, 0.04f) },
        { "ui_select", (0.5f, 0.04f) },
        { "ui_back", (0.45f, 0.04f) },
        { "ui_error", (0.5f, 0.1f) },
        { "ui_pause", (0.5f, 0.1f) },
        { "shop_open", (0.6f, 1f) },
        { "door_locked", (0.6f, 0.6f) },
        { "game_over", (0.7f, 1f) },
        { "level_up", (0.7f, 0.3f) },
        { "upgrade", (0.6f, 0.1f) },
    };

    private static AudioSource[] sources;
    private static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private static readonly string[] playing = new string[Voices];
    private static int next;

    // ---- public API ----

    // a sound in the world: panned left or right by where it is on screen
    public static void Play(string name, Vector3 position, float volume = 1f, float pitch = 1f) {
        PlayClip(name, volume, pitch, Pan(position), false);
    }

    // a sound with no place (UI, jingles, Freny's own voice)
    public static void Play(string name, float volume = 1f, float pitch = 1f) {
        PlayClip(name, volume, pitch, 0f, false);
    }

    // menu sounds: they keep playing while the game is paused (AudioListener.pause)
    public static void Ui(string name) {
        PlayClip(name, 1f, 1f, 0f, true);
    }

    public static bool Has(string name) {
        return Clips(name).Length > 0;
    }

    // ---- playing ----

    private static void PlayClip(string name, float volume, float pitch, float pan, bool ui) {
        AudioClip[] options = Clips(name);
        if (options.Length == 0) {
            return;
        }
        (float volume, float gap) setting = Settings.TryGetValue(name, out var s) ? s : (1f, 0f);
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(name, out float last) && now - last < setting.gap) {
            return;
        }
        Ensure();
        AudioSource source = FreeSource(name);
        if (source == null) {
            return;
        }
        lastPlayed[name] = now;

        source.clip = options[Random.Range(0, options.Length)];
        source.volume = Mathf.Clamp01(setting.volume * volume) * GameSettings.SfxVolume;
        source.pitch = pitch * Random.Range(0.95f, 1.05f);
        source.panStereo = pan;
        source.ignoreListenerPause = ui;
        source.Play();
    }

    // an idle source, or the oldest one; null when this sound already plays SameSoundVoices times
    private static AudioSource FreeSource(string name) {
        int copies = 0;
        for (int i = 0; i < Voices; i++) {
            if (sources[i].isPlaying && playing[i] == name) {
                copies++;
            }
        }
        if (copies >= SameSoundVoices) {
            return null;
        }
        for (int i = 0; i < Voices; i++) {
            int index = (next + i) % Voices;
            if (!sources[index].isPlaying) {
                return Take(index, name);
            }
        }
        return Take(next, name);
    }

    private static AudioSource Take(int index, string name) {
        next = (index + 1) % Voices;
        playing[index] = name;
        sources[index].Stop();
        return sources[index];
    }

    private static float Pan(Vector3 position) {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) {
            return 0f;
        }
        float halfWidth = cam.orthographicSize * cam.aspect;
        float x = (position.x - cam.transform.position.x) / Mathf.Max(0.01f, halfWidth);
        return Mathf.Clamp(x, -1f, 1f) * PanWidth;
    }

    // "coin" -> Sfx/coin, or Sfx/coin_1, Sfx/coin_2... when there are variations
    private static AudioClip[] Clips(string name) {
        if (clips.TryGetValue(name, out AudioClip[] cached)) {
            return cached;
        }
        List<AudioClip> found = new List<AudioClip>();
        AudioClip single = Resources.Load<AudioClip>("Sfx/" + name);
        if (single != null) {
            found.Add(single);
        }
        for (int i = 1; ; i++) {
            AudioClip variation = Resources.Load<AudioClip>("Sfx/" + name + "_" + i);
            if (variation == null) {
                break;
            }
            found.Add(variation);
        }
        AudioClip[] result = found.ToArray();
        clips[name] = result;
        return result;
    }

    // ---- setup ----

    private static void Ensure() {
        if (sources != null && sources[0] != null) {
            return;
        }
        GameObject root = new GameObject("Sfx");
        Object.DontDestroyOnLoad(root);
        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++) {
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sources[i] = source;
        }
    }
}
