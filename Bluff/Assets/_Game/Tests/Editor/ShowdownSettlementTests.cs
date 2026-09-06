using NUnit.Framework;

public sealed class ShowdownSettlementTests
{
    [Test]
    public void SettleShowdown_AwardsEntirePotToPlayerWinner()
    {
        GameState gameState = CreateShowdownGame(6, 4, 4, 5);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(14));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        AssertRoundEndedAfterShowdown(gameState);
    }

    [Test]
    public void SettleShowdown_SecondRequestDoesNotPayWinnerAgain()
    {
        GameState gameState = CreateShowdownGame(6, 4, 4, 5);
        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));

        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;

        Assert.That(
            gameState.TrySettleShowdown(out RoundWinner secondWinner),
            Is.False);

        Assert.That(secondWinner, Is.EqualTo(RoundWinner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        AssertRoundEndedAfterShowdown(gameState);
    }

    [Test]
    public void SettleShowdown_AwardsEntirePotToDealerWinner()
    {
        GameState gameState = CreateShowdownGame(9, 4, 4, 7);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Dealer));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(14));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        AssertRoundEndedAfterShowdown(gameState);
    }

    [Test]
    public void SettleShowdown_PreservesPotForDraw()
    {
        GameState gameState = CreateShowdownGame(9, 9, 4, 7);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        AssertRoundEndedAfterShowdown(gameState);
    }

    [Test]
    public void SettleShowdown_FailsOutsideShowdownWithoutChangingState()
    {
        GameState gameState = CreateGameWithCards(6, 4, 4, 5);

        AssertSettlementFailsWithoutChangingState(gameState);
    }

    [Test]
    public void SettleShowdown_FailsWithMissingCardsWithoutChangingState()
    {
        var gameState = new GameState(10, 10, CreateDeck());
        Assert.That(gameState.Pot.TryAdd(4), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        AssertSettlementFailsWithoutChangingState(gameState);
    }

    [Test]
    public void SettleShowdown_FailsOnWinnerChipOverflowWithoutChangingState()
    {
        GameState gameState = CreateGameWithCards(6, 4, 4, 5, int.MaxValue, 10);
        Assert.That(gameState.Pot.TryAdd(1), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        AssertSettlementFailsWithoutChangingState(gameState);
    }

    private static void AssertRoundEndedAfterShowdown(GameState gameState)
    {
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Showdown));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    private static void AssertSettlementFailsWithoutChangingState(
        GameState gameState)
    {
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;
        int playerTotalBet = gameState.Betting.PlayerTotalBet;
        int dealerTotalBet = gameState.Betting.DealerTotalBet;
        GamePhase phase = gameState.Phase;
        TurnOwner turn = gameState.CurrentTurn;
        RoundEndReason roundEndReason = gameState.RoundEndReason;
        TurnOwner foldedBy = gameState.FoldedBy;
        Card playerCard = gameState.PlayerCard;
        Card dealerCard = gameState.DealerCard;
        Card communityCard1 = gameState.CommunityCard1;
        Card communityCard2 = gameState.CommunityCard2;

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.False);

        Assert.That(winner, Is.EqualTo(RoundWinner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(playerTotalBet));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(dealerTotalBet));
        Assert.That(gameState.Phase, Is.EqualTo(phase));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(turn));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(roundEndReason));
        Assert.That(gameState.FoldedBy, Is.EqualTo(foldedBy));
        Assert.That(gameState.PlayerCard, Is.SameAs(playerCard));
        Assert.That(gameState.DealerCard, Is.SameAs(dealerCard));
        Assert.That(gameState.CommunityCard1, Is.SameAs(communityCard1));
        Assert.That(gameState.CommunityCard2, Is.SameAs(communityCard2));
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
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(4), Is.True);
        Assert.That(gameState.TryCall(), Is.True);
        return gameState;
    }

    private static GameState CreateGameWithCards(
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        int playerChips = 10,
        int dealerChips = 10)
    {
        var gameState = new GameState(playerChips, dealerChips, CreateDeck());
        gameState.TrySetPlayerCard(new Card(playerRank));
        gameState.TrySetDealerCard(new Card(dealerRank));
        gameState.TrySetCommunityCards(
            new Card(communityRank1),
            new Card(communityRank2));
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[]
        {
            new Card(1),
            new Card(2),
            new Card(3),
            new Card(4)
        });
    }
}
