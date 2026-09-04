using System.Collections.Generic;
using UnityEngine;

public class ItemSystem : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    private const int AnteAmount = 1;
    private const int TotalAnteAmount = AnteAmount * 2;
    private const int MaximumFoldPenaltyAmount = 10;

    private ItemGameApi itemGameApi;

    public List<GameObject> itemList = new List<GameObject>(); // 전체 아이템 목록

    [SerializeField] private GameObject[] playerItemSpawnPos = new GameObject[4]; // 플레이어 아이템 스폰 위치
    [SerializeField] private GameObject[] dealerItemSpawnPos = new GameObject[4]; // 딜러 아이템 스폰 위치

    [Header("아이템 수치")]
    [SerializeField] private int chipPocketAmount = 2; // 칩 포켓 아이템으로 얻는 칩 수량

    public void Initialize(ItemGameApi itemGameApi)
    {
        this.itemGameApi = itemGameApi;
    }

    public void GetItem() // 플레이어 아이템 지급과 딜러 아이템 지급 각각 분리 필요
    {
        // 플레이어 아이템 지급
        GameObject randomPlayerItem = itemList[Random.Range(0, itemList.Count)]; // 랜덤 아이템 선택
        int playerInventoryIndex = inventory.GetInventoryIndex(TurnOwner.Player); // 플레이어 인벤토리에서 빈 슬롯 확인
        if (playerInventoryIndex != -1) // 인벤토리 공간 확인
        {
            GameObject playerItem = Instantiate(randomPlayerItem, playerItemSpawnPos[playerInventoryIndex].transform.position, Quaternion.identity); // 아이템을 게임 씬에 생성
            if (inventory.AddItem(TurnOwner.Player, playerItem)) // 아이템을 플레이어 인벤토리에 추가
            {
                if (playerItem.TryGetComponent<Item>(out var playerItemComponent))
                {
                    playerItemComponent.itemSystem = this;
                }
            }
        }

        // 딜러 아이템 지급
        GameObject randomDealerItem = itemList[Random.Range(0, itemList.Count)]; // 랜덤 아이템 선택
        int dealerInventoryIndex = inventory.GetInventoryIndex(TurnOwner.Dealer); // 딜러 인벤토리에서 빈 슬롯 확인
        if (dealerInventoryIndex != -1) // 인벤토리 공간 확인
        {
            GameObject dealerItem = Instantiate(randomDealerItem, dealerItemSpawnPos[dealerInventoryIndex].transform.position, Quaternion.identity); // 아이템을 게임 씬에 생성
            if (inventory.AddItem(TurnOwner.Dealer, dealerItem)) // 아이템을 딜러 인벤토리에 추가
            {
                if (dealerItem.TryGetComponent<Item>(out var dealerItemComponent))
                {
                    dealerItemComponent.itemSystem = this;
                }
            }
        }
    }

    public bool UseItem(TurnOwner target, GameObject item)
    {
        // 인벤토리에 아이템이 있는지 검사
        if (!inventory.HasItem(target, item))
        {
            Debug.LogWarning("인벤토리에 아이템이 없습니다.");
            return false;
        }

        ItemType type = item.GetComponent<Item>().itemData.itemType;

        // 아이템 사용 가능 여부 확인
        if (!CanUseItem(type))
        {
            return false;
        }

        switch (type)
        {
            case ItemType.refreshCard:
                RefreshCard();
                break;
            case ItemType.prizmChip:
                PrizmChip();
                break;
            case ItemType.chipPocket:
                ChipPocket();
                break;
            case ItemType.defy:
                Defy();
                break;
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                return false;
        }

        // 아이템 사용 후 인벤토리에서 제거
        inventory.TryRemoveItem(target, item);

        return true;
    }

    private bool CanUseItem(ItemType ItemType)
    {
        // 아이템은 Betting Phase에서만 사용 가능
        if (itemGameApi.GetCurrentPhase() != GamePhase.Betting)
        {
            Debug.LogWarning("아이템은 Betting Phase에서만 사용 가능합니다.");
            return false;
        }

        // 아이템은 내 턴일 때만 사용 가능
        if (itemGameApi.GetCurrentTurn() != TurnOwner.Player)
        {
            Debug.LogWarning("아이템은 내 차례일 때만 사용할 수 있습니다.");
            return false;
        }

        // 개별 아이템 사용 조건 확인
        switch (ItemType)
        {
            case ItemType.refreshCard:
                // Betting된 칩이 없을 경우에만 사용 가능
                if (itemGameApi.GetPot() != TotalAnteAmount) // 전 라운드가 무승부였을 때도 진행 가능하게 재구성해야함
                {
                    Debug.LogWarning("'새로고침 카드' 아이템은 베팅이 진행되기 전에 사용 가능합니다.");
                    return false;
                }
                break;
            case ItemType.prizmChip:
                // 다른 조건 없음
                break;
            case ItemType.chipPocket:
                // 다른 조건 없음
                break;
            case ItemType.defy:
                // Betting된 칩이 있을 경우에만 사용 가능
                if (itemGameApi.GetPot() == TotalAnteAmount)
                {
                    Debug.LogWarning("'디파이' 아이템은 베팅이 진행된 후에 사용 가능합니다.");
                    return false;
                }
                break;
            default:
                Debug.LogWarning("잘못된 아이템 타입입니다.");
                return false;
        }

        return true;
    }

    // 아이템 효과
    private void RefreshCard() // 작동 확인
    {
        itemGameApi.TryReplaceCard();

        Debug.Log("'새로고침 카드' 아이템이 사용되었습니다. 시드 카드와 각 플레이어의 카드를 재설정합니다.");
    }

    private void PrizmChip() // 작동 확인
    {
        itemGameApi.TryFoldWithoutPenalty();
        Debug.Log("'프리즘 칩' 아이템이 사용되었습니다. 라운드를 포기합니다. 발생한 페널티를 무시합니다.");
    }

    private void ChipPocket() // 작동 확인
    {
        itemGameApi.TryGiveChips(TurnOwner.Player, chipPocketAmount);
        Debug.Log("'칩 포켓' 아이템이 사용되었습니다. 일정량의 칩을 얻습니다.");
    }

    private void Defy() // 작동 확인
    {
        itemGameApi.TryCall();
        Debug.Log("'디파이' 아이템이 사용되었습니다. 상대의 레이즈를 무시하고 베팅을 강제 종료합니다.");
    }
}
