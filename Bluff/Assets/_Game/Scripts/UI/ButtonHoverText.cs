using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI targetText;
    [Header("표시할 문구 설정")]
    [TextArea(2, 4)]
    [SerializeField] private string hoverDescription;
    [SerializeField] private string defaultDescription = "";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetText != null)
        {
            targetText.text = hoverDescription;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetText != null)
        {
            targetText.text = defaultDescription;
        }
    }
}
