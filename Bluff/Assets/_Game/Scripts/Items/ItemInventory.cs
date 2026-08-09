using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemInventory", menuName = "Scriptable Objects/Item Inventory")]
public class ItemInventory : ScriptableObject
{
    // 플레이어 아이템 인벤토리 (일단 만들어는 놨는데 안 쓸지도)
    public List<ItemData> items = new List<ItemData>();
}
