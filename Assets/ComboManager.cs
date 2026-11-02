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

    public void HandleComboManager() {
        SetCombo(combo += 0.1f);
        Sfx.Play("combo", 1f, Mathf.Min(1f + (combo - 1f) * 0.5f, 1.8f)); // higher with each kill in a row
        RestartBlinkingRoutine();
    }
    public void UpdateComboText() {
        if (timeRemaining <= 0) {
            combo = 1.0f;   
        }
        // building the string every frame created garbage for the GC; only rebuild it when it changes
        if (combo != shownCombo) {
            shownCombo = combo;
            comboText.text = combo.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }
    }

    public float GetCombo() {
        return combo;
    }


    public void SetCombo(float combo) {
        this.combo = combo;
    }

}
