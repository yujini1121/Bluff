using NUnit.Framework;

public sealed class RoundTransitionTests
{
    [Test]
    public void PrepareNextRound_SucceedsAfterFoldAndResetsRoundState()
    {
        GameState gameState = CreateGameWithCards(1, 2, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        Assert.That(gameState.TryRaise(2), Is.True);
        Assert.That(gameState.TryFold(), Is.True);

        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.PlayerCard, Is.Null);
        Assert.That(gameState.DealerCard, Is.Null);
        Assert.That(gameState.CommunityCard1, Is.Null);
        Assert.That(gameState.CommunityCard2, Is.Null);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.None));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
    }

    [Test]
    public void PrepareNextRound_ResetsBettingCardsAndTurnFromRoundEnd()
    {
        GameState gameState = CreateGameWithCards(1, 2, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(3), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.RoundEnd), Is.True);
        int potAmount = gameState.Pot.Amount;

        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.PlayerCard, Is.Null);
        Assert.That(gameState.DealerCard, Is.Null);
        Assert.That(gameState.CommunityCard1, Is.Null);
        Assert.That(gameState.CommunityCard2, Is.Null);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
    }

    [Test]
    public void PrepareNextRound_FailsDuringBettingWithoutChangingState()
    {
        GameState gameState = CreateGameWithCards(1, 2, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(3), Is.True);

        AssertPrepareNextRoundFailsWithoutChangingState(gameState);
    }

    [Test]
    public void PrepareNextRound_FailsDuringShowdownWithoutChangingState()
    {
        GameState gameState = CreateGameWithCards(6, 4, 4, 5);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(2), Is.True);
        Assert.That(gameState.TryCall(), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        AssertPrepareNextRoundFailsWithoutChangingState(gameState);
    }

    [Test]
    public void PrepareNextRound_DoesNotChangeSettledWinnerChipsOrEmptyPot()
    {
        GameState gameState = CreateGameWithCards(6, 4, 4, 5);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(2), Is.True);
        Assert.That(gameState.TryCall(), Is.True);
        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));

        Assert.That(gameState.PlayerChips.TryAdd(gameState.Pot.TakeAll()), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.RoundEnd), Is.True);
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.Zero);
    }

    [Test]
    public void PrepareNextRound_PreservesCarriedPotAfterDraw()
    {
        GameState gameState = CreateGameWithCards(9, 9, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(4), Is.True);
        Assert.That(gameState.TryCall(), Is.True);
        Assert.That(gameState.TryDetermineWinner(out RoundWinner winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
        Assert.That(gameState.TrySetPhase(GamePhase.RoundEnd), Is.True);
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.PlayerCard, Is.Null);
        Assert.That(gameState.DealerCard, Is.Null);
        Assert.That(gameState.CommunityCard1, Is.Null);
        Assert.That(gameState.CommunityCard2, Is.Null);
    }

    private static void AssertPrepareNextRoundFailsWithoutChangingState(
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

        Assert.That(gameState.TryPrepareNextRound(), Is.False);

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

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
