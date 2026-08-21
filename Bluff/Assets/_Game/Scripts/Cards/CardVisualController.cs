using System;
using DG.Tweening;
using UnityEngine;

public sealed class CardVisualController : MonoBehaviour
{
    [Header("Community Card")]
    [SerializeField] private CardVisual communityCardVisual1;
    [SerializeField] private CardVisual communityCardVisual2;

    [Header("Dealer Card")]
    [SerializeField] private CardVisual dealerCardVisual;

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
        RefreshCards();
    }

    public void RefreshCards()
    {
        SetCard(communityCardVisual1, gameState?.CommunityCard1);
        SetCard(communityCardVisual2, gameState?.CommunityCard2);
        SetCard(dealerCardVisual, gameState?.DealerCard);
    }

    public bool TryPlayDeal(Action onCompleted, Action onFailed)
    {
        if (gameState == null ||
            cardDealPoint == null ||
            communityCardVisual1 == null ||
            communityCardVisual2 == null ||
            dealerCardVisual == null ||
            gameState.CommunityCard1 == null ||
            gameState.CommunityCard2 == null ||
            gameState.DealerCard == null ||
            dealSequence != null ||
            !isActiveAndEnabled ||
            onCompleted == null ||
            onFailed == null)
        {
            return false;
        }

        activeDealVisuals = new[]
        {
            communityCardVisual1,
            communityCardVisual2,
            dealerCardVisual
        };
        originalTransforms = new CardTransformState[
            activeDealVisuals.Length];
        dealCompleted = onCompleted;
        dealFailed = onFailed;

        SetCard(communityCardVisual1, gameState.CommunityCard1);
        SetCard(communityCardVisual2, gameState.CommunityCard2);
        SetCard(dealerCardVisual, gameState.DealerCard);

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

    private void OnDisable()
    {
        FailDeal(ShouldNotifyUi());
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
