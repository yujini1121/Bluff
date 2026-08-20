using NUnit.Framework;

public sealed class CheckActionTests
{
    [Test]
    public void Check_SucceedsAtZeroCallAmountWithoutMovingChips()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        long totalChipsBefore = GetTotalChips(gameState);

        Assert.That(gameState.TryCheck(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(GetTotalChips(gameState), Is.EqualTo(totalChipsBefore));
    }

    [Test]
    public void FirstCheck_RecordsOwnerAndSwitchesToOpponent()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryCheck(), Is.True);

        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
    }

    [Test]
    public void ConsecutiveChecks_EndBettingAndMoveToShowdown()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryCheck(), Is.True);
        Assert.That(gameState.TryCheck(), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
    }

    [Test]
    public void Check_FailsWithOutstandingCallWithoutChangingState()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(4), Is.True);
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;
        int playerTotalBet = gameState.Betting.PlayerTotalBet;
        int dealerTotalBet = gameState.Betting.DealerTotalBet;

        Assert.That(gameState.TryCheck(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(playerTotalBet));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(dealerTotalBet));
        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void Check_FailsOutsideBettingWithoutChangingState()
    {
        var gameState = new GameState(10, 10, CreateDeck());
        Assert.That(gameState.Turn.TrySet(TurnOwner.Player), Is.True);

        Assert.That(gameState.TryCheck(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void RaiseAfterCheck_ClearsPendingCheckAndKeepsBettingActive()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        Assert.That(gameState.TryCheck(), Is.True);

        Assert.That(gameState.TryRaise(2), Is.True);

        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(2));
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Player), Is.EqualTo(2));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void PrepareNextRound_ClearsPendingCheck()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        Assert.That(gameState.TryCheck(), Is.True);
        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.TrySetPhase(GamePhase.RoundEnd), Is.True);

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.Betting.PendingCheckBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    private static GameState CreateBettingGame(TurnOwner firstTurn)
    {
        var gameState = new GameState(10, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(firstTurn);
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }

    private static long GetTotalChips(GameState gameState)
    {
        return (long)gameState.PlayerChips.Count +
               gameState.DealerChips.Count +
               gameState.Pot.Amount;
    }
}
