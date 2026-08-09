using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemInventory", menuName = "Scriptable Objects/Item Inventory")]
public class ItemInventory : ScriptableObject
{
    public List<ItemData> items = new List<ItemData>();
}
