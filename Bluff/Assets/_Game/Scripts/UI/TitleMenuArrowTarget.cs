using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TitleMenuArrowTarget : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private TitleMenuArrow arrow;

    private Button button;
    private RectTransform targetRect;

    private void Awake()
    {
        button = GetComponent<Button>();
        targetRect = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        MoveArrow();
    }

    public void OnSelect(BaseEventData eventData)
    {
        MoveArrow();
    }

    private void MoveArrow()
    {
        if (isActiveAndEnabled && arrow != null && button.IsActive() && button.IsInteractable())
        {
            arrow.MoveTo(targetRect);
        }
    }
}
