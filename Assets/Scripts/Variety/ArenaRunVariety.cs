using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One arena run's blessing and mutator (ArenaVariety adds it to Freny):
//  - at the start, the game waits while the player picks any of the ten blessings:
//    cards, clicked, touched, or picked with the arrows / d-pad and Enter / A, or 1-9 and 0.
//    A run the autopilot starts gets Blessings.AutopilotChoice at once (nobody is there to pick:
//    José, 2026-11-01, the idle arena keeps going on its own);
//  - the blessing's effects that are not plain stats (RunModifiers covers those): the potion that
//    comes back, the armor every 2 minutes, the heart every 20 kills, the coin magnet, the dash;
//  - the mutator's start (Arsenal, Pés de vento);
//  - a small badge under the weapons with the blessing's icon and the mutator's name, and both
//    announced in the middle of the screen when the run begins.
public class ArenaRunVariety : MonoBehaviour {

    private const float PotionBack = 45f;        // Frasco cheio
    private const float ArmorEvery = 120f;       // Pele de pedra
    private const int KillsPerHeart = 20;        // Vampiro
    private const float MagnetRadius = 4f;       // Ímã de moedas, units
    private const float MagnetSpeed = 9f;
    private const float AnnounceSeconds = 5f;

    private Player player;
    private FrenyInput input;
    private Dash dash;
    private GameManager gameManager;

    private Canvas pickerCanvas;
    private Button[] cards;
    private readonly System.Collections.Generic.List<Outline> cardEdges = new System.Collections.Generic.List<Outline>();
    private Blessing[] offered;
    public static bool PickerOpen { get; private set; }   // CombatHud draws over canvases: it waits
    private bool picking;
    private float announcedAt = -100f;

    private float potionBackAt = -1f;
    private float nextArmorAt;
    private int vampireKills;
    private readonly List<Coin> pulled = new List<Coin>();

    private GUIStyle textStyle;

    public bool Picking => picking;

    private IEnumerator Start() {
        player = GetComponent<Player>();
        input = GetComponent<FrenyInput>();
        dash = GetComponent<Dash>();
        gameManager = FindAnyObjectByType<GameManager>();
        Enemy.Killed += OnKilled;
        GameCursor.ShowWhile(this, () => picking);
        offered = System.Array.ConvertAll(Blessings.All, info => info.id);
        if (input == null || !input.AutoEnabled) {
            OpenPicker();   // at once, before the first frame of play
        }
        yield return null;  // the Player's Start (loadout, the Armeiro's helmet) has run
        yield return null;
        StartMutator();
        if (!picking && !Blessings.Current.HasValue) {
            Choose(Blessings.AutopilotChoice);
        }
    }

    private void OnDestroy() {
        Enemy.Killed -= OnKilled;
        if (picking) {
            ClosePicker();
        }
    }

    // ==== the mutator's start =================================================================

    private void StartMutator() {
        if (player == null) {
            return;
        }
        if (WeeklyMutators.Active(Mutator.Arsenal)) {
            player.GiveShotgun(12);
            player.AddGrenades(4);
        }
        if (WeeklyMutators.Active(Mutator.SwiftFeet)) {
            if (dash != null) {
                dash.dashCooldown *= 0.5f;
            }
            player.ApplyUpgrades();   // the speed (RunModifiers.SpeedMultiplier)
        }
    }

    // ==== the blessing ========================================================================

    private void Choose(Blessing blessing) {
        Blessings.Choose(blessing);
        announcedAt = Time.time;
        if (player == null) {
            return;
        }
        player.ApplyUpgrades();   // hearts, speed, sword cooldown (RunModifiers)
        switch (blessing) {
            case Blessing.QuickDash:
                if (dash != null) {
                    dash.dashVelocity *= 1.3f;
                    dash.dashCooldown *= 0.5f;
                }
                break;
            case Blessing.FullFlask:
                if (gameManager != null) {
                    gameManager.HasPotion = true;
                }
                break;
            case Blessing.Grenadier:
                player.AddGrenades(3);
                break;
            case Blessing.StoneSkin:
                player.GiveArmor();
                nextArmorAt = Time.time + ArmorEvery;
                break;
        }
    }

