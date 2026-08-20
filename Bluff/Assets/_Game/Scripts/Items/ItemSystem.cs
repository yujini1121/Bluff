using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemSystem : MonoBehaviour
{
    public List<GameObject> itemList = new List<GameObject>(); // 전체 아이템 목록
    public List<GameObject> itemInventory = new List<GameObject>(); // 플레이어 인벤토리 아이템 목록

    private readonly GameState gameState;
    private ItemGameApi itemGameApi;

    [Header("아이템 세부 수치")]
    [SerializeField] private int chipPocketAmount = 5; // chipPocket 아이템으로 얻는 칩 수량
    private Vector3 itemSpawnPos = Vector3.zero; // 아이템 스폰 위치

    [Header("Test")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Betting; // 현재 게임 단계

    public ItemSystem(GameState gameState, ItemGameApi itemGameApi)
    {
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.itemGameApi = itemGameApi ?? throw new ArgumentNullException(nameof(itemGameApi));
    }

    public void GetItem()
    {
        GameObject randomItem = itemList[UnityEngine.Random.Range(0, itemList.Count)]; // 랜덤 아이템 선택
        GameObject item = Instantiate(randomItem, itemSpawnPos, Quaternion.identity); // 아이템을 게임 씬에 생성

        if (item.TryGetComponent<Item>(out var itemComponent))
        {
            itemComponent.itemSystem = this;
        }

        itemInventory.Add(item); // 아이템을 인벤토리에 추가
    }

    private bool CheckHasItem(GameObject item) // 아이템 보유 여부 확인
    {
        return itemInventory.Contains(item);
    }

    private bool CheckCanUseItem(ItemType itemtype) // 아이템 사용 가능 여부 확인
    {
        switch (itemtype)
        {
            case ItemType.chipPocket:
                // 베팅 페이즈가 아니면 사용 불가능
                if (currentPhase != GamePhase.Betting) // gameState 참조에서 오류 발생하여 우선 고정값 사용
                {
                    Debug.LogWarning("아이템은 베팅 단계에서만 사용할 수 있습니다.");
                    return false;
                }
                break;
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                return false;
        }

        return true;
    }

    public bool UseItem(GameObject item)
    {
        if (!CheckHasItem(item)) // 아이템 보유 여부 확인
        {
            Debug.LogWarning("해당 아이템을 보유하고 있지 않습니다.");
            return false;
        }

        ItemType itemType = item.GetComponent<Item>().itemData.itemType;

        if (!CheckCanUseItem(itemType)) // 아이템 사용 가능 여부 확인
        {
            return false;
        }

        switch (itemType) // 아이템 타입에 따라 효과 적용
        {
            case ItemType.refreshCard:
                UseRefreshCard();
                break;
            case ItemType.prizmChip:
                UsePrizmChip();
                break;
            case ItemType.chipPocket:
                UseChipPocket();
                break;
            case ItemType.checker:
                UseChecker();
                break;
            default:
                Debug.LogWarning("아이템 타입을 알 수 없습니다.");
                return false;
        }

        return true;
    }

    // 아이템 효과
    private void UseRefreshCard()
    {
        // 1. Player & AI & Seed Card Reset
        // 2. Deck Shuffle
        // 3. Player & AI & Seed Card Draw
    }

    private void UsePrizmChip()
    {
        // 패널티 칩을 반납하지 않는 로직 구현
    }

    private void UseChipPocket()
    {
        // 일정량의 칩을 얻는 로직 구현
        itemGameApi.TryGiveChips(TurnOwner.Player, chipPocketAmount); // itemGameApi 참조에서 오류 발생
    }

    private void UseChecker()
    {
        // 베팅을 강제 종료하는 로직 구현
    }
}
