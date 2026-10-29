using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class ItemUseNotice : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float holdDuration = 2.4f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.45f;
    [SerializeField] private Ease fadeInEase = Ease.OutQuad;
    [SerializeField] private Ease fadeOutEase = Ease.InOutSine;

    [Header("Responsive Layout")]
    [SerializeField, Min(0f)] private float horizontalMargin = 24f;

    private Sequence sequence;
    private float preferredWidth;

    public static ItemUseNotice Create(ItemUseNotice prefab, Canvas canvas, TMP_FontAsset fallbackFont = null)
    {
        if (prefab == null || canvas == null) return null;
        ItemUseNotice notice = Instantiate(prefab, canvas.transform, false);
        notice.name = prefab.name;
        if (notice.messageText.font == null && fallbackFont != null)
            notice.messageText.font = fallbackFont;
        notice.preferredWidth = ((RectTransform)notice.transform).sizeDelta.x;
        notice.ResizeToCanvas();
        notice.Hide();
        return notice;
    }

    private void ResizeToCanvas()
    {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        if (parent == null) return;
        float availableWidth = Mathf.Max(0f, parent.rect.width - horizontalMargin * 2f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(preferredWidth, availableWidth));
    }

    public void Show(ItemUseFailureReason reason)
    {
        sequence?.Kill(false);
        ResizeToCanvas();
        messageText.text = GetMessage(reason);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        sequence = DOTween.Sequence().SetAutoKill(true).SetUpdate(true);
        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeInEase));
        sequence.AppendInterval(holdDuration);
        sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase));
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