    private void OnKilled(Enemy enemy) {
        if (!Blessings.Has(Blessing.Vampire) || player == null || player.IsDead) {
            return;
        }
        vampireKills++;
        if (vampireKills >= KillsPerHeart) {
            if (player.CollectPotion(1)) {
                vampireKills -= KillsPerHeart;
            } else {
                vampireKills = KillsPerHeart;   // full: the heart waits for the next kill after a hit
            }
        }
    }

    private void Update() {
        if (picking) {
            KeepPaused();
            return;
        }
        if (player == null || player.IsDead || !Blessings.Current.HasValue) {
            return;
        }
        if (Blessings.Has(Blessing.FullFlask) && gameManager != null) {
            if (gameManager.HasPotion) {
                potionBackAt = -1f;
            } else if (potionBackAt < 0f) {
                potionBackAt = Time.time + PotionBack;
            } else if (Time.time >= potionBackAt) {
                gameManager.HasPotion = true;
                potionBackAt = -1f;
                DamageNumbers.Show(player.HeadPoint + Vector3.up * 0.4f, "+Poção", DamageNumbers.Healed);
            }
        }
        if (Blessings.Has(Blessing.StoneSkin) && Time.time >= nextArmorAt) {
            player.GiveArmor();
            nextArmorAt = Time.time + ArmorEvery;
        }
        if (Blessings.Has(Blessing.CoinMagnet)) {
            PullCoins();
        }
    }

