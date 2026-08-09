using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSystem : MonoBehaviour
{
    public List<ItemData> itemList = new List<ItemData>();
    public ItemInventory inventory;

    // 아이템 획득시 인벤토리에 추가
    public void GetItem(ItemData item)
    {
        inventory.items.Add(item);
        Debug.Log($"{item.itemName}을 얻었습니다.");
    }

    // 아이템 사용시 인벤토리에서 제거
    public void UseItem(ItemData item)
    {
        if (item.Use())
        {
            inventory.items.Remove(item);
        }
    }
}
