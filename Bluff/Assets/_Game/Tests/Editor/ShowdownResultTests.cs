using NUnit.Framework;

public sealed class ShowdownResultTests
{
    [Test]
    public void GetHandRank_DetectsNumber()
    {
        GameState gameState = CreateShowdownGame(1, 2, 4, 7);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Number));
    }

    [Test]
    public void GetHandRank_DetectsDouble()
    {
        GameState gameState = CreateShowdownGame(4, 2, 4, 7);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Double));
    }

    [Test]
    public void GetHandRank_DetectsStraight()
    {
        GameState gameState = CreateShowdownGame(6, 2, 4, 5);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Straight));
    }

    [Test]
    public void GetHandRank_DetectsTripleBeforeDouble()
    {
        GameState gameState = CreateShowdownGame(4, 2, 4, 4);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Triple));
    }

    [Test]
    public void GetHandRank_DetectsNineTenOneBoundaryStraight()
    {
        GameState gameState = CreateShowdownGame(9, 2, 10, 1);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Straight));
    }

    [Test]
    public void GetHandRank_DetectsTenOneTwoBoundaryStraight()
    {
        GameState gameState = CreateShowdownGame(10, 3, 1, 2);

        Assert.That(
            gameState.TryGetHandRank(TurnOwner.Player, out HandRank handRank),
            Is.True);
        Assert.That(handRank, Is.EqualTo(HandRank.Straight));
    }

    [Test]
    public void HandRank_StrengthOrderIsNumberDoubleStraightTriple()
    {
        Assert.That((int)HandRank.Double, Is.GreaterThan((int)HandRank.Number));
        Assert.That((int)HandRank.Straight, Is.GreaterThan((int)HandRank.Double));
        Assert.That((int)HandRank.Triple, Is.GreaterThan((int)HandRank.Straight));
    }

    [Test]
    public void DetermineWinner_PlayerTripleBeatsDealerDouble()
    {
        GameState gameState = CreateShowdownGame(4, 7, 4, 4);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
    }

    [Test]
    public void DetermineWinner_PlayerStraightBeatsDealerDouble()
    {
        GameState gameState = CreateShowdownGame(6, 4, 4, 5);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
    }

    [Test]
    public void DetermineWinner_DealerDoubleBeatsPlayerNumber()
    {
        GameState gameState = CreateShowdownGame(9, 4, 4, 7);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Dealer));
    }

    [Test]
    public void DetermineWinner_HigherPlayerCardWinsWhenHandRanksMatch()
    {
        GameState gameState = CreateShowdownGame(9, 8, 4, 7);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
    }

    [Test]
    public void DetermineWinner_HigherDealerCardWinsWhenHandRanksMatch()
    {
        GameState gameState = CreateShowdownGame(3, 6, 4, 5);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Dealer));
    }

    [Test]
    public void DetermineWinner_ReturnsDrawWhenHandRanksAndPrivateCardsMatch()
    {
        GameState gameState = CreateShowdownGame(9, 9, 4, 7);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
    }

    [Test]
    public void DetermineWinner_FailsWhenAnyRequiredCardIsMissing()
    {
        var missingPlayerCard = new GameState(10, 10, CreateDeck());
        missingPlayerCard.TrySetDealerCard(new Card(2));
        missingPlayerCard.TrySetCommunityCards(new Card(4), new Card(7));
        missingPlayerCard.TrySetPhase(GamePhase.Showdown);

        var missingDealerCard = new GameState(10, 10, CreateDeck());
        missingDealerCard.TrySetPlayerCard(new Card(1));
        missingDealerCard.TrySetCommunityCards(new Card(4), new Card(7));
        missingDealerCard.TrySetPhase(GamePhase.Showdown);

        var missingCommunityCards = new GameState(10, 10, CreateDeck());
        missingCommunityCards.TrySetPlayerCard(new Card(1));
        missingCommunityCards.TrySetDealerCard(new Card(2));
        missingCommunityCards.TrySetPhase(GamePhase.Showdown);

        AssertWinnerFailsWithoutChangingState(missingPlayerCard);
        AssertWinnerFailsWithoutChangingState(missingDealerCard);
        AssertWinnerFailsWithoutChangingState(missingCommunityCards);
    }

    [Test]
    public void DetermineWinner_FailsOutsideShowdownWithoutChangingState()
    {
        GameState gameState = CreateGameWithCards(1, 2, 4, 7);

        AssertWinnerFailsWithoutChangingState(gameState);
    }

    private static GameState CreateShowdownGame(
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2)
    {
        GameState gameState = CreateGameWithCards(
            playerRank,
            dealerRank,
            communityRank1,
            communityRank2);
        gameState.TrySetPhase(GamePhase.Showdown);
        return gameState;
    }

    private static GameState CreateGameWithCards(
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2)
    {
        var gameState = new GameState(10, 10, CreateDeck());
        gameState.TrySetPlayerCard(new Card(playerRank));
        gameState.TrySetDealerCard(new Card(dealerRank));
        gameState.TrySetCommunityCards(
            new Card(communityRank1),
            new Card(communityRank2));
        return gameState;
    }

    private static void AssertWinnerFailsWithoutChangingState(GameState gameState)
    {
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;
        int playerTotalBet = gameState.Betting.PlayerTotalBet;
        int dealerTotalBet = gameState.Betting.DealerTotalBet;
        GamePhase phase = gameState.Phase;
        TurnOwner turn = gameState.CurrentTurn;
        RoundEndReason roundEndReason = gameState.RoundEndReason;

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.False);

        Assert.That(winner, Is.EqualTo(RoundWinner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(playerTotalBet));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(dealerTotalBet));
        Assert.That(gameState.Phase, Is.EqualTo(phase));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(turn));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(roundEndReason));
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
