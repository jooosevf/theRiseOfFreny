using System.Collections.Generic;
using UnityEngine;

// Plays an enemy's sprite sheets straight from Resources, for the enemies whose art came without a
// working Animator controller (the Escudeiro and the Ciclope). It follows the Enemy on its own: the
// walk while it moves, the attack while it swings, the hit for a moment after a hit, else the idle;
// the enemy forces a special clip (block, smash, vomit) with Play.
public class FrameAnimator : MonoBehaviour {
    public class Clip {
        public Sprite[] frames;
        public float fps;
        public bool loop;
    }

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
    private Enemy enemy;
    private SpriteRenderer sprite;
    private Vector3 lastPosition;
    private string current;
    private float clipStart;
    private string forced;
    private float forcedUntil;

    // the frames of a sliced sheet in Resources, in order (Name_0, Name_1 ... Name_10)
    public static Sprite[] Load(string path) {
        if (cache.TryGetValue(path, out Sprite[] frames)) {
            return frames;
        }
        frames = Resources.LoadAll<Sprite>(path);
        System.Array.Sort(frames, (a, b) => Index(a.name).CompareTo(Index(b.name)));
        cache[path] = frames;
        return frames;
    }

    private static int Index(string name) {
        int us = name.LastIndexOf('_');
        return us >= 0 && int.TryParse(name.Substring(us + 1), out int i) ? i : 0;
    }

    public void Setup(Enemy owner, SpriteRenderer renderer) {
        enemy = owner;
        sprite = renderer;
        lastPosition = owner.transform.position;
    }

    public void Add(string name, string path, float fps, bool loop = true) {
        Sprite[] frames = Load(path);
        if (frames.Length > 0) {
            clips[name] = new Clip { frames = frames, fps = fps, loop = loop };
        }
    }

    // plays `name` for `seconds`, over whatever the enemy is doing
    public void Play(string name, float seconds) {
        forced = name;
        forcedUntil = Time.time + seconds;
        Switch(name);
    }

    // freezes the frame on screen for `seconds` (the forced clip lasts that much longer)
    public void Hold(float seconds) {
        holdUntil = Time.time + seconds;
        forcedUntil += seconds;
    }

    public float Length(string name) {
        return clips.TryGetValue(name, out Clip clip) ? clip.frames.Length / clip.fps : 0f;
    }

    private float holdUntil;

    private void Switch(string name) {
        if (current != name) {
            current = name;
            clipStart = Time.time;
        }
    }

    private void LateUpdate() {
        if (enemy == null || sprite == null) {
            return;
        }
        Vector3 position = enemy.transform.position;
        bool moving = Mathf.Abs(position.x - lastPosition.x) > 0.2f * Time.deltaTime;
        lastPosition = position;

        string want;
        if (forced != null && Time.time < forcedUntil) {
            want = forced;
        } else {
            forced = null;
            want = enemy.IsAttacking && clips.ContainsKey("attack") ? "attack"
                : Time.time - enemy.LastHitTime < 0.3f && clips.ContainsKey("hit") ? "hit"
                : moving && clips.ContainsKey("walk") ? "walk"
                : "idle";
        }
        Switch(want);
        if (!clips.TryGetValue(current, out Clip clip)) {
            return;
        }
        if (Time.time < holdUntil) {
            clipStart += Time.deltaTime;   // the clip's clock stands still while held
        }
        int frame = (int)((Time.time - clipStart) * clip.fps);
        frame = clip.loop ? frame % clip.frames.Length : Mathf.Min(frame, clip.frames.Length - 1);
        sprite.sprite = clip.frames[frame];
    }
}
