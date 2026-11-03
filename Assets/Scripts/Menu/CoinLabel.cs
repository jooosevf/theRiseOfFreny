using System;
using UnityEngine;
using UnityEngine.UI;

// "Moedas: 1234" with the coin before it, that follows the saved coins while its page is open
// (José, 2026-11-03: spend at the Armeira, go back, and the number must already be the new one;
// and every place that shows coins shows the coin). Also the coin after a price on a shop row.
public class CoinLabel : MonoBehaviour {

    private Text text;
    private Func<string> value;

    public static Sprite Coin {
        get {
            MenuArt art = MenuArt.Get;
            return art != null ? MenuArt.Trim(art.coin) : null;
        }
    }

    // a centred row in a page's column (or any layout group)
    public static CoinLabel Create(Transform parent, int size, Color color, Func<string> value = null) {
        RectTransform row = MenuKit.MakeRect("Coins", parent);
        MenuKit.Size(row, -1f, size + 12f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Image icon = MenuKit.MakeImage("Coin", row, Color.white);
        icon.sprite = Coin;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        MenuKit.Size(icon, size + 8f, size + 8f);

        CoinLabel label = row.gameObject.AddComponent<CoinLabel>();
        label.text = MenuKit.MakeText(row, "", size, color, TextAnchor.MiddleLeft);
        label.text.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.value = value ?? (() => "Moedas: " + FrenyProgress.Coins);
        label.Refresh();
        return label;
    }

    private void OnEnable() {
        Refresh();
    }

    private void Update() {
        Refresh();
    }

    private void Refresh() {
        if (text == null || value == null) {
            return;
        }
        string now = value();
        if (text.text != now) {
            text.text = now;
        }
    }

    // a shop row's price (InventoryScreens.ItemButton) with the coin after it when it is paid in coins;
    // anything else there ("comprado", "3 pontos", "Usando") goes without
    public static void SetPrice(Text price, string label, bool coins) {
        price.text = label;
        Transform button = price.transform.parent;
        Transform found = button.Find("Price Coin");
        Image icon = found != null ? found.GetComponent<Image>() : null;
        if (icon == null && coins && Coin != null) {
            icon = MenuKit.MakeImage("Price Coin", button, Color.white);
            icon.sprite = Coin;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            rect.sizeDelta = Vector2.one * (price.fontSize + 8f);
        }
        bool show = coins && icon != null;
        if (icon != null) {
            icon.enabled = show;
        }
        Vector2 max = price.rectTransform.offsetMax;
        price.rectTransform.offsetMax = new Vector2(show ? -12f - (price.fontSize + 8f) - 6f : -12f, max.y);
    }
}
