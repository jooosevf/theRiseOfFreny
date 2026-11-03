using System;
using UnityEngine;
using UnityEngine.UI;

// A button's text that follows a value while its page is open, such as "Conquistas (nova!)" going
// back to "Conquistas" once the page was opened and closed (MenuStack.Back doesn't rebuild the page).
public class LiveLabel : MonoBehaviour {

    private Text text;
    private Func<string> label;
    private Func<bool> highlight;
    private Color normal;

    public static void Attach(Button button, Func<string> label, Func<bool> highlight = null) {
        if (button == null) {
            return;
        }
        LiveLabel live = button.gameObject.AddComponent<LiveLabel>();
        live.text = button.GetComponentInChildren<Text>();
        live.label = label;
        live.highlight = highlight;
        live.normal = live.text != null ? live.text.color : Color.white;
        live.Refresh();
    }

    private void OnEnable() {
        Refresh();
    }

    private void Update() {
        Refresh();
    }

    private void Refresh() {
        if (text == null || label == null) {
            return;
        }
        string now = label();
        if (text.text != now) {
            text.text = now;
        }
        text.color = highlight != null && highlight() ? AchievementsScreen.Earned : normal;
    }
}
