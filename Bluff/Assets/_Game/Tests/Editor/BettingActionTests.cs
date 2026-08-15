using NUnit.Framework;

public sealed class BettingActionTests
{
    [Test]
    public void Call_MatchesOutstandingBetAndMovesToShowdown()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);

        Assert.That(gameState.TryRaise(4), Is.True);
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Player), Is.EqualTo(4));

        Assert.That(gameState.TryCall(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Call_FailsWithoutAnOutstandingBet()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryCall(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void Call_FailsWhenCurrentOwnerCannotCoverTheBet()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer, 3, 10);

        Assert.That(gameState.TryRaise(4), Is.True);

        Assert.That(gameState.TryCall(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(3));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(4));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void Raise_MatchesCurrentBetAddsRaiseAndSwitchesTurn()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);

        Assert.That(gameState.TryRaise(4), Is.True);

        Assert.That(gameState.TryRaise(3), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(3));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(11));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(7));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Dealer), Is.EqualTo(3));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));

        Assert.That(gameState.TryCall(), Is.True);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(14));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
    }

    [Test]
    public void Raise_FailsWhenCurrentOwnerCannotCoverCallAndRaise()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer, 5, 10);

        Assert.That(gameState.TryRaise(4), Is.True);

        Assert.That(gameState.TryRaise(2), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(5));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(4));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void AllIn_BetsAllChipsAndSwitchesTurnWhenOpponentMustRespond()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player, 7, 10);

        Assert.That(gameState.TryAllIn(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(7));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(7));
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Dealer), Is.EqualTo(7));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
    }

    [Test]
    public void AllIn_MovesToShowdownWhenItDoesNotExceedOpponentBet()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer, 3, 10);

        Assert.That(gameState.TryRaise(5), Is.True);

        Assert.That(gameState.TryAllIn(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(5));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(5));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Fold_AwardsPotToOpponentAndEndsRound()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryRaise(2), Is.True);
        Assert.That(gameState.TryRaise(2), Is.True);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(8));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(12));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Fold_RecordsDealerAsTheOwnerWhoFolded()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryRaise(2), Is.True);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Fold_FailsWithoutChangingRoundOrBettingState()
    {
        var gameState = new GameState(10, 10, CreateDeck());

        Assert.That(gameState.TryFold(), Is.False);
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.TryFold(), Is.False);

        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void ResetRoundResult_ClearsFoldInformation()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryFold(), Is.True);

        gameState.ResetRoundResult();

        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.None));
    }

    [Test]
    public void BettingActions_FailOutsideBettingPhaseOrWithoutAnActiveTurn()
    {
        GameState gameState = new GameState(10, 10, CreateDeck());

        Assert.That(gameState.TryCall(), Is.False);
        Assert.That(gameState.TryRaise(1), Is.False);
        Assert.That(gameState.TryAllIn(), Is.False);
        Assert.That(gameState.TryFold(), Is.False);

        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);

        Assert.That(gameState.TryCall(), Is.False);
        Assert.That(gameState.TryRaise(1), Is.False);
        Assert.That(gameState.TryAllIn(), Is.False);
        Assert.That(gameState.TryFold(), Is.False);
    }

    private static GameState CreateBettingGame(
        TurnOwner firstTurn,
        int playerChips = 10,
        int dealerChips = 10)
    {
        var gameState = new GameState(playerChips, dealerChips, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(firstTurn);
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
