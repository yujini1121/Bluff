using Michsky.UI.Shift;
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
    [SerializeField] private ResultFlowText resultText;
    [SerializeField] private GameObject finalResultContent;
    [SerializeField] private TMP_Text finalResultTitleText;
    [SerializeField] private TMP_Text finalResultDetailText;

    [Header("Gameplay HUD")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private GameObject dealerHandRankUI;
    [SerializeField] private TMP_Text dealerHandRankText;
    [SerializeField] private SwitchCamera switchCamera;
    [SerializeField] private ItemUseNotice itemUseNoticePrefab;

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
    [SerializeField] private TMP_Text raiseAmountText; // 일단 살려둠
    [SerializeField] private Slider raiseSlider;
    [SerializeField] private Button raiseExecuteButton;

    private readonly List<TMP_Text> callActionTexts = new List<TMP_Text>();
    private readonly List<Button> callActionButtons = new List<Button>();
    private ItemUseNotice itemUseNotice;
    public CanvasGroup GameplayHud => GetComponent<CanvasGroup>();

    public void Initialize()
    {
        ApplyUiFont();
        CacheCallActionTexts();
    }

    public void ShowItemUseFailure(ItemUseFailureReason reason)
    {
        if (!isActiveAndEnabled || roundText == null)
        {
            return;
        }

        if (itemUseNotice == null)
        {
            Canvas canvas = roundText.canvas;
            if (canvas == null || itemUseNoticePrefab == null) return;
            itemUseNotice = ItemUseNotice.Create(itemUseNoticePrefab, canvas.rootCanvas, uiFont != null ? uiFont : roundText.font);
        }

        itemUseNotice.Show(reason);
    }

    public void HideItemUseFailure()
    {
        if (itemUseNotice != null) itemUseNotice.Hide();
    }

    private void OnDisable()
    {
        HideItemUseFailure();
    }

    public bool HasReferences()
    {
        return resultOverlay != null &&
               resultText != null && finalResultContent != null &&
               finalResultTitleText != null && finalResultDetailText != null &&
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
               raiseAmountText != null &&
               raiseExecuteButton != null &&
               resolveShowdownButton != null &&
               nextRoundButton != null &&
               restartButton != null;
    }

    public void RefreshRound(GameMode gameMode, int currentRound)
    {
        roundText.text = gameMode == GameMode.RoundLimited
            ? $"Round {currentRound} / {GameState.MaximumRoundCount}"
            : $"Round {currentRound}";
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
            !switchCamera.IsDealerCameraSettled ||
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

        string actionText = useAllInText ? "All in" : "Call";

        for (int index = 0; index < callActionTexts.Count; index++)
        {
            callActionTexts[index].text = actionText;
        }

        bool canRaise = canAcceptPlayerBettingInput && maxRaiseBy > 0;

        raiseSlider.interactable = canRaise;
        raiseSlider.maxValue = maxRaiseBy;
        raiseSlider.value = selectedRaiseBy;
        raiseExecuteButton.interactable = canRaise;
        raiseExecuteButton.GetComponentInChildren<MainButton>().buttonText = maxRaiseBy > 0 &&
                                selectedRaiseBy == maxRaiseBy
            ? "All in [A]"
            : $"Raise (+{selectedRaiseBy}) [R]";
    }

    public void RefreshProgress(
        bool showResolveShowdown,
        bool showNextRound,
        bool canResolveShowdown,
        bool canStartNextRound,
        bool viewEnding = false)
    {
        contextActionArea.SetActive(showResolveShowdown || showNextRound);
        resolveShowdownButton.gameObject.SetActive(showResolveShowdown);
        nextRoundButton.gameObject.SetActive(showNextRound);
        resolveShowdownButton.interactable = canResolveShowdown;
        nextRoundButton.interactable = canStartNextRound;
        if (resultText == null) return;
        string label = viewEnding ? resultText.viewEndingLabel : resultText.nextRoundLabel;
        var styledButton = nextRoundButton.GetComponent<MainButton>();
        if (styledButton != null) styledButton.buttonText = label;
        foreach (var text in nextRoundButton.GetComponentsInChildren<TMP_Text>(true))
            text.text = label;
    }

    public void RefreshResult(
        GameState gameState,
        RoundWinner roundWinner,
        HandRank playerHandRank,
        HandRank dealerHandRank,
        bool isShowdownResultVisible,
        bool isFoldResultVisible,
        bool canRestartGame,
        bool suppressResult = false)
    {
        // RefreshResult is called every frame, so hiding the object once is insufficient.
        if (suppressResult)
        {
            resultOverlay.SetActive(false);
            restartButton.interactable = false;
            return;
        }
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
        restartButton.gameObject.SetActive(false);
        restartButton.interactable = false;

        if (!shouldShow || resultText == null)
        {
            return;
        }
        bool useFinalContent = isGameOver && finalResultContent != null;
        if (finalResultContent != null) finalResultContent.SetActive(useFinalContent);
        resultTitleText.gameObject.SetActive(!useFinalContent);
        resultDetailText.gameObject.SetActive(!useFinalContent);

        if (isFoldResult)
        {
            bool playerWon = gameState.FoldedBy == TurnOwner.Dealer;
            resultTitleText.text = playerWon ? resultText.roundPlayerWin : resultText.roundDealerWin;
            resultDetailText.text = BuildFoldResultSummary(gameState);
        }
        else if (roundWinner == RoundWinner.Draw)
        {
            resultTitleText.text = resultText.roundDraw;
            resultDetailText.text = string.Format(resultText.carriedPotFormat, gameState.Pot.Amount);
        }
        else if (isShowdownResult)
        {
            bool playerIsWinner = roundWinner == RoundWinner.Player;
            resultTitleText.text = playerIsWinner ? resultText.roundPlayerWin : resultText.roundDealerWin;
            SetWinningHandDetail(gameState, playerHandRank, dealerHandRank, playerIsWinner);
        }
        else
        {
            resultTitleText.text = resultText.matchComplete;
            resultDetailText.text = string.Empty;
        }

        if (isGameOver)
        {
            string matchResult = gameState.FinalWinner == GameWinner.Draw ? resultText.matchDraw :
                gameState.FinalWinner == GameWinner.Player ? resultText.matchPlayerWin : resultText.matchDealerWin;
            resultDetailText.text = string.Format(resultText.finalSummaryFormat, resultDetailText.text,
                matchResult, gameState.PlayerChips.Count, gameState.DealerChips.Count);
            if (useFinalContent)
            {
                finalResultTitleText.text = resultTitleText.text;
                finalResultDetailText.text = resultDetailText.text;
            }
        }
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

    private string BuildFoldResultSummary(GameState gameState)
    {
        string foldedBy = gameState.FoldedBy == TurnOwner.Player
            ? resultText.playerFold
            : resultText.dealerFold;

        if (gameState.FoldPenaltyAmount == 0)
        {
            return foldedBy;
        }

        return string.Format(resultText.foldPenaltyFormat, foldedBy, gameState.FoldPenaltyAmount);
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

    private string HandRankGameText(HandRank handRank)
    {
        switch (handRank)
        {
            case HandRank.Number:
                return resultText.numberHand;
            case HandRank.Double:
                return resultText.doubleHand;
            case HandRank.Straight:
                return resultText.straightHand;
            case HandRank.Triple:
                return resultText.tripleHand;
            default:
                return resultText.unknownHand;
        }
    }

    private string BuildHandDisplayText(
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
