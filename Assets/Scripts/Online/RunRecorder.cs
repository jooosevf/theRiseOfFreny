using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Online {
    // The run's replay, so the ADMs can watch a record before it counts (José, 2026-11-05): ten
    // times a second, where Freny and every enemy are. Kills, hits and the score are already in the
    // run log (RunLog), which the viewer (freny-online admin, replay page) lays over this.
    // Binary, gzipped and in base64, sent with the run's log to the ranking API, which keeps it only
    // for a run that goes to review or into a board's top.
    //
    // Format, little-endian, version 1:
    //   "FRR1", float step (s), 4 floats: the camera's left, right, bottom, top (the arena's screen)
    //   2 shorts: Freny's drawn size (width, height)
    //   ushort n, n x (4 shorts: the ground's boxes, xMin yMin xMax yMax)
    //   then frames until the end: ushort time (in steps), Freny (short x, short y, byte flags,
    //   byte health, byte armor), ushort count, count x enemy (ushort id, byte type, short x,
    //   short y, byte flags), and a new type the first time it shows up: "type" 255, byte length +
    //   UTF-8 name, 2 shorts (its drawn size), then the real type byte.
    // Positions are in 1/16 world units. Flags: 1 facing right, 2 on the ground, 4 dead,
    // 8 the autopilot drives (Freny); 1 facing right (enemy).
    public class RunRecorder {
        public const float Step = 0.1f;
        private const float Unit = 16f;
        private const int MaxBytes = 6 * 1024 * 1024;   // raw; well over an hour of a full arena
        private const int MaxBoxes = 200;

        private readonly MemoryStream _raw = new MemoryStream(64 * 1024);
        private readonly BinaryWriter _out;
        private readonly Dictionary<Enemy, ushort> _ids = new Dictionary<Enemy, ushort>();
        private readonly Dictionary<string, byte> _types = new Dictionary<string, byte>();
        private readonly float _start;
        private float _next;
        private bool _full;

        public RunRecorder(float start, Player player) {
            _start = start;
            _out = new BinaryWriter(_raw);
            _out.Write(new[] { (byte)'F', (byte)'R', (byte)'R', (byte)'1' });
            _out.Write(Step);
            _out.Write(ArenaBounds.Active ? ArenaBounds.Left : -20f);
            _out.Write(ArenaBounds.Active ? ArenaBounds.Right : 20f);
            _out.Write(ArenaBounds.Active ? ArenaBounds.Bottom : -12f);
            _out.Write(ArenaBounds.Active ? ArenaBounds.Top : 12f);
            WriteSize(player != null ? player.gameObject : null);
            WriteGround();
        }

        // the solid, still colliders (floor, walls, platforms): the viewer draws them as the arena
        private void WriteGround() {
            List<Bounds> boxes = new List<Bounds>();
            foreach (Collider2D col in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)) {
                if (boxes.Count >= MaxBoxes) break;
                if (col.isTrigger || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                Rigidbody2D body = col.attachedRigidbody;
                if (body != null && body.bodyType != RigidbodyType2D.Static) continue;
                if (col.GetComponentInParent<Enemy>() != null || col.GetComponentInParent<Player>() != null) continue;
                boxes.Add(col.bounds);
            }
            _out.Write((ushort)boxes.Count);
            foreach (Bounds b in boxes) {
                _out.Write(Pos(b.min.x));
                _out.Write(Pos(b.min.y));
                _out.Write(Pos(b.max.x));
                _out.Write(Pos(b.max.y));
            }
        }

        // how big it is drawn (all its sprites together), for the viewer's pictures
        private void WriteSize(GameObject thing) {
            Bounds size = new Bounds();
            bool any = false;
            if (thing != null) {
                foreach (SpriteRenderer sprite in thing.GetComponentsInChildren<SpriteRenderer>()) {
                    if (!sprite.enabled || sprite.sprite == null) continue;
                    if (any) size.Encapsulate(sprite.bounds);
                    else size = sprite.bounds;
                    any = true;
                }
            }
            _out.Write(Pos(any ? size.size.x : 1f));
            _out.Write(Pos(any ? size.size.y : 1f));
        }

        private static short Pos(float v) {
            return (short)Mathf.Clamp(Mathf.RoundToInt(v * Unit), short.MinValue, short.MaxValue);
        }

        // called every frame while the run goes on; writes one frame each Step of game time
        public void Tick(Player player, bool autopilot) {
            if (_full || player == null) return;
            float now = Time.time - _start;
            if (now < _next) return;
            _next = now + Step;
            if (_raw.Length > MaxBytes) {
                _full = true;   // the start of a very long run is kept; the log has the rest
                return;
            }
            _out.Write((ushort)Mathf.Min(ushort.MaxValue, Mathf.RoundToInt(now / Step)));
            Vector3 p = player.transform.position;
            _out.Write(Pos(p.x));
            _out.Write(Pos(p.y));
            _out.Write((byte)((player.FacingRight ? 1 : 0) | (player.IsGrounded ? 2 : 0) | (player.IsDead ? 4 : 0) | (autopilot ? 8 : 0)));
            _out.Write((byte)Mathf.Clamp(player.Health, 0, 255));
            _out.Write((byte)Mathf.Clamp(player.Armor, 0, 255));

            int count = 0;
            foreach (Enemy enemy in Enemy.Active) {
                if (enemy != null) count++;
            }
            _out.Write((ushort)count);
            foreach (Enemy enemy in Enemy.Active) {
                if (enemy == null) continue;
                if (!_ids.TryGetValue(enemy, out ushort id)) {
                    id = (ushort)(_ids.Count + 1);
                    _ids[enemy] = id;
                }
                _out.Write(id);
                string type = RunLog.PrefabName(enemy.gameObject.name);
                if (!_types.TryGetValue(type, out byte index)) {
                    index = (byte)Mathf.Min(_types.Count, 254);
                    _types[type] = index;
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(type.Length > 40 ? type.Substring(0, 40) : type);
                    _out.Write((byte)255);
                    _out.Write((byte)name.Length);
                    _out.Write(name);
                    WriteSize(enemy.gameObject);
                }
                _out.Write(index);
                Vector3 e = enemy.transform.position;
                _out.Write(Pos(e.x));
                _out.Write(Pos(e.y));
                _out.Write((byte)(enemy.transform.lossyScale.x > 0 ? 1 : 0));
            }
            if (_ids.Count > 60000) _ids.Clear();   // ids stay small; a very long run reuses them
        }

        // the whole replay, gzipped, in base64 (what RunLog.ToJson sends as "replay")
        public string ToBase64() {
            _out.Flush();
            using (MemoryStream zipped = new MemoryStream()) {
                using (GZipStream gzip = new GZipStream(zipped, System.IO.Compression.CompressionLevel.Optimal, true)) {
                    _raw.Position = 0;
                    _raw.CopyTo(gzip);
                }
                _raw.Position = _raw.Length;
                return Convert.ToBase64String(zipped.GetBuffer(), 0, (int)zipped.Length);
            }
        }
    }
}
