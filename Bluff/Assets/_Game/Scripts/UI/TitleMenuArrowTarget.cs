using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TitleMenuArrowTarget : MonoBehaviour,
    IPointerEnterHandler, IPointerMoveHandler, IPointerDownHandler, IDeselectHandler
{
    [SerializeField] private TitleMenuArrow arrow;

    private Button button;

    public TitleMenuArrow Arrow => arrow;
    public RectTransform TargetRect { get; private set; }
    public bool CanShowArrow => isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable();

    private void Awake()
    {
        button = GetComponent<Button>();
        TargetRect = GetComponent<RectTransform>();
    }

    public void ResetTint()
    {
        if (button == null || !button.IsActive() || button.transition != Selectable.Transition.ColorTint ||
            button.targetGraphic == null) return;

        var colors = button.colors;
        var tint = button.IsInteractable() ? colors.normalColor : colors.disabledColor;
        button.targetGraphic.CrossFadeColor(tint * colors.colorMultiplier, 0f, true, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (arrow != null) arrow.SelectFromPointer(this, eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        // UGUI also sends move events to the previous hit as the pointer leaves it.
        var hit = eventData.pointerCurrentRaycast.gameObject;
        if (eventData.IsPointerMoving() && hit != null && hit.transform.IsChildOf(transform))
        {
            OnPointerEnter(eventData);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) ReleasePointerSelection();
    }

    public void OnDeselect(BaseEventData eventData) => ReleasePointerSelection();

    private void OnDisable() => ReleasePointerSelection();

    private void ReleasePointerSelection()
    {
        if (arrow != null) arrow.ReleasePointerSelection(this);
    }
}
