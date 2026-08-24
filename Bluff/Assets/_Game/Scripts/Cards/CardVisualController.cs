using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class CardVisualController : MonoBehaviour
{
    [Header("Community Card")]
    [SerializeField] private CardVisual communityCardVisual1;
    [SerializeField] private CardVisual communityCardVisual2;

    [Header("Dealer Card")]
    [SerializeField] private CardVisual dealerCardVisual;
    [SerializeField] private Transform dealerCardRoot;
    [SerializeField] private Transform dealerCardPoint;
    [SerializeField] private Transform dealerShowdownCardPoint;

    [Header("Player Private Card")]
    [FormerlySerializedAs("playerRevealCardVisual")]
    [SerializeField] private CardVisual playerCardVisual;
    [SerializeField] private Transform playerCardRoot;
    [SerializeField] private Transform playerCardPoint;
    [SerializeField] private Transform playerShowdownCardPoint;
    [FormerlySerializedAs("playerRevealDuration")]
    [SerializeField, Min(0f)] private float showdownRevealDuration = 0.3f;
    [SerializeField, Min(0f)] private float showdownCardInterval = 0.2f;

    [Header("Private Card Scale")]
    [SerializeField] private Vector3 dealerNormalCardScale =
        new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Vector3 playerNormalCardScale = Vector3.one;
    [SerializeField] private Vector3 showdownCardScale = Vector3.one;

    [Header("Deal")]
    [SerializeField] private Transform cardDealPoint;
    [SerializeField, Min(0f)] private float dealDuration = 0.2f;
    [SerializeField, Min(0f)] private float dealInterval = 0.05f;

    private GameState gameState;
    private Sequence dealSequence;
    private CardVisual[] activeDealVisuals;
    private Transform[] activeDealTransforms;
    private CardTransformState[] originalTransforms;
    private Action dealCompleted;
    private Action dealFailed;
    private Sequence showdownRevealSequence;
    private Action showdownRevealCompleted;
    private Action showdownRevealFailed;
    private bool isDealerCardFollowingNormalPoint;
    private bool isPlayerCardFollowingNormalPoint;
    private bool isApplicationQuitting;

    private struct CardTransformState
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    public void Initialize(GameState state)
    {
        gameState = state;
        RefreshCards();
    }

    public void RefreshCards()
    {
        SetCard(communityCardVisual1, gameState?.CommunityCard1);
        SetCard(communityCardVisual2, gameState?.CommunityCard2);
        ResetPrivateCardVisuals();
    }

    public bool TryPlayShowdownReveal(
        Action onCompleted,
        Action onFailed)
    {
        if (gameState == null ||
            gameState.PlayerCard == null ||
            gameState.DealerCard == null ||
            playerCardVisual == null ||
            dealerCardVisual == null ||
            playerCardRoot == null ||
            dealerCardRoot == null ||
            dealerCardPoint == null ||
            playerCardPoint == null ||
            playerShowdownCardPoint == null ||
            dealerShowdownCardPoint == null ||
            dealSequence != null ||
            showdownRevealSequence != null ||
            !isActiveAndEnabled ||
            onCompleted == null ||
            onFailed == null)
        {
            return false;
        }

        ResetPrivateCardVisuals();

        if (!HasValidPrivateCardHierarchy())
        {
            return false;
        }

        StopDealerNormalFollow();
        showdownRevealCompleted = onCompleted;
        showdownRevealFailed = onFailed;
        float revealDuration = Mathf.Max(0f, showdownRevealDuration);

        showdownRevealSequence = DOTween.Sequence();
        showdownRevealSequence.Append(
            dealerCardRoot
                .DOMove(
                    dealerShowdownCardPoint.position,
                    revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            dealerCardRoot
                .DORotateQuaternion(
                    dealerShowdownCardPoint.rotation,
                    revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            dealerCardVisual.transform
                .DOScale(showdownCardScale, revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.AppendInterval(
            Mathf.Max(0f, showdownCardInterval));
        showdownRevealSequence.AppendCallback(StopPlayerNormalFollow);
        showdownRevealSequence.Append(
            playerCardRoot
                .DOMove(
                    playerShowdownCardPoint.position,
                    revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            playerCardRoot
                .DORotateQuaternion(
                    playerShowdownCardPoint.rotation,
                    revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            playerCardVisual.transform
                .DOScale(showdownCardScale, revealDuration)
                .SetEase(Ease.OutQuad));
        showdownRevealSequence
            .OnComplete(CompleteShowdownReveal)
            .OnKill(HandleShowdownRevealKilled);
        return true;
    }

    public void ShowShowdownCardsImmediately()
    {
        if (showdownRevealSequence != null)
        {
            FailShowdownReveal(false);
        }

        isPlayerCardFollowingNormalPoint = false;
        isDealerCardFollowingNormalPoint = false;

        if (gameState?.PlayerCard == null ||
            !PlacePrivateCardAtAnchor(
                playerCardRoot,
                playerCardVisual,
                playerShowdownCardPoint,
                showdownCardScale))
        {
            playerCardVisual?.Clear();
        }
        else
        {
            SetCard(playerCardVisual, gameState.PlayerCard);
        }

        if (gameState?.DealerCard == null ||
            !PlacePrivateCardAtAnchor(
                dealerCardRoot,
                dealerCardVisual,
                dealerShowdownCardPoint,
                showdownCardScale))
        {
            dealerCardVisual?.Clear();
        }
        else
        {
            SetCard(dealerCardVisual, gameState.DealerCard);
        }
    }

    public void ResetPrivateCardVisuals()
    {
        if (showdownRevealSequence != null)
        {
            FailShowdownReveal(false);
        }

        isPlayerCardFollowingNormalPoint = false;
        isDealerCardFollowingNormalPoint = false;

        SyncPrivateCardAtNormalPoint(
            playerCardRoot,
            playerCardVisual,
            playerCardPoint,
            gameState?.PlayerCard,
            playerNormalCardScale,
            out isPlayerCardFollowingNormalPoint);
        SyncPrivateCardAtNormalPoint(
            dealerCardRoot,
            dealerCardVisual,
            dealerCardPoint,
            gameState?.DealerCard,
            dealerNormalCardScale,
            out isDealerCardFollowingNormalPoint);
    }

    public bool TryPlayDeal(Action onCompleted, Action onFailed)
    {
        if (gameState == null ||
            cardDealPoint == null ||
            communityCardVisual1 == null ||
            communityCardVisual2 == null ||
            dealerCardVisual == null ||
            playerCardVisual == null ||
            dealerCardRoot == null ||
            playerCardRoot == null ||
            dealerCardPoint == null ||
            playerCardPoint == null ||
            gameState.CommunityCard1 == null ||
            gameState.CommunityCard2 == null ||
            gameState.DealerCard == null ||
            gameState.PlayerCard == null ||
            dealSequence != null ||
            !isActiveAndEnabled ||
            onCompleted == null ||
            onFailed == null)
        {
            return false;
        }

        ResetPrivateCardVisuals();

        if (!HasValidPrivateCardHierarchy())
        {
            return false;
        }

        isDealerCardFollowingNormalPoint = false;
        isPlayerCardFollowingNormalPoint = false;
        activeDealVisuals = new[]
        {
            communityCardVisual1,
            communityCardVisual2,
            dealerCardVisual,
            playerCardVisual
        };
        activeDealTransforms = new[]
        {
            communityCardVisual1.transform,
            communityCardVisual2.transform,
            dealerCardRoot,
            playerCardRoot
        };
        originalTransforms = new CardTransformState[
            activeDealVisuals.Length];
        dealCompleted = onCompleted;
        dealFailed = onFailed;

        SetCard(communityCardVisual1, gameState.CommunityCard1);
        SetCard(communityCardVisual2, gameState.CommunityCard2);
        SetCard(dealerCardVisual, gameState.DealerCard);
        SetCard(playerCardVisual, gameState.PlayerCard);

        for (int index = 0; index < activeDealVisuals.Length; index++)
        {
            Transform cardTransform = activeDealTransforms[index];
            Transform parent = cardTransform.parent;
            originalTransforms[index] = new CardTransformState
            {
                LocalPosition = cardTransform.localPosition,
                LocalRotation = cardTransform.localRotation,
                LocalScale = cardTransform.localScale
            };

            cardTransform.localPosition = parent != null
                ? parent.InverseTransformPoint(cardDealPoint.position)
                : cardDealPoint.position;
            cardTransform.localRotation =
                originalTransforms[index].LocalRotation;
            cardTransform.localScale = originalTransforms[index].LocalScale;
            activeDealVisuals[index].SetVisible(false);
        }

        dealSequence = DOTween.Sequence();

        for (int index = 0; index < activeDealVisuals.Length; index++)
        {
            int dealIndex = index;
            dealSequence.AppendCallback(() => ShowDealCard(dealIndex));

            if (index < 2)
            {
                dealSequence.Append(
                    activeDealTransforms[index]
                        .DOLocalMove(
                            originalTransforms[index].LocalPosition,
                            Mathf.Max(0f, dealDuration))
                        .SetEase(Ease.OutQuad));
            }
            else
            {
                Transform cardPoint = index == 2
                    ? dealerCardPoint
                    : playerCardPoint;
                dealSequence.Append(
                    CreatePrivateCardDealTween(
                        activeDealTransforms[index],
                        cardPoint));
            }

            if (index < activeDealVisuals.Length - 1 && dealInterval > 0f)
            {
                dealSequence.AppendInterval(dealInterval);
            }
        }

        dealSequence
            .OnComplete(CompleteDeal)
            .OnKill(HandleDealKilled);
        return true;
    }

    private Tween CreatePrivateCardDealTween(
        Transform cardRoot,
        Transform cardPoint)
    {
        Vector3 startPosition = cardRoot.position;
        Quaternion startRotation = cardRoot.rotation;

        return DOVirtual
            .Float(
                0f,
                1f,
                Mathf.Max(0f, dealDuration),
                progress =>
                {
                    if (cardRoot == null || cardPoint == null)
                    {
                        return;
                    }

                    cardRoot.localScale = Vector3.one;
                    cardRoot.SetPositionAndRotation(
                        Vector3.Lerp(
                            startPosition,
                            cardPoint.position,
                            progress),
                        Quaternion.Slerp(
                            startRotation,
                            cardPoint.rotation,
                            progress));
                })
            .SetEase(Ease.OutQuad);
    }

    private void ShowDealCard(int index)
    {
        if (activeDealVisuals == null ||
            index < 0 ||
            index >= activeDealVisuals.Length ||
            activeDealVisuals[index] == null)
        {
            FailDeal(ShouldNotifyUi());
            return;
        }

        activeDealVisuals[index].SetVisible(true);
    }

    private void CompleteDeal()
    {
        if (!HasValidDealVisuals())
        {
            FailDeal(ShouldNotifyUi());
            return;
        }

        Action completedCallback = ShouldNotifyUi()
            ? dealCompleted
            : null;
        RestoreFinalTransforms();
        RestorePrivateCardStartTransforms();
        ClearDealState();
        completedCallback?.Invoke();
    }

    private void HandleDealKilled()
    {
        if (dealSequence != null)
        {
            FailDeal(ShouldNotifyUi(), false);
        }
    }

    private void FailDeal(bool notifyFailure, bool killSequence = true)
    {
        if (dealSequence == null && activeDealVisuals == null)
        {
            return;
        }

        Action failedCallback = notifyFailure ? dealFailed : null;
        Sequence activeSequence = dealSequence;
        dealSequence = null;

        if (killSequence)
        {
            activeSequence?.Kill(false);
        }

        RestoreFinalTransforms();
        RestorePrivateCardStartTransforms();
        ClearDealState();
        failedCallback?.Invoke();
    }

    private bool HasValidDealVisuals()
    {
        if (activeDealVisuals == null ||
            activeDealTransforms == null ||
            originalTransforms == null ||
            activeDealVisuals.Length != activeDealTransforms.Length ||
            activeDealVisuals.Length != originalTransforms.Length)
        {
            return false;
        }

        for (int index = 0; index < activeDealVisuals.Length; index++)
        {
            if (activeDealVisuals[index] == null)
            {
                return false;
            }

            if (activeDealTransforms[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreFinalTransforms()
    {
        if (activeDealVisuals == null ||
            activeDealTransforms == null ||
            originalTransforms == null)
        {
            return;
        }

        int restoreCount = Mathf.Min(
            activeDealVisuals.Length,
            Mathf.Min(
                activeDealTransforms.Length,
                originalTransforms.Length));

        for (int index = 0; index < restoreCount; index++)
        {
            CardVisual cardVisual = activeDealVisuals[index];

            if (cardVisual == null)
            {
                continue;
            }

            Transform cardTransform = activeDealTransforms[index];

            if (cardTransform == null)
            {
                continue;
            }

            cardTransform.localPosition =
                originalTransforms[index].LocalPosition;
            cardTransform.localRotation =
                originalTransforms[index].LocalRotation;
            cardTransform.localScale = originalTransforms[index].LocalScale;
        }
    }

    private void ClearDealState()
    {
        dealSequence = null;
        activeDealVisuals = null;
        activeDealTransforms = null;
        originalTransforms = null;
        dealCompleted = null;
        dealFailed = null;
    }

    private void CompleteShowdownReveal()
    {
        if (playerCardVisual == null ||
            dealerCardVisual == null ||
            playerCardRoot == null ||
            dealerCardRoot == null ||
            playerShowdownCardPoint == null ||
            dealerShowdownCardPoint == null ||
            !HasValidPrivateCardHierarchy())
        {
            FailShowdownReveal(ShouldNotifyUi());
            return;
        }

        Action completedCallback = ShouldNotifyUi()
            ? showdownRevealCompleted
            : null;
        bool playerPlaced = PlacePrivateCardAtAnchor(
            playerCardRoot,
            playerCardVisual,
            playerShowdownCardPoint,
            showdownCardScale);
        bool dealerPlaced = PlacePrivateCardAtAnchor(
            dealerCardRoot,
            dealerCardVisual,
            dealerShowdownCardPoint,
            showdownCardScale);

        if (!playerPlaced || !dealerPlaced)
        {
            FailShowdownReveal(ShouldNotifyUi());
            return;
        }

        isPlayerCardFollowingNormalPoint = false;
        isDealerCardFollowingNormalPoint = false;
        ClearShowdownRevealState();
        playerCardVisual.SetVisible(true);
        dealerCardVisual.SetVisible(true);
        completedCallback?.Invoke();
    }

    private void HandleShowdownRevealKilled()
    {
        if (showdownRevealSequence != null)
        {
            FailShowdownReveal(ShouldNotifyUi(), false);
        }
    }

    private void FailShowdownReveal(
        bool notifyFailure,
        bool killSequence = true)
    {
        if (showdownRevealSequence == null &&
            showdownRevealCompleted == null &&
            showdownRevealFailed == null)
        {
            return;
        }

        Action failedCallback = notifyFailure
            ? showdownRevealFailed
            : null;
        Sequence activeSequence = showdownRevealSequence;
        showdownRevealSequence = null;

        if (killSequence)
        {
            activeSequence?.Kill(false);
        }

        RestorePrivateCardStartTransforms();
        ClearShowdownRevealState();
        failedCallback?.Invoke();
    }

    private void SyncPrivateCardAtNormalPoint(
        Transform cardRoot,
        CardVisual cardVisual,
        Transform cardPoint,
        Card card,
        Vector3 visualScale,
        out bool followEnabled)
    {
        followEnabled = PlacePrivateCardAtAnchor(
            cardRoot,
            cardVisual,
            cardPoint,
            visualScale);

        if (!followEnabled)
        {
            cardVisual?.Clear();
            return;
        }

        SetCard(cardVisual, card);
    }

    private bool PlacePrivateCardAtAnchor(
        Transform cardRoot,
        CardVisual cardVisual,
        Transform cardPoint,
        Vector3 visualScale)
    {
        if (cardRoot == null ||
            cardVisual == null ||
            cardPoint == null ||
            cardVisual.transform.parent != cardRoot)
        {
            return false;
        }

        cardRoot.localScale = Vector3.one;
        cardRoot.SetPositionAndRotation(
            cardPoint.position,
            cardPoint.rotation);
        Transform visualTransform = cardVisual.transform;
        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = visualScale;

        return true;
    }

    private void RestorePrivateCardStartTransforms()
    {
        isPlayerCardFollowingNormalPoint = PlacePrivateCardAtAnchor(
            playerCardRoot,
            playerCardVisual,
            playerCardPoint,
            playerNormalCardScale);
        isDealerCardFollowingNormalPoint = PlacePrivateCardAtAnchor(
            dealerCardRoot,
            dealerCardVisual,
            dealerCardPoint,
            dealerNormalCardScale);
    }

    private bool HasValidPrivateCardHierarchy()
    {
        return dealerCardRoot != null &&
               playerCardRoot != null &&
               dealerCardVisual != null &&
               playerCardVisual != null &&
               dealerCardVisual.transform.parent == dealerCardRoot &&
               playerCardVisual.transform.parent == playerCardRoot;
    }

    private void StopDealerNormalFollow()
    {
        FollowPrivateCardRoot(dealerCardRoot, dealerCardPoint);
        isDealerCardFollowingNormalPoint = false;
    }

    private void StopPlayerNormalFollow()
    {
        FollowPrivateCardRoot(playerCardRoot, playerCardPoint);
        isPlayerCardFollowingNormalPoint = false;
    }

    private static void FollowPrivateCardRoot(
        Transform cardRoot,
        Transform cardPoint)
    {
        if (cardRoot == null || cardPoint == null)
        {
            return;
        }

        cardRoot.localScale = Vector3.one;
        cardRoot.SetPositionAndRotation(
            cardPoint.position,
            cardPoint.rotation);
    }

    private void ClearShowdownRevealState()
    {
        showdownRevealSequence = null;
        showdownRevealCompleted = null;
        showdownRevealFailed = null;
    }

    private void LateUpdate()
    {
        if (dealerCardRoot != null)
        {
            dealerCardRoot.localScale = Vector3.one;

            if (isDealerCardFollowingNormalPoint)
            {
                FollowPrivateCardRoot(
                    dealerCardRoot,
                    dealerCardPoint);
            }
        }

        if (playerCardRoot != null)
        {
            playerCardRoot.localScale = Vector3.one;

            if (isPlayerCardFollowingNormalPoint)
            {
                FollowPrivateCardRoot(
                    playerCardRoot,
                    playerCardPoint);
            }
        }
    }

    private void OnDisable()
    {
        FailDeal(ShouldNotifyUi());
        FailShowdownReveal(ShouldNotifyUi());
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private bool ShouldNotifyUi()
    {
        return !isApplicationQuitting &&
               Application.isPlaying &&
               gameObject.scene.IsValid() &&
               gameObject.scene.isLoaded;
    }

    private static void SetCard(CardVisual cardVisual, Card card)
    {
        if (cardVisual != null)
        {
            cardVisual.SetCard(card);
        }
    }
}
