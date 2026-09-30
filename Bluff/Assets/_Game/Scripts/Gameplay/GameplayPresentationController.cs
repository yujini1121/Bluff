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

    private bool isChipPocketAnimating;
    private ChipPocketPresentation activeChipPocket;

    private bool isDefyAnimating;
    private DefyPresentation activeDefy;

    private bool isPrizmAnimating;
    private PrizmPresentation activePrizm;

    private bool isRefreshItemAnimating;
    private RefreshCardPresentation activeRefreshItem;
    private bool refreshCardStartedForItem;

    public event Action<GameplayPresentationCue> CueRaised;

    public bool IsChipAnimating => isChipAnimating;
    public bool IsCardAnimating => isCardAnimating;
    public bool IsChipPocketAnimating => isChipPocketAnimating;
    public bool IsDefyAnimating => isDefyAnimating;
    public bool IsPrizmAnimating => isPrizmAnimating;
    public bool IsRefreshItemAnimating => isRefreshItemAnimating;
    public bool IsBusy => isChipAnimating || isCardAnimating ||
                          isChipPocketAnimating || isDefyAnimating ||
                          isPrizmAnimating || isRefreshItemAnimating;

    public void Initialize(GameState gameState, Action presentationChanged)
    {
        this.gameState = gameState;
        this.presentationChanged = presentationChanged;
        cardVisualController?.Initialize(gameState, RaiseCue);
        chipVisualController?.Initialize(gameState);
    }

    public void PlayItemUseCue(ItemType type)
    {
        switch (type)
        {
            case ItemType.refreshCard:
                RaiseCue(GameplayPresentationCue.Item_RefreshCard);
                break;
            case ItemType.prizmChip:
                RaiseCue(GameplayPresentationCue.Item_PrizmChip);
                break;
            case ItemType.chipPocket:
                RaiseCue(GameplayPresentationCue.Item_ChipsPocket);
                break;
            case ItemType.defy:
                RaiseCue(GameplayPresentationCue.Item_Defy);
                break;
        }
    }

    public void PlayClickCue()
    {
        RaiseCue(GameplayPresentationCue.Click);
    }

    public void PlayChipPocket(TurnOwner owner, GameObject item)
    {
        ChipPocketPresentation pocket = item != null
            ? item.GetComponentInChildren<ChipPocketPresentation>()
            : null;
        if (isChipPocketAnimating || chipVisualController == null || item == null ||
            !chipVisualController.TryGetChipPocketTargets(owner, out Vector3[] targets) ||
            pocket == null)
        {
            chipVisualController?.RefreshChips();
            return;
        }

        isChipPocketAnimating = true;
        activeChipPocket = pocket;
        if (!pocket.TryPlay(targets, RefreshChips, OnChipPocketFinished))
        {
            isChipPocketAnimating = false;
            activeChipPocket = null;
            chipVisualController.RefreshChips();
        }
    }

    private void OnChipPocketFinished()
    {
        isChipPocketAnimating = false;
        activeChipPocket = null;
        if (isActiveAndEnabled)
        {
            NotifyChanged();
        }
    }

    private void CancelChipPocketPresentation()
    {
        activeChipPocket?.Cancel();
    }

    public void PlayDefy(TurnOwner owner, GameObject item)
    {
        DefyPresentation defy = item != null
            ? item.GetComponentInChildren<DefyPresentation>()
            : null;
        if (!isActiveAndEnabled || isDefyAnimating ||
            chipVisualController == null || defy == null ||
            !chipVisualController.TryGetDefyTarget(owner, out Vector3 target))
        {
            SyncDefyChips();
            return;
        }

        isDefyAnimating = true;
        activeDefy = defy;
        if (!defy.TryPlay(target, SyncDefyChips, OnDefyFinished))
        {
            isDefyAnimating = false;
            activeDefy = null;
            SyncDefyChips();
        }
    }

    private void SyncDefyChips()
    {
        if (chipVisualController != null &&
            (!Application.isPlaying || gameObject.scene.isLoaded))
        {
            chipVisualController.RefreshChips();
        }
    }

    private void OnDefyFinished()
    {
        isDefyAnimating = false;
        activeDefy = null;
        if (isActiveAndEnabled &&
            (!Application.isPlaying || gameObject.scene.isLoaded))
        {
            NotifyChanged();
        }
    }

    private void CancelDefyPresentation()
    {
        activeDefy?.Cancel();
    }

    public void PlayPrizm(TurnOwner owner, GameObject item)
    {
        PrizmPresentation prizm = item != null
            ? item.GetComponentInChildren<PrizmPresentation>()
            : null;
        if (!isActiveAndEnabled || isPrizmAnimating ||
            chipVisualController == null || prizm == null ||
            !chipVisualController.TryGetPrizmTarget(owner, out Vector3 target))
        {
            return;
        }

        isPrizmAnimating = true;
        activePrizm = prizm;
        if (!prizm.TryPlay(target, OnPrizmFinished))
        {
            isPrizmAnimating = false;
            activePrizm = null;
        }
    }

    private void OnPrizmFinished()
    {
        isPrizmAnimating = false;
        activePrizm = null;
        if (isActiveAndEnabled &&
            (!Application.isPlaying || gameObject.scene.isLoaded))
        {
            NotifyChanged();
        }
    }

    private void CancelPrizmPresentation()
    {
        activePrizm?.Cancel();
        activePrizm = null;
        isPrizmAnimating = false;
    }

    public void PlayRefreshItem(TurnOwner owner, GameObject item)
    {
        RefreshCardPresentation refreshItem = item != null
            ? item.GetComponentInChildren<RefreshCardPresentation>()
            : null;
        if (refreshItem == null)
        {
            PlayRefresh();
            return;
        }

        isRefreshItemAnimating = true;
        activeRefreshItem = refreshItem;
        refreshCardStartedForItem = false;
        if (!refreshItem.TryPlay(OnRefreshItemActivated, OnRefreshItemFinished))
        {
            isRefreshItemAnimating = false;
            activeRefreshItem = null;
            refreshCardStartedForItem = false;
            PlayRefresh();
        }
    }

    private void OnRefreshItemActivated()
    {
        if (activeRefreshItem == null) return;
        if (!isActiveAndEnabled ||
            (Application.isPlaying && !gameObject.scene.isLoaded))
        {
            activeRefreshItem.Cancel();
            return;
        }

        refreshCardStartedForItem = true;
        StartCardRefresh();
        if (!isCardAnimating)
        {
            StartRefreshItemConsume();
        }
    }

    public void PlayRefresh()
    {
        // 시작 연출 중에는 기존 딜 완료/실패 경로가 최신 카드를 동기화
        if (IsBusy)
        {
            return;
        }

        StartCardRefresh();
    }

    private void StartCardRefresh()
    {
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

    private void OnRefreshCompleted()
    {
        isCardAnimating = false;
        if (activeRefreshItem != null) StartRefreshItemConsume();
        else NotifyChanged();
    }

    private void OnRefreshFailed()
    {
        cardVisualController?.RefreshCards();
        isCardAnimating = false;
        if (activeRefreshItem != null) StartRefreshItemConsume();
        else NotifyChanged();
    }

    private void StartRefreshItemConsume()
    {
        if (activeRefreshItem == null) return;

        activeRefreshItem.Consume();
    }

    private void OnRefreshItemFinished()
    {
        if (activeRefreshItem == null) return;

        bool startCardRefresh = !refreshCardStartedForItem;
        isRefreshItemAnimating = false;
        activeRefreshItem = null;
        refreshCardStartedForItem = false;
        if (!isActiveAndEnabled ||
            (Application.isPlaying && !gameObject.scene.isLoaded))
        {
            isCardAnimating = false;
            return;
        }

        if (startCardRefresh)
        {
            PlayRefresh();
        }
        else
        {
            NotifyChanged();
        }
    }

    private void CancelRefreshPresentation()
    {
        RefreshCardPresentation refreshItem = activeRefreshItem;
        activeRefreshItem = null;
        isRefreshItemAnimating = false;
        refreshCardStartedForItem = false;
        isCardAnimating = false;
        refreshItem?.Cancel();
        if (refreshItem != null && cardVisualController != null &&
            cardVisualController.gameObject.scene.isLoaded)
        {
            cardVisualController.RefreshCards();
        }
    }

    private void OnDisable()
    {
        CancelPrizmPresentation();
        CancelDefyPresentation();
        CancelRefreshPresentation();
        CancelChipPocketPresentation();
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
            RaiseCue(GameplayPresentationCue.ChipBet);
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
            RaiseCue(GameplayPresentationCue.ChipBet);
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
        if (dealerAnimationController != null)
        {
            dealerAnimationController.StopThink();
        }
    }

    public void RefreshCards()
    {
        cardVisualController?.RefreshCards();
    }

    public void RefreshChips()
    {
        if (chipVisualController != null)
        {
            chipVisualController.RefreshChips();
        }
    }

    public void RefreshChipsIfChanged(
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBefore)
    {
        if (chipVisualController == null || isChipPocketAnimating ||
            isDefyAnimating ||
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

    private void RaiseCue(GameplayPresentationCue cue)
    {
        CueRaised?.Invoke(cue);
    }
}
