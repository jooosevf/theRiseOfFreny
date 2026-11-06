using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One arena run's blessing and mutator (ArenaVariety adds it to Freny):
//  - when a golden chest opens (OfferBlessing), the game waits while the player picks one of three
//    blessings. Only a click or a touch right on a card takes it (and only half a second after the
//    cards come up): Space / Enter / 1-2-3 used to take the first card mid-fight, since Space and
//    A are also the jump and 1-2 the weapons. On a gamepad: the d-pad picks a card and A held for
//    a second takes it;
//    The autopilot picks by Blessings.AutopilotOrder, without stopping the game;
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
    private const float ClickDelay = 0.5f;       // real seconds before a click on a card counts
    private const float PadHold = 1f;            // real seconds A is held on a card to take it

    private Player player;
    private FrenyInput input;
    private Dash dash;
    private GameManager gameManager;

    private Canvas pickerCanvas;
    private Button[] cards;
    private readonly System.Collections.Generic.List<Outline> cardEdges = new System.Collections.Generic.List<Outline>();
    private Blessing[] offered;
    private readonly List<Image> holdBars = new List<Image>();
    private float openedAt;
    private float padHeldSince = -1f;
    private bool flying;                         // the taken card's last part: the HUD shows again
    private int chosenIndex = -1;                // the card taken: its effect plays, nothing else counts
    private CanvasGroup panelGroup;
    private Image shadeImage;
    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    public static bool PickerOpen { get; private set; }   // CombatHud draws over canvases: it waits
    private bool picking;
    private float announcedAt = -100f;
    private float blessingAt = -100f;
    private Blessing lastTaken;

    private float potionBackAt = -1f;
    private float nextArmorAt;
    private int vampireKills;
    private readonly List<Coin> pulled = new List<Coin>();

    private GUIStyle textStyle;

    private IEnumerator Start() {
        player = GetComponent<Player>();
        input = GetComponent<FrenyInput>();
        dash = GetComponent<Dash>();
        gameManager = FindAnyObjectByType<GameManager>();
        Enemy.Killed += OnKilled;
        GameCursor.ShowWhile(this, () => picking);
        yield return null;  // the Player's Start (loadout, the Armeira's helmet) has run
        yield return null;
        StartMutator();
        announcedAt = Time.time;   // this week's rule
    }

    // the golden chest was opened: one more blessing for this run, if any is left
    public void OfferBlessing() {
        if (!Features.Blessings) {
            return;
        }
        offered = Blessings.Draw();
        if (offered.Length == 0 || player == null || player.IsDead) {
            return;
        }
        // only while the autopilot is really driving: with Auto on and the player at the keys
        // (his input takes over for 3 s), the cards come up as in a manual run
        if (input != null && input.IsAutopilotDriving) {
            foreach (Blessing blessing in Blessings.AutopilotOrder) {
                if (System.Array.IndexOf(offered, blessing) >= 0) {
                    Choose(blessing);
                    return;
                }
            }
        }
        OpenPicker();
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
        blessingAt = Time.time;
        lastTaken = blessing;
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

    // co-op client: a kill of this player's on the host (for the Vampiro)
    public void CreditKill() {
        OnKilled(null);
    }

    private void OnKilled(Enemy enemy) {
        if (enemy != null && CoopWorld.RemoteHit) {
            return;   // co-op host: another player's kill
        }
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
        if (player == null || player.IsDead || Blessings.Held.Count == 0) {
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

    private const float CoopPickSeconds = 6f;   // co-op: time to pick before a card is taken for him
    private const float CoopShieldAfter = 2f;   // and how long he stays safe after it (José, 2026-11-06)
    private Text countdown;

    private void OpenPicker() {
        picking = true;
        PickerOpen = true;
        openedAt = Time.unscaledTime;
        padHeldSince = -1f;
        KeepPaused();
        MenuKit.EnsureEventSystem();
        // under the pause menu (30000), over the HUD
        pickerCanvas = MenuKit.CreateCanvas("Blessings", 29000, null);
        Image shade = MenuKit.MakeImage("Shade", pickerCanvas.transform, MenuKit.ShadeColor);
        MenuKit.Stretch(shade.rectTransform);
        shadeImage = shade;
        chosenIndex = -1;

        Image panel = MenuKit.MakeImage("Panel", pickerCanvas.transform, MenuKit.PanelColor);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 400f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        Outline edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = MenuKit.ButtonColor;
        edge.effectDistance = new Vector2(3f, -3f);

        Text title = MenuKit.MakeText(panel.transform, "Escolha uma bênção", MenuKit.TitleSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0f, 160f), new Vector2(720f, 50f));
        Text sub = MenuKit.MakeText(panel.transform, "Vale até o fim da corrida e soma com as outras.",
            MenuKit.SmallTextSize, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
        Place(sub.rectTransform, new Vector2(0f, 122f), new Vector2(720f, 24f));
        countdown = null;
        if (CoopWorld.Active) {
            // the room goes on behind the cards: a lighter shade, and the time left
            shade.color = new Color(MenuKit.ShadeColor.r, MenuKit.ShadeColor.g, MenuKit.ShadeColor.b, MenuKit.ShadeColor.a * 0.4f);
            countdown = MenuKit.MakeText(panel.transform, "", MenuKit.SmallTextSize, MenuKit.TitleColor, TextAnchor.MiddleCenter);
            Place(countdown.rectTransform, new Vector2(0f, -178f), new Vector2(720f, 24f));
        }

        cards = new Button[offered.Length];
        cardEdges.Clear();
        holdBars.Clear();
        for (int i = 0; i < offered.Length; i++) {
            cards[i] = Card(panel.transform, i, offered[i], new Vector2((i - (offered.Length - 1) / 2f) * 228f, -10f));
        }
        for (int i = 0; i < cards.Length; i++) {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = cards[(i - 1 + cards.Length) % cards.Length];
            nav.selectOnRight = cards[(i + 1) % cards.Length];
            cards[i].navigation = nav;
        }
        Text hint = MenuKit.MakeText(panel.transform, "Clique ou toque na bênção que quiser (no controle: escolha e segure A)",
            MenuKit.SmallTextSize - 2, MenuKit.DimTextColor, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0f, -150f), new Vector2(720f, 24f));
        // nothing comes selected: a Space, Enter or A from the fight has nothing to press
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // a card only a pointer (mouse, touch) takes, and only when the press began after the delay:
    // Submit (Space, Enter, A) does nothing on it
    private class ClickOnlyCard : Button {
        public float armedAt;
        private bool pressedArmed;

        public override void OnPointerDown(PointerEventData eventData) {
            pressedArmed = eventData.button == PointerEventData.InputButton.Left && Time.unscaledTime >= armedAt;
            base.OnPointerDown(eventData);
        }

        public override void OnPointerClick(PointerEventData eventData) {
            if (pressedArmed && eventData.button == PointerEventData.InputButton.Left) {
                base.OnPointerClick(eventData);
            }
            pressedArmed = false;
        }

        public override void OnSubmit(BaseEventData eventData) { }
    }

    private Button MakeCard(Transform parent, Action onClick) {
        Image background = MenuKit.MakeImage("Button", parent, Color.white);
        ClickOnlyCard button = background.gameObject.AddComponent<ClickOnlyCard>();
        button.armedAt = openedAt + ClickDelay;
        button.targetGraphic = background;
        button.colors = MenuKit.Colors();
        button.onClick.AddListener(() => onClick());
        Text text = MenuKit.MakeText(background.transform, "", MenuKit.TextSize, MenuKit.TextColor, TextAnchor.MiddleCenter);
        MenuKit.Stretch(text.rectTransform);
        return button;
    }

    private Button Card(Transform parent, int index, Blessing blessing, Vector2 position) {
        Blessings.Info info = Blessings.Of(blessing);
        Button card = MakeCard(parent, () => Pick(index));
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

        // fills while A is held on this card
        Image bar = MenuKit.MakeImage("Hold", card.transform, MenuKit.SelectedColor);
        bar.raycastTarget = false;
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 0f);
        barRect.pivot = new Vector2(0f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 6f);
        holdBars.Add(bar);

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
        if (CoopWorld.Active) {
            // José, 2026-11-06: a room never stops for one player. He picks quickly, can't be hit
            // meanwhile and for CoopShieldAfter seconds after, the enemies leave him alone, and when
            // time is up a card is taken for him
            if (player != null) {
                player.Shield(CoopShieldAfter);
            }
            CoopWorld.ShieldedUntil = Time.time + CoopShieldAfter;
            float left = openedAt + CoopPickSeconds - Time.unscaledTime;
            if (countdown != null) {
                countdown.text = chosenIndex >= 0 ? "" : $"Escolha rápido: {Mathf.CeilToInt(Mathf.Max(0f, left))} s";
            }
            if (left <= 0f && chosenIndex < 0 && offered != null && offered.Length > 0) {
                Pick(UnityEngine.Random.Range(0, offered.Length));
            }
        } else {
            Time.timeScale = 0f;
        }
        InputBindings.BlockGameplay(true);
        if (cards == null || chosenIndex >= 0) {
            return;
        }
        EventSystem events = EventSystem.current;
        if (events != null) {
            GameObject selected = events.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(pickerCanvas.transform)) {
                // the mouse left nothing selected: the first key or stick push selects again
                bool moved = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f ||
                             selected != null;
                if (moved) {
                    events.SetSelectedGameObject(cards[0].gameObject);
                }
            }
        }
        // the picked card's edge
        GameObject current = events != null ? events.currentSelectedGameObject : null;
        int on = -1;
        for (int i = 0; i < cards.Length && i < cardEdges.Count; i++) {
            cardEdges[i].enabled = cards[i] != null && current == cards[i].gameObject;
            if (cardEdges[i].enabled) {
                on = i;
            }
        }
        // a gamepad's A held on the card takes it; a press that began before the delay (a jump
        // held from the fight) never counts
        float now = Time.unscaledTime;
        if (on >= 0 && Input.GetKeyDown(KeyCode.JoystickButton0) && now >= openedAt + ClickDelay) {
            padHeldSince = now;
        }
        if (on < 0 || !Input.GetKey(KeyCode.JoystickButton0)) {
            padHeldSince = -1f;
        }
        float held = padHeldSince < 0f ? 0f : Mathf.Clamp01((now - padHeldSince) / PadHold);
        for (int i = 0; i < holdBars.Count; i++) {
            if (holdBars[i] != null) {
                holdBars[i].rectTransform.sizeDelta = new Vector2(i == on ? 216f * held : 0f, 6f);
            }
        }
        if (held >= 1f) {
            padHeldSince = -1f;
            Pick(on);
        }
    }

    private void Pick(int index) {
        if (!picking || chosenIndex >= 0 || GameMenus.Paused || index < 0 || index >= offered.Length) {
            return;
        }
        chosenIndex = index;
        Sfx.Ui("upgrade");
        StartCoroutine(PickEffect(index));
    }

    // the taken card: a gold edge, the rest of the screen fades out leaving only it, then it flies
    // to its slot under the weapons (where the held blessings show) and the run goes on.
    // Real time: the game stays paused until it ends.
    private IEnumerator PickEffect(int index) {
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(null);
        }
        GraphicRaycaster raycaster = pickerCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) {
            raycaster.enabled = false;   // no more clicks on anything
        }
        // the button's tint multiplies the whole card, gold edge included (it came out brown): bake
        // the card's colour into the image and drop the tint
        Button button = cards[index];
        Color cardColor = button.colors.highlightedColor;
        button.transition = Selectable.Transition.None;
        if (button.targetGraphic != null) {
            button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);   // also stops the running tint fade
            button.targetGraphic.color = cardColor;
        }
        RectTransform card = (RectTransform)button.transform;
        card.SetParent(pickerCanvas.transform, true);   // out of the panel, so it stays when the panel goes
        CanvasGroup cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        Outline edge = index < cardEdges.Count ? cardEdges[index] : null;
        if (edge != null) {
            edge.enabled = true;
            edge.effectColor = Gold;
            edge.effectDistance = new Vector2(5f, -5f);
        }
        Outline glow = card.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
        glow.effectDistance = new Vector2(10f, -10f);
        Color shadeFrom = shadeImage != null ? shadeImage.color : Color.clear;
        Vector3 from = card.position;

        // 1: the others fade out, the card grows a little and its edge pulses
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) {
            float k = t / 0.35f;
            panelGroup.alpha = 1f - k;
            card.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, Smooth(k));
            glow.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f + 0.35f * Mathf.Sin(k * Mathf.PI));
            yield return WhileMenuPaused();
        }
        panelGroup.alpha = 0f;
        // 2: only the card, a moment to read it
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime) {
            glow.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f + 0.25f * Mathf.Sin(t * 14f));
            yield return WhileMenuPaused();
        }
        // 3: it flies to its slot and shrinks, the shade lifts; the HUD comes back so it lands on it
        flying = true;
        PickerOpen = false;
        float scale = Screen.width / 1920f;
        float box = 40f * scale;
        int slot = Blessings.Held.Count;
        Vector3 to = new Vector3(74f * scale + slot * (box + 4f * scale) + box / 2f, Screen.height - (300f * scale + box / 2f), 0f);
        float startScale = card.localScale.x;
        float endScale = Mathf.Max(0.05f, box / Mathf.Max(1f, card.rect.width * card.lossyScale.x / startScale));
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) {
            float k = Smooth(t / 0.4f);
            card.position = Vector3.Lerp(from, to, k);
            card.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k);
            cardGroup.alpha = k < 0.8f ? 1f : 1f - (k - 0.8f) / 0.2f;
            if (shadeImage != null) {
                shadeImage.color = new Color(shadeFrom.r, shadeFrom.g, shadeFrom.b, shadeFrom.a * (1f - k));
            }
            yield return WhileMenuPaused();
        }
        card.position = to;   // a long frame can end the loop short of the slot
        Sfx.Ui("ui_select");
        ClosePicker();
        Choose(offered[index]);
    }

    private static float Smooth(float k) => k * k * (3f - 2f * k);

    // the pause menu over the cards holds the effect too
    private static IEnumerator WhileMenuPaused() {
        do {
            yield return null;
        } while (GameMenus.Paused);
    }

    private void ClosePicker() {
        picking = false;
        PickerOpen = false;
        chosenIndex = -1;
        flying = false;
        panelGroup = null;
        shadeImage = null;
        cards = null;   // gone with the canvas: the next chest's picker must not touch them
        cardEdges.Clear();
        holdBars.Clear();
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
        Mutator? mutator = WeeklyMutators.ThisRun;
        if ((picking && !flying) || GameMenus.Paused || player == null || (Blessings.Held.Count == 0 && !mutator.HasValue)) {
            return;
        }
        float scale = Screen.width / 1920f;
        if (textStyle == null) {
            textStyle = new GUIStyle(GUI.skin.label) { font = MenuKit.Font, wordWrap = false, clipping = TextClipping.Overflow };
        }

        if (!player.IsDead) {
            // under the weapons row of CombatHud, at the portrait's left edge: every blessing he holds
            float x = 74f * scale, y = 300f * scale, box = 40f * scale, gap = 4f * scale;
            for (int i = 0; i < Blessings.Held.Count; i++) {
                Rect slot = new Rect(x + i * (box + gap), y, box, box);
                GUI.color = new Color(0.04f, 0.02f, 0.08f, 0.7f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture);
                GUI.color = Color.white;
                Sprite icon = Blessings.Icon(Blessings.Held[i]);
                if (icon != null) {
                    DrawSprite(new Rect(slot.x + 4f * scale, slot.y + 4f * scale, box - 8f * scale, box - 8f * scale), icon);
                }
            }
            if (mutator.HasValue) {
                textStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(10f * scale));
                float below = Blessings.Held.Count > 0 ? box + gap : 0f;
                Label(new Rect(x, y + below, 400f * scale, 18f * scale), WeeklyMutators.Of(mutator.Value).name, MenuKit.DimTextColor);
            }
        }

        TextAnchor old = textStyle.alignment;
        textStyle.alignment = TextAnchor.MiddleCenter;
        float line = Screen.height * 0.24f;
        float sinceBlessing = Time.time - blessingAt;
        if (sinceBlessing >= 0f && sinceBlessing < AnnounceSeconds) {
            textStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(22f * scale));
            Label(new Rect(0f, line, Screen.width, 40f * scale), "Bênção: " + Blessings.Of(lastTaken).name,
                Fade(MenuKit.TitleColor, Mathf.Clamp01((AnnounceSeconds - sinceBlessing) / 0.8f)));
        }
        float since = Time.time - announcedAt;
        if (mutator.HasValue && since >= 0f && since < AnnounceSeconds) {
            float alpha = Mathf.Clamp01((AnnounceSeconds - since) / 0.8f);
            textStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(22f * scale));
            Label(new Rect(0f, line + 44f * scale, Screen.width, 36f * scale), WeeklyMutators.Title(mutator.Value), Fade(new Color(1f, 0.85f, 0.3f), alpha));
            textStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(13f * scale));
            Label(new Rect(0f, line + 84f * scale, Screen.width, 28f * scale), WeeklyMutators.Of(mutator.Value).what, Fade(Color.white, alpha));
        }
        textStyle.alignment = old;
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
