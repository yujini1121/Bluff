using NUnit.Framework;

public sealed class RoundStartTests
{
    [Test]
    public void StartRound_DealsCardsAndStartsBettingWithSelectedTurn()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));

        Assert.That(gameState.TryStartRound(TurnOwner.Dealer), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.PlayerCard, Is.Not.Null);
        Assert.That(gameState.DealerCard, Is.Not.Null);
        Assert.That(gameState.CommunityCard1, Is.Not.Null);
        Assert.That(gameState.CommunityCard2, Is.Not.Null);
        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(2));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(9));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(9));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(1));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(1));
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Player), Is.Zero);
        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Dealer), Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(2));
    }

    [Test]
    public void StartRound_AddsAntesToCarriedPot()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));
        Assert.That(gameState.Pot.TryAdd(3), Is.True);

        Assert.That(gameState.TryStartRound(TurnOwner.Player), Is.True);

        Assert.That(gameState.Pot.Amount, Is.EqualTo(5));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(9));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(9));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(1));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(1));
    }

    [Test]
    public void StartRound_FailsWhenPlayerCannotPayAnteWithoutChangingState()
    {
        var gameState = new GameState(0, 10, CreateDeck(6));

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Player);
    }

    [Test]
    public void StartRound_FailsWhenDealerCannotPayAnteWithoutChangingState()
    {
        var gameState = new GameState(10, 0, CreateDeck(6));

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Player);
    }

    [Test]
    public void StartRound_FailsWhenPotCannotReceiveAntesWithoutChangingState()
    {
        var gameState = new GameState(10, 10, CreateDeck(6));
        Assert.That(gameState.Pot.TryAdd(int.MaxValue), Is.True);

        AssertStartRoundFailsWithoutChangingState(
            gameState,
            TurnOwner.Player);
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
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int pot = gameState.Pot.Amount;
        int playerTotalBet = gameState.Betting.PlayerTotalBet;
        int dealerTotalBet = gameState.Betting.DealerTotalBet;
        RoundEndReason roundEndReason = gameState.RoundEndReason;
        TurnOwner foldedBy = gameState.FoldedBy;
        GameWinner finalWinner = gameState.FinalWinner;

        Assert.That(gameState.TryStartRound(firstTurn), Is.False);

        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(remainingCards));
        Assert.That(gameState.Phase, Is.EqualTo(phase));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(turn));
        Assert.That(gameState.PlayerCard, Is.SameAs(playerCard));
        Assert.That(gameState.DealerCard, Is.SameAs(dealerCard));
        Assert.That(gameState.CommunityCard1, Is.SameAs(communityCard1));
        Assert.That(gameState.CommunityCard2, Is.SameAs(communityCard2));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(pot));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(playerTotalBet));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(dealerTotalBet));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(roundEndReason));
        Assert.That(gameState.FoldedBy, Is.EqualTo(foldedBy));
        Assert.That(gameState.FinalWinner, Is.EqualTo(finalWinner));
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
