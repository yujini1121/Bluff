using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IntroSlotHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private Image coinIcon;
    private int previousBalance;
    private float displayedBalance, balanceFrom, changeTime;
    private bool wasVisible;
    private Color baseColor;

    private void Awake()
    {
        if (coinText) baseColor = coinText.color;
        Hide();
    }

    public void Show(bool visible, int coins, bool deferChange = false)
    {
        if (!coinText) return;
        coinText.gameObject.SetActive(visible);
        if (!visible) { wasVisible = false; return; }
        if (deferChange && wasVisible) coins = previousBalance;
        if (visible)
        {
            if (!wasVisible)
            {
                displayedBalance = balanceFrom = previousBalance = coins;
                changeTime = 1;
            }
            else if (coins != previousBalance)
            {
                balanceFrom = displayedBalance;
                previousBalance = coins;
                changeTime = 0;
            }
            wasVisible = true;
            changeTime = Mathf.Min(1, changeTime + Time.unscaledDeltaTime / .55f);
            displayedBalance = Mathf.Lerp(balanceFrom, coins, Mathf.SmoothStep(0, 1, changeTime));
            coinText.text = changeTime >= 1 ? coins.ToString() : Mathf.RoundToInt(displayedBalance).ToString();
            float pulse = Mathf.Sin(changeTime * Mathf.PI);
            Color changeColor = coins >= balanceFrom ? new Color(.45f, 1, .7f) : new Color(1, .65f, .35f);
            coinText.color = Color.Lerp(baseColor, changeColor, pulse * .75f);
            if (coinIcon)
            {
                coinIcon.rectTransform.anchoredPosition = new Vector2(-coinText.preferredWidth - 8f, 0f);
                coinIcon.rectTransform.localScale = Vector3.one * (1 + pulse * .15f);
            }
        }
    }

    public void Hide()
    {
        wasVisible = false;
        if (coinText) coinText.gameObject.SetActive(false);
    }
}
