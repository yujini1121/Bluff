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

    public void Initialize(GameState state)
    {
        gameState = state;
        RefreshChips();
    }

    public void RefreshChips()
    {
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
        int stackSize = Mathf.Max(1, maxChipsPerStack);
        float xSpacing = Mathf.Max(0f, stackSpacing);
        float ySpacing = Mathf.Max(0f, chipHeightSpacing);

        for (int index = 0; index < instances.Count; index++)
        {
            int stackIndex = index / stackSize;
            int heightIndex = index % stackSize;

            instances[index].transform.localPosition = new Vector3(
                stackIndex * xSpacing,
                heightIndex * ySpacing,
                0f);
        }
    }
}
