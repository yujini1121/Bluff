using System.Collections.Generic;
using UnityEngine;

public class ItemSystem : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    private const int AnteAmount = 1;
    private const int TotalAnteAmount = AnteAmount * 2;

    private ItemGameApi itemGameApi;

    public event System.Action RefreshCardSucceeded;
    internal event System.Action<GameObject> PlayerItemUseRequested;

    public List<GameObject> itemList = new List<GameObject>(); // 전체 아이템 목록

    [SerializeField] private GameObject[] playerItemSpawnPos = new GameObject[4]; // 플레이어 아이템 스폰 위치
    [SerializeField] private GameObject[] dealerItemSpawnPos = new GameObject[4]; // 딜러 아이템 스폰 위치

    [Header("아이템 수치")]
    [SerializeField] private int chipPocketAmount = 2; // 칩 포켓 아이템으로 얻는 칩 수량

    public void Initialize(ItemGameApi itemGameApi)
    {
        this.itemGameApi = itemGameApi;
    }

    public void GetItem()
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

    // 효과가 성공한 경우에만 아이템 소모 <- 안정성을 위해
    internal void RequestPlayerItemUse(GameObject item)
    {
        if (item != null &&
            item.TryGetComponent(out Item itemComponent) &&
            itemComponent.itemData != null &&
            (itemComponent.itemData.itemType == ItemType.refreshCard ||
             itemComponent.itemData.itemType == ItemType.prizmChip ||
             itemComponent.itemData.itemType == ItemType.chipPocket ||
             itemComponent.itemData.itemType == ItemType.defy) &&
            PlayerItemUseRequested != null)
        {
            PlayerItemUseRequested.Invoke(item);    // 플레이어가 어떤 아이템을 사용하고 싶어하는지 전달
            return;
        }

        UseItem(TurnOwner.Player, item);
    }

    // true -> 아이템 사용 성공 / false -> 아이템 사용 실패 반환
    public bool UseItem(TurnOwner owner, GameObject item)
    {
        if (!TryGetValidItem(owner, item, out ItemType type))
        {
            return false;
        }

        if (!CanUseItem(owner, type))
        {
            return false;
        }

        if (!TryApplyEffect(owner, type))
        {
            return false;
        }

        return RemoveUsedItem(owner, item);
    }

    // 사용할 ItemType 가져오기
    private bool TryGetValidItem(TurnOwner owner, GameObject item, out ItemType type)
    {
        type = default;

        if (inventory == null || item == null || !inventory.HasItem(owner, item))
        {
            Debug.LogWarning("인벤토리에 아이템이 없습니다.");
            return false;
        }

        if (!item.TryGetComponent(out Item itemComponent) ||
            itemComponent.itemData == null)
        {
            Debug.LogWarning("아이템 정보가 없습니다.");
            return false;
        }

        type = itemComponent.itemData.itemType;
        return true;
    }

    // 아이템 종류에 맞는 효과 실행 -> 성공 여부 반환시키기 
    private bool TryApplyEffect(TurnOwner owner, ItemType type)
    {
        switch (type)
        {
            case ItemType.refreshCard:
                return RefreshCard();
            case ItemType.prizmChip:
                return PrizmChip();
            case ItemType.chipPocket:
                return ChipPocket(owner);
            case ItemType.defy:
                return Defy();
            default:
                Debug.LogWarning("아이템이 사용되지 않았습니다.");
                return false;
        }
    }

    // 인벤토리 먼저 제거 -> 아이템도 실제로 제거 
    private bool RemoveUsedItem(TurnOwner owner, GameObject item)
    {
        if (!inventory.TryRemoveItem(owner, item))
        {
            return false;
        }

        if (Application.isPlaying)
        {
            Destroy(item);
        }
        else
        {
            DestroyImmediate(item);
        }

        return true;
    }

    // 보유 중인 아이템 정보 전달
    public IReadOnlyList<ItemType> GetOwnedItemTypes(TurnOwner owner)
    {
        GameObject[] ownedItems = GetOwnedItems(owner);

        if (ownedItems == null)
        {
            return System.Array.Empty<ItemType>();
        }

        var ownedItemTypes = new List<ItemType>();

        foreach (GameObject ownedItem in ownedItems)
        {
            if (ownedItem != null &&
                ownedItem.TryGetComponent(out Item itemComponent) &&
                itemComponent.itemData != null)
            {
                ownedItemTypes.Add(itemComponent.itemData.itemType);
            }
        }

        return ownedItemTypes;
    }

    // 실제 보유 아이템 찾고 사용
    public bool TryUseItem(TurnOwner owner, ItemType type)
    {
        GameObject[] ownedItems = GetOwnedItems(owner);

        if (ownedItems == null)
        {
            return false;
        }

        foreach (GameObject ownedItem in ownedItems)
        {
            if (ownedItem != null &&
                ownedItem.TryGetComponent(out Item itemComponent) &&
                itemComponent.itemData != null &&
                itemComponent.itemData.itemType == type)
            {
                return UseItem(owner, ownedItem);
            }
        }

        Debug.LogWarning("인벤토리에 해당 타입의 아이템이 없습니다.");
        return false;
    }

    // 아이템 사용자(Owner)에 맞는 인벤토리 가져옴
    private GameObject[] GetOwnedItems(TurnOwner owner)
    {
        if (inventory == null)
        {
            return null;
        }

        switch (owner)
        {
            case TurnOwner.Player:
                return inventory.playerItemInventory;
            case TurnOwner.Dealer:
                return inventory.dealerItemInventory;
            default:
                return null;
        }
    }

    private bool CanUseItem(TurnOwner owner, ItemType itemType)
    {
        // 아이템은 Betting Phase에서만 사용 가능
        if (itemGameApi.GetCurrentPhase() != GamePhase.Betting)
        {
            Debug.LogWarning("아이템은 Betting Phase에서만 사용 가능합니다.");
            return false;
        }

        // 아이템은 자신의 턴일 때만 사용 가능 
        if (itemGameApi.GetCurrentTurn() != owner)
        {
            Debug.LogWarning("아이템은 내 차례일 때만 사용할 수 있습니다.");
            return false;
        }

        // 개별 아이템 사용 조건 확인
        switch (itemType)
        {
            case ItemType.refreshCard:
                // 현재 라운드의 자발적 베팅 전에만 사용 가능 -> Draw로 Pot이 이월될때 버그 발생해서 수정
                if (!itemGameApi.IsBeforeVoluntaryBetting())
                {
                    Debug.LogWarning("'새로고침 카드' 아이템은 베팅이 진행되기 전에 사용 가능합니다.");
                    return false;
                }
                break;
            case ItemType.prizmChip:
                break;
            case ItemType.chipPocket:
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
    private bool RefreshCard()
    {
        if (!itemGameApi.TryReplaceCard())
        {
            return false;
        }

        // Refresh 성공 이벤트 신호 발생 -> 카드 비주얼 업데이트 갱신
        RefreshCardSucceeded?.Invoke();
        Debug.Log("'새로고침 카드' 아이템이 사용되었습니다. 시드 카드와 각 플레이어의 카드를 재설정합니다.");
        return true;
    }

    private bool PrizmChip()
    {
        if (!itemGameApi.TryFoldWithoutPenalty())
        {
            return false;
        }

        Debug.Log("'프리즘 칩' 아이템이 사용되었습니다. 라운드를 포기합니다. 발생한 페널티를 무시합니다.");
        return true;
    }

    private bool ChipPocket(TurnOwner owner)
    {
        if (!itemGameApi.TryGiveChips(owner, chipPocketAmount))
        {
            return false;
        }

        Debug.Log("'칩 포켓' 아이템이 사용되었습니다. 일정량의 칩을 얻습니다.");
        return true;
    }

    private bool Defy()
    {
        if (!itemGameApi.TryCall())
        {
            return false;
        }

        Debug.Log("'디파이' 아이템이 사용되었습니다. 상대의 레이즈를 무시하고 베팅을 강제 종료합니다.");
        return true;
    }
}
