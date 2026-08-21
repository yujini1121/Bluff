using System.Collections.Generic;
using UnityEngine;

public sealed class ChipVisualController : MonoBehaviour
{
    [Header("Chip")]
    [SerializeField] private Transform playerChipArea;
    [SerializeField] private Transform dealerChipArea;
    [SerializeField] private Transform potArea;

    [Header("Prefabs")]
    [SerializeField] private GameObject[] chipPrefabs;

    [Header("Stack")]
    [SerializeField, Min(1)] private int maxChipsPerStack = 10;
    [SerializeField, Min(0f)] private float stackSpacing = 0.08f;
    [SerializeField, Min(0f)] private float chipHeightSpacing = 0.02f;

    private readonly List<GameObject> playerChipInstances =
        new List<GameObject>();
    private readonly List<GameObject> dealerChipInstances =
        new List<GameObject>();
    private readonly List<GameObject> potChipInstances =
        new List<GameObject>();
    private readonly System.Random prefabRandom = new System.Random();

    private GameState gameState;
    private readonly List<GameObject> pendingChips =
        new List<GameObject>();
    private readonly List<Quaternion> pendingRotations =
        new List<Quaternion>();
    private readonly List<Vector3> pendingScales =
        new List<Vector3>();

    public void Initialize(GameState state)
    {
        gameState = state;
        RefreshChips();
    }

    public void RefreshChips()
    {
        if (pendingChips.Count > 0)
        {
            return;
        }

        int playerChipCount = gameState?.PlayerChips.Count ?? 0;
        int dealerChipCount = gameState?.DealerChips.Count ?? 0;
        int potChipCount = gameState?.Pot.Amount ?? 0;

        MatchChipCount(
            playerChipInstances,
            playerChipCount,
            playerChipArea,
            chipPrefabs);
        MatchChipCount(
            dealerChipInstances,
            dealerChipCount,
            dealerChipArea,
            chipPrefabs);
        MatchChipCount(
            potChipInstances,
            potChipCount,
            potArea,
            chipPrefabs);
    }

    public bool TryBeginDealerBet(
        int chipCount,
        out GameObject[] chips,
        out Vector3[] potTargetPositions)
    {
        chips = null;
        potTargetPositions = null;

        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            dealerChipArea == null ||
            potArea == null)
        {
            return false;
        }

        int potCountBeforeDealerBet = gameState.Pot.Amount - chipCount;

        if (potCountBeforeDealerBet < 0)
        {
            return false;
        }

        MatchChipCount(
            playerChipInstances,
            gameState.PlayerChips.Count,
            playerChipArea,
            chipPrefabs);
        MatchChipCount(
            potChipInstances,
            potCountBeforeDealerBet,
            potArea,
            chipPrefabs);
        RemoveMissingInstances(dealerChipInstances);

        if (playerChipInstances.Count != gameState.PlayerChips.Count ||
            dealerChipInstances.Count < chipCount ||
            dealerChipInstances.Count !=
                gameState.DealerChips.Count + chipCount ||
            potChipInstances.Count != potCountBeforeDealerBet)
        {
            return false;
        }

        chips = new GameObject[chipCount];
        potTargetPositions = new Vector3[chipCount];

        for (int index = 0; index < chipCount; index++)
        {
            GameObject chip = dealerChipInstances[
                dealerChipInstances.Count - 1 - index];

            pendingChips.Add(chip);
            pendingRotations.Add(chip.transform.localRotation);
            pendingScales.Add(chip.transform.localScale);
            chips[index] = chip;
            potTargetPositions[index] = potArea.TransformPoint(
                GetChipLocalPosition(potChipInstances.Count + index));
        }

        return true;
    }

    public bool CompleteDealerBet(GameObject[] chips)
    {
        if (!CanCompleteDealerBet(chips))
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            dealerChipInstances.Remove(pendingChips[index]);
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            chip.transform.SetParent(potArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            potChipInstances.Add(chip);
        }

        ClearPendingDealerBet();
        ArrangeChips(dealerChipInstances);
        ArrangeChips(potChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelDealerBet()
    {
        ClearPendingDealerBet();
    }

    private bool CanCompleteDealerBet(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            potArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !dealerChipInstances.Contains(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void ClearPendingDealerBet()
    {
        pendingChips.Clear();
        pendingRotations.Clear();
        pendingScales.Clear();
    }

    private void MatchChipCount(
        List<GameObject> instances,
        int targetCount,
        Transform chipArea,
        GameObject[] prefabs)
    {
        targetCount = Mathf.Max(0, targetCount);
        RemoveMissingInstances(instances);

        if (targetCount < instances.Count)
        {
            RemoveExtraChips(instances, targetCount);
        }
        else if (targetCount > instances.Count)
        {
            AddMissingChips(instances, targetCount, chipArea, prefabs);
        }

        ArrangeChips(instances);
    }

    private static void RemoveMissingInstances(List<GameObject> instances)
    {
        for (int index = instances.Count - 1; index >= 0; index--)
        {
            if (instances[index] == null)
            {
                instances.RemoveAt(index);
            }
        }
    }

    private static void RemoveExtraChips(
        List<GameObject> instances,
        int targetCount)
    {
        for (int index = instances.Count - 1; index >= targetCount; index--)
        {
            GameObject instance = instances[index];
            instances.RemoveAt(index);

            if (instance != null)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
        }
    }

    private void AddMissingChips(
        List<GameObject> instances,
        int targetCount,
        Transform chipArea,
        GameObject[] prefabs)
    {
        if (chipArea == null)
        {
            return;
        }

        while (instances.Count < targetCount)
        {
            GameObject chipPrefab = GetRandomPrefab(prefabs);

            if (chipPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(chipPrefab, chipArea, false);
            instances.Add(instance);
        }
    }

    private GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        int validPrefabCount = 0;

        for (int index = 0; index < prefabs.Length; index++)
        {
            if (prefabs[index] != null)
            {
                validPrefabCount++;
            }
        }

        if (validPrefabCount == 0)
        {
            return null;
        }

        int randomValidIndex = prefabRandom.Next(validPrefabCount);

        for (int index = 0; index < prefabs.Length; index++)
        {
            if (prefabs[index] == null)
            {
                continue;
            }

            if (randomValidIndex == 0)
            {
                return prefabs[index];
            }

            randomValidIndex--;
        }

        return null;
    }

    private void ArrangeChips(List<GameObject> instances)
    {
        for (int index = 0; index < instances.Count; index++)
        {
            instances[index].transform.localPosition =
                GetChipLocalPosition(index);
        }
    }

    private Vector3 GetChipLocalPosition(int index)
    {
        int stackSize = Mathf.Max(1, maxChipsPerStack);
        int stackIndex = index / stackSize;
        int heightIndex = index % stackSize;

        return new Vector3(
            stackIndex * Mathf.Max(0f, stackSpacing),
            heightIndex * Mathf.Max(0f, chipHeightSpacing),
            0f);
    }
}
