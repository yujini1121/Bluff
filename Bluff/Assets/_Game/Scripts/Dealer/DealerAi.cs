using System;

public enum DealerDecision
{
    None,
    Call,
    Raise,
    Fold,
    AllIn
}

public readonly struct DealerHandOdds
{
    public double WinProbability { get; }
    public double DrawProbability { get; }
    public double LossProbability { get; }
    public double ExpectedEquity =>
        WinProbability + DrawProbability * 0.5d;

    public DealerHandOdds(
        double winProbability,
        double drawProbability,
        double lossProbability)
    {
        WinProbability = winProbability;
        DrawProbability = drawProbability;
        LossProbability = lossProbability;
    }
}

public readonly struct DealerActionPlan
{
    public static DealerActionPlan None =>
        new DealerActionPlan(DealerDecision.None, 0);

    public DealerDecision Decision { get; }
    public int RaiseBy { get; }

    public DealerActionPlan(DealerDecision decision, int raiseBy = 0)
    {
        Decision = decision;
        RaiseBy = raiseBy;
    }
}

public sealed class DealerAi
{
    private const int MinimumCardRank = 1;
    private const int MaximumCardRank = 10;
    private const int CopiesPerRank = 4;
    private const double CallRiskPenalty = 0.15d;
    private const double PotOddsInfluence = 0.15d;

    private enum StrengthBand
    {
        Low,
        Medium,
        High
    }

    private readonly struct DealerContext
    {
        public int CallAmount { get; }
        public int DealerChips { get; }
        public int MaxRaiseBy { get; }
        public int AllInRaiseBy { get; }
        public double AdjustedEquity { get; }

        public bool IsShortAllIn => CallAmount > DealerChips;
        public bool CanRaise => MaxRaiseBy > 0;

        public DealerContext(
            int callAmount,
            int dealerChips,
            int maxRaiseBy,
            int allInRaiseBy,
            double adjustedEquity)
        {
            CallAmount = callAmount;
            DealerChips = dealerChips;
            MaxRaiseBy = maxRaiseBy;
            AllInRaiseBy = allInRaiseBy;
            AdjustedEquity = adjustedEquity;
        }
    }

    public bool TryEvaluate(
        GameState gameState,
        out DealerHandOdds odds)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        odds = default;

        if (gameState.PlayerCard == null ||
            gameState.CommunityCard1 == null ||
            gameState.CommunityCard2 == null)
        {
            return false;
        }

        int winWeight = 0;
        int drawWeight = 0;
        int lossWeight = 0;

        for (int rank = MinimumCardRank; rank <= MaximumCardRank; rank++)
        {
            int candidateWeight = GetRemainingRankCount(
                rank,
                gameState.PlayerCard,
                gameState.CommunityCard1,
                gameState.CommunityCard2);

            if (candidateWeight <= 0)
            {
                continue;
            }

            RoundWinner winner = EvaluateCandidate(
                rank,
                gameState.PlayerCard,
                gameState.CommunityCard1,
                gameState.CommunityCard2);

            if (winner == RoundWinner.Dealer)
            {
                winWeight += candidateWeight;
            }
            else if (winner == RoundWinner.Draw)
            {
                drawWeight += candidateWeight;
            }
            else if (winner == RoundWinner.Player)
            {
                lossWeight += candidateWeight;
            }
        }

        int totalWeight = winWeight + drawWeight + lossWeight;

        if (totalWeight <= 0)
        {
            return false;
        }

