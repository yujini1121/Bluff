using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class TitleMenuArrow : MonoBehaviour, IPointerExitHandler
{
    [SerializeField] private Image arrowImage;
    [SerializeField, Min(0f)] private float smoothTime = 0.1f;

    private RectTransform arrowRect;
    private TitleMenuArrowTarget visibleTarget;
    private TitleMenuArrowTarget pointerSelection;
    private TitleMenuArrowTarget navigationResumeTarget;
    private bool waitForPointerMove;
    private bool resetTintsAfterInput;
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

    public void SelectFromPointer(TitleMenuArrowTarget target, PointerEventData eventData)
    {
        var events = EventSystem.current;
        if (!isActiveAndEnabled || !target.CanShowArrow || events == null || events.alreadySelecting ||
            events.currentSelectedGameObject == target.gameObject) return;

        // Closing an overlay can send PointerEnter even though the mouse has not moved.
        if (waitForPointerMove && eventData.pointerId < 0 && !eventData.IsPointerMoving()) return;

        waitForPointerMove = false;
        events.SetSelectedGameObject(target.gameObject, eventData);
        pointerSelection = target;
    }

    public void ResetFocus(GameObject previousSelection)
    {
        pointerSelection = null;
        navigationResumeTarget = previousSelection != null ? previousSelection.GetComponent<TitleMenuArrowTarget>() : null;
        waitForPointerMove = true;
        resetTintsAfterInput = true;
        HideArrow();
    }

    public void ReleasePointerSelection(TitleMenuArrowTarget target)
    {
        if (pointerSelection == target) pointerSelection = null;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var events = EventSystem.current;
        if (pointerSelection != null && events != null &&
            events.currentSelectedGameObject == pointerSelection.gameObject)
        {
            events.SetSelectedGameObject(null, eventData);
        }

        pointerSelection = null;
    }

    private void LateUpdate()
    {
        var events = EventSystem.current;
        ResumeFocusOnNavigation(events);
        var selection = events != null ? events.currentSelectedGameObject : null;
        var target = selection != null ? selection.GetComponent<TitleMenuArrowTarget>() : null;
        if (resetTintsAfterInput)
        {
            // Submit can set the pressed tint after the panel's click handler has run.
            resetTintsAfterInput = false;
            foreach (var item in GetComponentsInChildren<TitleMenuArrowTarget>())
            {
                if (item.Arrow == this && item != target) item.ResetTint();
            }
        }

        if (target == null || target.Arrow != this || !target.CanShowArrow)
        {
            HideArrow();
            return;
        }

        SetVisibleTarget(target);
        if (!arrowImage.enabled) Canvas.ForceUpdateCanvases();

        Vector2 targetPosition = GetTargetPosition(target.TargetRect);
        if (!arrowImage.enabled || smoothTime <= 0f)
        {
            SetLocalPosition(targetPosition);
            velocity = Vector2.zero;
            arrowImage.enabled = true;
            return;
        }

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

    private void ResumeFocusOnNavigation(EventSystem events)
    {
        if (navigationResumeTarget == null || events == null) return;
        if (events.currentSelectedGameObject != null || !navigationResumeTarget.CanShowArrow)
        {
            navigationResumeTarget = null;
            return;
        }

        if (events.isFocused && events.sendNavigationEvents && events.currentInputModule is StandaloneInputModule module &&
            (module.input.GetButtonDown(module.verticalAxis) || module.input.GetButtonDown(module.horizontalAxis)))
        {
            events.SetSelectedGameObject(navigationResumeTarget.gameObject);
            navigationResumeTarget = null;
        }
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
        pointerSelection = null;
        navigationResumeTarget = null;
        HideArrow();
    }

    private void SetVisibleTarget(TitleMenuArrowTarget target)
    {
        if (visibleTarget == target) return;
        if (visibleTarget != null) visibleTarget.ResetTint();
        visibleTarget = target;
    }

    private void HideArrow()
    {
        SetVisibleTarget(null);
        if (arrowImage != null) arrowImage.enabled = false;
        velocity = Vector2.zero;
    }
}
