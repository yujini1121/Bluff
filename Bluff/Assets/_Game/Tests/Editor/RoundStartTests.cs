using NUnit.Framework;

public sealed class RoundStartTests
{
    [Test]
    public void StartRound_DealsCardsAndStartsBettingWithSelectedTurn()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));
        Assert.That(gameState.Pot.TryAdd(3), Is.True);

        Assert.That(gameState.TryStartRound(TurnOwner.Dealer), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.PlayerCard, Is.Not.Null);
        Assert.That(gameState.DealerCard, Is.Not.Null);
        Assert.That(gameState.CommunityCard1, Is.Not.Null);
        Assert.That(gameState.CommunityCard2, Is.Not.Null);
        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(2));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(3));
    }

    [Test]
    public void StartRound_FailsOutsideSetupWithoutChangingState()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.Turn.TrySet(TurnOwner.Player), Is.True);

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Dealer);
    }

    [TestCase(TurnOwner.None)]
    [TestCase((TurnOwner)99)]
    public void StartRound_FailsWithInvalidFirstTurnWithoutChangingState(
        TurnOwner firstTurn)
    {
        var gameState = new GameState(10, 10, CreateDeck(6));

        AssertStartRoundFailsWithoutChangingState(gameState, firstTurn);
    }

    [Test]
    public void StartRound_FailsWhenDeckHasFewerThanFourCards()
    {
        var gameState = new GameState(10, 10, CreateDeck(3));

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Player);
    }

    [Test]
    public void StartRound_FailsWhenSetupAlreadyHasCards()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));
        Assert.That(gameState.TrySetPlayerCard(new Card(10)), Is.True);

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Player);
    }

    private static void AssertStartRoundFailsWithoutChangingState(
        GameState gameState,
        TurnOwner firstTurn)
    {
        int remainingCards = gameState.Deck.RemainingCount;
        GamePhase phase = gameState.Phase;
        TurnOwner turn = gameState.CurrentTurn;
        Card playerCard = gameState.PlayerCard;
        Card dealerCard = gameState.DealerCard;
        Card communityCard1 = gameState.CommunityCard1;
        Card communityCard2 = gameState.CommunityCard2;

        Assert.That(gameState.TryStartRound(firstTurn), Is.False);

        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(remainingCards));
        Assert.That(gameState.Phase, Is.EqualTo(phase));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(turn));
        Assert.That(gameState.PlayerCard, Is.SameAs(playerCard));
        Assert.That(gameState.DealerCard, Is.SameAs(dealerCard));
        Assert.That(gameState.CommunityCard1, Is.SameAs(communityCard1));
        Assert.That(gameState.CommunityCard2, Is.SameAs(communityCard2));
    }

    private static Deck CreateDeck(int cardCount)
    {
        var cards = new Card[cardCount];

        for (int index = 0; index < cardCount; index++)
        {
            cards[index] = new Card(index % 10 + 1);
        }

        return new Deck(cards);
    }
}
