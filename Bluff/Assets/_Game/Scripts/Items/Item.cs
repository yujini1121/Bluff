using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] private ItemData itemData;

    private void OnMouseDown()
    {
        UseItem();
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