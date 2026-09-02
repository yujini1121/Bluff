using NUnit.Framework;

public sealed class GameOverTests
{
    [Test]
    public void SettleShowdown_PlayerWithNoChipsEndsGameWithDealerWinner()
    {
        GameState gameState = CreatePlayerEliminatedShowdown();

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Dealer));
        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(13));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
    }

    [Test]
    public void SettleShowdown_DealerWithNoChipsEndsGameWithPlayerWinner()
    {
        GameState gameState = CreateGameWithCards(10, 3, 6, 4, 4, 5);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        Assert.That(gameState.TryRaise(5), Is.True);
        Assert.That(gameState.TryAllIn(), Is.True);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(13));
        Assert.That(gameState.DealerChips.Count, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Player));
    }

    [Test]
    public void AllIn_DoesNotEndGameBeforeRoundSettlement()
    {
        var gameState = new GameState(7, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);

        Assert.That(gameState.TryAllIn(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
    }

    [Test]
    public void SettleShowdown_AllInPlayerWinsPotAndGameContinues()
    {
        GameState gameState = CreateGameWithCards(3, 10, 6, 4, 4, 5);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(5), Is.True);
        Assert.That(gameState.TryAllIn(), Is.True);
        Assert.That(gameState.PlayerChips.Count, Is.Zero);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(7));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
    }

    [Test]
    public void SettleShowdown_BothPlayersWithChipsDoesNotEndGame()
    {
        GameState gameState = CreateGameWithCards(10, 10, 6, 4, 4, 5);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(2), Is.True);
        Assert.That(gameState.TryCall(), Is.True);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(12));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(8));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
        Assert.That(gameState.TryPrepareNextRound(), Is.True);
    }

    [Test]
    public void GameOver_BlocksNextRoundAndBettingActionsWithoutChangingState()
    {
        GameState gameState = CreatePlayerEliminatedShowdown();
        Assert.That(gameState.TrySettleShowdown(out _), Is.True);

        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;
        int playerTotalBet = gameState.Betting.PlayerTotalBet;
        int dealerTotalBet = gameState.Betting.DealerTotalBet;
        RoundEndReason roundEndReason = gameState.RoundEndReason;
        TurnOwner foldedBy = gameState.FoldedBy;
        Card playerCard = gameState.PlayerCard;
        Card dealerCard = gameState.DealerCard;
        Card communityCard1 = gameState.CommunityCard1;
        Card communityCard2 = gameState.CommunityCard2;

        Assert.That(gameState.TryPrepareNextRound(), Is.False);
        Assert.That(gameState.TryCall(), Is.False);
        Assert.That(gameState.TryRaise(1), Is.False);
        Assert.That(gameState.TryAllIn(), Is.False);
        Assert.That(gameState.TryFold(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(playerTotalBet));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(dealerTotalBet));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(roundEndReason));
        Assert.That(gameState.FoldedBy, Is.EqualTo(foldedBy));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
        Assert.That(gameState.PlayerCard, Is.SameAs(playerCard));
        Assert.That(gameState.DealerCard, Is.SameAs(dealerCard));
        Assert.That(gameState.CommunityCard1, Is.SameAs(communityCard1));
        Assert.That(gameState.CommunityCard2, Is.SameAs(communityCard2));
    }

    [Test]
    public void SettleShowdown_DrawWithCarriedPotDoesNotEndGame()
    {
        GameState gameState = CreateGameWithCards(3, 5, 9, 9, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        Assert.That(gameState.TryAllIn(), Is.True);
        Assert.That(gameState.TryCall(), Is.True);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(2));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));

        Assert.That(gameState.TryPrepareNextRound(), Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
    }

    [Test]
    public void SettleShowdown_ExhaustedDeckWithPlayerChipLeadEndsGame()
    {
        GameState gameState = CreateGameWithCards(
            12,
            8,
            6,
            4,
            4,
            5,
            3);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        Assert.That(gameState.TrySettleShowdown(out _), Is.True);

        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Player));
        Assert.That(gameState.TryPrepareNextRound(), Is.False);
    }

    [Test]
    public void SettleShowdown_ExhaustedDeckWithDealerChipLeadEndsGame()
    {
        GameState gameState = CreateGameWithCards(
            8,
            12,
            9,
            4,
            4,
            7,
            3);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        Assert.That(gameState.TrySettleShowdown(out _), Is.True);

        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
        Assert.That(gameState.TryPrepareNextRound(), Is.False);
    }

    [Test]
    public void SettleShowdown_ExhaustedDeckWithEqualChipsEndsGameInDraw()
    {
        GameState gameState = CreateGameWithCards(
            10,
            10,
            9,
            9,
            4,
            7,
            3);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Draw));
        Assert.That(gameState.TryPrepareNextRound(), Is.False);
    }

    [Test]
    public void SettleShowdown_ExhaustedDeckDrawWithCarriedPotEndsGameInDraw()
    {
        GameState gameState = CreateGameWithCards(
            3,
            5,
            9,
            9,
            4,
            7,
            3);
        Assert.That(gameState.Pot.TryAdd(6), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);

        Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);

        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(3));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(5));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Draw));
        Assert.That(gameState.TryPrepareNextRound(), Is.False);
    }

    [Test]
    public void Fold_AfterFinalPayoutCanEndGame()
    {
        var gameState = new GameState(0, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));
    }

    private static GameState CreatePlayerEliminatedShowdown()
    {
        GameState gameState = CreateGameWithCards(3, 10, 9, 4, 4, 7);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(5), Is.True);
        Assert.That(gameState.TryAllIn(), Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        return gameState;
    }

    private static GameState CreateGameWithCards(
        int playerChips,
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        int deckCardCount = 4)
    {
        var gameState = new GameState(
            playerChips,
            dealerChips,
            CreateDeck(deckCardCount));
        gameState.TrySetPlayerCard(new Card(playerRank));
        gameState.TrySetDealerCard(new Card(dealerRank));
        gameState.TrySetCommunityCards(
            new Card(communityRank1),
            new Card(communityRank2));
        return gameState;
    }

    private static Deck CreateDeck(int cardCount = 4)
    {
        var cards = new Card[cardCount];

        for (int index = 0; index < cardCount; index++)
        {
            cards[index] = new Card(index % 10 + 1);
        }

        return new Deck(cards);
    }
}
