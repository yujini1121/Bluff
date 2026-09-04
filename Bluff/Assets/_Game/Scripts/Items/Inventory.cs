using UnityEngine;

[CreateAssetMenu(fileName = "Inventory", menuName = "Scriptable Objects/Inventory")]
public class Inventory : ScriptableObject
{
    private const int MaximumInventorySize = 4;

    public GameObject[] playerItemInventory = new GameObject[MaximumInventorySize]; // 플레이어 인벤토리
    public GameObject[] dealerItemInventory = new GameObject[MaximumInventorySize]; // 딜러 인벤토리

    public bool CanAddItem(TurnOwner target)
    {
        switch (target)
        {
            case TurnOwner.Player:
                if (playerItemInventory.Length >= MaximumInventorySize)
                {
                    Debug.LogWarning("플레이어 인벤토리가 가득차 아이템을 얻을 수 없습니다.");
                    return false;
                }
                break;
            case TurnOwner.Dealer:
                if (dealerItemInventory.Length >= MaximumInventorySize)
                {
                    Debug.LogWarning("딜러 인벤토리가 가득차 아이템을 얻을 수 없습니다.");
                    return false;
                }
                break;
            default:
                Debug.LogWarning("알 수 없는 대상입니다.");
                return false;
        }
        return true;
    }

    public int GetInventoryIndex(TurnOwner target)
    {
        switch (target)
        {
            case TurnOwner.Player:
                for (int i = 0; i < playerItemInventory.Length; i++)
                {
                    if (playerItemInventory[i] == null)
                    {
                        return i;
                    }
                }
                break;
            case TurnOwner.Dealer:
                for (int i = 0; i < dealerItemInventory.Length; i++)
                {
                    if (dealerItemInventory[i] == null)
                    {
                        return i;
                    }
                }
                break;
            default:
                Debug.LogWarning("알 수 없는 대상입니다.");
                return -1;
        }
        return -1;
    }

    public bool AddItem(TurnOwner target, GameObject item)
    {
        switch (target)
        {
            case TurnOwner.Player:
                for (int i = 0; i < playerItemInventory.Length; i++)
                {
                    if (playerItemInventory[i] == null)
                    {
                        playerItemInventory[i] = item;
                        break;
                    }
                }
                break;
            case TurnOwner.Dealer:
                for (int i = 0; i < dealerItemInventory.Length; i++)
                {
                    if (dealerItemInventory[i] == null)
                    {
                        dealerItemInventory[i] = item;
                        break;
                    }
                }
                break;
            default:
                Debug.LogWarning("알 수 없는 대상입니다.");
                return false;
        }

        return true;
    }

    public bool HasItem(TurnOwner target, GameObject item)
    {
        switch (target)
        {
            case TurnOwner.Player:
                for (int i = 0; i < playerItemInventory.Length; i++)
                {
                    if (playerItemInventory[i] == item)
                    {
                        return true;
                    }
                }
                return false;
            case TurnOwner.Dealer:
                for (int i = 0; i < dealerItemInventory.Length; i++)
                {
                    if (dealerItemInventory[i] == item)
                    {
                        return true;
                    }
                }
                return false;
            default:
                Debug.LogWarning("알 수 없는 대상입니다.");
                return false;
        }
    }

    public bool TryRemoveItem(TurnOwner target, GameObject item)
    {
        switch (target)
        {
            case TurnOwner.Player:
                if (!HasItem(target, item))
                {
                    Debug.LogWarning("플레이어 인벤토리에 아이템이 없습니다.");
                    return false;
                }
                playerItemInventory[System.Array.IndexOf(playerItemInventory, item)] = null;
                break;
            case TurnOwner.Dealer:
                if (!HasItem(target, item))
                {
                    Debug.LogWarning("딜러 인벤토리에 아이템이 없습니다.");
                    return false;
                }
                dealerItemInventory[System.Array.IndexOf(dealerItemInventory, item)] = null;
                break;
            default:
                Debug.LogWarning("알 수 없는 대상입니다.");
                return false;
        }
        return true;
    }
}
