using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TitleMenuArrowTarget : MonoBehaviour,
    IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TitleMenuArrow arrow;

    private Button button;

    public RectTransform TargetRect { get; private set; }
    public bool CanShowArrow => isActiveAndEnabled && button.IsActive() && button.IsInteractable();

    private void Awake()
    {
        button = GetComponent<Button>();
        TargetRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (arrow != null) arrow.SetHovered(this, true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (arrow != null) arrow.SetSelected(this, true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (arrow != null) arrow.SetSelected(this, false);
    }

    private void OnDisable()
    {
        if (arrow == null) return;

        arrow.SetHovered(this, false);
        arrow.SetSelected(this, false);
    }
}
