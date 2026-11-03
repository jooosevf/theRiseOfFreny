using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
    
    private static UIManager _instance;

    public static UIManager Instance {
        get {
            // many scenes have no UIManager; callers check for null (logging here spammed the console)
            return _instance;
        }
    }

    public Text coinCountShop;
    public Image selectionImg;
    public Text coinCountHUD;
    public Text scoreCountHUD;
    public Image[] healthBars;

    // the score lives here; scoreCountHUD only displays it (before, the text itself was parsed back every time)
    public int Score { get => _score; private set => _score = value; }
    private GuardedInt _score;   // Cheat Engine can't find it by value (AntiCheat)

    private ComboManager comboManager;

    // each kill that scores: the enemy and the combo multiplier it got. OnlineServices logs them so
    // the ranking server can check the run adds up to its score
    public static event Action<Enemy, float> KillScored;

    private void Awake() {
        _instance = this;
        if (scoreCountHUD && int.TryParse(scoreCountHUD.text, out int initialScore)) {
            Score = initialScore;
        }
        // José, 2026-11-03: by day the white number got lost against the clouds
        if (coinCountHUD && !coinCountHUD.GetComponent<Outline>()) {
            foreach (float thickness in new[] { 2f, 4f }) {
                Outline edge = coinCountHUD.gameObject.AddComponent<Outline>();
                edge.effectColor = Color.black;
                edge.effectDistance = new Vector2(thickness, -thickness);
            }
        }
    }

    // the HUD's coins, short so they don't grow across the screen: 999, 1k, 1.2k, 9.9k, 10k, 1m, 1.2m,
    // 10m (cut, never rounded up: 1,999 is 1.9k). The pause and the shops show the exact number
    public static string ShortCoins(int coins) {
        if (coins < 1000) {
            return coins.ToString();
        }
        return coins < 1000000 ? Short(coins, 1000, "k") : Short(coins, 1000000, "m");
    }

    private static string Short(int coins, int unit, string letter) {
        int whole = coins / unit;
        int tenths = coins / (unit / 10) % 10;
        return whole >= 10 || tenths == 0 ? whole + letter : whole + "." + tenths + letter;
    }

    //Optimize... 2 variables for the same thing?
    public void UpdateCoins(int coinCount) {
        if (coinCountHUD) {
            coinCountHUD.text = ShortCoins(coinCount);
        }
        if (coinCountShop) {
            coinCountShop.text = coinCount.ToString();
        }
    }

    public void UpdateScore(float newScore, bool add) {
        int amount = Mathf.RoundToInt(newScore);
        Score += add ? amount : -amount;
        if (scoreCountHUD) {
            scoreCountHUD.text = Score.ToString();
        }
    }

    public void UpdateShopSelection(int xPos, int yPos) {
        if (!selectionImg) {
            return;
        }
        selectionImg.rectTransform.anchoredPosition = new Vector2(xPos, yPos);
    }

    public void UpdateLives(int health, bool losingHealth) {
        // one bar per life: bars below the current health stay on, the rest go off
        // (losingHealth is kept so existing callers still compile)
        EnsureBars(health);
        for (int i = 0; i < healthBars.Length; i++) {
            if (healthBars[i]) {
                healthBars[i].enabled = i < health;
            }
        }
    }

    // the scenes have 4 hearts; the Armeira's Vida máxima adds more in the same row and spacing,
    // each with its empty heart behind, so a lost heart shows empty like the first four
    public void EnsureBars(int count) {
        if (healthBars == null || healthBars.Length < 2 || healthBars.Length >= count
            || healthBars[0] == null || healthBars[healthBars.Length - 1] == null) {
            return;
        }
        RectTransform first = healthBars[0].rectTransform;
        Image last = healthBars[healthBars.Length - 1];
        Vector2 spacing = (last.rectTransform.anchoredPosition - first.anchoredPosition) / (healthBars.Length - 1);
        List<Image> bars = new List<Image>(healthBars);
        AddEmptyHearts(last.rectTransform.parent, count - bars.Count);
        while (bars.Count < count) {
            Image heart = Instantiate(last, last.rectTransform.parent);
            heart.name = "Health_Bar_" + (bars.Count + 1);
            heart.rectTransform.anchoredPosition = last.rectTransform.anchoredPosition + spacing * (bars.Count - healthBars.Length + 1);
            bars.Add(heart);
        }
        healthBars = bars.ToArray();
    }

        // the empty hearts sit in "Empty_Hearts", next to the full ones' "Full_Hearts"
    private static void AddEmptyHearts(Transform fullRow, int extra) {
        Transform empties = fullRow != null && fullRow.parent != null ? fullRow.parent.Find("Empty_Hearts") : null;
        if (empties == null || empties.childCount < 2 || extra <= 0) {
            return;
        }
        RectTransform first = empties.GetChild(0) as RectTransform;
        RectTransform last = empties.GetChild(empties.childCount - 1) as RectTransform;
        if (first == null || last == null) {
            return;
        }
        Vector2 spacing = (last.anchoredPosition - first.anchoredPosition) / (empties.childCount - 1);
        for (int i = 1; i <= extra; i++) {
            RectTransform heart = Instantiate(last, empties);
            heart.name = "Heart_Empty_Bonus_" + i;
            heart.anchoredPosition = last.anchoredPosition + spacing * i;
        }
    }

    // José, 2026-11-01: while a Ciclope is on the floor every other kill is worth 1 point, combo or
    // not, so nobody farms points off the small ones around it (the kill log tells the server, RunLog)
    public static bool ScoresFlat(Enemy enemy) {
        return enemy.OnePointKill || (Features.CyclopsFlatScore && !(enemy is Cyclops) && Cyclops.AnyFighting);
    }

    // what killing it right now would pay (the Nuke's cap, ArenaPowerUps)
    public int PointsFor(Enemy enemy) {
        if (!comboManager) {
            comboManager = FindAnyObjectByType<ComboManager>();
        }
        return ScoresFlat(enemy) ? 1 : Mathf.RoundToInt(enemy.scoreToAdd * (comboManager ? comboManager.GetCombo() : 1f));
    }

        public void HandleComboManager(Enemy enemy) {
        if (!comboManager) {
            comboManager = FindAnyObjectByType<ComboManager>();
        }

        if (!comboManager) {
            // scenes without a combo counter just score the base value
            UpdateScore(ScoresFlat(enemy) ? 1 : enemy.scoreToAdd, true);
            KillScored?.Invoke(enemy, 1f);
            return;
        }

        // the combo still counts up on a 1-point kill
        UpdateScore(ScoresFlat(enemy) ? 1f : enemy.scoreToAdd * comboManager.GetCombo(), true); // todo -> each enemy = combo multiplier
        KillScored?.Invoke(enemy, comboManager.GetCombo());
        comboManager.HandleComboManager(enemy.WeakKill);
    }
}
