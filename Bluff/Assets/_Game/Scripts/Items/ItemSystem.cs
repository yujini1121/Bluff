using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSystem : MonoBehaviour
{
    // 전체 아이템 목록
    public List<GameObject> itemList = new List<GameObject>();

    private Vector3 itemSpawnPos = Vector3.zero; // 아이템 스폰 위치

    public void GetItem()
    {
        GameObject randomItem = itemList[Random.Range(0, itemList.Count)];
        Instantiate(randomItem, itemSpawnPos, Quaternion.identity);
    }
}
