using System;
using UnityEngine;

public sealed class GameplayPresentationController : MonoBehaviour
{
    [SerializeField] private CardVisualController cardVisualController;
    [SerializeField] private ChipVisualController chipVisualController;
    [SerializeField] private DealerAnimationController dealerAnimationController;

    private GameState gameState;
    private Action presentationChanged;
    private bool isChipAnimating;
    private bool isCardAnimating;
    private bool isFoldRevealComplete;

    public bool IsChipAnimating => isChipAnimating;
    public bool IsCardAnimating => isCardAnimating;
    public bool IsBusy => isChipAnimating || isCardAnimating;

    public void Initialize(GameState gameState, Action presentationChanged)
    {
        this.gameState = gameState;
        this.presentationChanged = presentationChanged;
        cardVisualController?.Initialize(gameState);
        chipVisualController?.Initialize(gameState);
    }

    public void PlayRefresh()
    {
        // 시작 연출 중에는 기존 딜 완료/실패 경로가 최신 카드를 동기화
        if (IsBusy)
        {
            return;
        }

        isCardAnimating = true;

        bool refreshStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayRefresh(
                OnRefreshCompleted,
                OnRefreshFailed);

        if (!refreshStarted)
        {
            isCardAnimating = false;
            cardVisualController?.RefreshCards();
        }
    }

    public void PlayRoundStart()
    {
        if (!TryStartRoundAnte())
        {
            chipVisualController?.RefreshChips();
            TryStartCardDeal();
        }
    }

