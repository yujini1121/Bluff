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
    private GameObject pendingChip;
    private Quaternion pendingRotation;
    private Vector3 pendingScale;

    public void Initialize(GameState state)
    {
        gameState = state;
        RefreshChips();
    }

    public void RefreshChips()
    {
        if (pendingChip != null)
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
        out GameObject chip,
        out Vector3 potTargetPosition)
    {
        chip = null;
        potTargetPosition = default;

        RemoveMissingInstances(dealerChipInstances);
        RemoveMissingInstances(potChipInstances);

        if (gameState == null ||
            pendingChip != null ||
            dealerChipArea == null ||
            potArea == null ||
            dealerChipInstances.Count == 0 ||
            dealerChipInstances.Count != gameState.DealerChips.Count + 1 ||
            potChipInstances.Count + 1 != gameState.Pot.Amount)
        {
            return false;
        }

        pendingChip =
            dealerChipInstances[dealerChipInstances.Count - 1];
        pendingRotation = pendingChip.transform.localRotation;
        pendingScale = pendingChip.transform.localScale;
        chip = pendingChip;
        potTargetPosition = potArea.TransformPoint(
            GetChipLocalPosition(potChipInstances.Count));
        return true;
    }

    public bool CompleteDealerBet(GameObject chip)
    {
        if (chip == null ||
            chip != pendingChip ||
            potArea == null ||
            !dealerChipInstances.Remove(chip))
        {
            return false;
        }

        chip.transform.SetParent(potArea, true);
        chip.transform.localRotation = pendingRotation;
        chip.transform.localScale = pendingScale;
        potChipInstances.Add(chip);
        pendingChip = null;
        ArrangeChips(dealerChipInstances);
        ArrangeChips(potChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelDealerBet()
    {
        pendingChip = null;
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
