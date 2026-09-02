using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class TitleMenuButtonFX : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private RectTransform underline;
    [SerializeField] private CanvasGroup visualGroup;

    [Header("Hover")]
    [SerializeField] private float hoverOffsetX = 10f;
    [SerializeField] private float hoverDuration = 0.15f;

    [Header("Other Button")]
    [SerializeField] private float dimAlpha = 0.45f;

    private TitleMenuController controller;
    private Vector2 basePosition;
    private bool isHovered;

    private void Awake()
    {
        basePosition = visualRoot.anchoredPosition;

        if (underline != null)
        {
            Vector3 scale = underline.localScale;
            scale.x = 0f;
            underline.localScale = scale;
        }
    }

    public void Initialize(TitleMenuController owner)
    {
        controller = owner;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        controller?.SetHovered(this);

        visualRoot.DOAnchorPosX(
            basePosition.x + hoverOffsetX,
            hoverDuration
        ).SetEase(Ease.OutQuad);

        underline?.DOScaleX(1f, hoverDuration)
            .SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        controller?.ClearHovered(this);

        visualRoot.DOAnchorPosX(
            basePosition.x,
            hoverDuration
        ).SetEase(Ease.OutQuad);

        underline?.DOScaleX(0f, hoverDuration)
            .SetEase(Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        visualRoot.DOScale(0.96f, 0.06f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        visualRoot.DOScale(1f, 0.08f);
    }

    public void SetDimmed(bool dimmed)
    {
        visualGroup.DOFade(
            dimmed ? dimAlpha : 1f,
            0.12f
        );
    }

    public void PlayEntrance(float delay)
    {
        visualGroup.alpha = 0f;

        visualRoot.anchoredPosition =
            basePosition + Vector2.left * 20f;

        Sequence sequence = DOTween.Sequence();

        sequence.SetDelay(delay);

        sequence.Join(
            visualGroup.DOFade(1f, 0.25f)
        );

        sequence.Join(
            visualRoot.DOAnchorPos(
                basePosition,
                0.3f
            ).SetEase(Ease.OutQuad)
        );
    }
}