using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemUseNotice : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private Sequence sequence;

    public static ItemUseNotice Create(Canvas canvas, TMP_FontAsset font)
    {
        var root = new GameObject("ItemUseNotice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.SetActive(false);
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -165f);
        rect.sizeDelta = new Vector2(Mathf.Clamp(((RectTransform)canvas.transform).rect.width - 48f, 240f, 660f), 72f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.035f, 0.035f, 0.085f, 0.94f);
        background.raycastTarget = false;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.25f, 0.52f, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accent.layer = root.layer;
        accent.transform.SetParent(rect, false);
        var accentRect = (RectTransform)accent.transform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(3f, 0f);
        accentRect.anchoredPosition = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(1f, 0.3f, 0.55f);
        accent.GetComponent<Image>().raycastTarget = false;

        var textObject = new GameObject("Reason", typeof(RectTransform));
        textObject.layer = root.layer;
        textObject.transform.SetParent(rect, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(24f, 10f);
        text.rectTransform.offsetMax = new Vector2(-24f, -10f);
        text.font = font;
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.96f, 0.96f, 1f);
        text.raycastTarget = false;
        text.richText = false;

        var notice = root.AddComponent<ItemUseNotice>();
        notice.canvasGroup = root.GetComponent<CanvasGroup>();
        notice.canvasGroup.blocksRaycasts = false;
        notice.canvasGroup.interactable = false;
        notice.canvasGroup.alpha = 0f;
        notice.messageText = text;
        return notice;
    }

    public void Show(ItemUseFailureReason reason)
    {
        sequence?.Kill(false);
        messageText.text = GetMessage(reason);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        sequence = DOTween.Sequence().SetAutoKill(true).SetUpdate(true);
        sequence.Append(canvasGroup.DOFade(1f, 0.15f));
        sequence.AppendInterval(2.4f);
        sequence.Append(canvasGroup.DOFade(0f, 0.45f).SetEase(Ease.InOutSine));
        sequence.OnComplete(() =>
        {
            sequence = null;
            gameObject.SetActive(false);
        });
    }

    public void Hide()
    {
        sequence?.Kill(false);
        sequence = null;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        sequence?.Kill(false);
        sequence = null;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public static string GetMessage(ItemUseFailureReason reason)
    {
        switch (reason)
        {
            case ItemUseFailureReason.NotOwned:
                return "플레이어가 보유한 아이템만 사용할 수 있습니다.";
            case ItemUseFailureReason.InvalidItem:
                return "아이템 정보를 확인할 수 없습니다.";
            case ItemUseFailureReason.NotBettingPhase:
                return "아이템은 베팅 중에만 사용할 수 있습니다.";
            case ItemUseFailureReason.NotYourTurn:
                return "아이템은 플레이어 턴에만 사용할 수 있습니다.";
            case ItemUseFailureReason.RefreshAfterBetting:
                return "이 아이템은 베팅 전에만 사용할 수 있습니다.";
            case ItemUseFailureReason.NoRaiseToDefy:
                return "이 아이템은 상대가 레이즈했을 때만 사용할 수 있습니다.";
            case ItemUseFailureReason.ActionInProgress:
                return "잠시 후 다시 사용해 주십시오.";
            default:
                return "지금은 이 아이템을 사용할 수 없습니다.";
        }
    }
}
