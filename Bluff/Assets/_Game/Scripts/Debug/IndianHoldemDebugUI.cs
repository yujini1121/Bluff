using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IndianHoldemDebugUI : MonoBehaviour
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
    [SerializeField, Min(0f), InspectorName("딜러 행동 대기 시간")]
    private float dealerActionDelay = 0.75f;
    [SerializeField, InspectorName("UI 폰트 (한글 지원 권장)")]
    private TMP_FontAsset uiFont;
    [SerializeField] private ItemSystem itemSystem;

    [Header("3D 카드 표시")]
    [SerializeField] private CardVisualController cardVisualController;

    [Header("메인 게임 화면")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text dealerCardText;
    [SerializeField] private TMP_Text dealerChipsText;
    [SerializeField] private TMP_Text dealerTotalBetText;
    [SerializeField] private TMP_Text communityCard1Text;
    [SerializeField] private TMP_Text communityCard2Text;
    [SerializeField] private TMP_Text potText;
    [SerializeField] private TMP_Text playerCardText;
    [SerializeField] private TMP_Text playerChipsText;
    [SerializeField] private TMP_Text playerTotalBetText;
    [SerializeField] private TMP_Text dealerNameText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Outline dealerCardOutline;
    [SerializeField] private Outline playerCardOutline;

    [Header("Turn 강조 색상")]
    [SerializeField] private Color turnHighlightColor =
        new Color32(228, 181, 84, 255);
    [SerializeField] private Color idleNameColor =
        new Color32(235, 240, 244, 255);
    [SerializeField] private Color idleCardOutlineColor =
        new Color32(187, 176, 149, 255);

    [Header("결과 오버레이")]
    [SerializeField] private GameObject resultOverlay;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultDetailText;

    [Header("디버그 패널")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TMP_Text debugInfoText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button debugToggleButton;

    [Header("행동 영역")]
    [SerializeField] private GameObject dealerActionBar;
    [SerializeField] private GameObject playerActionBar;
    [SerializeField] private GameObject contextActionArea;
    [SerializeField] private Button[] bettingActionButtons;
    [SerializeField] private Button resolveShowdownButton;
    [SerializeField] private Button nextRoundButton;

    private readonly List<string> logs = new List<string>();
    private readonly List<TMP_Text> callActionTexts = new List<TMP_Text>();
    private GameState gameState;
    private DealerAi dealerAi;
    private Coroutine dealerActionCoroutine;
    private HandRank playerHandRank;
    private HandRank dealerHandRank;
    private RoundWinner roundWinner;
    private bool debugPanelOpen;
    private bool isActionProcessing;

    private void Awake()
    {
        ApplyUiFont();

        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "인디언 홀덤 UI 참조가 연결되지 않았습니다. " +
                "Inspector에서 UI 참조를 확인해주세요.",
                this);
            enabled = false;
            return;
        }

        debugPanelOpen = false;
        dealerAi = new DealerAi();
        CacheCallActionTexts();
        CreateDebugGame();
        RefreshView();
    }

    private void OnValidate()
    {
        ApplyUiFont();
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
        isActionProcessing = false;
        CancelDealerAction();
    }

    public void OnCallClicked()
    {
        if (gameState == null)
        {
            return;
        }

        if (gameState.Betting.GetCallAmount(TurnOwner.Player) == 0)
        {
            RunPlayerBettingAction("체크", gameState.TryCheck);
        }
        else
        {
            RunPlayerBettingAction("콜", gameState.TryCall);
        }
    }

    public void OnFoldClicked()
    {
        RunPlayerBettingAction("폴드", () => gameState.TryFold());
    }

    public void OnRaiseOneClicked()
    {
        RunPlayerBettingAction("레이즈 +1", () => gameState.TryRaise(1));
    }

    public void OnRaiseFiveClicked()
    {
        RunPlayerBettingAction("레이즈 +5", () => gameState.TryRaise(5));
    }

    public void OnAllInClicked()
    {
        RunPlayerBettingAction("올인", () => gameState.TryAllIn());
    }

    public void OnResolveShowdownClicked()
    {
        RunProgressAction(GamePhase.Showdown, ResolveShowdown);
    }

    public void OnNextRoundClicked()
    {
        RunProgressAction(GamePhase.RoundEnd, PrepareAndStartNextRound);
    }

    public void OnDebugToggleClicked()
    {
        debugPanelOpen = !debugPanelOpen;
        RefreshView();
    }

    private void CreateDebugGame()
    {
        Deck deck = Deck.CreateIndianHoldemDeck();
        deck.Shuffle();

        gameState = new GameState(
            Mathf.Max(0, playerStartingChips),
            Mathf.Max(0, dealerStartingChips),
            deck);
        //itemSystem.Initialize(new ItemGameApi(gameState));

        if (cardVisualController != null)
        {
            cardVisualController.Initialize(gameState);
        }

        ResetDisplayedRoundResult();
        StartRound();
    }

    private void StartRound()
    {
        TurnOwner roundFirstTurn = firstTurn == TurnOwner.Dealer
            ? TurnOwner.Dealer
            : TurnOwner.Player;

        if (!gameState.TryStartRound(roundFirstTurn))
        {
            AddLog("라운드 시작 실패 - 남은 카드와 현재 단계를 확인하세요");
            return;
        }

        cardVisualController?.RefreshCards();
        ResetDisplayedRoundResult();
        AddLog($"라운드 시작 - {OwnerText(gameState.CurrentTurn)} 선공");
    }

    private void PrepareAndStartNextRound()
    {
        int carriedPot = gameState.Pot.Amount;

        if (!gameState.TryPrepareNextRound())
        {
            AddLog("다음 라운드 준비 실패");
            return;
        }

        cardVisualController?.RefreshCards();
        ResetDisplayedRoundResult();
        AddLog($"다음 라운드 준비 - 이월 팟: {carriedPot}");
        StartRound();
    }

    private void RunPlayerBettingAction(string actionName, Func<bool> action)
    {
        if (!CanAcceptPlayerBettingInput() || action == null)
        {
            return;
        }

        isActionProcessing = true;

        try
        {
            if (!action())
            {
                AddLog($"{OwnerText(TurnOwner.Player)} {actionName} 실패");
                return;
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
               !isActionProcessing &&
               dealerActionCoroutine == null &&
               gameState.Phase == GamePhase.Betting &&
               gameState.CurrentTurn == TurnOwner.Player;
    }

    private bool CanAcceptProgressInput(GamePhase requiredPhase)
    {
        return gameState != null &&
               !isActionProcessing &&
               dealerActionCoroutine == null &&
               gameState.Phase == requiredPhase;
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
            AddLog($"게임 종료 - {GameWinnerText(gameState.FinalWinner)} 승리");
        }
    }

    private void TryScheduleDealerAction()
    {
        if (isActionProcessing ||
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

        StopCoroutine(dealerActionCoroutine);
        dealerActionCoroutine = null;
    }

    private IEnumerator PerformDealerActionAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, dealerActionDelay));

        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            dealerActionCoroutine = null;
            RefreshView();
            yield break;
        }

        isActionProcessing = true;

        try
        {
            int randomRoll = UnityEngine.Random.Range(0, 100);
            DealerDecision decision = dealerAi.Decide(gameState, randomRoll);

            if (dealerAi.TryExecute(gameState, decision))
            {
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

    private void ResolveShowdown()
    {
        gameState.TryGetHandRank(TurnOwner.Player, out playerHandRank);
        gameState.TryGetHandRank(TurnOwner.Dealer, out dealerHandRank);
        int potBeforeSettlement = gameState.Pot.Amount;

        if (!gameState.TrySettleShowdown(out roundWinner))
        {
            AddLog("쇼다운 정산 실패");
            return;
        }

        AddLog(
            $"플레이어 {HandRankText(playerHandRank)} / " +
            $"딜러 {HandRankText(dealerHandRank)}");
        AddLog($"라운드 승자 - {RoundWinnerText(roundWinner)}");
        AddLog(roundWinner == RoundWinner.Draw
            ? $"무승부 - 팟 {gameState.Pot.Amount} 이월"
            : $"팟 {potBeforeSettlement} 정산 완료");

        if (gameState.Phase == GamePhase.GameOver)
        {
            AddLog($"게임 종료 - {GameWinnerText(gameState.FinalWinner)} 승리");
        }
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
        phaseText.text = PhaseGameText(gameState.Phase);
        turnText.text = TurnGameText(gameState.CurrentTurn);

        dealerCardText.text = CardText(gameState.DealerCard);
        dealerChipsText.text =
            $"CHIPS\n<size=40>{gameState.DealerChips.Count}</size>";
        dealerTotalBetText.text =
            $"BET\n<size=34>{gameState.Betting.DealerTotalBet}</size>";

        communityCard1Text.text = CardText(gameState.CommunityCard1);
        communityCard2Text.text = CardText(gameState.CommunityCard2);
        potText.text = $"POT\n<size=50>{gameState.Pot.Amount}</size>";

        playerCardText.text = CardText(gameState.PlayerCard);
        playerChipsText.text =
            $"CHIPS\n<size=40>{gameState.PlayerChips.Count}</size>";
        playerTotalBetText.text =
            $"BET\n<size=34>{gameState.Betting.PlayerTotalBet}</size>";

        debugInfoText.text = BuildDebugInfo();
        messageText.text = string.Join("\n", logs);
        debugPanel.SetActive(debugPanelOpen);
        RefreshCallActionTexts();

        bool dealerTurn = gameState.Phase == GamePhase.Betting &&
                          gameState.CurrentTurn == TurnOwner.Dealer;
        bool playerTurn = gameState.Phase == GamePhase.Betting &&
                          gameState.CurrentTurn == TurnOwner.Player;
        bool canAcceptPlayerBettingInput = CanAcceptPlayerBettingInput();
        dealerActionBar.SetActive(dealerTurn);
        playerActionBar.SetActive(playerTurn);

        for (int index = 0; index < bettingActionButtons.Length; index++)
        {
            bettingActionButtons[index].interactable =
                canAcceptPlayerBettingInput;
        }

        RefreshTurnHighlight(dealerTurn, playerTurn);

        bool canResolve = gameState.Phase == GamePhase.Showdown;
        bool canStartNextRound = gameState.Phase == GamePhase.RoundEnd;
        contextActionArea.SetActive(canResolve || canStartNextRound);
        resolveShowdownButton.gameObject.SetActive(canResolve);
        nextRoundButton.gameObject.SetActive(canStartNextRound);
        resolveShowdownButton.interactable =
            CanAcceptProgressInput(GamePhase.Showdown);
        nextRoundButton.interactable =
            CanAcceptProgressInput(GamePhase.RoundEnd);

        RefreshResultOverlay();
    }

    private void CacheCallActionTexts()
    {
        callActionTexts.Clear();

        for (int buttonIndex = 0;
             buttonIndex < bettingActionButtons.Length;
             buttonIndex++)
        {
            Button button = bettingActionButtons[buttonIndex];

            for (int eventIndex = 0;
                 eventIndex < button.onClick.GetPersistentEventCount();
                 eventIndex++)
            {
                if (button.onClick.GetPersistentMethodName(eventIndex) !=
                    nameof(OnCallClicked))
                {
                    continue;
                }

                TMP_Text actionText =
                    button.GetComponentInChildren<TMP_Text>(true);

                if (actionText != null)
                {
                    callActionTexts.Add(actionText);
                }

                break;
            }
        }
    }

    private void RefreshCallActionTexts()
    {
        string actionText =
            gameState.Betting.GetCallAmount(gameState.CurrentTurn) == 0
                ? "CHECK"
                : "CALL";

        for (int index = 0; index < callActionTexts.Count; index++)
        {
            callActionTexts[index].text = actionText;
        }
    }

    private void RefreshTurnHighlight(bool dealerTurn, bool playerTurn)
    {
        dealerNameText.text = dealerTurn ? "▶  DEALER  ◀" : "DEALER";
        playerNameText.text = playerTurn ? "▶  PLAYER  ◀" : "PLAYER";
        dealerNameText.color = dealerTurn ? turnHighlightColor : idleNameColor;
        playerNameText.color = playerTurn ? turnHighlightColor : idleNameColor;

        dealerCardOutline.effectColor = dealerTurn
            ? turnHighlightColor
            : idleCardOutlineColor;
        playerCardOutline.effectColor = playerTurn
            ? turnHighlightColor
            : idleCardOutlineColor;
        dealerCardOutline.effectDistance = dealerTurn
            ? new Vector2(4, -4)
            : new Vector2(2, -2);
        playerCardOutline.effectDistance = playerTurn
            ? new Vector2(4, -4)
            : new Vector2(2, -2);
    }

    private void RefreshResultOverlay()
    {
        bool isGameOver = gameState.Phase == GamePhase.GameOver &&
                          gameState.FinalWinner != GameWinner.None;
        bool isShowdownResult = roundWinner != RoundWinner.None &&
                                (gameState.Phase == GamePhase.RoundEnd ||
                                 gameState.Phase == GamePhase.GameOver);
        bool isFoldResult = gameState.RoundEndReason == RoundEndReason.Fold &&
                            (gameState.Phase == GamePhase.RoundEnd ||
                             gameState.Phase == GamePhase.GameOver);
        bool shouldShow = isGameOver || isShowdownResult || isFoldResult;

        resultOverlay.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        if (isGameOver)
        {
            resultTitleText.text = gameState.FinalWinner == GameWinner.Player
                ? "PLAYER WINS"
                : "DEALER WINS";
            string gameOverDetail = isFoldResult
                ? BuildFoldResultSummary()
                : BuildHandRankSummary();
            resultDetailText.text = "GAME OVER\n" + gameOverDetail;
            return;
        }

        if (isFoldResult)
        {
            bool playerWon = gameState.FoldedBy == TurnOwner.Dealer;
            resultTitleText.text = playerWon ? "PLAYER WIN" : "DEALER WIN";
            resultDetailText.text = BuildFoldResultSummary();
            return;
        }

        if (roundWinner == RoundWinner.Draw)
        {
            resultTitleText.text = "DRAW";
            resultDetailText.text = BuildHandRankSummary();
            return;
        }

        bool playerIsWinner = roundWinner == RoundWinner.Player;
        HandRank winnerHandRank = playerIsWinner
            ? playerHandRank
            : dealerHandRank;
        resultTitleText.text = playerIsWinner ? "PLAYER WIN" : "DEALER WIN";
        resultDetailText.text =
            HandRankGameText(winnerHandRank) + "\n" + BuildHandRankSummary();
    }

    private string BuildDebugInfo()
    {
        return
            $"Phase          {gameState.Phase}\n" +
            $"Current Turn   {gameState.CurrentTurn}\n" +
            $"Player Rank    {playerHandRank}\n" +
            $"Dealer Rank    {dealerHandRank}\n" +
            $"Round Winner   {roundWinner}\n" +
            $"Game Winner    {gameState.FinalWinner}\n" +
            $"End Reason     {gameState.RoundEndReason}\n" +
            $"Player Bet     {gameState.Betting.PlayerTotalBet}\n" +
            $"Dealer Bet     {gameState.Betting.DealerTotalBet}\n" +
            $"Call Amount    " +
            $"{gameState.Betting.GetCallAmount(gameState.CurrentTurn)}\n" +
            $"Folded By      {gameState.FoldedBy}\n" +
            $"Fold Penalty   {BuildFoldPenaltyDebugText()}\n" +
            $"Deck Remaining {gameState.Deck.RemainingCount}";
    }

    private string BuildFoldResultSummary()
    {
        string foldedBy = gameState.FoldedBy == TurnOwner.Player
            ? "PLAYER FOLD"
            : "DEALER FOLD";

        if (gameState.FoldPenaltyAmount == 0)
        {
            return foldedBy;
        }

        return foldedBy + " · PENALTY -" + gameState.FoldPenaltyAmount;
    }

    private string BuildFoldPenaltyDebugText()
    {
        if (gameState.FoldPenaltyAmount == 0)
        {
            return "0";
        }

        string foldedBy = gameState.FoldedBy == TurnOwner.Player
            ? "Player"
            : "Dealer";
        return foldedBy + " -" + gameState.FoldPenaltyAmount;
    }

    private string BuildHandRankSummary()
    {
        return
            $"PLAYER {HandRankGameText(playerHandRank)}  ·  " +
            $"DEALER {HandRankGameText(dealerHandRank)}";
    }

    private bool HasRequiredReferences()
    {
        return phaseText != null &&
               turnText != null &&
               dealerCardText != null &&
               dealerChipsText != null &&
               dealerTotalBetText != null &&
               communityCard1Text != null &&
               communityCard2Text != null &&
               potText != null &&
               playerCardText != null &&
               playerChipsText != null &&
               playerTotalBetText != null &&
               dealerNameText != null &&
               playerNameText != null &&
               dealerCardOutline != null &&
               playerCardOutline != null &&
               resultOverlay != null &&
               resultTitleText != null &&
               resultDetailText != null &&
               debugPanel != null &&
               debugInfoText != null &&
               messageText != null &&
               debugToggleButton != null &&
               dealerActionBar != null &&
               playerActionBar != null &&
               contextActionArea != null &&
               HasAllBettingActionButtons() &&
               resolveShowdownButton != null &&
               nextRoundButton != null;
    }

    private bool HasAllBettingActionButtons()
    {
        if (bettingActionButtons == null || bettingActionButtons.Length != 10)
        {
            return false;
        }

        for (int index = 0; index < bettingActionButtons.Length; index++)
        {
            if (bettingActionButtons[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyUiFont()
    {
        if (uiFont == null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int index = 0; index < texts.Length; index++)
        {
            texts[index].font = uiFont;
        }
    }

    private void ResetDisplayedRoundResult()
    {
        playerHandRank = HandRank.None;
        dealerHandRank = HandRank.None;
        roundWinner = RoundWinner.None;
    }

    private void AddLog(string message)
    {
        logs.Add(message);

        while (logs.Count > Mathf.Max(1, maxLogLines))
        {
            logs.RemoveAt(0);
        }
    }

    private static string CardText(Card card)
    {
        return card == null ? "-" : card.ToString();
    }

    private static string PhaseGameText(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Setup:
                return "SETUP";
            case GamePhase.Betting:
                return "BETTING";
            case GamePhase.Showdown:
                return "SHOWDOWN";
            case GamePhase.RoundEnd:
                return "ROUND END";
            case GamePhase.GameOver:
                return "GAME OVER";
            default:
                return "UNKNOWN";
        }
    }

    private static string TurnGameText(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return "PLAYER TURN";
            case TurnOwner.Dealer:
                return "DEALER TURN";
            default:
                return string.Empty;
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
            case DealerDecision.Check:
                return "CHECK";
            case DealerDecision.Call:
                return "CALL";
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

    private static string HandRankGameText(HandRank handRank)
    {
        switch (handRank)
        {
            case HandRank.Number:
                return "NUMBER";
            case HandRank.Double:
                return "DOUBLE";
            case HandRank.Straight:
                return "STRAIGHT";
            case HandRank.Triple:
                return "TRIPLE";
            default:
                return "-";
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
            default:
                return "없음";
        }
    }
}
