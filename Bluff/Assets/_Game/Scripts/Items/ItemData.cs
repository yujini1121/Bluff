using UnityEngine;
using UnityEngine.Purchasing;

public enum ItemType
{
    test,
    refreshCard,
    prizmChip,
    chipPocket,
    checker
}

public enum CanUseType
{
    SetupPhase,
    BettingPhase,
    ResultPhase
}

// 에셋 생성 메뉴 경로 및 기본 파일 이름 설정
[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;
    public ItemType itemType;
    public CanUseType canUseType;

    [TextArea] public string description;

    public bool Use()
    {
        switch (itemType)
        {
            case ItemType.test: // 테스트 아이템 사용
                Debug.Log("테스트 아이템이 사용되었습니다.");
                break;
            case ItemType.refreshCard: // '새로고침 카드' 사용
                Debug.Log("'새로고침 카드' 아이템이 사용되었습니다. 시드 카드와 각 플레이어의 카드를 재설정합니다.");
                break;
            case ItemType.prizmChip: // '프리즘 칩' 사용
                Debug.Log("'프리즘 칩' 아이템이 사용되었습니다. 패널티 칩을 반납하지 않습니다.");
                break;
            case ItemType.chipPocket: // '칩 포켓' 사용
                Debug.Log("'칩 포켓' 아이템이 사용되었습니다. 일정량의 칩을 얻습니다.");
                break;
            case ItemType.checker: // '체커' 사용
                Debug.Log("'체커' 아이템이 사용되었습니다. 베팅을 강제 종료합니다.");
                break;
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                return false;
        }

        return true;
    }
}