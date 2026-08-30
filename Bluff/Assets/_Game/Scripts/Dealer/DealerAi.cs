using System;

public enum DealerDecision
{
    None,
    Call,
    Raise,
    Fold,
    AllIn
}

public sealed class DealerAi
{
    public const int DealerRaiseAmount = 1;
    public const int NumberFoldChance = 10;
    public const int DoubleFoldChance = 35;
    public const int StrongHandFoldChance = 70;
    public const int NumberRaiseChance = 35;
    public const int DoubleRaiseChance = 20;
    public const int StrongHandRaiseChance = 5;

    public DealerDecision Decide(GameState gameState, int randomRoll)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            return DealerDecision.None;
        }

        int callAmount = gameState.Betting.GetCallAmount(TurnOwner.Dealer);

        if (callAmount > gameState.DealerChips.Count)
        {
            return DealerDecision.AllIn;
        }

        bool canRaise = CanRaise(gameState, callAmount);

        if (callAmount == 0)
        {
            return canRaise
                ? DealerDecision.Raise
                : DealerDecision.Fold;
        }

        if (!gameState.TryGetVisiblePlayerHandRank(out HandRank playerHandRank))
        {
            return DealerDecision.Call;
        }

        int normalizedRoll = Math.Max(0, Math.Min(99, randomRoll));
        int raiseChance = GetRaiseChance(playerHandRank);

        int foldChance = GetFoldChance(playerHandRank);

        if (normalizedRoll < foldChance)
        {
            return DealerDecision.Fold;
        }

        if (canRaise && normalizedRoll < foldChance + raiseChance)
        {
            return DealerDecision.Raise;
        }

        return DealerDecision.Call;
    }

    public bool TryExecute(GameState gameState, DealerDecision decision)
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

        switch (decision)
        {
            case DealerDecision.Call:
                return gameState.TryCall();
            case DealerDecision.Raise:
                return gameState.TryRaise(DealerRaiseAmount);
            case DealerDecision.Fold:
                return gameState.TryFold();
            case DealerDecision.AllIn:
                return gameState.TryAllIn();
            default:
                return false;
        }
    }

    private static int GetFoldChance(HandRank playerHandRank)
    {
        switch (playerHandRank)
        {
            case HandRank.Number:
                return NumberFoldChance;
            case HandRank.Double:
                return DoubleFoldChance;
            case HandRank.Straight:
            case HandRank.Triple:
                return StrongHandFoldChance;
            default:
                return 0;
        }
    }

    private static int GetRaiseChance(HandRank playerHandRank)
    {
        switch (playerHandRank)
        {
            case HandRank.Number:
                return NumberRaiseChance;
            case HandRank.Double:
                return DoubleRaiseChance;
            case HandRank.Straight:
            case HandRank.Triple:
                return StrongHandRaiseChance;
            default:
                return 0;
        }
    }

    private static bool CanRaise(GameState gameState, int callAmount)
    {
        return gameState.PlayerChips.Count > 0 &&
               (long)callAmount + DealerRaiseAmount <=
               gameState.DealerChips.Count;
    }
}
