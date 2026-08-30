using NUnit.Framework;

public sealed class DealerAiTests
{
    [Test]
    public void Decide_WithNoCallAmount_SelectsRaiseWhenRaiseIsPossible()
    {
        GameState gameState = CreateDealerTurnGame(
            10,
            10,
            1,
            2,
            4,
            7);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 50);

        Assert.That(decision, Is.EqualTo(DealerDecision.Raise));
    }

    [TestCase(1, 4, 7, 5, DealerDecision.Fold)]
    [TestCase(1, 4, 7, 50, DealerDecision.Call)]
    [TestCase(4, 4, 7, 20, DealerDecision.Fold)]
    [TestCase(4, 4, 7, 50, DealerDecision.Raise)]
    [TestCase(6, 4, 5, 50, DealerDecision.Fold)]
    [TestCase(6, 4, 5, 80, DealerDecision.Call)]
    [TestCase(4, 4, 4, 50, DealerDecision.Fold)]
    public void Decide_CallableBetUsesVisiblePlayerRankAndFixedRoll(
        int playerRank,
        int communityRank1,
        int communityRank2,
        int randomRoll,
        DealerDecision expectedDecision)
    {
        GameState gameState = CreateDealerResponseGame(
            10,
            playerRank,
            2,
            communityRank1,
            communityRank2,
            4);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, randomRoll);

        Assert.That(decision, Is.EqualTo(expectedDecision));
    }

    [Test]
    public void Decide_SameRollRaisesAgainstNumberButFoldsAgainstStrongHand()
    {
        GameState numberGame = CreateDealerResponseGame(10, 1, 2, 4, 7, 4);
        GameState straightGame = CreateDealerResponseGame(10, 6, 2, 4, 5, 4);
        var dealerAi = new DealerAi();

        DealerDecision numberDecision = dealerAi.Decide(numberGame, 20);
        DealerDecision straightDecision = dealerAi.Decide(straightGame, 20);

        Assert.That(numberDecision, Is.EqualTo(DealerDecision.Raise));
        Assert.That(straightDecision, Is.EqualTo(DealerDecision.Fold));
    }

    [TestCase(1, 4, 7, 10)]
    [TestCase(4, 4, 7, 35)]
    [TestCase(6, 4, 5, 70)]
    [TestCase(4, 4, 4, 70)]
    public void Decide_CallableBetSelectsRaiseWithinRankChance(
        int playerRank,
        int communityRank1,
        int communityRank2,
        int randomRoll)
    {
        GameState gameState = CreateDealerResponseGame(
            10,
            playerRank,
            2,
            communityRank1,
            communityRank2,
            4);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, randomRoll);

        Assert.That(decision, Is.EqualTo(DealerDecision.Raise));
    }

    [Test]
    public void Decide_WithNoCallAmountCanRaiseAgainstNumber()
    {
        GameState gameState = CreateDealerTurnGame(10, 10, 1, 2, 4, 7);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 34);

        Assert.That(decision, Is.EqualTo(DealerDecision.Raise));
    }

    [Test]
    public void Decide_WithNoCallAmountIgnoresPreviousRaiseChance()
    {
        GameState gameState = CreateDealerTurnGame(10, 10, 1, 2, 4, 7);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 35);

        Assert.That(decision, Is.EqualTo(DealerDecision.Raise));
    }

    [Test]
    public void Decide_WhenRaiseChipsAreInsufficient_SelectsCall()
    {
        GameState gameState = CreateDealerResponseGame(4, 1, 2, 4, 7, 4);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 10);

        Assert.That(decision, Is.EqualTo(DealerDecision.Call));
    }

    [Test]
    public void Decide_WhenPlayerHasNoChips_DoesNotRaise()
    {
        GameState gameState = CreateDealerTurnGame(0, 10, 1, 2, 4, 7);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 0);

        Assert.That(decision, Is.EqualTo(DealerDecision.Fold));
    }

    [Test]
    public void Decide_WhenDealerCannotCoverCall_SelectsAllIn()
    {
        GameState gameState = CreateDealerResponseGame(3, 1, 2, 4, 7, 5);
        var dealerAi = new DealerAi();

        DealerDecision decision = dealerAi.Decide(gameState, 99);

        Assert.That(gameState.Betting.GetCallAmount(TurnOwner.Dealer), Is.EqualTo(5));
        Assert.That(decision, Is.EqualTo(DealerDecision.AllIn));
    }

    [Test]
    public void Decide_ChangingOnlyDealerCardDoesNotChangeDecision()
    {
        GameState lowDealerCardGame = CreateDealerResponseGame(
            10,
            6,
            1,
            4,
            5,
            4);
        GameState highDealerCardGame = CreateDealerResponseGame(
            10,
            6,
            10,
            4,
            5,
            4);
        var dealerAi = new DealerAi();

        DealerDecision lowCardDecision = dealerAi.Decide(lowDealerCardGame, 50);
        DealerDecision highCardDecision = dealerAi.Decide(highDealerCardGame, 50);

        Assert.That(lowCardDecision, Is.EqualTo(DealerDecision.Fold));
        Assert.That(highCardDecision, Is.EqualTo(lowCardDecision));
    }

    [Test]
    public void Execute_RaiseAtEqualBetsReturnsTurnToPlayer()
    {
        GameState gameState = CreateDealerTurnGame(10, 10, 1, 2, 4, 7);
        var dealerAi = new DealerAi();
        DealerDecision decision = dealerAi.Decide(gameState, 50);

        Assert.That(dealerAi.TryExecute(gameState, decision), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(9));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(1));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(1));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
    }

    [Test]
    public void Execute_SecondRequestInSameDealerTurnIsRejected()
    {
        GameState gameState = CreateDealerTurnGame(10, 10, 1, 2, 4, 7);
        var dealerAi = new DealerAi();

        Assert.That(
            dealerAi.TryExecute(gameState, DealerDecision.Fold),
            Is.True);

        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;

        Assert.That(
            dealerAi.TryExecute(gameState, DealerDecision.Fold),
            Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Execute_CallUsesExistingGameStateSettlement()
    {
        GameState gameState = CreateDealerResponseGame(20, 1, 2, 4, 7, 4);
        var dealerAi = new DealerAi();
        DealerDecision decision = dealerAi.Decide(gameState, 99);

        Assert.That(decision, Is.EqualTo(DealerDecision.Call));
        Assert.That(dealerAi.TryExecute(gameState, decision), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(16));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(16));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Execute_RaiseUsesGameStateRaiseAndReturnsTurnToPlayer()
    {
        GameState gameState = CreateDealerResponseGame(20, 1, 2, 4, 7, 3);
        var dealerAi = new DealerAi();
        DealerDecision decision = dealerAi.Decide(gameState, 10);

        Assert.That(decision, Is.EqualTo(DealerDecision.Raise));
        Assert.That(dealerAi.TryExecute(gameState, decision), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(17));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(16));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(7));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(
            gameState.Betting.GetCallAmount(TurnOwner.Player),
            Is.EqualTo(1));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
    }

    [Test]
    public void Execute_CallThatUsesAllDealerChipsRemainsCall()
    {
        GameState gameState = CreateDealerResponseGame(4, 1, 2, 4, 7, 4);
        var dealerAi = new DealerAi();
        DealerDecision decision = dealerAi.Decide(gameState, 99);

        Assert.That(decision, Is.EqualTo(DealerDecision.Call));
        Assert.That(dealerAi.TryExecute(gameState, decision), Is.True);

        Assert.That(gameState.DealerChips.Count, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Execute_ShortAllInUsesExistingRefundAndShowdownFlow()
    {
        GameState gameState = CreateDealerResponseGame(3, 1, 2, 4, 7, 5);
        var dealerAi = new DealerAi();
        DealerDecision decision = dealerAi.Decide(gameState, 99);

        Assert.That(decision, Is.EqualTo(DealerDecision.AllIn));
        Assert.That(dealerAi.TryExecute(gameState, decision), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(17));
        Assert.That(gameState.DealerChips.Count, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Execute_RefusesToActForPlayerTurnWithoutChangingState()
    {
        var gameState = new GameState(10, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        var dealerAi = new DealerAi();

        Assert.That(
            dealerAi.TryExecute(gameState, DealerDecision.Fold),
            Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    private static GameState CreateDealerResponseGame(
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        int raiseBy)
    {
        GameState gameState = CreateDealerTurnGame(
            20,
            dealerChips,
            playerRank,
            dealerRank,
            communityRank1,
            communityRank2,
            TurnOwner.Player);
        Assert.That(gameState.TryRaise(raiseBy), Is.True);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        return gameState;
    }

    private static GameState CreateDealerTurnGame(
        int playerChips,
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        TurnOwner turn = TurnOwner.Dealer)
    {
        var gameState = new GameState(playerChips, dealerChips, CreateDeck());
        gameState.TrySetPlayerCard(new Card(playerRank));
        gameState.TrySetDealerCard(new Card(dealerRank));
        gameState.TrySetCommunityCards(
            new Card(communityRank1),
            new Card(communityRank2));
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(turn);
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
