using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameplayController : MonoBehaviour
{
    [Header("게임 시작 설정")]
    [SerializeField, Min(0), InspectorName("플레이어 시작 칩")]
    private int playerStartingChips = 10;
    [SerializeField, Min(0), InspectorName("딜러 시작 칩")]
    private int dealerStartingChips = 10;
    [SerializeField, InspectorName("라운드 선공")]
    private TurnOwner firstTurn = TurnOwner.Player;
    [SerializeField, Min(1), InspectorName("최대 로그 줄 수")]
    private int maxLogLines = 4;
    [SerializeField] private ItemSystem itemSystem;

    [Header("UI")]
    [SerializeField] private GameplayView gameplayView;

    [Header("딜러 생각 시간 설정")]
    [SerializeField, Min(0f)]
    private float minDealerThinkDelay = 0.6f;
    [SerializeField, Min(0f)]
    private float maxDealerThinkDelay = 1.5f;

    [Header("3D 카드 표시")]
    [SerializeField] private CardVisualController cardVisualController;

    [Header("3D 칩 표시")]
    [SerializeField] private ChipVisualController chipVisualController;
    [SerializeField] private DealerAnimationController dealerAnimationController;

    [SerializeField, Min(0f), InspectorName("결과 표시 후 정산 대기 시간")]
    private float showdownResultDelay = 1f;

    private readonly List<string> logs = new List<string>();
    private GameState gameState;
    private ItemSystem subscribedItemSystem;
    private TurnOwner nextRoundFirstTurn;
    private DealerTurnController dealerTurn;
    private Coroutine dealerActionCoroutine;
    private Coroutine showdownPresentationCoroutine;
    private HandRank playerHandRank;
    private HandRank dealerHandRank;
    private RoundWinner roundWinner;
    private bool debugPanelOpen;
    private bool isActionProcessing;
    private bool isChipAnimating;
    private bool isCardAnimating;
    private bool isShowdownResultVisible;
    private bool isFoldResultVisible;
    private bool isShuttingDown;
    private bool isRestarting;
    private bool recoverShowdownPresentationOnEnable;
    private bool recoverFoldPresentationOnEnable;
    private int selectedRaiseAmount = 1;

    public int CurrentPlayerChipCount =>
        gameState?.PlayerChips.Count ?? 0;
    public int CurrentDealerChipCount =>
        gameState?.DealerChips.Count ?? 0;
    public int CurrentDeckRemainingCount =>
        gameState?.Deck.RemainingCount ?? 0;
    public GameMode CurrentGameMode =>
        gameState?.GameMode ?? GameModeSelection.SelectedMode;
    public int CurrentRound => gameState?.CurrentRound ?? 0;

    private void OnEnable()
    {
        isShuttingDown = false;
        SubscribeToItemSystemEvents();

        if (recoverFoldPresentationOnEnable &&
            gameState != null &&
            gameState.RoundEndReason == RoundEndReason.Fold &&
            (gameState.Phase == GamePhase.RoundEnd ||
             gameState.Phase == GamePhase.GameOver))
        {
            recoverFoldPresentationOnEnable = false;
            isCardAnimating = false;
            isFoldResultVisible = true;
            cardVisualController?.ShowShowdownCardsImmediately();
            chipVisualController?.RefreshChips();
            return;
        }

        if (!recoverShowdownPresentationOnEnable ||
            gameState == null ||
            gameState.RoundEndReason != RoundEndReason.Showdown ||
            roundWinner == RoundWinner.None ||
            (gameState.Phase != GamePhase.RoundEnd &&
             gameState.Phase != GamePhase.GameOver))
        {
            return;
        }

        recoverShowdownPresentationOnEnable = false;
        isCardAnimating = false;
        isShowdownResultVisible = true;
        cardVisualController?.ShowShowdownCardsImmediately();
        chipVisualController?.RefreshChips();
    }

    private void Awake()
    {
        if (gameplayView == null || !gameplayView.HasReferences())
        {
            Debug.LogError(
                "인디언 홀덤 UI 참조가 연결되지 않았습니다. " +
                "Inspector에서 UI 참조를 확인해주세요.",
                this);
            enabled = false;
            return;
        }

        gameplayView.Initialize();
        debugPanelOpen = false;
        CreateGame();
        RefreshView();
    }

    private void Start()
    {
        if (gameState != null && itemSystem != null)
        {
            StartRound();
        }
    }

    private void Update()
    {
        if (gameState == null)
        {
            return;
        }

        UpdateVisibleHandRanks();
        CancelInvalidDealerAction();
        TryScheduleDealerAction();
        RefreshView();
    }

    private void OnDisable()
    {
        UnsubscribeFromItemSystemEvents(unsubscribePlayerRequests: false);
        recoverFoldPresentationOnEnable =
            isCardAnimating &&
            gameState != null &&
            gameState.RoundEndReason == RoundEndReason.Fold &&
            !isFoldResultVisible;
        recoverShowdownPresentationOnEnable =
            isCardAnimating &&
            gameState != null &&
            gameState.RoundEndReason == RoundEndReason.Showdown &&
            roundWinner != RoundWinner.None;
        isShuttingDown = true;
        isActionProcessing = false;
        isCardAnimating = false;
        CancelDealerAction();

        if (showdownPresentationCoroutine != null)
        {
            StopCoroutine(showdownPresentationCoroutine);
            showdownPresentationCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromItemSystemEvents();
    }

    public void OnCallClicked()
    {
        if (IsPlayerShortAllInRequired())
        {
            RunPlayerBettingAction(
                "올인",
                () => gameState.TryAllIn(),
                playChipSfx: true);
            return;
        }

        RunPlayerBettingAction(
            "콜",
            () => gameState.TryCall(),
            playChipSfx: true);
    }

    public void OnFoldClicked()
    {
        RunPlayerBettingAction(
            "폴드",
            () => gameState.TryFold(),
            playChipSfx: false);
    }

    public void OnRaiseDecreaseClicked()
    {
        if (!CanSelectPlayerRaise(out int maxRaiseAmount))
        {
            return;
        }

        ClampRaiseAmount(maxRaiseAmount);
        selectedRaiseAmount = Mathf.Max(1, selectedRaiseAmount - 1);
        RefreshView();
    }

    public void OnRaiseIncreaseClicked()
    {
        if (!CanSelectPlayerRaise(out int maxRaiseAmount))
        {
            return;
        }

        ClampRaiseAmount(maxRaiseAmount);
        selectedRaiseAmount = Mathf.Min(maxRaiseAmount, selectedRaiseAmount + 1);
        RefreshView();
    }

    public void OnRaiseMaxClicked()
    {
        if (!CanSelectPlayerRaise(out int maxRaiseAmount))
        {
            return;
        }

        selectedRaiseAmount = maxRaiseAmount;
        RefreshView();
    }

    public void OnRaiseClicked()
    {
        if (!CanSelectPlayerRaise(out int maxRaiseAmount))
        {
            return;
        }

        ClampRaiseAmount(maxRaiseAmount);
        int raiseBy = selectedRaiseAmount;
        bool isAllIn = raiseBy == maxRaiseAmount;
        string actionName = isAllIn ? "올인" : $"레이즈 +{raiseBy}";

        RunPlayerBettingAction(
            actionName,
            () =>
            {
                bool succeeded = isAllIn
                    ? gameState.TryAllIn()
                    : gameState.TryRaise(raiseBy);

                if (succeeded)
                {
                    selectedRaiseAmount = 1;
                }

                return succeeded;
            },
            playChipSfx: true);
    }

    public void OnResolveShowdownClicked()
    {
        RunProgressAction(GamePhase.Showdown, ResolveShowdown);
    }

    public void OnNextRoundClicked()
    {
        RunProgressAction(GamePhase.RoundEnd, PrepareAndStartNextRound);
    }

    public void OnRestartClicked()
    {
        if (!CanRestartGame())
        {
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        int buildIndex = currentScene.buildIndex;

        if (!currentScene.IsValid() ||
            !currentScene.isLoaded ||
            buildIndex < 0 ||
            buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError(
                "현재 활성 Scene이 Build Settings에 등록되지 않아 " +
                "게임을 Restart할 수 없습니다.",
                this);
            return;
        }

        isRestarting = true;
        isActionProcessing = true;
        gameplayView.SetRestartEnabled(false);
        CancelDealerAction();
        SceneManager.LoadScene(buildIndex);
    }

    public void OnDebugToggleClicked()
    {
        debugPanelOpen = !debugPanelOpen;
        RefreshView();
    }

    private void CreateGame()
    {
        Deck deck = Deck.CreateIndianHoldemDeck();
        deck.Shuffle();

        gameState = new GameState(
            Mathf.Max(0, playerStartingChips),
            Mathf.Max(0, dealerStartingChips),
            deck,
            GameModeSelection.SelectedMode);
        nextRoundFirstTurn = firstTurn == TurnOwner.Dealer
            ? TurnOwner.Dealer
            : TurnOwner.Player;

        if (itemSystem == null)
        {
            itemSystem = FindObjectOfType<ItemSystem>();

            if (itemSystem == null)
            {
                Debug.LogError(
                    "[GameplayController] ItemSystem 참조를 찾을 수 없습니다.",
                    this);
                return;
            }
        }

        itemSystem.Initialize(new ItemGameApi(gameState));
        dealerTurn = new DealerTurnController();
        dealerTurn.Initialize(gameState, itemSystem, AddLog);
        SubscribeToItemSystemEvents();

        if (cardVisualController != null)
        {
            cardVisualController.Initialize(gameState);
        }

        if (chipVisualController != null)
        {
            chipVisualController.Initialize(gameState);
        }

        ResetRoundResult();
    }

    private void SubscribeToItemSystemEvents()
    {
        UnsubscribeFromItemSystemEvents();
        subscribedItemSystem = itemSystem;

        if (subscribedItemSystem != null)
        {
            subscribedItemSystem.RefreshCardSucceeded += OnRefreshCardSucceeded;
            subscribedItemSystem.PlayerItemUseRequested += OnPlayerItemUseRequested;
        }
    }

    private void UnsubscribeFromItemSystemEvents(bool unsubscribePlayerRequests = true)
    {
        if (subscribedItemSystem != null)
        {
            subscribedItemSystem.RefreshCardSucceeded -= OnRefreshCardSucceeded;
            if (unsubscribePlayerRequests)
            {
                subscribedItemSystem.PlayerItemUseRequested -= OnPlayerItemUseRequested;
            }
        }

        if (unsubscribePlayerRequests)
        {
            subscribedItemSystem = null;
        }
    }

    private void OnPlayerItemUseRequested(GameObject item)
    {
        if (!isActiveAndEnabled || itemSystem == null || item == null)
        {
            return;
        }

        string actionName = item.TryGetComponent(out Item itemComponent) &&
                            itemComponent.itemData != null
            ? $"ITEM {itemComponent.itemData.itemType}"
            : "ITEM";
        RunPlayerBettingAction(
            actionName,
            () => itemSystem.UseItem(TurnOwner.Player, item),
            playChipSfx: false);
    }

    private void OnRefreshCardSucceeded()
    {
        // 시작 연출 중에는 기존 딜 완료/실패 경로가 최신 카드를 동기화
        if (isCardAnimating || isChipAnimating)
        {
            return;
        }

        isCardAnimating = true;

        bool refreshStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayRefresh(
                OnRefreshPresentationCompleted,
                OnRefreshPresentationFailed);

        if (!refreshStarted)
        {
            isCardAnimating = false;
            cardVisualController?.RefreshCards();
        }
    }

    private void OnRefreshPresentationCompleted()
    {
        if (!CanHandlePresentationCallback())
        {
            return;
        }

        isCardAnimating = false;
        RefreshView();
    }

    private void OnRefreshPresentationFailed()
    {
        if (!CanHandlePresentationCallback())
        {
            return;
        }

        cardVisualController?.RefreshCards();
        isCardAnimating = false;
        RefreshView();
    }

    private void StartRound()
    {
        TurnOwner roundFirstTurn = nextRoundFirstTurn;

        if (!gameState.TryStartRound(roundFirstTurn))
        {
            isCardAnimating = false;
            AddLog("라운드 시작 실패 - 남은 카드와 현재 단계를 확인하세요");
            return;
        }

        RunRoundStartEffects();
        ResetRoundResult();
        AddLog($"라운드 시작 - {OwnerText(gameState.CurrentTurn)} 선공");

        if (!TryStartRoundAnteAnimation())
        {
            chipVisualController?.RefreshChips();
            TryStartCardDeal();
        }
    }

    private void PrepareAndStartNextRound()
    {
        int carriedPot = gameState.Pot.Amount;
        TurnOwner resolvedNextFirstTurn =
            GetNextRoundFirstTurnFromRoundResult();

        if (!gameState.TryPrepareNextRound())
        {
            AddLog("다음 라운드 준비 실패");
            return;
        }

        nextRoundFirstTurn = resolvedNextFirstTurn;
        cardVisualController?.RefreshCards();
        ResetRoundResult();
        AddLog($"다음 라운드 준비 - 이월 팟: {carriedPot}");
        StartRound();
    }

    private TurnOwner GetNextRoundFirstTurnFromRoundResult()
    {
        if (gameState == null || gameState.Phase == GamePhase.GameOver)
        {
            return nextRoundFirstTurn;
        }

        if (gameState.RoundEndReason == RoundEndReason.Fold)
        {
            if (gameState.FoldedBy == TurnOwner.Player)
            {
                return TurnOwner.Dealer;
            }

            if (gameState.FoldedBy == TurnOwner.Dealer)
            {
                return TurnOwner.Player;
            }
        }

        if (gameState.RoundEndReason == RoundEndReason.Showdown)
        {
            if (roundWinner == RoundWinner.Player)
            {
                return TurnOwner.Player;
            }

            if (roundWinner == RoundWinner.Dealer)
            {
                return TurnOwner.Dealer;
            }
        }

        return nextRoundFirstTurn;
    }

    private void RunPlayerBettingAction(
        string actionName,
        Func<bool> action,
        bool playChipSfx)
    {
        if (!CanAcceptPlayerBettingInput() || action == null)
        {
            return;
        }

        isActionProcessing = true;
        int playerChipsBefore = gameState.PlayerChips.Count;
        int dealerChipsBefore = gameState.DealerChips.Count;
        int potBefore = gameState.Pot.Amount;

        try
        {
            if (!action())
            {
                AddLog($"{OwnerText(TurnOwner.Player)} {actionName} 실패");
                return;
            }

            if (playChipSfx)
            {
                SoundSystem.Instance.PlayChipStackSFX();
            }

            bool isPlayerFold =
                gameState.RoundEndReason == RoundEndReason.Fold &&
                gameState.FoldedBy == TurnOwner.Player;
            bool foldSettlementStarted =
                isPlayerFold &&
                TryStartFoldSettlement(
                    TurnOwner.Player,
                    potBefore,
                    gameState.FoldPenaltyAmount);
            int potIncrease = gameState.Pot.Amount - potBefore;
            int playerChipDecrease =
                playerChipsBefore - gameState.PlayerChips.Count;
            bool playerBetAnimationStarted =
                !isPlayerFold &&
                potIncrease > 0 &&
                playerChipDecrease == potIncrease &&
                TryStartPlayerBetAnimation(potIncrease);

            if (!foldSettlementStarted && !playerBetAnimationStarted)
            {
                RefreshChipsIfChanged(
                    playerChipsBefore,
                    dealerChipsBefore,
                    potBefore);
            }

            if (isPlayerFold && !foldSettlementStarted)
            {
                StartFoldCardReveal();
            }

            AddLog($"{OwnerText(TurnOwner.Player)} {actionName}");
            AddBettingResultLog();
        }
        finally
        {
            isActionProcessing = false;
            RefreshView();
        }
    }

    private void RunProgressAction(GamePhase requiredPhase, Action action)
    {
        if (!CanAcceptProgressInput(requiredPhase) || action == null)
        {
            return;
        }

        isActionProcessing = true;

        try
        {
            action();
        }
        finally
        {
            isActionProcessing = false;
            RefreshView();
        }
    }

    private bool CanAcceptPlayerBettingInput()
    {
        return gameState != null &&
               !isRestarting &&
               !isActionProcessing &&
               !isChipAnimating &&
               !isCardAnimating &&
               dealerActionCoroutine == null &&
               gameState.Phase == GamePhase.Betting &&
               gameState.CurrentTurn == TurnOwner.Player;
    }

    private bool CanAcceptProgressInput(GamePhase requiredPhase)
    {
        return gameState != null &&
               !isRestarting &&
               !isActionProcessing &&
               !isChipAnimating &&
               !isCardAnimating &&
               dealerActionCoroutine == null &&
               gameState.Phase == requiredPhase;
    }

    private bool CanRestartGame()
    {
        if (gameState == null ||
            isRestarting ||
            isShuttingDown ||
            !isActiveAndEnabled ||
            !gameObject.scene.IsValid() ||
            !gameObject.scene.isLoaded ||
            gameState.Phase != GamePhase.GameOver ||
            gameState.FinalWinner == GameWinner.None ||
            isActionProcessing ||
            isChipAnimating ||
            isCardAnimating ||
            dealerActionCoroutine != null ||
            showdownPresentationCoroutine != null)
        {
            return false;
        }

        bool hasSettledShowdown =
            gameState.RoundEndReason == RoundEndReason.Showdown &&
            roundWinner != RoundWinner.None;

        return !hasSettledShowdown || isShowdownResultVisible;
    }

    private void AddBettingResultLog()
    {
        if (gameState.Phase == GamePhase.Showdown)
        {
            AddLog("베팅 종료 - 쇼다운을 정산하세요");
        }
        else if (gameState.Phase == GamePhase.RoundEnd)
        {
            AddLog($"라운드 종료 - {RoundEndReasonText(gameState.RoundEndReason)}");
        }
        else if (gameState.Phase == GamePhase.GameOver)
        {
            AddLog(GameOverLogText(gameState.FinalWinner));
        }
    }

    private void TryScheduleDealerAction()
    {
        if (isRestarting ||
            isActionProcessing ||
            isChipAnimating ||
            isCardAnimating ||
            dealerActionCoroutine != null ||
            gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            return;
        }

        dealerActionCoroutine = StartCoroutine(PerformDealerActionAfterDelay());
    }

    private void CancelInvalidDealerAction()
    {
        if (dealerActionCoroutine == null ||
            (gameState.Phase == GamePhase.Betting &&
             gameState.CurrentTurn == TurnOwner.Dealer))
        {
            return;
        }

        CancelDealerAction();
    }

    private void CancelDealerAction()
    {
        if (dealerActionCoroutine == null)
        {
            return;
        }

        if (dealerAnimationController != null)
        {
            dealerAnimationController.StopThink();
        }

        StopCoroutine(dealerActionCoroutine);
        dealerActionCoroutine = null;
    }

    private IEnumerator PerformDealerActionAfterDelay()
    {
        bool thinkStarted =
            dealerAnimationController != null &&
            dealerAnimationController.TryPlayThink();

        float minimumThinkDelay = Mathf.Max(
            0f,
            Mathf.Min(minDealerThinkDelay, maxDealerThinkDelay));
        float maximumThinkDelay = Mathf.Max(
            minimumThinkDelay,
            Mathf.Max(minDealerThinkDelay, maxDealerThinkDelay));
        float thinkDelay = UnityEngine.Random.Range(
            minimumThinkDelay,
            maximumThinkDelay);
        yield return new WaitForSeconds(thinkDelay);

        if (thinkStarted && dealerAnimationController != null)
        {
            dealerAnimationController.StopThink();
        }

        if (isChipAnimating ||
            isCardAnimating ||
            gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            dealerActionCoroutine = null;
            RefreshView();
            yield break;
        }

        isActionProcessing = true;

        try
        {
            int actionRoll = UnityEngine.Random.Range(0, 100);
            int raiseRoll = UnityEngine.Random.Range(0, 100);
            int playerChipsBefore = gameState.PlayerChips.Count;
            int dealerChipsBefore = gameState.DealerChips.Count;
            int potBefore = gameState.Pot.Amount;

            if (!dealerTurn.TryPrepareAction(actionRoll, raiseRoll, out DealerActionPlan actionPlan))
            {
                bool isDealerItemFold =
                    gameState.RoundEndReason == RoundEndReason.Fold &&
                    gameState.FoldedBy == TurnOwner.Dealer;
                bool foldPresentationStarted =
                    isDealerItemFold &&
                    TryStartDealerFoldPresentation(
                        TurnOwner.Dealer,
                        potBefore,
                        gameState.FoldPenaltyAmount);

                if (!foldPresentationStarted)
                {
                    RefreshChipsIfChanged(playerChipsBefore, dealerChipsBefore, potBefore);
                }

                if (isDealerItemFold && !foldPresentationStarted)
                {
                    StartFoldCardReveal();
                }

                AddBettingResultLog();
                yield break;
            }

            while (isCardAnimating)
            {
                if (isShuttingDown ||
                    isRestarting ||
                    !isActiveAndEnabled)
                {
                    yield break;
                }

                yield return null;
            }

            if (isShuttingDown ||
                isRestarting ||
                !isActiveAndEnabled ||
                gameState.Phase != GamePhase.Betting ||
                gameState.CurrentTurn != TurnOwner.Dealer)
            {
                yield break;
            }

            RefreshChipsIfChanged(playerChipsBefore, dealerChipsBefore, potBefore);
            playerChipsBefore = gameState.PlayerChips.Count;
            dealerChipsBefore = gameState.DealerChips.Count;
            potBefore = gameState.Pot.Amount;
            DealerDecision decision = actionPlan.Decision;

            if (dealerTurn.TryExecute(actionPlan))
            {
                if (decision == DealerDecision.Call ||
                    decision == DealerDecision.Raise ||
                    decision == DealerDecision.AllIn)
                {
                    SoundSystem.Instance.PlayChipStackSFX();
                }

                bool isDealerFold =
                    gameState.RoundEndReason == RoundEndReason.Fold &&
                    gameState.FoldedBy == TurnOwner.Dealer;
                bool foldPresentationStarted =
                    isDealerFold &&
                    TryStartDealerFoldPresentation(
                        TurnOwner.Dealer,
                        potBefore,
                        gameState.FoldPenaltyAmount);
                int movedChipCount =
                    dealerChipsBefore - gameState.DealerChips.Count;
                bool isAllInCall =
                    decision == DealerDecision.Call &&
                    dealerChipsBefore > 0 &&
                    gameState.DealerChips.Count == 0;
                bool useAllInAnimation =
                    decision == DealerDecision.AllIn || isAllInCall;
                bool betAnimationStarted =
                    !isDealerFold &&
                    (decision == DealerDecision.Call ||
                     decision == DealerDecision.Raise ||
                     decision == DealerDecision.AllIn) &&
                    movedChipCount > 0 &&
                    TryStartDealerBetAnimation(
                        movedChipCount,
                        useAllInAnimation);

                if (!foldPresentationStarted && !betAnimationStarted)
                {
                    RefreshChipsIfChanged(
                        playerChipsBefore,
                        dealerChipsBefore,
                        potBefore);
                }

                if (isDealerFold && !foldPresentationStarted)
                {
                    StartFoldCardReveal();
                }

                AddLog($"DEALER {DealerDecisionText(decision)}");
                AddBettingResultLog();
            }
            else
            {
                AddLog($"DEALER {DealerDecisionText(decision)} 실패");
            }
        }
        finally
        {
            isActionProcessing = false;
            dealerActionCoroutine = null;
            RefreshView();
        }
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

    private void RunRoundStartEffects()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        SoundSystem soundSystem = SoundSystem.Instance;
        soundSystem.PlayCardSFX();
        soundSystem.PlayCardSFX();
        soundSystem.PlayCardSFX();
        soundSystem.PlayCardSFX();
        itemSystem.GetItem();
    }

    private bool TryStartRoundAnteAnimation()
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
        RefreshView();
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
        RefreshView();
    }

    private void OnCardDealCompleted()
    {
        isCardAnimating = false;
        cardVisualController?.RefreshCards();
        RefreshView();
    }

    private void OnCardDealFailed()
    {
        isCardAnimating = false;
        cardVisualController?.RefreshCards();
        RefreshView();
    }

    private bool TryStartPlayerBetAnimation(int chipCount)
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

        RefreshView();
    }

    private void OnPlayerBetChipsMoveFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelPlayerBet();
            chipVisualController.RefreshChips();
        }

        RefreshView();
    }

    private bool TryStartPlayerCollect(int chipCount)
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

        RefreshView();
    }

    private void OnPlayerCollectFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelPlayerCollect();
            chipVisualController.RefreshChips();
        }

        RefreshView();
    }

    private bool TryStartDrawSettlement()
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

        RefreshView();
    }

    private void OnDrawSettlementFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDrawSettlement();
            chipVisualController.RefreshChips();
        }

        RefreshView();
    }

    private bool TryStartFoldSettlement(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount)
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
                OnFoldSettlementCompleted,
                OnFoldSettlementFailed))
        {
            return true;
        }

        isChipAnimating = false;
        return false;
    }

    private bool TryStartDealerFoldPresentation(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount)
    {
        isChipAnimating = true;

        if (dealerAnimationController != null &&
            dealerAnimationController.TryPlayFold(
                () => OnDealerFoldAnimationFinished(
                    foldedBy,
                    potChipCount,
                    penaltyChipCount),
                () => OnDealerFoldAnimationFinished(
                    foldedBy,
                    potChipCount,
                    penaltyChipCount)))
        {
            return true;
        }

        isChipAnimating = false;
        return TryStartFoldSettlement(
            foldedBy,
            potChipCount,
            penaltyChipCount);
    }

    private void OnDealerFoldAnimationFinished(
        TurnOwner foldedBy,
        int potChipCount,
        int penaltyChipCount)
    {
        isChipAnimating = false;

        if (!TryStartFoldSettlement(
                foldedBy,
                potChipCount,
                penaltyChipCount))
        {
            chipVisualController?.RefreshChips();
            StartFoldCardReveal();
        }
    }

    private void OnFoldSettlementCompleted()
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

        StartFoldCardReveal();
    }

    private void OnFoldSettlementFailed()
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelFoldSettlement();
            chipVisualController.RefreshChips();
        }

        StartFoldCardReveal();
    }

    private void StartFoldCardReveal()
    {
        if (gameState == null ||
            gameState.RoundEndReason != RoundEndReason.Fold ||
            isFoldResultVisible ||
            isCardAnimating)
        {
            return;
        }

        isFoldResultVisible = false;
        isCardAnimating = true;

        bool revealStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayShowdownReveal(
                CompleteFoldCardReveal,
                () =>
                {
                    cardVisualController?.ShowShowdownCardsImmediately();
                    CompleteFoldCardReveal();
                });

        if (!revealStarted)
        {
            cardVisualController?.ShowShowdownCardsImmediately();
            CompleteFoldCardReveal();
            return;
        }

        RefreshView();
    }

    private void CompleteFoldCardReveal()
    {
        if (!CanHandlePresentationCallback())
        {
            return;
        }

        isCardAnimating = false;
        isFoldResultVisible = true;
        RefreshView();
    }

    private bool TryStartDealerBetAnimation(
        int chipCount,
        bool useAllInAnimation)
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

        RefreshView();
    }

    private void OnDealerBetChipsMoveFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDealerBet();
            chipVisualController.RefreshChips();
        }

        RefreshView();
    }

    private bool TryStartDealerCollectAnimation(int chipCount)
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

        RefreshView();
    }

    private void OnDealerCollectFailed(GameObject[] chips)
    {
        isChipAnimating = false;

        if (chipVisualController != null)
        {
            chipVisualController.CancelDealerCollect();
            chipVisualController.RefreshChips();
        }

        RefreshView();
    }

    private void ResolveShowdown()
    {
        gameState.TryGetHandRank(TurnOwner.Player, out playerHandRank);
        gameState.TryGetHandRank(TurnOwner.Dealer, out dealerHandRank);
        int playerChipsBefore = gameState.PlayerChips.Count;
        int dealerChipsBefore = gameState.DealerChips.Count;
        int potBeforeSettlement = gameState.Pot.Amount;

        if (!gameState.TrySettleShowdown(out roundWinner))
        {
            AddLog("쇼다운 정산 실패");
            return;
        }

        isShowdownResultVisible = false;
        isCardAnimating = true;

        Action continuePresentation = () =>
            ContinueShowdownAfterReveal(
                playerChipsBefore,
                dealerChipsBefore,
                potBeforeSettlement);

        bool revealStarted =
            cardVisualController != null &&
            cardVisualController.TryPlayShowdownReveal(
                continuePresentation,
                () =>
                {
                    cardVisualController?.ShowShowdownCardsImmediately();
                    continuePresentation();
                });

        if (!revealStarted)
        {
            cardVisualController?.ShowShowdownCardsImmediately();
            continuePresentation();
        }
    }

    private void ContinueShowdownAfterReveal(
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBeforeSettlement)
    {
        if (!CanHandlePresentationCallback())
        {
            return;
        }

        isShowdownResultVisible = true;
        AddLog(
            $"플레이어 {HandRankText(playerHandRank)} / " +
            $"딜러 {HandRankText(dealerHandRank)}");
        AddLog($"라운드 승자 - {RoundWinnerText(roundWinner)}");
        AddLog(roundWinner == RoundWinner.Draw
            ? $"무승부 - 팟 {gameState.Pot.Amount} 이월"
            : $"팟 {potBeforeSettlement} 정산 예정");

        if (gameState.Phase == GamePhase.GameOver)
        {
            AddLog(GameOverLogText(gameState.FinalWinner));
        }

        RefreshView();
        showdownPresentationCoroutine = StartCoroutine(
            ContinueShowdownAfterResultDelay(
                playerChipsBefore,
                dealerChipsBefore,
                potBeforeSettlement));
    }

    private IEnumerator ContinueShowdownAfterResultDelay(
        int playerChipsBefore,
        int dealerChipsBefore,
        int potBeforeSettlement)
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, showdownResultDelay));

        showdownPresentationCoroutine = null;

        if (!CanHandlePresentationCallback())
        {
            yield break;
        }

        isCardAnimating = false;
        bool collectAnimationStarted = false;

        if (potBeforeSettlement > 0)
        {
            if (roundWinner == RoundWinner.Player)
            {
                collectAnimationStarted =
                    TryStartPlayerCollect(potBeforeSettlement);
            }
            else if (roundWinner == RoundWinner.Dealer)
            {
                collectAnimationStarted =
                    TryStartDealerCollectAnimation(potBeforeSettlement);
            }
            else if (roundWinner == RoundWinner.Draw)
            {
                collectAnimationStarted = TryStartDrawSettlement();
            }
        }

        if (!collectAnimationStarted)
        {
            RefreshChipsIfChanged(
                playerChipsBefore,
                dealerChipsBefore,
                potBeforeSettlement);
        }

        RefreshView();
    }

    private bool CanHandlePresentationCallback()
    {
        return this != null &&
               !isShuttingDown &&
               isActiveAndEnabled &&
               gameObject.scene.IsValid() &&
               gameObject.scene.isLoaded;
    }

    private void RefreshChipsIfChanged(
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

    private void UpdateVisibleHandRanks()
    {
        if (gameState.Phase != GamePhase.Showdown)
        {
            return;
        }

        gameState.TryGetHandRank(TurnOwner.Player, out playerHandRank);
        gameState.TryGetHandRank(TurnOwner.Dealer, out dealerHandRank);
    }

    private void RefreshView()
    {
        gameplayView.RefreshRound(CurrentGameMode, CurrentRound);
        gameplayView.RefreshDealerHand(gameState);
        gameplayView.RefreshDebug(
            gameState,
            playerHandRank,
            dealerHandRank,
            roundWinner,
            debugPanelOpen,
            logs);

        bool playerTurn = gameState.Phase == GamePhase.Betting &&
                          gameState.CurrentTurn == TurnOwner.Player;
        bool canAcceptPlayerBettingInput = CanAcceptPlayerBettingInput();
        bool canCall =
            canAcceptPlayerBettingInput &&
            gameState.Betting.GetCallAmount(TurnOwner.Player) > 0;
        int maxRaiseAmount = GetMaxRaiseAmount();
        ClampRaiseAmount(maxRaiseAmount);
        gameplayView.RefreshBetting(
            playerTurn,
            canAcceptPlayerBettingInput,
            canCall,
            IsPlayerShortAllInRequired(),
            selectedRaiseAmount,
            maxRaiseAmount);

        bool canResolve = gameState.Phase == GamePhase.Showdown;
        bool canStartNextRound = gameState.Phase == GamePhase.RoundEnd;
        gameplayView.RefreshProgress(
            canResolve,
            canStartNextRound,
            CanAcceptProgressInput(GamePhase.Showdown),
            CanAcceptProgressInput(GamePhase.RoundEnd));
        gameplayView.RefreshResult(
            gameState,
            roundWinner,
            playerHandRank,
            dealerHandRank,
            isShowdownResultVisible,
            isFoldResultVisible,
            CanRestartGame());
    }

    private bool IsPlayerShortAllInRequired()
    {
        if (gameState == null)
        {
            return false;
        }

        int callAmount =
            gameState.Betting.GetCallAmount(TurnOwner.Player);
        return callAmount > gameState.PlayerChips.Count;
    }

    private bool CanSelectPlayerRaise(out int maxRaiseAmount)
    {
        maxRaiseAmount = GetMaxRaiseAmount();
        return CanAcceptPlayerBettingInput() && maxRaiseAmount > 0;
    }

    private int GetMaxRaiseAmount()
    {
        if (gameState == null)
        {
            return 0;
        }

        int callAmount =
            gameState.Betting.GetCallAmount(TurnOwner.Player);
        return gameState.PlayerChips.Count - callAmount;
    }

    private void ClampRaiseAmount(int maxRaiseAmount)
    {
        selectedRaiseAmount = maxRaiseAmount > 0
            ? Mathf.Clamp(selectedRaiseAmount, 1, maxRaiseAmount)
            : 1;
    }

    private void ResetRoundResult()
    {
        playerHandRank = HandRank.None;
        dealerHandRank = HandRank.None;
        roundWinner = RoundWinner.None;
        isShowdownResultVisible = false;
        isFoldResultVisible = false;
    }

    private void AddLog(string message)
    {
        logs.Add(message);

        while (logs.Count > Mathf.Max(1, maxLogLines))
        {
            logs.RemoveAt(0);
        }
    }

    private static string OwnerText(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return "플레이어";
            case TurnOwner.Dealer:
                return "딜러";
            default:
                return "없음";
        }
    }

    private static string DealerDecisionText(DealerDecision decision)
    {
        switch (decision)
        {
            case DealerDecision.Call:
                return "CALL";
            case DealerDecision.Raise:
                return "RAISE";
            case DealerDecision.Fold:
                return "FOLD";
            case DealerDecision.AllIn:
                return "ALL-IN";
            default:
                return "NONE";
        }
    }

    private static string HandRankText(HandRank handRank)
    {
        switch (handRank)
        {
            case HandRank.Number:
                return "숫자";
            case HandRank.Double:
                return "더블";
            case HandRank.Straight:
                return "스트레이트";
            case HandRank.Triple:
                return "트리플";
            default:
                return "없음";
        }
    }

    private static string RoundWinnerText(RoundWinner winner)
    {
        switch (winner)
        {
            case RoundWinner.Player:
                return "플레이어";
            case RoundWinner.Dealer:
                return "딜러";
            case RoundWinner.Draw:
                return "무승부";
            default:
                return "없음";
        }
    }

    private static string RoundEndReasonText(RoundEndReason reason)
    {
        switch (reason)
        {
            case RoundEndReason.Fold:
                return "폴드";
            case RoundEndReason.Showdown:
                return "쇼다운";
            default:
                return "없음";
        }
    }

    private static string GameWinnerText(GameWinner winner)
    {
        switch (winner)
        {
            case GameWinner.Player:
                return "플레이어";
            case GameWinner.Dealer:
                return "딜러";
            case GameWinner.Draw:
                return "무승부";
            default:
                return "없음";
        }
    }

    private static string GameOverLogText(GameWinner winner)
    {
        return winner == GameWinner.Draw
            ? "게임 종료 - 무승부"
            : $"게임 종료 - {GameWinnerText(winner)} 승리";
    }
}
