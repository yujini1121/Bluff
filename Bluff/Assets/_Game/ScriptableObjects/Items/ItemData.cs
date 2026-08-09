using UnityEngine;
using UnityEngine.Purchasing;

public enum ItemType
{
    test // 테스트 아이템
}

// 에셋 생성 메뉴 경로 및 기본 파일 이름 설정
[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;
    public ItemType itemType;
    [TextArea] public string description;

    public bool Use()
    {
        switch (itemType)
        {
            case ItemType.test: // 테스트 아이템 사용
                Debug.Log("테스트 아이템이 사용되었습니다.");
                return true;
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                break;
        }

        return false;
    }
}