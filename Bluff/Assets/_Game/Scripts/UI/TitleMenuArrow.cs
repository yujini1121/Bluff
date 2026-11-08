using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class TitleMenuArrow : MonoBehaviour, IPointerExitHandler
{
    [SerializeField] private Image arrowImage;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    private RectTransform arrowRect;
    private TitleMenuArrowTarget hoveredTarget;
    private TitleMenuArrowTarget selectedTarget;
    private bool preferSelection;
    private Vector2 velocity;

    private void Awake()
    {
        if (arrowImage == null)
        {
            enabled = false;
            return;
        }

        arrowRect = arrowImage.rectTransform;
        arrowImage.raycastTarget = false;
        arrowImage.enabled = false;
    }

    public void SetHovered(TitleMenuArrowTarget target, bool isHovered)
    {
        if (isHovered)
        {
            hoveredTarget = target;
            preferSelection = false;
        }
        else if (hoveredTarget == target)
        {
            hoveredTarget = null;
        }
    }

    public void SetSelected(TitleMenuArrowTarget target, bool isSelected)
    {
        if (isSelected)
        {
            selectedTarget = target;
            preferSelection = true;
        }
        else if (selectedTarget == target)
        {
            selectedTarget = null;
        }
    }

    public void OnPointerExit(PointerEventData eventData) => hoveredTarget = null;

    private void LateUpdate()
    {
        var target = preferSelection ? selectedTarget : hoveredTarget;
        if (target == null || !target.CanShowArrow)
        {
            target = preferSelection ? hoveredTarget : selectedTarget;
        }

        if (target == null || !target.CanShowArrow)
        {
            HideArrow();
            return;
        }

        if (!arrowImage.enabled)
        {
            Canvas.ForceUpdateCanvases();
            SetLocalPosition(GetTargetPosition(target.TargetRect));
            arrowImage.enabled = true;
            return;
        }

        Vector2 targetPosition = GetTargetPosition(target.TargetRect);
        Vector2 position = Vector2.SmoothDamp(
            arrowRect.localPosition,
            targetPosition,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if ((position - targetPosition).sqrMagnitude < 0.0001f)
        {
            position = targetPosition;
            velocity = Vector2.zero;
        }

        SetLocalPosition(position);
    }

    private Vector2 GetTargetPosition(RectTransform target)
    {
        Rect buttonRect = target.rect;
        Vector3 worldPosition = target.TransformPoint(
            new Vector3(buttonRect.xMin, buttonRect.center.y, 0f));
        Vector3 localPosition = arrowRect.parent.InverseTransformPoint(worldPosition);
        localPosition.x -= arrowRect.rect.xMax * arrowRect.localScale.x;
        return localPosition;
    }

    private void SetLocalPosition(Vector2 position)
    {
        arrowRect.localPosition = new Vector3(position.x, position.y, arrowRect.localPosition.z);
    }

    private void OnDisable()
    {
        hoveredTarget = null;
        HideArrow();
    }

    private void HideArrow()
    {
        if (arrowImage != null) arrowImage.enabled = false;
        velocity = Vector2.zero;
    }
}
