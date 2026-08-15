using NUnit.Framework;

public sealed class BettingActionTests
{
    [Test]
    public void Call_MatchesOutstandingBetAndMovesToShowdown()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);

        Assert.That(gameState.TryPlaceBet(4), Is.True);
        Assert.That(gameState.Turn.TrySwitch(), Is.True);
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Player), Is.EqualTo(4));

        Assert.That(gameState.TryCall(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Betting.PlayerBet, Is.EqualTo(4));
        Assert.That(gameState.Betting.DealerBet, Is.EqualTo(4));
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

        Assert.That(gameState.TryPlaceBet(4), Is.True);
        Assert.That(gameState.Turn.TrySwitch(), Is.True);

        Assert.That(gameState.TryCall(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(3));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void Fold_AwardsPotToOpponentAndEndsRound()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);

        Assert.That(gameState.TryPlaceBet(2), Is.True);
        Assert.That(gameState.Turn.TrySwitch(), Is.True);
        Assert.That(gameState.TryPlaceBet(4), Is.True);
        Assert.That(gameState.Turn.TrySwitch(), Is.True);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(8));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(12));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerBet, Is.Zero);
        Assert.That(gameState.Betting.DealerBet, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void BettingActions_FailOutsideBettingPhaseOrWithoutAnActiveTurn()
    {
        GameState gameState = new GameState(10, 10, CreateDeck());

        Assert.That(gameState.TryCall(), Is.False);
        Assert.That(gameState.TryFold(), Is.False);
        Assert.That(gameState.TryPlaceBet(1), Is.False);

        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);

        Assert.That(gameState.TryCall(), Is.False);
        Assert.That(gameState.TryFold(), Is.False);
        Assert.That(gameState.TryPlaceBet(1), Is.False);
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
