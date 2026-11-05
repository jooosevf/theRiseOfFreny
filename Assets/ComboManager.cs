using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComboManager : MonoBehaviour
{

    public float totalTime;
    private float timeRemaining;
    private Text comboText;
    private float combo = 1.0f;
    private Color originalColor;
    private Coroutine mainCoroutine;
    private float shownCombo = -1f;
    private bool halfStep;   // a weak enemy's kill waiting for its pair (HandleComboManager)

    // when the multiplier last went up (CombatHud pops it)
    public float LastRise { get; private set; } = -10f;

    // Start is called before the first frame update
    void Start()
    {
        timeRemaining = totalTime;
        comboText = GetComponent<Text>();
        originalColor = comboText.color;
        // RestartBlinkingRoutine();
        UpdateComboText();
    }

    // Update is called once per frame
    void Update()
    {
        timeRemaining -= Time.deltaTime;
        UpdateComboText();
    }


    IEnumerator BlinkText() {
    while (timeRemaining > 0)
    {
        comboText.enabled = !comboText.enabled;

        // Adjust the blinking speed based on the remaining time
        float blinkSpeed = Mathf.Lerp(.01f, 0.8f, timeRemaining / totalTime);

        yield return new WaitForSeconds(blinkSpeed);
    }

    UpdateComboText();
    comboText.enabled = true; 
    }


    public void RestartBlinkingRoutine() {
        if (mainCoroutine != null) {
            StopCoroutine(mainCoroutine);
        }
        timeRemaining = totalTime;
        mainCoroutine = StartCoroutine(BlinkText());
    }

    // José, 2026-11-02: a weak enemy (Enemy.WeakKill) only counts half: two of them for each 0.1x, and
    // the one that doesn't raise it still keeps the combo alive. The ranking API knows the same
    // enemies (freny-online api/_lib/rules.js, "weak")
    public void HandleComboManager(bool weak) {
        if (weak && Features.WeakCombo) {
            halfStep = !halfStep;
        }
        if (!weak || !Features.WeakCombo || !halfStep) {
            SetCombo(combo += 0.1f);
            LastRise = Time.unscaledTime;
        }
        Sfx.Play("combo", 1f, Mathf.Min(1f + (combo - 1f) * 0.5f, 1.8f)); // higher with each kill in a row
        RestartBlinkingRoutine();
    }
    public void UpdateComboText() {
        if (timeRemaining <= 0) {
            combo = 1.0f;
            halfStep = false;
        }
        // building the string every frame created garbage for the GC; only rebuild it when it changes
        if (combo != shownCombo) {
            shownCombo = combo;
            comboText.text = combo.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }
    }

    public float TimeLeft => timeRemaining;

    // co-op: the room has one combo, and the host keeps it
    public void ShowFromHost(float value, float timeLeft) {
        if (value > combo + 0.01f) {
            LastRise = Time.unscaledTime;
            Sfx.Play("combo", 1f, Mathf.Min(1f + (value - 1f) * 0.5f, 1.8f));
        }
        combo = value;
        timeRemaining = timeLeft;
    }

    public float GetCombo() {
        return combo;
    }


    public void SetCombo(float combo) {
        this.combo = combo;
    }

}
