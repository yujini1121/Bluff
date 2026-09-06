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
    [SerializeField] private Transform dealerCardPoint;
    [SerializeField] private Transform dealerShowdownCardPoint;

    [Header("Player Private Card")]
    [FormerlySerializedAs("playerRevealCardVisual")]
    [SerializeField] private CardVisual playerCardVisual;
    [SerializeField] private Transform playerCardPoint;
    [SerializeField] private Transform playerShowdownCardPoint;
    [FormerlySerializedAs("playerRevealDuration")]
    [SerializeField, Min(0f)] private float showdownRevealDuration = 0.3f;

    [Header("Deal")]
    [SerializeField] private Transform cardDealPoint;
    [SerializeField, Min(0f)] private float dealDuration = 0.2f;
    [SerializeField, Min(0f)] private float dealInterval = 0.05f;

    private GameState gameState;
    private Sequence dealSequence;
    private CardVisual[] activeDealVisuals;
    private CardTransformState[] originalTransforms;
    private Action dealCompleted;
    private Action dealFailed;
    private Sequence showdownRevealSequence;
    private Action showdownRevealCompleted;
    private Action showdownRevealFailed;
    private Vector3 dealerCardLocalScale;
    private Vector3 playerCardLocalScale;
    private bool hasPrivateCardScales;
    private bool isApplicationQuitting;

    private struct CardTransformState
    {
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    public void Initialize(GameState state)
    {
        gameState = state;
        CachePrivateCardScales();
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
        Transform playerTransform = playerCardVisual.transform;
        Transform dealerTransform = dealerCardVisual.transform;
        ReparentForShowdownTween(
            playerTransform,
            playerShowdownCardPoint);
        ReparentForShowdownTween(
            dealerTransform,
            dealerShowdownCardPoint);
        showdownRevealCompleted = onCompleted;
        showdownRevealFailed = onFailed;

        showdownRevealSequence = DOTween.Sequence();
        showdownRevealSequence.Append(
            playerTransform
                .DOMove(
                    playerShowdownCardPoint.position,
                    Mathf.Max(0f, showdownRevealDuration))
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            playerTransform
                .DORotateQuaternion(
                    playerShowdownCardPoint.rotation,
                    Mathf.Max(0f, showdownRevealDuration))
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            dealerTransform
                .DOMove(
                    dealerShowdownCardPoint.position,
                    Mathf.Max(0f, showdownRevealDuration))
                .SetEase(Ease.OutQuad));
        showdownRevealSequence.Join(
            dealerTransform
                .DORotateQuaternion(
                    dealerShowdownCardPoint.rotation,
                    Mathf.Max(0f, showdownRevealDuration))
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

        if (gameState?.PlayerCard == null ||
            !PlaceCardAtPoint(
                playerCardVisual,
                playerShowdownCardPoint,
                playerCardLocalScale))
        {
            playerCardVisual?.Clear();
        }
        else
        {
            SetCard(playerCardVisual, gameState.PlayerCard);
        }

        if (gameState?.DealerCard == null ||
            !PlaceCardAtPoint(
                dealerCardVisual,
                dealerShowdownCardPoint,
                dealerCardLocalScale))
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

        SyncPrivateCardAtNormalPoint(
            playerCardVisual,
            playerCardPoint,
            gameState?.PlayerCard,
            playerCardLocalScale);
        SyncPrivateCardAtNormalPoint(
            dealerCardVisual,
            dealerCardPoint,
            gameState?.DealerCard,
            dealerCardLocalScale);
    }

    public bool TryPlayDeal(Action onCompleted, Action onFailed)
    {
        if (gameState == null ||
            cardDealPoint == null ||
            communityCardVisual1 == null ||
            communityCardVisual2 == null ||
            dealerCardVisual == null ||
            playerCardVisual == null ||
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
        activeDealVisuals = new[]
        {
            communityCardVisual1,
            communityCardVisual2,
            dealerCardVisual,
            playerCardVisual
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
            Transform cardTransform = activeDealVisuals[index].transform;
            Transform parent = cardTransform.parent;
            originalTransforms[index] = new CardTransformState
            {
                Parent = parent,
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
            dealSequence.Append(
                activeDealVisuals[index].transform
                    .DOLocalMove(
                        originalTransforms[index].LocalPosition,
                        Mathf.Max(0f, dealDuration))
                    .SetEase(Ease.OutQuad));

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
        ClearDealState();
        failedCallback?.Invoke();
    }

    private bool HasValidDealVisuals()
    {
        if (activeDealVisuals == null ||
            originalTransforms == null ||
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
        }

        return true;
    }

    private void RestoreFinalTransforms()
    {
        if (activeDealVisuals == null || originalTransforms == null)
        {
            return;
        }

        int restoreCount = Mathf.Min(
            activeDealVisuals.Length,
            originalTransforms.Length);

        for (int index = 0; index < restoreCount; index++)
        {
            CardVisual cardVisual = activeDealVisuals[index];

            if (cardVisual == null)
            {
                continue;
            }

            Transform cardTransform = cardVisual.transform;
            Transform originalParent = originalTransforms[index].Parent;
            cardTransform.SetParent(
                originalParent != null ? originalParent : null,
                false);
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
        originalTransforms = null;
        dealCompleted = null;
        dealFailed = null;
    }

    private void CompleteShowdownReveal()
    {
        if (playerCardVisual == null ||
            dealerCardVisual == null ||
            playerShowdownCardPoint == null ||
            dealerShowdownCardPoint == null)
        {
            FailShowdownReveal(ShouldNotifyUi());
            return;
        }

        Action completedCallback = ShouldNotifyUi()
            ? showdownRevealCompleted
            : null;
        PlaceCardAtPoint(
            playerCardVisual,
            playerShowdownCardPoint,
            playerCardLocalScale);
        PlaceCardAtPoint(
            dealerCardVisual,
            dealerShowdownCardPoint,
            dealerCardLocalScale);
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

    private void CachePrivateCardScales()
    {
        if (hasPrivateCardScales ||
            dealerCardVisual == null ||
            playerCardVisual == null)
        {
            return;
        }

        dealerCardLocalScale = dealerCardVisual.transform.localScale;
        playerCardLocalScale = playerCardVisual.transform.localScale;
        hasPrivateCardScales = true;
    }

    private void SyncPrivateCardAtNormalPoint(
        CardVisual cardVisual,
        Transform cardPoint,
        Card card,
        Vector3 localScale)
    {
        if (!PlaceCardAtPoint(cardVisual, cardPoint, localScale))
        {
            cardVisual?.Clear();
            return;
        }

        SetCard(cardVisual, card);
    }

    private bool PlaceCardAtPoint(
        CardVisual cardVisual,
        Transform cardPoint,
        Vector3 localScale)
    {
        if (cardVisual == null || cardPoint == null)
        {
            return false;
        }

        CachePrivateCardScales();
        Transform cardTransform = cardVisual.transform;
        cardTransform.SetParent(cardPoint, false);
        cardTransform.localPosition = Vector3.zero;
        cardTransform.localRotation = Quaternion.identity;

        if (hasPrivateCardScales)
        {
            cardTransform.localScale = localScale;
        }

        return true;
    }

    private void RestorePrivateCardStartTransforms()
    {
        PlaceCardAtPoint(
            playerCardVisual,
            playerCardPoint,
            playerCardLocalScale);
        PlaceCardAtPoint(
            dealerCardVisual,
            dealerCardPoint,
            dealerCardLocalScale);
    }

    private static void ReparentForShowdownTween(
        Transform cardTransform,
        Transform showdownPoint)
    {
        if (cardTransform == null || showdownPoint == null)
        {
            return;
        }

        Transform stableParent = showdownPoint.parent;
        cardTransform.SetParent(stableParent, true);
    }

    private void ClearShowdownRevealState()
    {
        showdownRevealSequence = null;
        showdownRevealCompleted = null;
        showdownRevealFailed = null;
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

    public void RefreshAllCards()
    {
        playerCardVisual?.SetCard(gameState?.PlayerCard);
        dealerCardVisual?.SetCard(gameState?.DealerCard);
        communityCardVisual1?.SetCard(gameState?.CommunityCard1);
        communityCardVisual2?.SetCard(gameState?.CommunityCard2);
    }
}
