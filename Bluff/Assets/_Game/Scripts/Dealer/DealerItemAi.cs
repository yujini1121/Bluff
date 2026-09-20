using System;
using System.Collections.Generic;

public readonly struct DealerItemPlan
{
    public static DealerItemPlan None => default;
    public static DealerItemPlan RefreshCard =>
        new DealerItemPlan(ItemType.refreshCard);
    public static DealerItemPlan PrizmChip =>
        new DealerItemPlan(ItemType.prizmChip);
    public static DealerItemPlan ChipPocket =>
        new DealerItemPlan(ItemType.chipPocket);
    public static DealerItemPlan Defy =>
        new DealerItemPlan(ItemType.defy);

    public ItemType? SelectedItem { get; }
    public bool ShouldUseItem => SelectedItem.HasValue;

    private DealerItemPlan(ItemType selectedItem)
    {
        SelectedItem = selectedItem;
    }
}

public sealed class DealerItemAi
{
    private const int InitialAnteBet = 1;
    private const int LowChipThreshold = 5;
    private const double LowEquityThreshold = 0.35d;
    private const double BurdensomeCallRatio = 0.5d;

    private readonly DealerAi dealerAi = new DealerAi();

    public DealerItemPlan Decide(
        GameState gameState,
        IEnumerable<ItemType> availableItems,
        DealerActionPlan actionPlan)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        if (availableItems == null)
        {
            throw new ArgumentNullException(nameof(availableItems));
        }

        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            return DealerItemPlan.None;
        }

        var itemSet = new HashSet<ItemType>(availableItems);
        int callAmount =
            gameState.Betting.GetCallAmount(TurnOwner.Dealer);

        if (itemSet.Contains(ItemType.prizmChip) &&
            callAmount > 0 &&
            actionPlan.Decision == DealerDecision.Fold)
        {
            return DealerItemPlan.PrizmChip;
        }

        if (itemSet.Contains(ItemType.defy) &&
            IsContinuingAction(actionPlan.Decision) &&
            IsBurdensomeCall(callAmount, gameState.DealerChips.Count) &&
            TryGetExpectedEquity(gameState, out double defyEquity) &&
            defyEquity >= LowEquityThreshold)
        {
            return DealerItemPlan.Defy;
        }

        if (itemSet.Contains(ItemType.refreshCard) &&
            IsBeforeVoluntaryBetting(gameState, callAmount) &&
            TryGetExpectedEquity(gameState, out double refreshEquity) &&
            refreshEquity < LowEquityThreshold)
        {
            return DealerItemPlan.RefreshCard;
        }

        if (itemSet.Contains(ItemType.chipPocket) &&
            gameState.DealerChips.Count <= LowChipThreshold)
        {
            return DealerItemPlan.ChipPocket;
        }

        return DealerItemPlan.None;
    }

    private bool TryGetExpectedEquity(
        GameState gameState,
        out double expectedEquity)
    {
        expectedEquity = 0d;

        if (!dealerAi.TryEvaluate(gameState, out DealerHandOdds odds))
        {
            return false;
        }

        expectedEquity = odds.ExpectedEquity;
        return true;
    }

    private static bool IsBeforeVoluntaryBetting(
        GameState gameState,
        int callAmount)
    {
        return callAmount == 0 &&
               gameState.Betting.PlayerTotalBet ==
               gameState.Betting.DealerTotalBet &&
               gameState.Betting.PlayerTotalBet <= InitialAnteBet;
    }

    private static bool IsContinuingAction(DealerDecision decision)
    {
        return decision == DealerDecision.Call ||
               decision == DealerDecision.Raise ||
               decision == DealerDecision.AllIn;
    }

    private static bool IsBurdensomeCall(
        int callAmount,
        int dealerChips)
    {
        return callAmount > 0 &&
               dealerChips > 0 &&
               callAmount / (double)dealerChips >= BurdensomeCallRatio;
    }
}
