using System;
using System.Collections.Generic;
using UnityEngine;

// The mouse pointer is hidden while Freny is being played (the mouse buttons are the sword and
// the shot, and a pointer in the middle of the fight only confused), and comes back on every
// screen that is clicked: menus, pause, the end of a run, the shop. José, 2026-11-01: it is also
// locked in the middle of the game while playing, because in the browser the pointer wandered off
// the canvas and a sword click landed outside the game. In the web build that is the browser's
// pointer lock: it starts on the first click in the game, and Esc gives the pointer back, which
// GameMenus takes as a pause (LockLost).
public static class GameCursor {

    private struct Request {
        public UnityEngine.Object owner;
        public Func<bool> wants;
    }

    private static readonly List<Request> requests = new List<Request>();
    private static bool hidden;
    private static bool sawLocked;   // the lock was really on (the browser grants it on a click)
    private static bool lockLost;

    // a gameplay screen that needs the pointer while wants() is true (dropped with its owner)
    public static void ShowWhile(UnityEngine.Object owner, Func<bool> wants) {
        requests.Add(new Request { owner = owner, wants = wants });
    }

    // called every frame by GameMenus: playing is a gameplay scene, not paused, window focused
    public static void Apply(bool playing) {
        bool hide = playing && !Wanted();
        if (hide != hidden || Cursor.visible == hide) {
            hidden = hide;
            Cursor.visible = !hide;
        }
        // touch screens have no pointer to keep
        bool lockIt = hide && !GameMenus.SawTouch;
        if (!lockIt) {
            sawLocked = false;
            if (Cursor.lockState != CursorLockMode.None) {
                Cursor.lockState = CursorLockMode.None;
            }
            return;
        }
        if (Cursor.lockState == CursorLockMode.Locked) {
            sawLocked = true;
        } else if (sawLocked) {
            // it was on and something else turned it off: Esc in the browser (or in the editor)
            sawLocked = false;
            lockLost = true;
        } else {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    // once after the player took the pointer back while playing (GameMenus pauses)
    public static bool LockLost() {
        bool lost = lockLost;
        lockLost = false;
        return lost;
    }

    private static bool Wanted() {
        bool wanted = false;
        for (int i = requests.Count - 1; i >= 0; i--) {
            if (requests[i].owner == null) {
                requests.RemoveAt(i);
            } else if (!wanted && requests[i].wants()) {
                wanted = true;
            }
        }
        return wanted;
    }
}
