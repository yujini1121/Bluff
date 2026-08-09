using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    public bool canUse = true; // 아이템 사용 가능 여부

    private void OnMouseDown()
    {
        if (canUse)
        {
            UseItem();
        }
    }

    public void UseItem()
    {
        if (itemData.Use())
        {
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("아이템을 사용하지 못했습니다.");
        }
    }
}