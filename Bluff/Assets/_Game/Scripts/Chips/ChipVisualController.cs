using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class ChipVisualController : MonoBehaviour
{
    private const float PlayerChipMoveDuration = 0.25f;
    private const float PlayerChipMoveTimeout = 4f;

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
    private readonly List<Transform> pendingParents =
        new List<Transform>();
    private readonly List<Vector3> pendingLocalPositions =
        new List<Vector3>();
    private readonly List<Tween> playerMoveTweens =
        new List<Tween>();
    private bool isDealerCollectPending;
    private bool isPlayerBetPending;
    private bool isPlayerCollectPending;
    private int completedPlayerMoveTweenCount;
    private Coroutine playerMoveTimeoutCoroutine;
    private Action<GameObject[]> playerMoveCompleted;
    private Action<GameObject[]> playerMoveFailed;

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
            pendingParents.Add(chip.transform.parent);
            pendingLocalPositions.Add(chip.transform.localPosition);
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

        ClearPendingChipMove();
        ArrangeChips(dealerChipInstances);
        ArrangeChips(potChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelDealerBet()
    {
        ClearPendingChipMove();
    }

    public bool TryBeginDealerCollect(
        int chipCount,
        out GameObject[] chips,
        out Vector3[] dealerTargetPositions)
    {
        chips = null;
        dealerTargetPositions = null;

        RemoveMissingInstances(dealerChipInstances);
        RemoveMissingInstances(potChipInstances);

        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            dealerChipArea == null ||
            potArea == null ||
            potChipInstances.Count < chipCount ||
            dealerChipInstances.Count + chipCount !=
                gameState.DealerChips.Count ||
            potChipInstances.Count != gameState.Pot.Amount + chipCount)
        {
            return false;
        }

        chips = new GameObject[chipCount];
        dealerTargetPositions = new Vector3[chipCount];

        for (int index = 0; index < chipCount; index++)
        {
            GameObject chip = potChipInstances[
                potChipInstances.Count - 1 - index];

            pendingChips.Add(chip);
            pendingParents.Add(chip.transform.parent);
            pendingLocalPositions.Add(chip.transform.localPosition);
            pendingRotations.Add(chip.transform.localRotation);
            pendingScales.Add(chip.transform.localScale);
            chips[index] = chip;
            dealerTargetPositions[index] = dealerChipArea.TransformPoint(
                GetChipLocalPosition(dealerChipInstances.Count + index));
        }

        isDealerCollectPending = true;
        return true;
    }

    public bool CompleteDealerCollect(GameObject[] chips)
    {
        if (!CanCompleteDealerCollect(chips))
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            potChipInstances.Remove(pendingChips[index]);
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            chip.transform.SetParent(dealerChipArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            dealerChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(potChipInstances);
        ArrangeChips(dealerChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelDealerCollect()
    {
        ClearPendingChipMove();
    }

    public bool TryBeginPlayerBet(
        int chipCount,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            playerChipArea == null ||
            potArea == null ||
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(potChipInstances);

        int potCountBeforePlayerBet = gameState.Pot.Amount - chipCount;

        if (potCountBeforePlayerBet < 0 ||
            playerChipInstances.Count < chipCount ||
            playerChipInstances.Count !=
                gameState.PlayerChips.Count + chipCount ||
            potChipInstances.Count != potCountBeforePlayerBet)
        {
            return false;
        }

        Vector3[] potTargetPositions = new Vector3[chipCount];

        for (int index = 0; index < chipCount; index++)
        {
            GameObject chip = playerChipInstances[
                playerChipInstances.Count - 1 - index];

            if (chip == null || !chip.activeInHierarchy)
            {
                ClearPendingChipMove();
                return false;
            }

            pendingChips.Add(chip);
            pendingParents.Add(chip.transform.parent);
            pendingLocalPositions.Add(chip.transform.localPosition);
            pendingRotations.Add(chip.transform.localRotation);
            pendingScales.Add(chip.transform.localScale);
            potTargetPositions[index] = potArea.TransformPoint(
                GetChipLocalPosition(potChipInstances.Count + index));
        }

        isPlayerBetPending = true;
        return StartPlayerChipMove(
            potTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool CompletePlayerBet(GameObject[] chips)
    {
        if (!CanCompletePlayerBet(chips))
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            playerChipInstances.Remove(pendingChips[index]);
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            chip.transform.SetParent(potArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            potChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(playerChipInstances);
        ArrangeChips(potChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelPlayerBet()
    {
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        ClearPendingChipMove();
    }

    public bool TryBeginPlayerCollect(
        int chipCount,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            playerChipArea == null ||
            potArea == null ||
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(potChipInstances);

        if (potChipInstances.Count < chipCount ||
            playerChipInstances.Count + chipCount !=
                gameState.PlayerChips.Count ||
            potChipInstances.Count != gameState.Pot.Amount + chipCount)
        {
            return false;
        }

        Vector3[] playerTargetPositions = new Vector3[chipCount];

        for (int index = 0; index < chipCount; index++)
        {
            GameObject chip = potChipInstances[
                potChipInstances.Count - 1 - index];

            if (chip == null || !chip.activeInHierarchy)
            {
                ClearPendingChipMove();
                return false;
            }

            pendingChips.Add(chip);
            pendingParents.Add(chip.transform.parent);
            pendingLocalPositions.Add(chip.transform.localPosition);
            pendingRotations.Add(chip.transform.localRotation);
            pendingScales.Add(chip.transform.localScale);
            playerTargetPositions[index] = playerChipArea.TransformPoint(
                GetChipLocalPosition(playerChipInstances.Count + index));
        }

        isPlayerCollectPending = true;
        return StartPlayerChipMove(
            playerTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool CompletePlayerCollect(GameObject[] chips)
    {
        if (!CanCompletePlayerCollect(chips))
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            potChipInstances.Remove(pendingChips[index]);
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            chip.transform.SetParent(playerChipArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            playerChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(potChipInstances);
        ArrangeChips(playerChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelPlayerCollect()
    {
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        ClearPendingChipMove();
    }

    private bool CanCompleteDealerBet(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            isDealerCollectPending ||
            isPlayerBetPending ||
            isPlayerCollectPending ||
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

    private bool CanCompletePlayerBet(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            !isPlayerBetPending ||
            isPlayerCollectPending ||
            potArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !playerChipInstances.Contains(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCompletePlayerCollect(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            !isPlayerCollectPending ||
            isPlayerBetPending ||
            playerChipArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !potChipInstances.Contains(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCompleteDealerCollect(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            !isDealerCollectPending ||
            dealerChipArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !potChipInstances.Contains(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool StartPlayerChipMove(
        Vector3[] targetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (targetPositions == null ||
            targetPositions.Length != pendingChips.Count)
        {
            ClearPendingChipMove();
            return false;
        }

        playerMoveCompleted = onMoveCompleted;
        playerMoveFailed = onMoveFailed;
        completedPlayerMoveTweenCount = 0;

        for (int index = 0; index < pendingChips.Count; index++)
        {
            Transform chip = pendingChips[index].transform;
            chip.SetParent(null, true);
            Tween moveTween = chip
                .DOMove(
                    targetPositions[index],
                    PlayerChipMoveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(CompletePlayerChipMoveTween);
            playerMoveTweens.Add(moveTween);
        }

        playerMoveTimeoutCoroutine =
            StartCoroutine(PlayerChipMoveTimeoutRoutine());
        return true;
    }

    private IEnumerator PlayerChipMoveTimeoutRoutine()
    {
        yield return new WaitForSeconds(PlayerChipMoveTimeout);
        playerMoveTimeoutCoroutine = null;
        FailPlayerChipMove();
    }

    private void CompletePlayerChipMoveTween()
    {
        if (!isPlayerBetPending && !isPlayerCollectPending)
        {
            return;
        }

        completedPlayerMoveTweenCount++;

        if (completedPlayerMoveTweenCount < pendingChips.Count)
        {
            return;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (pendingChips[index] == null ||
                !pendingChips[index].activeInHierarchy)
            {
                FailPlayerChipMove();
                return;
            }
        }

        GameObject[] completedChips = pendingChips.ToArray();
        Action<GameObject[]> completedCallback = playerMoveCompleted;
        ClearPlayerChipMoveState();
        completedCallback?.Invoke(completedChips);
    }

    private void FailPlayerChipMove()
    {
        if ((!isPlayerBetPending && !isPlayerCollectPending) ||
            playerMoveFailed == null)
        {
            return;
        }

        GameObject[] failedChips = pendingChips.ToArray();
        Action<GameObject[]> failedCallback = playerMoveFailed;
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        failedCallback?.Invoke(failedChips);
    }

    private void RestorePendingChips()
    {
        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];

            if (chip == null)
            {
                continue;
            }

            chip.transform.SetParent(pendingParents[index], false);
            chip.transform.localPosition = pendingLocalPositions[index];
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
        }
    }

    private void ClearPlayerChipMoveState()
    {
        if (playerMoveTimeoutCoroutine != null)
        {
            StopCoroutine(playerMoveTimeoutCoroutine);
            playerMoveTimeoutCoroutine = null;
        }

        playerMoveTweens.Clear();
        completedPlayerMoveTweenCount = 0;
        playerMoveCompleted = null;
        playerMoveFailed = null;
    }

    private void KillPlayerChipMoveTweens()
    {
        for (int index = 0; index < playerMoveTweens.Count; index++)
        {
            playerMoveTweens[index]?.Kill(false);
        }

        playerMoveTweens.Clear();
    }

    private void ClearPendingChipMove()
    {
        pendingChips.Clear();
        pendingParents.Clear();
        pendingLocalPositions.Clear();
        pendingRotations.Clear();
        pendingScales.Clear();
        isDealerCollectPending = false;
        isPlayerBetPending = false;
        isPlayerCollectPending = false;
    }

    private void OnDisable()
    {
        FailPlayerChipMove();
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
