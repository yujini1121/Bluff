using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IntroSlotHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private Image coinIcon;

    private void Awake() => Hide();

    public void Show(bool visible, int coins)
    {
        if (!coinText) return;
        coinText.gameObject.SetActive(visible);
        if (visible)
        {
            coinText.text = coins.ToString();
            if (coinIcon)
                coinIcon.rectTransform.anchoredPosition = new Vector2(-coinText.preferredWidth - 8f, 0f);
        }
    }

    public void Hide()
    {
        if (coinText) coinText.gameObject.SetActive(false);
    }
}
