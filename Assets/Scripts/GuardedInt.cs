using System;
using System.Collections.Generic;

// An int Cheat Engine can't find by searching for its value: it is kept xor'd with a key that
// changes on every write, plus a check word. Bytes changed from outside break the check and the
// run is flagged (AntiCheat.Flag), so the ranking server refuses it. Drop-in for an int field:
//   public int Health { get => _health; set => _health = value; }   private GuardedInt _health;
public struct GuardedInt {
    private int _key;
    private int _hidden;
    private int _check;

    public GuardedInt(int value) {
        _key = AntiCheat.NextKey();
        _hidden = value ^ _key;
        _check = Check(value, _key);
    }

    public int Value {
        get {
            int value = _hidden ^ _key;
            // a never-written field (all zero) reads 0
            if ((_key != 0 || _hidden != 0 || _check != 0) && _check != Check(value, _key)) {
                AntiCheat.Flag("mem");
            }
            return value;
        }
    }

    private static int Check(int value, int key) {
        unchecked {
            uint h = (uint)value * 0x9E3779B1u ^ (uint)key;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            return (int)(h ^ (h >> 13)) | 1;   // never 0, so a zeroed field doesn't pass
        }
    }

    public static implicit operator int(GuardedInt guarded) => guarded.Value;
    public static implicit operator GuardedInt(int value) => new GuardedInt(value);
}

// What the game noticed about a ranked run being tampered with: memory edits (GuardedInt) and a
// clock running at another speed than the computer's (OnlineServices). The flags go in the run's
// log; the ranking API refuses a run that carries any.
public static class AntiCheat {
    private static readonly HashSet<string> _flags = new HashSet<string>();
    private static uint _state = (uint)System.Environment.TickCount | 1u;

    public static IEnumerable<string> Flags => _flags;
    public static bool Tampered => _flags.Count > 0;

    public static void Flag(string kind) {
        if (_flags.Add(kind)) {
            UnityEngine.Debug.LogWarning("AntiCheat: " + kind);
        }
    }

    public static void Reset() {
        _flags.Clear();
    }

    // xorshift: cheap, and never 0
    public static int NextKey() {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (int)(_state | 1u);
    }
}
