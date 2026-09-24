using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayView : MonoBehaviour
{
    [Header("UI Font")]
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("Result UI")]
    [SerializeField] private GameObject resultOverlay;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultDetailText;
    [SerializeField] private Button restartButton;

    [Header("Gameplay HUD")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private GameObject dealerHandRankUI;
    [SerializeField] private TMP_Text dealerHandRankText;
    [SerializeField] private SwitchCamera switchCamera;

    [Header("Debug UI")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TMP_Text debugInfoText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button debugToggleButton;

    [Header("Action UI")]
    [SerializeField] private GameObject playerActionBar;
    [SerializeField] private GameObject contextActionArea;
    [SerializeField] private Button[] bettingActionButtons;
    [SerializeField] private Button resolveShowdownButton;
    [SerializeField] private Button nextRoundButton;

    [Header("Raise UI")]
    [SerializeField] private Button raiseDecreaseButton;
    [SerializeField] private TMP_Text raiseAmountText;
    [SerializeField] private Button raiseIncreaseButton;
    [SerializeField] private Button raiseMaxButton;
    [SerializeField] private Button raiseExecuteButton;
    [SerializeField] private TMP_Text raiseExecuteText;

    private readonly List<TMP_Text> callActionTexts = new List<TMP_Text>();
    private readonly List<Button> callActionButtons = new List<Button>();

    public void Initialize()
    {
        ApplyUiFont();
        CacheCallActionTexts();
    }

    public bool HasReferences()
    {
        return resultOverlay != null &&
               resultTitleText != null &&
               resultDetailText != null &&
               roundText != null &&
               debugPanel != null &&
               debugInfoText != null &&
               messageText != null &&
               debugToggleButton != null &&
               playerActionBar != null &&
               contextActionArea != null &&
               HasAllBettingActionButtons() &&
               raiseDecreaseButton != null &&
               raiseAmountText != null &&
               raiseIncreaseButton != null &&
               raiseMaxButton != null &&
               raiseExecuteButton != null &&
               raiseExecuteText != null &&
               resolveShowdownButton != null &&
               nextRoundButton != null &&
               restartButton != null;
    }

    public void RefreshRound(GameMode gameMode, int currentRound)
    {
        roundText.text = gameMode == GameMode.RoundLimited
            ? $"ROUND {currentRound} / {GameState.MaximumRoundCount}"
            : $"ROUND {currentRound}";
    }

    public void RefreshDealerHand(GameState gameState)
    {
        if (dealerHandRankUI == null)
        {
            return;
        }

        if (dealerHandRankText == null ||
            switchCamera == null ||
            gameState.Phase != GamePhase.Betting ||
            switchCamera.CurrentView != CameraView.Dealer ||
            !gameState.TryGetVisibleDealerHandRank(
                out HandRank visibleDealerHandRank))
        {
            dealerHandRankUI.SetActive(false);
            return;
        }

        dealerHandRankText.text = BuildHandDisplayText(
            gameState.DealerCard,
            gameState.CommunityCard1,
            gameState.CommunityCard2,
            visibleDealerHandRank);
        dealerHandRankUI.SetActive(true);
    }

    public void RefreshDebug(
        GameState gameState,
        HandRank playerHandRank,
        HandRank dealerHandRank,
        RoundWinner roundWinner,
        bool debugPanelOpen,
        IReadOnlyList<string> logs)
    {
        debugInfoText.text = BuildDebugInfo(
            gameState,
            playerHandRank,
            dealerHandRank,
            roundWinner);
        messageText.text = string.Join("\n", logs);
        debugPanel.SetActive(debugPanelOpen);
    }

    public void RefreshBetting(
        bool playerTurn,
        bool canAcceptPlayerBettingInput,
        bool canCall,
        bool useAllInText,
        int selectedRaiseBy,
        int maxRaiseBy)
    {
        playerActionBar.SetActive(playerTurn);

        for (int index = 0; index < bettingActionButtons.Length; index++)
        {
            bettingActionButtons[index].interactable =
                canAcceptPlayerBettingInput;
        }

        for (int index = 0; index < callActionButtons.Count; index++)
        {
            callActionButtons[index].interactable = canCall;
        }

        string actionText = useAllInText ? "ALL IN" : "CALL";

        for (int index = 0; index < callActionTexts.Count; index++)
        {
            callActionTexts[index].text = actionText;
        }

        bool canRaise = canAcceptPlayerBettingInput && maxRaiseBy > 0;
        raiseDecreaseButton.interactable =
            canRaise && selectedRaiseBy > 1;
        raiseIncreaseButton.interactable =
            canRaise && selectedRaiseBy < maxRaiseBy;
        raiseMaxButton.interactable =
            canRaise && selectedRaiseBy < maxRaiseBy;
        raiseExecuteButton.interactable = canRaise;

        raiseAmountText.text = maxRaiseBy > 0
            ? $"+{selectedRaiseBy}"
            : "+0";
        raiseExecuteText.text = maxRaiseBy > 0 &&
                                selectedRaiseBy == maxRaiseBy
            ? "ALL IN"
            : "RAISE";
    }

    public void RefreshProgress(
        bool showResolveShowdown,
        bool showNextRound,
        bool canResolveShowdown,
        bool canStartNextRound)
    {
        contextActionArea.SetActive(showResolveShowdown || showNextRound);
        resolveShowdownButton.gameObject.SetActive(showResolveShowdown);
        nextRoundButton.gameObject.SetActive(showNextRound);
        resolveShowdownButton.interactable = canResolveShowdown;
        nextRoundButton.interactable = canStartNextRound;
    }

    public void RefreshResult(
        GameState gameState,
        RoundWinner roundWinner,
        HandRank playerHandRank,
        HandRank dealerHandRank,
        bool isShowdownResultVisible,
        bool isFoldResultVisible,
        bool canRestartGame)
    {
        bool hasSettledShowdown =
            gameState.RoundEndReason == RoundEndReason.Showdown &&
            roundWinner != RoundWinner.None &&
            (gameState.Phase == GamePhase.RoundEnd ||
             gameState.Phase == GamePhase.GameOver);
        bool hasFoldResult =
            gameState.RoundEndReason == RoundEndReason.Fold &&
            (gameState.Phase == GamePhase.RoundEnd ||
             gameState.Phase == GamePhase.GameOver);
        bool isGameOver = gameState.Phase == GamePhase.GameOver &&
                          gameState.FinalWinner != GameWinner.None &&
                          (!hasSettledShowdown ||
                           isShowdownResultVisible) &&
                          (!hasFoldResult || isFoldResultVisible);
        bool isShowdownResult = hasSettledShowdown &&
                                isShowdownResultVisible;
        bool isFoldResult = hasFoldResult && isFoldResultVisible;
        bool shouldShow = isGameOver || isShowdownResult || isFoldResult;

        resultOverlay.SetActive(shouldShow);
        restartButton.gameObject.SetActive(isGameOver);
        restartButton.interactable = isGameOver && canRestartGame;

        if (!shouldShow)
        {
            return;
        }

        if (isGameOver)
        {
            switch (gameState.FinalWinner)
            {
                case GameWinner.Player:
                    resultTitleText.text = "PLAYER WINS";
                    break;
                case GameWinner.Dealer:
                    resultTitleText.text = "DEALER WINS";
                    break;
                case GameWinner.Draw:
                    resultTitleText.text = "DRAW";
                    break;
                default:
                    resultTitleText.text = string.Empty;
                    break;
            }

            bool isFinalRoundLimitedResult =
                gameState.GameMode == GameMode.RoundLimited &&
                gameState.CurrentRound >= GameState.MaximumRoundCount;
            if (isFinalRoundLimitedResult)
            {
                resultTitleText.text =
                    "MATCH RESULT\n\n" + resultTitleText.text;
                resultDetailText.text = BuildFinalChipSummary(gameState);
                return;
            }

            if (isFoldResult)
            {
                resultDetailText.text = BuildFoldResultSummary(gameState);
                return;
            }

            if (gameState.FinalWinner == GameWinner.Draw ||
                roundWinner == RoundWinner.Draw)
            {
                resultDetailText.text = string.Empty;
                return;
            }

            SetWinningHandDetail(
                gameState,
                playerHandRank,
                dealerHandRank,
                gameState.FinalWinner == GameWinner.Player);
            return;
        }

        if (isFoldResult)
        {
            bool playerWon = gameState.FoldedBy == TurnOwner.Dealer;
            resultTitleText.text = playerWon ? "PLAYER WIN" : "DEALER WIN";
            resultDetailText.text = BuildFoldResultSummary(gameState);
            return;
        }

        if (roundWinner == RoundWinner.Draw)
        {
            resultTitleText.text = "DRAW";
            resultDetailText.text = string.Empty;
            return;
        }

        bool playerIsWinner = roundWinner == RoundWinner.Player;
        resultTitleText.text = playerIsWinner ? "PLAYER WIN" : "DEALER WIN";
        SetWinningHandDetail(
            gameState,
            playerHandRank,
            dealerHandRank,
            playerIsWinner);
    }

    public void SetRestartEnabled(bool enabled)
    {
        restartButton.interactable = enabled;
    }

    private void OnValidate()
    {
        ApplyUiFont();
    }

    private void CacheCallActionTexts()
    {
        callActionTexts.Clear();
        callActionButtons.Clear();

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
                    nameof(GameplayController.OnCallClicked))
                {
                    continue;
                }

                TMP_Text actionText =
                    button.GetComponentInChildren<TMP_Text>(true);

                if (actionText != null)
                {
                    callActionTexts.Add(actionText);
                }

                callActionButtons.Add(button);
                break;
            }
        }
    }

    private bool HasAllBettingActionButtons()
    {
        if (bettingActionButtons == null || bettingActionButtons.Length == 0)
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

    private static string BuildDebugInfo(
        GameState gameState,
        HandRank playerHandRank,
        HandRank dealerHandRank,
        RoundWinner roundWinner)
    {
        return
            $"Game Mode      {gameState.GameMode}\n" +
            $"Round          {gameState.CurrentRound}\n" +
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
            $"Fold Penalty   {BuildFoldPenaltyDebugText(gameState)}\n" +
            $"Deck Remaining {gameState.Deck.RemainingCount}";
    }

    private static string BuildFoldResultSummary(GameState gameState)
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

    private static string BuildFoldPenaltyDebugText(GameState gameState)
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

    private void SetWinningHandDetail(
        GameState gameState,
        HandRank playerHandRank,
        HandRank dealerHandRank,
        bool playerIsWinner)
    {
        Card winnerCard = playerIsWinner
            ? gameState.PlayerCard
            : gameState.DealerCard;
        HandRank winnerHandRank = playerIsWinner
            ? playerHandRank
            : dealerHandRank;

        resultDetailText.text = BuildHandDisplayText(
            winnerCard,
            gameState.CommunityCard1,
            gameState.CommunityCard2,
            winnerHandRank);
    }

    private static string BuildFinalChipSummary(GameState gameState)
    {
        return
            "FINAL CHIPS" +
            $"PLAYER {gameState.PlayerChips.Count} · " +
            $"DEALER {gameState.DealerChips.Count}";
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

    private static string BuildHandDisplayText(
        Card privateCard,
        Card communityCard1,
        Card communityCard2,
        HandRank handRank)
    {
        if (privateCard == null ||
            communityCard1 == null ||
            communityCard2 == null)
        {
            return HandRankGameText(handRank);
        }

        int displayRank;

        switch (handRank)
        {
            case HandRank.Number:
                displayRank = privateCard.Rank;
                return $"{displayRank}  {HandRankGameText(handRank)}";
            case HandRank.Triple:
                displayRank = privateCard.Rank;
                return $"{displayRank}  {HandRankGameText(handRank)}";
            case HandRank.Double:
                displayRank = privateCard.Rank == communityCard1.Rank ||
                              privateCard.Rank == communityCard2.Rank
                    ? privateCard.Rank
                    : communityCard1.Rank;
                return $"{displayRank}  {HandRankGameText(handRank)}";
            case HandRank.Straight:
                return $"{BuildStraightRankText(privateCard, communityCard1, communityCard2)}  " +
                       HandRankGameText(handRank);
            default:
                return HandRankGameText(handRank);
        }
    }

    private static string BuildStraightRankText(
        Card privateCard,
        Card communityCard1,
        Card communityCard2)
    {
        int[] ranks =
        {
            privateCard.Rank,
            communityCard1.Rank,
            communityCard2.Rank
        };
        Array.Sort(ranks);

        if (ranks[0] == 1 && ranks[1] == 9 && ranks[2] == 10)
        {
            return "9 · 10 · 1";
        }

        if (ranks[0] == 1 && ranks[1] == 2 && ranks[2] == 10)
        {
            return "10 · 1 · 2";
        }

        return $"{ranks[0]} · {ranks[1]} · {ranks[2]}";
    }
}
