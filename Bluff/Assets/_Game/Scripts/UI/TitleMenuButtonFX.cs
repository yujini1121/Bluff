using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("Pastel Color")]
    [SerializeField] private float colorSpeed = 5f;

    [Header("Other Button")]
    [SerializeField] private float dimAlpha = 0.45f;

    [Header("Diamond")]
    [SerializeField] private RectTransform leftDiamond;
    [SerializeField] private RectTransform rightDiamond;
    [SerializeField] private float indicatorDuration = 0.15f;

    private TMP_Text label;

    private Image leftDiamondImage;
    private Image rightDiamondImage;

    private TitleMenuController controller;

    private Vector2 basePosition;
    private bool isHovered;

    private static readonly Color NormalColor = Color.white;

    private void Awake()
    {
        basePosition = visualRoot.anchoredPosition;

        label = visualRoot.GetComponentInChildren<TMP_Text>(true);

        leftDiamondImage = leftDiamond?.GetComponent<Image>();
        rightDiamondImage = rightDiamond?.GetComponent<Image>();

        SetColor(NormalColor);

        if (underline != null)
        {
            Vector3 scale = underline.localScale;
            scale.x = 0f;
            underline.localScale = scale;
        }

        if (leftDiamond != null)
        {
            leftDiamond.localScale = Vector3.zero;
        }

        if (rightDiamond != null)
        {
            rightDiamond.localScale = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!isHovered)
        {
            return;
        }

        float t = Time.unscaledTime * colorSpeed;

        float r = 0.75f + Mathf.Sin(t) * 0.25f;
        float g = 0.65f + Mathf.Sin(t + 2.1f) * 0.25f;
        float b = 0.75f + Mathf.Sin(t + 4.2f) * 0.25f;

        Color pastelColor = new Color(r, g, b, 1f);

        SetColor(pastelColor);
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

        underline?.DOScaleX(
            1f,
            hoverDuration
        ).SetEase(Ease.OutQuad);

        leftDiamond?.DOScale(
            1f,
            indicatorDuration
        ).SetEase(Ease.OutBack);

        rightDiamond?.DOScale(
            1f,
            indicatorDuration
        ).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        controller?.ClearHovered(this);

        visualRoot.DOAnchorPosX(
            basePosition.x,
            hoverDuration
        ).SetEase(Ease.OutQuad);

        underline?.DOScaleX(
            0f,
            hoverDuration
        ).SetEase(Ease.OutQuad);

        leftDiamond?.DOScale(
            0f,
            indicatorDuration
        ).SetEase(Ease.InQuad);

        rightDiamond?.DOScale(
            0f,
            indicatorDuration
        ).SetEase(Ease.InQuad);

        SetColor(NormalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        visualRoot.DOScale(
            0.96f,
            0.06f
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        visualRoot.DOScale(
            1f,
            0.08f
        );
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
            visualGroup.DOFade(
                1f,
                0.25f
            )
        );

        sequence.Join(
            visualRoot.DOAnchorPos(
                basePosition,
                0.3f
            ).SetEase(Ease.OutQuad)
        );
    }

    private void SetColor(Color color)
    {
        if (label != null)
        {
            label.color = color;
        }

        if (leftDiamondImage != null)
        {
            leftDiamondImage.color = color;
        }

        if (rightDiamondImage != null)
        {
            rightDiamondImage.color = color;
        }
    }

    private void OnDisable()
    {
        isHovered = false;

        visualRoot?.DOKill();
        underline?.DOKill();
        visualGroup?.DOKill();

        leftDiamond?.DOKill();
        rightDiamond?.DOKill();

        SetColor(NormalColor);
    }
}