    // coins near him leave the floor and fly to him; their own trigger is off meanwhile, so each
    // is counted once, here
    private void PullCoins() {
        Vector3 target = player.BodyCenter;
        foreach (Coin coin in Coin.Active) {
            if (coin != null && coin.enabled && (coin.transform.position - target).sqrMagnitude <= MagnetRadius * MagnetRadius) {
                pulled.Add(coin);
            }
        }
        for (int i = pulled.Count - 1; i >= 0; i--) {
            Coin coin = pulled[i];
            if (coin == null) {
                pulled.RemoveAt(i);
                continue;
            }
            if (coin.enabled) {
                coin.enabled = false;   // no more bobbing, blinking or expiring
                foreach (Collider2D c in coin.GetComponentsInChildren<Collider2D>()) {
                    c.enabled = false;
                }
                SpriteRenderer sprite = coin.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null) {
                    sprite.enabled = true;
                }
            }
            Vector3 p = coin.transform.position;
            float speed = MagnetSpeed + Mathf.Max(0f, MagnetRadius - Vector3.Distance(p, target)) * 4f;
            p = Vector3.MoveTowards(p, target, speed * Time.deltaTime);
            coin.transform.position = p;
            if ((p - target).sqrMagnitude < 0.3f * 0.3f) {
                player.AddCoins(RunModifiers.ScaleCoins(coin.coins));
                Sfx.Play("coin", p);
                pulled.RemoveAt(i);
                Destroy(coin.gameObject);
            }
        }
    }

    // ==== the picker ==========================================================================

    private void OpenPicker() {
        picking = true;
        PickerOpen = true;
        KeepPaused();
        MenuKit.EnsureEventSystem();
        // under the pause menu (30000), over the HUD
        pickerCanvas = MenuKit.CreateCanvas("Blessings", 29000, null);
        Image shade = MenuKit.MakeImage("Shade", pickerCanvas.transform, MenuKit.ShadeColor);
        MenuKit.Stretch(shade.rectTransform);

        Image panel = MenuKit.MakeImage("Panel", pickerCanvas.transform, MenuKit.PanelColor);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1180f, 560f);
        rect.anchoredPosition = new Vector2(0f, -20f);   // under the score
        Outline edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = MenuKit.ButtonColor;
        edge.effectDistance = new Vector2(3f, -3f);

        Text title = MenuKit.MakeText(panel.transform, "Escolha uma bênção", MenuKit.TitleSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0f, 250f), new Vector2(1000f, 50f));
        Mutator mutator = WeeklyMutators.ThisRun ?? WeeklyMutators.ThisWeek;
        Text sub = MenuKit.MakeText(panel.transform,
            "Vale a corrida toda. " + WeeklyMutators.Title(mutator) + ": " + WeeklyMutators.Of(mutator).what,
            MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
        Place(sub.rectTransform, new Vector2(0f, 205f), new Vector2(1100f, 44f));

        cards = new Button[offered.Length];
        cardEdges.Clear();
        for (int i = 0; i < offered.Length; i++) {
            int column = i % CardColumns, row = i / CardColumns;
            cards[i] = Card(panel.transform, i, offered[i], new Vector2((column - (CardColumns - 1) / 2f) * 228f, 76f - row * 200f));
        }
        for (int i = 0; i < cards.Length; i++) {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = cards[(i - 1 + cards.Length) % cards.Length];
            nav.selectOnRight = cards[(i + 1) % cards.Length];
            nav.selectOnUp = cards[(i - CardColumns + cards.Length) % cards.Length];
            nav.selectOnDown = cards[(i + CardColumns) % cards.Length];
            cards[i].navigation = nav;
        }
        Text hint = MenuKit.MakeText(panel.transform, "Clique, toque ou use as setas e Enter (ou as teclas de 1 a 9 e 0)",
            MenuKit.SmallTextSize - 2, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0f, -250f), new Vector2(1000f, 24f));
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(cards[0].gameObject);
        }
    }

    private const int CardColumns = 5;

    private Button Card(Transform parent, int index, Blessing blessing, Vector2 position) {
        Blessings.Info info = Blessings.Of(blessing);
        Button card = MenuKit.MakeButton(parent, "", () => Pick(index));
        // the menus' bright selection colour drowned the card's pink name and its text: a card
        // lights up halfway, and its edge shows which one is picked
        ColorBlock colors = MenuKit.Colors();
        colors.highlightedColor = colors.selectedColor = Color.Lerp(MenuKit.ButtonColor, MenuKit.SelectedColor, 0.45f);
        card.colors = colors;
        Outline edge = card.gameObject.AddComponent<Outline>();
        edge.effectColor = MenuKit.SelectedColor;
        edge.effectDistance = new Vector2(3f, -3f);
        edge.enabled = false;
        cardEdges.Add(edge);
        RectTransform rect = card.GetComponent<RectTransform>();
        Place(rect, position, new Vector2(216f, 190f));

        Image icon = MenuKit.MakeImage("Icon", card.transform, Color.white);
        icon.sprite = Blessings.Icon(blessing);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        Place(icon.rectTransform, new Vector2(0f, 58f), new Vector2(48f, 48f));

        Text label = card.GetComponentInChildren<Text>();
        label.text = info.name;
        label.color = MenuKit.TitleColor;
        Place(label.rectTransform, new Vector2(0f, 16f), new Vector2(208f, 30f));

        Text what = MenuKit.MakeText(card.transform, info.what, MenuKit.SmallTextSize, MenuKit.TextColor, TextAnchor.UpperCenter);
        Place(what.rectTransform, new Vector2(0f, -46f), new Vector2(204f, 92f));

        Text key = MenuKit.MakeText(card.transform, ((index + 1) % 10).ToString(), MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.UpperLeft);
        Place(key.rectTransform, new Vector2(-92f, 78f), new Vector2(30f, 24f));
        return card;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // the run waits while the cards are up; the pause menu over them pauses and resumes on its own
    // (and its Resume puts the time back), so this takes it again once the pause menu is gone
    private void KeepPaused() {
        if (GameMenus.Paused) {
            return;
        }
        Time.timeScale = 0f;
        InputBindings.BlockGameplay(true);
        if (cards == null) {
            return;
        }
        for (int i = 0; i < cards.Length; i++) {
            // 1..9, then 0 for the tenth
            if (Input.GetKeyDown(i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0) || Input.GetKeyDown(i < 9 ? KeyCode.Keypad1 + i : KeyCode.Keypad0)) {
                Pick(i);
                return;
            }
        }
        EventSystem events = EventSystem.current;
        if (events != null) {
            GameObject selected = events.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(pickerCanvas.transform)) {
                // the mouse left nothing selected: the first key or stick push selects again
                bool moved = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f ||
                             Input.GetButtonDown("Submit") || selected != null;
                if (moved) {
                    events.SetSelectedGameObject(cards[0].gameObject);
                }
            }
        }
        // the picked card's edge
        GameObject current = events != null ? events.currentSelectedGameObject : null;
        for (int i = 0; i < cards.Length && i < cardEdges.Count; i++) {
            cardEdges[i].enabled = cards[i] != null && current == cards[i].gameObject;
        }
    }

    private void Pick(int index) {
        if (!picking || GameMenus.Paused || index < 0 || index >= offered.Length) {
            return;
        }
        Sfx.Ui("upgrade");
        ClosePicker();
        Choose(offered[index]);
    }

    private void ClosePicker() {
        picking = false;
        PickerOpen = false;
        if (pickerCanvas != null) {
            Destroy(pickerCanvas.gameObject);
        }
        if (!GameMenus.Paused) {
            InputBindings.BlockGameplay(false);
            GameSpeed.Apply();
        }
    }

    // ==== the badge and the announcement ======================================================

    private void OnGUI() {
        if (picking || GameMenus.Paused || player == null || !Blessings.Current.HasValue) {
            return;
        }
        float scale = Screen.width / 1920f;
        if (textStyle == null) {
            textStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, wordWrap = false, clipping = TextClipping.Overflow };
        }
        Blessing blessing = Blessings.Current.Value;
        Mutator? mutator = WeeklyMutators.ThisRun;

        if (!player.IsDead) {
            // under the weapons row of CombatHud, at the portrait's left edge
            float x = 74f * scale, y = 300f * scale, box = 46f * scale;
            GUI.color = new Color(0.04f, 0.02f, 0.08f, 0.7f);
            GUI.DrawTexture(new Rect(x, y, box, box), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Sprite icon = Blessings.Icon(blessing);
            if (icon != null) {
                DrawSprite(new Rect(x + 4f * scale, y + 4f * scale, box - 8f * scale, box - 8f * scale), icon);
            }
            textStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(12f * scale));
            Label(new Rect(x + box + 8f * scale, y + 2f * scale, 400f * scale, 20f * scale), Blessings.Of(blessing).name, MenuKit.TitleColor);
            if (mutator.HasValue) {
                textStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(10f * scale));
                Label(new Rect(x + box + 8f * scale, y + 24f * scale, 400f * scale, 18f * scale), WeeklyMutators.Of(mutator.Value).name, MenuKit.DimTextColor);
            }
        }

        float since = Time.time - announcedAt;
        if (since >= 0f && since < AnnounceSeconds) {
            float alpha = Mathf.Clamp01((AnnounceSeconds - since) / 0.8f);
            TextAnchor old = textStyle.alignment;
            textStyle.alignment = TextAnchor.MiddleCenter;
            float y = Screen.height * 0.24f;
            textStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(22f * scale));
            Label(new Rect(0f, y, Screen.width, 40f * scale), "Bênção: " + Blessings.Of(blessing).name, Fade(MenuKit.TitleColor, alpha));
            if (mutator.HasValue) {
                Label(new Rect(0f, y + 44f * scale, Screen.width, 36f * scale), WeeklyMutators.Title(mutator.Value), Fade(new Color(1f, 0.85f, 0.29f), alpha));
                textStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(13f * scale));
                Label(new Rect(0f, y + 84f * scale, Screen.width, 28f * scale), WeeklyMutators.Of(mutator.Value).what, Fade(Color.white, alpha));
            }
            textStyle.alignment = old;
        }
    }

    private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

    private void Label(Rect rect, string text, Color color) {
        Color shadow = new Color(0f, 0f, 0f, color.a * 0.85f);
        textStyle.normal.textColor = shadow;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, textStyle);
        textStyle.normal.textColor = color;
        GUI.Label(rect, text, textStyle);
    }

    private static void DrawSprite(Rect rect, Sprite sprite) {
        Texture tex = sprite.texture;
        Rect r = sprite.textureRect;
        float aspect = r.width / r.height;
        if (aspect > rect.width / rect.height) {
            float h = rect.width / aspect;
            rect = new Rect(rect.x, rect.y + (rect.height - h) / 2f, rect.width, h);
        } else {
            float w = rect.height * aspect;
            rect = new Rect(rect.x + (rect.width - w) / 2f, rect.y, w, rect.height);
        }
        GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
    }
}