    public bool PlayPlayerBet(int chipCount)
    {
        if (chipVisualController == null)
        {
            return false;
        }

        isChipAnimating = true;

        if (chipVisualController.TryBeginPlayerBet(
                chipCount,
                OnPlayerBetChipsMoved,
                OnPlayerBetChipsMoveFailed))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    public bool PlayPlayerCollect(int chipCount)
    {
        if (chipVisualController == null)
        {
            return false;
        }

        isChipAnimating = true;

        if (chipVisualController.TryBeginPlayerCollect(
                chipCount,
                OnPlayerCollectCompleted,
                OnPlayerCollectFailed))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    public bool PlayDrawSettlement()
    {
        if (chipVisualController == null)
        {
            return false;
        }

        isChipAnimating = true;

        if (chipVisualController.TryBeginDrawSettlement(
                OnDrawSettlementCompleted,
                OnDrawSettlementFailed))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    public void PlayFold(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount,
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBefore,
        Action onRevealCompleted)
    {
        isFoldRevealComplete = false;

        if (foldedBy == TurnOwner.Dealer)
        {
            isChipAnimating = true;

            if (dealerAnimationController != null &&
                dealerAnimationController.TryPlayFold(
                    () => ContinueFoldAfterDealerAnimation(
                        foldedBy,
                        potChipCount,
                        penaltyChipCount,
                        onRevealCompleted),
                    () => ContinueFoldAfterDealerAnimation(
                        foldedBy,
                        potChipCount,
                        penaltyChipCount,
                        onRevealCompleted)))
            {
                return;
            }

            isChipAnimating = false;
        }

        StartFoldSettlementOrRecover(
            foldedBy,
            potChipCount,
            penaltyChipCount,
            playerChipsBefore,
            dealerChipsBefore,
            potBefore,
            onRevealCompleted);
    }

    public bool PlayDealerBet(int chipCount, bool useAllInAnimation)
    {
        if (chipVisualController == null ||
            dealerAnimationController == null ||
            !chipVisualController.TryBeginDealerBet(
                chipCount,
                out GameObject[] chips,
                out Vector3[] betAreaTargetPositions))
        {
            return false;
        }

        isChipAnimating = true;
        bool animationStarted = useAllInAnimation
            ? dealerAnimationController.TryPlayAllInChips(
                chips,
                betAreaTargetPositions,
                OnDealerBetChipsMoved,
                OnDealerBetChipsMoveFailed)
            : dealerAnimationController.TryPlayCallChips(
                chips,
                betAreaTargetPositions,
                OnDealerBetChipsMoved,
                OnDealerBetChipsMoveFailed);

        if (animationStarted)
        {
            return true;
        }

        isChipAnimating = false;
        chipVisualController.CancelDealerBet();
        return false;
    }

    public bool PlayDealerCollect(int chipCount)
    {
        if (chipVisualController == null ||
            dealerAnimationController == null ||
            !chipVisualController.TryBeginDealerCollect(
                chipCount,
                out GameObject[] chips,
                out Vector3[] dealerTargetPositions))
        {
            return false;
        }

        isChipAnimating = true;

        if (dealerAnimationController.TryPlayCollectChips(
                chips,
                dealerTargetPositions,
                OnDealerCollectCompleted,
                OnDealerCollectFailed))
        {
            return true;
        }

        isChipAnimating = false;
        chipVisualController.CancelDealerCollect();
        return false;
    }

    public void PlayShowdownReveal(Action onCompleted)
    {
        isCardAnimating = true;

        bool revealStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayShowdownReveal(
                onCompleted,
                () =>
                {
                    cardVisualController?.ShowShowdownCardsImmediately();
                    onCompleted?.Invoke();
                });

        if (!revealStarted)
        {
            cardVisualController?.ShowShowdownCardsImmediately();
            onCompleted?.Invoke();
        }
    }

    public bool TryPlayDealerThink()
    {
        return dealerAnimationController != null &&
               dealerAnimationController.TryPlayThink();
    }

    public void StopDealerThink()
    {
        dealerAnimationController?.StopThink();
    }

    public void RefreshCards()
    {
        cardVisualController?.RefreshCards();
    }

    public void RefreshChips()
    {
        chipVisualController?.RefreshChips();
    }

    public void RefreshChipsIfChanged(
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBefore)
    {
        if (chipVisualController == null ||
            (playerChipsBefore == gameState.PlayerChips.Count &&
             dealerChipsBefore == gameState.DealerChips.Count &&
             potBefore == gameState.Pot.Amount))
        {
            return;
        }

        chipVisualController.RefreshChips();
    }

    public void RecoverShowdown()
    {
        isCardAnimating = false;
        cardVisualController?.ShowShowdownCardsImmediately();
        chipVisualController?.RefreshChips();
    }

    public void FinishCardPresentation()
    {
        isCardAnimating = false;
    }

    private void OnRefreshCompleted()
    {
        isCardAnimating = false;
        NotifyChanged();
    }

    private void OnRefreshFailed()
    {
        cardVisualController?.RefreshCards();
        isCardAnimating = false;
        NotifyChanged();
    }

    private bool TryStartRoundAnte()
    {
        if (chipVisualController == null)
        {
            return false;
        }

        isChipAnimating = true;

        if (chipVisualController.TryBeginRoundAnte(
                OnRoundAnteChipsMoved,
                OnRoundAnteChipsMoveFailed))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    private void OnRoundAnteChipsMoved(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompleteRoundAnte(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelRoundAnte();
            chipVisualController.RefreshChips();
        }

        TryStartCardDeal();
        NotifyChanged();
    }

    private void OnRoundAnteChipsMoveFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelRoundAnte();
            chipVisualController.RefreshChips();
        }

        TryStartCardDeal();
        NotifyChanged();
    }

    private void TryStartCardDeal()
    {
        if (cardVisualController == null)
        {
            isCardAnimating = false;
            return;
        }

        isCardAnimating = true;

        if (cardVisualController.TryPlayDeal(
                OnCardDealCompleted,
                OnCardDealFailed))
        {
            return;
        }

        isCardAnimating = false;
        cardVisualController.RefreshCards();
    }

    private void OnCardDealCompleted()
    {
        isCardAnimating = false;
        cardVisualController?.RefreshCards();
        NotifyChanged();
    }

    private void OnCardDealFailed()
    {
        isCardAnimating = false;
        cardVisualController?.RefreshCards();
        NotifyChanged();
    }

    private void OnPlayerBetChipsMoved(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompletePlayerBet(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelPlayerBet();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnPlayerBetChipsMoveFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelPlayerBet();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnPlayerCollectCompleted(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompletePlayerCollect(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelPlayerCollect();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnPlayerCollectFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelPlayerCollect();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnDrawSettlementCompleted(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompleteDrawSettlement(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelDrawSettlement();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnDrawSettlementFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDrawSettlement();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void ContinueFoldAfterDealerAnimation(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount,
        Action onRevealCompleted)
    {
        isChipAnimating = false;

        if (TryStartFoldSettlement(
                foldedBy,
                potChipCount,
                penaltyChipCount,
                onRevealCompleted))
        {
            return;
        }

        chipVisualController?.RefreshChips();
        PlayFoldReveal(onRevealCompleted);
    }

    private void StartFoldSettlementOrRecover(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount,
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBefore,
        Action onRevealCompleted)
    {
        if (TryStartFoldSettlement(
                foldedBy,
                potChipCount,
                penaltyChipCount,
                onRevealCompleted))
        {
            return;
        }

        RefreshChipsIfChanged(
            playerChipsBefore,
            dealerChipsBefore,
            potBefore);
        PlayFoldReveal(onRevealCompleted);
    }

    private bool TryStartFoldSettlement(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount,
        Action onRevealCompleted)
    {
        if (chipVisualController == null)
        {
            return false;
        }

        isChipAnimating = true;

        if (chipVisualController.TryBeginFoldSettlement(
                foldedBy,
                potChipCount,
                penaltyChipCount,
                () => OnFoldSettlementCompleted(onRevealCompleted),
                () => OnFoldSettlementFailed(onRevealCompleted)))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    private void OnFoldSettlementCompleted(Action onRevealCompleted)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompleteFoldSettlement();

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelFoldSettlement();
            chipVisualController.RefreshChips();
        }

        PlayFoldReveal(onRevealCompleted);
    }

    private void OnFoldSettlementFailed(Action onRevealCompleted)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelFoldSettlement();
            chipVisualController.RefreshChips();
        }

        PlayFoldReveal(onRevealCompleted);
    }

    private void PlayFoldReveal(Action onRevealCompleted)
    {
        if (gameState == null ||
            gameState.RoundEndReason != RoundEndReason.Fold ||
            isFoldRevealComplete ||
            isCardAnimating)
        {
            return;
        }

        isCardAnimating = true;

        bool revealStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayShowdownReveal(
                () => CompleteFoldReveal(onRevealCompleted),
                () =>
                {
                    cardVisualController?.ShowShowdownCardsImmediately();
                    CompleteFoldReveal(onRevealCompleted);
                });

        if (!revealStarted)
        {
            cardVisualController?.ShowShowdownCardsImmediately();
            CompleteFoldReveal(onRevealCompleted);
            return;
        }

        NotifyChanged();
    }

    private void CompleteFoldReveal(Action onRevealCompleted)
    {
        isCardAnimating = false;
        isFoldRevealComplete = true;
        onRevealCompleted?.Invoke();
    }

    private void OnDealerBetChipsMoved(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompleteDealerBet(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelDealerBet();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnDealerBetChipsMoveFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDealerBet();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnDealerCollectCompleted(GameObject[] chips)
    {
        bool moveCompleted =
            chipVisualController != null &&
            chipVisualController.CompleteDealerCollect(chips);

        isChipAnimating = false;

        if (!moveCompleted && chipVisualController != null)
        {
            chipVisualController.CancelDealerCollect();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void OnDealerCollectFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDealerCollect();
            chipVisualController.RefreshChips();
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        presentationChanged?.Invoke();
    }
}
