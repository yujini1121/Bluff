using TMPro;
using UnityEngine;

public class ObjectHoverText : MonoBehaviour
{
    Item item;
    private TextMeshProUGUI targetText;

    void Awake()
    {
        item = this.gameObject.GetComponent<Item>();
        targetText = GameObject.Find("Description Text").GetComponent<TextMeshProUGUI>();
    }

    // 마우스 커서가 콜라이더 영역 안으로 들어올 때
    private void OnMouseEnter()
    {
        if (targetText != null)
        {
            targetText.text = item != null ? item.GetDescription() : string.Empty;
        }
    }

    // 마우스 커서가 콜라이더 영역 밖으로 벗어날 때
    private void OnMouseExit()
    {
        if (targetText != null)
        {
            targetText.text = "";
        }
    }
}
