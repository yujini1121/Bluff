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
    [SerializeField] private Transform playerBetAreaPoint;
    [SerializeField] private Transform dealerBetAreaPoint;
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
    private readonly List<GameObject> playerBetChipInstances =
        new List<GameObject>();
    private readonly List<GameObject> dealerBetChipInstances =
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
    private bool isRoundAntePending;
    private bool isPlayerBetPending;
    private bool isPlayerCollectPending;
    private bool isDrawSettlementPending;
    private bool isFoldSettlementPending;
    private int pendingAntePlayerChipCount;
    private TurnOwner pendingFoldedBy;
    private int pendingFoldPotChipCount;
    private int completedPlayerMoveTweenCount;
    private Coroutine playerMoveTimeoutCoroutine;
    private Action<GameObject[]> playerMoveCompleted;
    private Action<GameObject[]> playerMoveFailed;
    private bool isApplicationQuitting;

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
        int playerBetChipCount = GetPlayerBetChipCount();
        int dealerBetChipCount = GetDealerBetChipCount();
        // Pot remains authoritative; only its 3D presentation is split.
        int potChipCount = GetCarryPotChipCount(
            playerBetChipCount,
            dealerBetChipCount);

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
            playerBetChipInstances,
            playerBetChipCount,
            playerBetAreaPoint,
            chipPrefabs);
        MatchChipCount(
            dealerBetChipInstances,
            dealerBetChipCount,
            dealerBetAreaPoint,
            chipPrefabs);
        MatchChipCount(
            potChipInstances,
            potChipCount,
            potArea,
            chipPrefabs);
    }

    public bool TryBeginRoundAnte(
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (gameState == null ||
            pendingChips.Count > 0 ||
            playerChipArea == null ||
            dealerChipArea == null ||
            playerBetAreaPoint == null ||
            dealerBetAreaPoint == null ||
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null)
        {
            return false;
        }

        int playerAnteCount = gameState.Betting.PlayerTotalBet;
        int dealerAnteCount = gameState.Betting.DealerTotalBet;

        if (playerAnteCount <= 0 || dealerAnteCount <= 0)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(dealerChipInstances);
        RemoveMissingInstances(playerBetChipInstances);
        RemoveMissingInstances(dealerBetChipInstances);

        if (playerBetChipInstances.Count != 0 ||
            dealerBetChipInstances.Count != 0 ||
            playerChipInstances.Count !=
                gameState.PlayerChips.Count + playerAnteCount ||
            dealerChipInstances.Count !=
                gameState.DealerChips.Count + dealerAnteCount)
        {
            return false;
        }

        Vector3[] targetPositions =
            new Vector3[playerAnteCount + dealerAnteCount];

        for (int index = 0; index < playerAnteCount; index++)
        {
            GameObject chip = playerChipInstances[
                playerChipInstances.Count - 1 - index];

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                return false;
            }

            targetPositions[index] = playerBetAreaPoint.TransformPoint(
                GetChipLocalPosition(index));
        }

        for (int index = 0; index < dealerAnteCount; index++)
        {
            GameObject chip = dealerChipInstances[
                dealerChipInstances.Count - 1 - index];
            int targetIndex = playerAnteCount + index;

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                return false;
            }

            targetPositions[targetIndex] =
                dealerBetAreaPoint.TransformPoint(
                    GetChipLocalPosition(index));
        }

        isRoundAntePending = true;
        pendingAntePlayerChipCount = playerAnteCount;
        return StartPlayerChipMove(
            targetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool CompleteRoundAnte(GameObject[] chips)
    {
        if (!CanCompleteRoundAnte(chips))
        {
            return false;
        }

        for (int index = 0;
             index < pendingAntePlayerChipCount;
             index++)
        {
            GameObject chip = pendingChips[index];
            playerChipInstances.Remove(chip);
            MoveChipToArea(
                chip,
                playerBetAreaPoint,
                playerBetChipInstances,
                index);
        }

        for (int index = pendingAntePlayerChipCount;
             index < pendingChips.Count;
             index++)
        {
            GameObject chip = pendingChips[index];
            dealerChipInstances.Remove(chip);
            MoveChipToArea(
                chip,
                dealerBetAreaPoint,
                dealerBetChipInstances,
                index);
        }

        ClearPendingChipMove();
        ArrangeChips(playerChipInstances);
        ArrangeChips(dealerChipInstances);
        ArrangeChips(playerBetChipInstances);
        ArrangeChips(dealerBetChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelRoundAnte()
    {
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        ClearPendingChipMove();
    }

    public bool TryBeginDealerBet(
        int chipCount,
        out GameObject[] chips,
        out Vector3[] betAreaTargetPositions)
    {
        chips = null;
        betAreaTargetPositions = null;

        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            dealerChipArea == null ||
            dealerBetAreaPoint == null)
        {
            return false;
        }

        int dealerBetCountBefore =
            gameState.Betting.DealerTotalBet - chipCount;
        int playerBetChipCount = GetPlayerBetChipCount();
        int carryPotChipCount = GetCarryPotChipCount(
            playerBetChipCount,
            gameState.Betting.DealerTotalBet);

        if (dealerBetCountBefore < 0 || carryPotChipCount < 0)
        {
            return false;
        }

        MatchChipCount(
            playerChipInstances,
            gameState.PlayerChips.Count,
            playerChipArea,
            chipPrefabs);
        MatchChipCount(
            playerBetChipInstances,
            playerBetChipCount,
            playerBetAreaPoint,
            chipPrefabs);
        MatchChipCount(
            dealerBetChipInstances,
            dealerBetCountBefore,
            dealerBetAreaPoint,
            chipPrefabs);
        MatchChipCount(
            potChipInstances,
            carryPotChipCount,
            potArea,
            chipPrefabs);
        RemoveMissingInstances(dealerChipInstances);

        if (playerChipInstances.Count != gameState.PlayerChips.Count ||
            playerBetChipInstances.Count != playerBetChipCount ||
            dealerBetChipInstances.Count != dealerBetCountBefore ||
            dealerChipInstances.Count < chipCount ||
            dealerChipInstances.Count !=
                gameState.DealerChips.Count + chipCount ||
            potChipInstances.Count != carryPotChipCount)
        {
            return false;
        }

        chips = new GameObject[chipCount];
        betAreaTargetPositions = new Vector3[chipCount];

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
            betAreaTargetPositions[index] =
                dealerBetAreaPoint.TransformPoint(
                    GetChipLocalPosition(
                        dealerBetChipInstances.Count + index));
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
            chip.transform.SetParent(dealerBetAreaPoint, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            dealerBetChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(dealerChipInstances);
        ArrangeChips(dealerBetChipInstances);
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
        RemoveMissingInstances(playerBetChipInstances);
        RemoveMissingInstances(dealerBetChipInstances);
        RemoveMissingInstances(potChipInstances);

        int settlementChipCount = GetSettlementVisualChipCount();

        if (gameState == null ||
            chipCount <= 0 ||
            pendingChips.Count > 0 ||
            dealerChipArea == null ||
            settlementChipCount != chipCount ||
            dealerChipInstances.Count + chipCount !=
                gameState.DealerChips.Count)
        {
            return false;
        }

        chips = new GameObject[chipCount];
        dealerTargetPositions = new Vector3[chipCount];

        List<GameObject> settlementChips = GetSettlementVisualChips();

        for (int index = 0; index < settlementChips.Count; index++)
        {
            GameObject chip = settlementChips[index];

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                chips = null;
                dealerTargetPositions = null;
                return false;
            }

            chips[index] = chip;
            dealerTargetPositions[index] = dealerChipArea.TransformPoint(
                GetChipLocalPosition(
                    dealerChipInstances.Count + index,
                    true));
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
            GameObject chip = pendingChips[index];
            RemoveSettlementVisualChip(chip);
            chip.transform.SetParent(dealerChipArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            dealerChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(playerBetChipInstances);
        ArrangeChips(dealerBetChipInstances);
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
            playerBetAreaPoint == null ||
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(playerBetChipInstances);

        int playerBetCountBefore =
            gameState.Betting.PlayerTotalBet - chipCount;

        if (playerBetCountBefore < 0 ||
            playerChipInstances.Count < chipCount ||
            playerChipInstances.Count !=
                gameState.PlayerChips.Count + chipCount ||
            playerBetChipInstances.Count != playerBetCountBefore)
        {
            return false;
        }

        Vector3[] betAreaTargetPositions = new Vector3[chipCount];

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
            betAreaTargetPositions[index] =
                playerBetAreaPoint.TransformPoint(
                    GetChipLocalPosition(
                        playerBetChipInstances.Count + index));
        }

        isPlayerBetPending = true;
        return StartPlayerChipMove(
            betAreaTargetPositions,
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
            chip.transform.SetParent(playerBetAreaPoint, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            playerBetChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(playerChipInstances);
        ArrangeChips(playerBetChipInstances);
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
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(playerBetChipInstances);
        RemoveMissingInstances(dealerBetChipInstances);
        RemoveMissingInstances(potChipInstances);

        int settlementChipCount = GetSettlementVisualChipCount();

        if (settlementChipCount != chipCount ||
            playerChipInstances.Count + chipCount !=
                gameState.PlayerChips.Count)
        {
            return false;
        }

        Vector3[] playerTargetPositions = new Vector3[chipCount];
        List<GameObject> settlementChips = GetSettlementVisualChips();

        for (int index = 0; index < settlementChips.Count; index++)
        {
            GameObject chip = settlementChips[index];

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                return false;
            }

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
            GameObject chip = pendingChips[index];
            RemoveSettlementVisualChip(chip);
            chip.transform.SetParent(playerChipArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            playerChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(playerBetChipInstances);
        ArrangeChips(dealerBetChipInstances);
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

    public bool TryBeginDrawSettlement(
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (gameState == null ||
            pendingChips.Count > 0 ||
            potArea == null ||
            !isActiveAndEnabled ||
            onMoveCompleted == null ||
            onMoveFailed == null ||
            gameState.RoundEndReason != RoundEndReason.Showdown ||
            gameState.Pot.Amount <= 0)
        {
            return false;
        }

        RemoveMissingInstances(playerBetChipInstances);
        RemoveMissingInstances(dealerBetChipInstances);
        RemoveMissingInstances(potChipInstances);

        int betChipCount = playerBetChipInstances.Count +
                           dealerBetChipInstances.Count;

        if (betChipCount <= 0 ||
            potChipInstances.Count + betChipCount !=
                gameState.Pot.Amount)
        {
            return false;
        }

        Vector3[] potTargetPositions = new Vector3[betChipCount];
        int targetIndex = 0;

        if (!TryAddDrawSettlementChips(
                playerBetChipInstances,
                potTargetPositions,
                ref targetIndex) ||
            !TryAddDrawSettlementChips(
                dealerBetChipInstances,
                potTargetPositions,
                ref targetIndex))
        {
            ClearPendingChipMove();
            return false;
        }

        isDrawSettlementPending = true;
        return StartPlayerChipMove(
            potTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool CompleteDrawSettlement(GameObject[] chips)
    {
        if (!CanCompleteDrawSettlement(chips))
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            playerBetChipInstances.Remove(chip);
            dealerBetChipInstances.Remove(chip);
            chip.transform.SetParent(potArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            potChipInstances.Add(chip);
        }

        ClearPendingChipMove();
        ArrangeChips(playerBetChipInstances);
        ArrangeChips(dealerBetChipInstances);
        ArrangeChips(potChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelDrawSettlement()
    {
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        ClearPendingChipMove();
    }

    public bool TryBeginFoldSettlement(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount,
        Action onCompleted,
        Action onFailed)
    {
        if (gameState == null ||
            (foldedBy != TurnOwner.Player &&
             foldedBy != TurnOwner.Dealer) ||
            potChipCount < 0 ||
            penaltyChipCount < 0 ||
            potChipCount + penaltyChipCount <= 0 ||
            pendingChips.Count > 0 ||
            playerChipArea == null ||
            dealerChipArea == null ||
            potArea == null ||
            !isActiveAndEnabled ||
            onCompleted == null ||
            onFailed == null ||
            gameState.RoundEndReason != RoundEndReason.Fold ||
            gameState.FoldedBy != foldedBy ||
            gameState.FoldPenaltyAmount != penaltyChipCount)
        {
            return false;
        }

        RemoveMissingInstances(playerChipInstances);
        RemoveMissingInstances(dealerChipInstances);
        RemoveMissingInstances(playerBetChipInstances);
        RemoveMissingInstances(dealerBetChipInstances);
        RemoveMissingInstances(potChipInstances);

        List<GameObject> foldedChipInstances = foldedBy == TurnOwner.Player
            ? playerChipInstances
            : dealerChipInstances;
        List<GameObject> winnerChipInstances = foldedBy == TurnOwner.Player
            ? dealerChipInstances
            : playerChipInstances;
        bool stackWinnerChipsFromRight =
            winnerChipInstances == dealerChipInstances;
        Transform winnerChipArea = foldedBy == TurnOwner.Player
            ? dealerChipArea
            : playerChipArea;
        int foldedGameChipCount = foldedBy == TurnOwner.Player
            ? gameState.PlayerChips.Count
            : gameState.DealerChips.Count;
        int winnerGameChipCount = foldedBy == TurnOwner.Player
            ? gameState.DealerChips.Count
            : gameState.PlayerChips.Count;
        int settlementVisualChipCount = GetSettlementVisualChipCount();

        if (foldedChipInstances.Count < penaltyChipCount ||
            settlementVisualChipCount != potChipCount ||
            foldedChipInstances.Count !=
                foldedGameChipCount + penaltyChipCount ||
            winnerChipInstances.Count + potChipCount + penaltyChipCount !=
                winnerGameChipCount)
        {
            return false;
        }

        Vector3[] targetPositions =
            new Vector3[potChipCount + penaltyChipCount];

        List<GameObject> settlementChips = GetSettlementVisualChips();

        for (int index = 0; index < settlementChips.Count; index++)
        {
            GameObject chip = settlementChips[index];

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                return false;
            }

            targetPositions[index] = winnerChipArea.TransformPoint(
                GetChipLocalPosition(
                    winnerChipInstances.Count + index,
                    stackWinnerChipsFromRight));
        }

        for (int index = 0; index < penaltyChipCount; index++)
        {
            GameObject chip = foldedChipInstances[
                foldedChipInstances.Count - 1 - index];
            int targetIndex = potChipCount + index;

            if (!TryAddPendingChip(chip))
            {
                ClearPendingChipMove();
                return false;
            }

            targetPositions[targetIndex] = winnerChipArea.TransformPoint(
                GetChipLocalPosition(
                    winnerChipInstances.Count + targetIndex,
                    stackWinnerChipsFromRight));
        }

        isFoldSettlementPending = true;
        pendingFoldedBy = foldedBy;
        pendingFoldPotChipCount = potChipCount;
        int delayedStartIndex = potChipCount > 0 && penaltyChipCount > 0
            ? potChipCount
            : int.MaxValue;
        return StartPlayerChipMove(
            targetPositions,
            _ => onCompleted(),
            _ => onFailed(),
            delayedStartIndex);
    }

    public bool CompleteFoldSettlement()
    {
        if (!CanCompleteFoldSettlement())
        {
            return false;
        }

        List<GameObject> foldedChipInstances =
            pendingFoldedBy == TurnOwner.Player
                ? playerChipInstances
                : dealerChipInstances;
        List<GameObject> winnerChipInstances =
            pendingFoldedBy == TurnOwner.Player
                ? dealerChipInstances
                : playerChipInstances;
        Transform winnerChipArea = pendingFoldedBy == TurnOwner.Player
            ? dealerChipArea
            : playerChipArea;

        for (int index = 0; index < pendingFoldPotChipCount; index++)
        {
            RemoveSettlementVisualChip(pendingChips[index]);
        }

        for (int index = pendingFoldPotChipCount;
             index < pendingChips.Count;
             index++)
        {
            foldedChipInstances.Remove(pendingChips[index]);
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];
            chip.transform.SetParent(winnerChipArea, true);
            chip.transform.localRotation = pendingRotations[index];
            chip.transform.localScale = pendingScales[index];
            winnerChipInstances.Add(chip);
        }

        ClearPlayerChipMoveState();
        ClearPendingChipMove();
        ArrangeChips(playerBetChipInstances);
        ArrangeChips(dealerBetChipInstances);
        ArrangeChips(potChipInstances);
        ArrangeChips(foldedChipInstances);
        ArrangeChips(winnerChipInstances);
        RefreshChips();
        return true;
    }

    public void CancelFoldSettlement()
    {
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();
        ClearPendingChipMove();
    }

    private bool CanCompleteRoundAnte(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            !isRoundAntePending ||
            pendingAntePlayerChipCount <= 0 ||
            pendingAntePlayerChipCount >= pendingChips.Count ||
            playerBetAreaPoint == null ||
            dealerBetAreaPoint == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            List<GameObject> sourceInstances =
                index < pendingAntePlayerChipCount
                    ? playerChipInstances
                    : dealerChipInstances;

            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !sourceInstances.Contains(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCompleteDrawSettlement(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            !isDrawSettlementPending ||
            potArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = chips[index];

            if (chip == null ||
                chip != pendingChips[index] ||
                (!playerBetChipInstances.Contains(chip) &&
                 !dealerBetChipInstances.Contains(chip)))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCompleteDealerBet(GameObject[] chips)
    {
        if (chips == null ||
            chips.Length == 0 ||
            chips.Length != pendingChips.Count ||
            isDealerCollectPending ||
            isRoundAntePending ||
            isPlayerBetPending ||
            isPlayerCollectPending ||
            isDrawSettlementPending ||
            isFoldSettlementPending ||
            dealerBetAreaPoint == null)
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
            isDrawSettlementPending ||
            isFoldSettlementPending ||
            playerBetAreaPoint == null)
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
            isDrawSettlementPending ||
            isFoldSettlementPending ||
            playerChipArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !IsSettlementVisualChip(chips[index]))
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
            isRoundAntePending ||
            isDrawSettlementPending ||
            isFoldSettlementPending ||
            dealerChipArea == null)
        {
            return false;
        }

        for (int index = 0; index < pendingChips.Count; index++)
        {
            if (chips[index] == null ||
                chips[index] != pendingChips[index] ||
                !IsSettlementVisualChip(chips[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanCompleteFoldSettlement()
    {
        if (!isFoldSettlementPending ||
            pendingChips.Count == 0 ||
            pendingFoldPotChipCount < 0 ||
            pendingFoldPotChipCount > pendingChips.Count ||
            (pendingFoldedBy != TurnOwner.Player &&
             pendingFoldedBy != TurnOwner.Dealer))
        {
            return false;
        }

        List<GameObject> foldedChipInstances =
            pendingFoldedBy == TurnOwner.Player
                ? playerChipInstances
                : dealerChipInstances;

        for (int index = 0; index < pendingChips.Count; index++)
        {
            GameObject chip = pendingChips[index];

            if (chip == null ||
                (index < pendingFoldPotChipCount
                    ? !IsSettlementVisualChip(chip)
                    : !foldedChipInstances.Contains(chip)))
            {
                return false;
            }
        }

        return true;
    }

    private bool StartPlayerChipMove(
        Vector3[] targetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed,
        int delayedStartIndex = int.MaxValue)
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
                .SetDelay(index >= delayedStartIndex
                    ? PlayerChipMoveDuration
                    : 0f)
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
        if (!isRoundAntePending &&
            !isPlayerBetPending &&
            !isPlayerCollectPending &&
            !isDrawSettlementPending &&
            !isFoldSettlementPending)
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

    private void FailPlayerChipMove(bool notifyFailure = true)
    {
        if (!isRoundAntePending &&
            !isPlayerBetPending &&
            !isPlayerCollectPending &&
            !isDrawSettlementPending &&
            !isFoldSettlementPending &&
            playerMoveFailed == null)
        {
            return;
        }

        GameObject[] failedChips = notifyFailure
            ? pendingChips.ToArray()
            : null;
        Action<GameObject[]> failedCallback = notifyFailure
            ? playerMoveFailed
            : null;
        KillPlayerChipMoveTweens();
        RestorePendingChips();
        ClearPlayerChipMoveState();

        if (!notifyFailure)
        {
            ClearPendingChipMove();
        }

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

            Transform pendingParent = pendingParents[index];
            chip.transform.SetParent(
                pendingParent != null ? pendingParent : null,
                false);
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

    private bool TryAddPendingChip(GameObject chip)
    {
        if (chip == null || !chip.activeInHierarchy)
        {
            return false;
        }

        pendingChips.Add(chip);
        pendingParents.Add(chip.transform.parent);
        pendingLocalPositions.Add(chip.transform.localPosition);
        pendingRotations.Add(chip.transform.localRotation);
        pendingScales.Add(chip.transform.localScale);
        return true;
    }

    private void ClearPendingChipMove()
    {
        pendingChips.Clear();
        pendingParents.Clear();
        pendingLocalPositions.Clear();
        pendingRotations.Clear();
        pendingScales.Clear();
        isDealerCollectPending = false;
        isRoundAntePending = false;
        isPlayerBetPending = false;
        isPlayerCollectPending = false;
        isDrawSettlementPending = false;
        isFoldSettlementPending = false;
        pendingAntePlayerChipCount = 0;
        pendingFoldedBy = TurnOwner.None;
        pendingFoldPotChipCount = 0;
    }

    private void OnDisable()
    {
        bool notifyFailure =
            !isApplicationQuitting &&
            Application.isPlaying &&
            gameObject.scene.IsValid() &&
            gameObject.scene.isLoaded;
        FailPlayerChipMove(notifyFailure);
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private int GetPlayerBetChipCount()
    {
        return gameState?.Betting.PlayerTotalBet ?? 0;
    }

    private int GetDealerBetChipCount()
    {
        return gameState?.Betting.DealerTotalBet ?? 0;
    }

    private int GetCarryPotChipCount(
        int playerBetChipCount,
        int dealerBetChipCount)
    {
        if (gameState == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            gameState.Pot.Amount -
            playerBetChipCount -
            dealerBetChipCount);
    }

    private int GetSettlementVisualChipCount()
    {
        return playerBetChipInstances.Count +
               dealerBetChipInstances.Count +
               potChipInstances.Count;
    }

    private List<GameObject> GetSettlementVisualChips()
    {
        List<GameObject> chips =
            new List<GameObject>(GetSettlementVisualChipCount());
        AddChipsInTransferOrder(playerBetChipInstances, chips);
        AddChipsInTransferOrder(dealerBetChipInstances, chips);
        AddChipsInTransferOrder(potChipInstances, chips);
        return chips;
    }

    private static void AddChipsInTransferOrder(
        List<GameObject> source,
        List<GameObject> destination)
    {
        for (int index = source.Count - 1; index >= 0; index--)
        {
            destination.Add(source[index]);
        }
    }

    private bool IsSettlementVisualChip(GameObject chip)
    {
        return playerBetChipInstances.Contains(chip) ||
               dealerBetChipInstances.Contains(chip) ||
               potChipInstances.Contains(chip);
    }

    private void RemoveSettlementVisualChip(GameObject chip)
    {
        playerBetChipInstances.Remove(chip);
        dealerBetChipInstances.Remove(chip);
        potChipInstances.Remove(chip);
    }

    private bool TryAddDrawSettlementChips(
        List<GameObject> source,
        Vector3[] targetPositions,
        ref int targetIndex)
    {
        for (int index = source.Count - 1; index >= 0; index--)
        {
            GameObject chip = source[index];

            if (!TryAddPendingChip(chip))
            {
                return false;
            }

            targetPositions[targetIndex] = potArea.TransformPoint(
                GetChipLocalPosition(
                    potChipInstances.Count + targetIndex));
            targetIndex++;
        }

        return true;
    }

    private void MoveChipToArea(
        GameObject chip,
        Transform chipArea,
        List<GameObject> instances,
        int pendingIndex)
    {
        chip.transform.SetParent(chipArea, true);
        chip.transform.localRotation = pendingRotations[pendingIndex];
        chip.transform.localScale = pendingScales[pendingIndex];
        instances.Add(chip);
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
        bool stackFromRight = instances == dealerChipInstances;

        for (int index = 0; index < instances.Count; index++)
        {
            instances[index].transform.localPosition =
                GetChipLocalPosition(index, stackFromRight);
        }
    }

    private Vector3 GetChipLocalPosition(
        int index,
        bool stackFromRight = false)
    {
        int stackSize = Mathf.Max(1, maxChipsPerStack);
        int stackIndex = index / stackSize;
        int heightIndex = index % stackSize;
        float stackDirection = stackFromRight ? -1f : 1f;

        return new Vector3(
            stackIndex * Mathf.Max(0f, stackSpacing) * stackDirection,
            heightIndex * Mathf.Max(0f, chipHeightSpacing),
            0f);
    }
}