        odds = new DealerHandOdds(
            winWeight / (double)totalWeight,
            drawWeight / (double)totalWeight,
            lossWeight / (double)totalWeight);
        return true;
    }

    public DealerActionPlan Decide(
        GameState gameState,
        int actionRoll,
        int raiseRoll)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer ||
            !TryCreateContext(gameState, out DealerContext context))
        {
            return DealerActionPlan.None;
        }

        DealerDecision decision = SelectDecision(
            context,
            NormalizeRoll(actionRoll));

        if (decision != DealerDecision.Raise)
        {
            return new DealerActionPlan(decision);
        }

        return CreateRaisePlan(context, NormalizeRoll(raiseRoll));
    }

    public bool TryExecute(GameState gameState, DealerActionPlan plan)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            return false;
        }

        switch (plan.Decision)
        {
            case DealerDecision.Call:
                return gameState.TryCall();
            case DealerDecision.Raise:
                return plan.RaiseBy > 0 &&
                       gameState.TryRaise(plan.RaiseBy);
            case DealerDecision.Fold:
                return gameState.TryFold();
            case DealerDecision.AllIn:
                return gameState.TryAllIn();
            default:
                return false;
        }
    }

    private bool TryCreateContext(
        GameState gameState,
        out DealerContext context)
    {
        context = default;

        if (!TryEvaluate(gameState, out DealerHandOdds odds))
        {
            return false;
        }

        int callAmount =
            gameState.Betting.GetCallAmount(TurnOwner.Dealer);
        int dealerChips = gameState.DealerChips.Count;
        int maxRaiseBy = CalculateMaxRaiseBy(gameState, callAmount);
        int allInRaiseBy = dealerChips > callAmount
            ? dealerChips - callAmount
            : 0;
        double callRisk = callAmount > 0 && dealerChips > 0
            ? Math.Min(1d, callAmount / (double)dealerChips)
            : 0d;
        double potOddsAdjustment = CalculatePotOddsAdjustment(
            gameState.Pot.Amount,
            callAmount);
        double adjustedEquity = Math.Max(
            0d,
            Math.Min(
                1d,
                odds.ExpectedEquity -
                callRisk * CallRiskPenalty +
                potOddsAdjustment));

        context = new DealerContext(
            callAmount,
            dealerChips,
            maxRaiseBy,
            allInRaiseBy,
            adjustedEquity);
        return true;
    }

    private static double CalculatePotOddsAdjustment(
        int currentPot,
        int callAmount)
    {
        if (callAmount <= 0)
        {
            return 0d;
        }

        double potAfterCall = currentPot + (double)callAmount;
        double requiredEquity = callAmount / potAfterCall;
        double potOddsAdvantage = 0.5d - requiredEquity;
        return potOddsAdvantage * PotOddsInfluence;
    }

    private static DealerDecision SelectDecision(
        DealerContext context,
        int actionRoll)
    {
        if (context.DealerChips <= 0)
        {
            return DealerDecision.Fold;
        }

        StrengthBand strength = GetStrengthBand(context.AdjustedEquity);

        if (context.IsShortAllIn)
        {
            return SelectFoldOrAllIn(strength, actionRoll);
        }

        if (context.CallAmount == 0)
        {
            return context.CanRaise
                ? SelectOpeningDecision(strength, actionRoll)
                : DealerDecision.Fold;
        }

        if (!context.CanRaise)
        {
            return SelectFoldOrCall(strength, actionRoll);
        }

        return SelectFacingBetDecision(strength, actionRoll);
    }

    private static DealerDecision SelectOpeningDecision(
        StrengthBand strength,
        int roll)
    {
        int foldChance;

        switch (strength)
        {
            case StrengthBand.Low:
                foldChance = 45;
                break;
            case StrengthBand.Medium:
                foldChance = 20;
                break;
            default:
                foldChance = 5;
                break;
        }

        return roll < foldChance
            ? DealerDecision.Fold
            : DealerDecision.Raise;
    }

    private static DealerDecision SelectFacingBetDecision(
        StrengthBand strength,
        int roll)
    {
        switch (strength)
        {
            case StrengthBand.Low:
                return SelectThreeWay(roll, 65, 30);
            case StrengthBand.Medium:
                return SelectThreeWay(roll, 20, 60);
            default:
                return SelectThreeWay(roll, 2, 28);
        }
    }

    private static DealerDecision SelectFoldOrCall(
        StrengthBand strength,
        int roll)
    {
        int foldChance;

        switch (strength)
        {
            case StrengthBand.Low:
                foldChance = 65;
                break;
            case StrengthBand.Medium:
                foldChance = 20;
                break;
            default:
                foldChance = 2;
                break;
        }

        return roll < foldChance
            ? DealerDecision.Fold
            : DealerDecision.Call;
    }

    private static DealerDecision SelectFoldOrAllIn(
        StrengthBand strength,
        int roll)
    {
        int foldChance;

        switch (strength)
        {
            case StrengthBand.Low:
                foldChance = 70;
                break;
            case StrengthBand.Medium:
                foldChance = 30;
                break;
            default:
                foldChance = 5;
                break;
        }

        return roll < foldChance
            ? DealerDecision.Fold
            : DealerDecision.AllIn;
    }

    private static DealerDecision SelectThreeWay(
        int roll,
        int foldChance,
        int callChance)
    {
        if (roll < foldChance)
        {
            return DealerDecision.Fold;
        }

        return roll < foldChance + callChance
            ? DealerDecision.Call
            : DealerDecision.Raise;
    }

    private static DealerActionPlan CreateRaisePlan(
        DealerContext context,
        int raiseRoll)
    {
        if (!context.CanRaise)
        {
            return context.CallAmount > 0 &&
                   context.CallAmount <= context.DealerChips
                ? new DealerActionPlan(DealerDecision.Call)
                : new DealerActionPlan(DealerDecision.Fold);
        }

        StrengthBand strength = GetStrengthBand(context.AdjustedEquity);
        int raiseBy = SelectRaiseBy(
            context.MaxRaiseBy,
            strength,
            raiseRoll);

        if (raiseBy == context.AllInRaiseBy)
        {
            return new DealerActionPlan(DealerDecision.AllIn, raiseBy);
        }

        return new DealerActionPlan(DealerDecision.Raise, raiseBy);
    }

    private static int SelectRaiseBy(
        int maxRaiseBy,
        StrengthBand strength,
        int roll)
    {
        int smallRaise = GetRaiseFraction(maxRaiseBy, 0.25d);
        int mediumRaise = GetRaiseFraction(maxRaiseBy, 0.5d);
        int largeRaise = GetRaiseFraction(maxRaiseBy, 0.75d);

        switch (strength)
        {
            case StrengthBand.Low:
                if (roll < 70)
                {
                    return smallRaise;
                }

                return roll < 95 ? mediumRaise : largeRaise;
            case StrengthBand.Medium:
                if (roll < 40)
                {
                    return smallRaise;
                }

                if (roll < 75)
                {
                    return mediumRaise;
                }

                return roll < 95 ? largeRaise : maxRaiseBy;
            default:
                if (roll < 20)
                {
                    return smallRaise;
                }

                if (roll < 50)
                {
                    return mediumRaise;
                }

                return roll < 80 ? largeRaise : maxRaiseBy;
        }
    }

    private static int GetRaiseFraction(int maxRaiseBy, double fraction)
    {
        return Math.Max(
            1,
            Math.Min(maxRaiseBy, (int)Math.Ceiling(maxRaiseBy * fraction)));
    }

    private static int CalculateMaxRaiseBy(
        GameState gameState,
        int callAmount)
    {
        if (gameState.PlayerChips.Count <= 0 ||
            callAmount < 0 ||
            callAmount >= gameState.DealerChips.Count)
        {
            return 0;
        }

        long maxRaiseBy = gameState.DealerChips.Count - (long)callAmount;
        long potCapacity = int.MaxValue -
                           (long)gameState.Pot.Amount -
                           callAmount;
        long betCapacity = int.MaxValue -
                           (long)gameState.Betting.DealerTotalBet -
                           callAmount;
        maxRaiseBy = Math.Min(maxRaiseBy, potCapacity);
        maxRaiseBy = Math.Min(maxRaiseBy, betCapacity);

        return maxRaiseBy > 0 ? (int)maxRaiseBy : 0;
    }

    private static StrengthBand GetStrengthBand(double equity)
    {
        if (equity < 0.35d)
        {
            return StrengthBand.Low;
        }

        return equity < 0.65d
            ? StrengthBand.Medium
            : StrengthBand.High;
    }

    private static int GetRemainingRankCount(
        int rank,
        Card playerCard,
        Card communityCard1,
        Card communityCard2)
    {
        int remaining = CopiesPerRank;

        if (playerCard.Rank == rank)
        {
            remaining--;
        }

        if (communityCard1.Rank == rank)
        {
            remaining--;
        }

        if (communityCard2.Rank == rank)
        {
            remaining--;
        }

        return remaining;
    }

    private static RoundWinner EvaluateCandidate(
        int dealerRank,
        Card playerCard,
        Card communityCard1,
        Card communityCard2)
    {
        var simulation = new GameState(
            0,
            0,
            new Deck(Array.Empty<Card>()));
        simulation.TrySetPlayerCard(playerCard);
        simulation.TrySetDealerCard(new Card(dealerRank));
        simulation.TrySetCommunityCards(
            communityCard1,
            communityCard2);
        simulation.TrySetPhase(GamePhase.Showdown);

        return simulation.TryDetermineWinner(out RoundWinner winner)
            ? winner
            : RoundWinner.None;
    }

    private static int NormalizeRoll(int randomRoll)
    {
        return Math.Max(0, Math.Min(99, randomRoll));
    }
}
