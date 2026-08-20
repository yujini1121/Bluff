using System;

public enum DealerDecision
{
    None,
    Check,
    Call,
    Fold,
    AllIn
}

public sealed class DealerAi
{
    public const int NumberFoldChance = 10;
    public const int DoubleFoldChance = 35;
    public const int StrongHandFoldChance = 70;

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

        if (callAmount == 0)
        {
            return DealerDecision.Check;
        }

        if (callAmount > gameState.DealerChips.Count)
        {
            return DealerDecision.AllIn;
        }

        if (!gameState.TryGetVisiblePlayerHandRank(out HandRank playerHandRank))
        {
            return DealerDecision.Call;
        }

        int foldChance = GetFoldChance(playerHandRank);
        int normalizedRoll = Math.Max(0, Math.Min(99, randomRoll));
        return normalizedRoll < foldChance
            ? DealerDecision.Fold
            : DealerDecision.Call;
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
            case DealerDecision.Check:
                return gameState.TryCheck();
            case DealerDecision.Call:
                return gameState.TryCall();
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
}
