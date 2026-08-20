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
        GameObject randomItem = itemList[Random.Range(0, itemList.Count)]; // 랜덤 아이템 선택
        ItemData itemData = randomItem.GetComponent<Item>().itemData; // 아이템 데이터 가져오기
        Instantiate(randomItem, itemSpawnPos, Quaternion.identity); // 아이템을 게임 씬에 생성
    }

    public bool UseItem(ItemData itemData)
    {
        switch (itemData.itemType) // 아이템 종류에 따라 조건 검사
        {
            case ItemType.test:
                Debug.Log("테스트 아이템이 사용되었습니다.");
                break;
            case ItemType.refreshCard:
                RefreshCard();
                Debug.Log("'새로고침 카드' 아이템이 사용되었습니다. 시드 카드와 각 플레이어의 카드를 재설정합니다.");
                break;
            case ItemType.prizmChip:
                Debug.Log("'프리즘 칩' 아이템이 사용되었습니다. 패널티 칩을 반납하지 않습니다.");
                break;
            case ItemType.chipPocket:
                Debug.Log("'칩 포켓' 아이템이 사용되었습니다. 일정량의 칩을 얻습니다.");
                break;
            case ItemType.checker:
                Debug.Log("'체커' 아이템이 사용되었습니다. 베팅을 강제 종료합니다.");
                break;
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                return false;
        }

        return true;
    }

    private bool CheckCanUseItem(ItemData itemData)
    {
        // 아이템 사용 가능 여부 확인
        switch (itemData.canUseType)
        {
            case CanUseType.SetupPhase:
                Debug.Log("아이템은 세팅 단계에서만 사용할 수 있습니다.");
                break;
            case CanUseType.BettingPhase:
                Debug.Log("아이템은 베팅 단계에서만 사용할 수 있습니다.");
                break;
            case CanUseType.ResultPhase:
                Debug.Log("아이템은 결과 단계에서만 사용할 수 있습니다.");
                break;
            default:
                Debug.LogWarning("아이템 사용 가능 여부를 확인할 수 없습니다.");
                return false;
        }

        return true;
    }

    // 아이템 효과
    private void RefreshCard()
    {
        // 1. Player & AI & Seed Card Reset
        // 2. Deck Shuffle
        // 3. Player & AI & Seed Card Draw
    }

    private void PrizmChip()
    {
        // 패널티 칩을 반납하지 않는 로직 구현
    }

    private void ChipPocket()
    {
        // 일정량의 칩을 얻는 로직 구현
    }

    private void Checker()
    {
        // 베팅을 강제 종료하는 로직 구현
    }
}
