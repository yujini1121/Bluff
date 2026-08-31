using NUnit.Framework;

public sealed class DealerAiTests
{
    [Test]
    public void Evaluate_ChangingOnlyDealerCardKeepsOddsAndPlanIdentical()
    {
        GameState lowDealerCardGame = CreateDealerResponseGame(
            20,
            4,
            1,
            4,
            4,
            2);
        GameState highDealerCardGame = CreateDealerResponseGame(
            20,
            4,
            10,
            4,
            4,
            2);
        var dealerAi = new DealerAi();

        Assert.That(
            dealerAi.TryEvaluate(
                lowDealerCardGame,
                out DealerHandOdds lowCardOdds),
            Is.True);
        Assert.That(
            dealerAi.TryEvaluate(
                highDealerCardGame,
                out DealerHandOdds highCardOdds),
            Is.True);
        DealerActionPlan lowCardPlan = dealerAi.Decide(
            lowDealerCardGame,
            99,
            10);
        DealerActionPlan highCardPlan = dealerAi.Decide(
            highDealerCardGame,
            99,
            10);

        AssertOddsEqual(lowCardOdds, highCardOdds);
        Assert.That(highCardPlan.Decision, Is.EqualTo(lowCardPlan.Decision));
        Assert.That(highCardPlan.RaiseBy, Is.EqualTo(lowCardPlan.RaiseBy));
    }

    [Test]
    public void Evaluate_FavorableObservationHasHigherExpectedOdds()
    {
        GameState favorableGame = CreateDealerTurnGame(
            20,
            20,
            1,
            2,
            4,
            7);
        GameState unfavorableGame = CreateDealerTurnGame(
            20,
            20,
            4,
            2,
            4,
            4);
        var dealerAi = new DealerAi();

        Assert.That(
            dealerAi.TryEvaluate(favorableGame, out DealerHandOdds favorable),
            Is.True);
        Assert.That(
            dealerAi.TryEvaluate(
                unfavorableGame,
                out DealerHandOdds unfavorable),
            Is.True);

        Assert.That(favorable.WinProbability, Is.GreaterThan(0.9d));
        Assert.That(unfavorable.LossProbability, Is.GreaterThan(0.9d));
        Assert.That(
            favorable.ExpectedEquity,
            Is.GreaterThan(unfavorable.ExpectedEquity));
        Assert.That(
            favorable.WinProbability +
            favorable.DrawProbability +
            favorable.LossProbability,
            Is.EqualTo(1d).Within(0.000001d));
    }

    [Test]
    public void Decide_LowOddsCanBluffRaiseAtHighActionRoll()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            4,
            2,
            4,
            4,
            2);
        var dealerAi = new DealerAi();

        DealerActionPlan plan = dealerAi.Decide(gameState, 99, 0);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.Raise));
        Assert.That(plan.RaiseBy, Is.GreaterThan(0));
    }

    [Test]
    public void Decide_HighOddsCanSlowPlayCall()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            1,
            2,
            4,
            7,
            2);
        var dealerAi = new DealerAi();

        DealerActionPlan plan = dealerAi.Decide(gameState, 10, 50);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.Call));
    }

    [Test]
    public void Decide_ZeroCallAmountDoesNotAlwaysRaise()
    {
        GameState gameState = CreateDealerTurnGame(
            20,
            20,
            1,
            2,
            4,
            7);
        var dealerAi = new DealerAi();

        DealerActionPlan foldPlan = dealerAi.Decide(gameState, 0, 0);
        DealerActionPlan raisePlan = dealerAi.Decide(gameState, 99, 0);

        Assert.That(foldPlan.Decision, Is.EqualTo(DealerDecision.Fold));
        Assert.That(raisePlan.Decision, Is.EqualTo(DealerDecision.Raise));
    }

    [Test]
    public void Decide_LargeCarriedPotMakesSameCallMoreAttractive()
    {
        GameState smallPotGame = CreateDealerResponseGame(
            20,
            9,
            2,
            4,
            7,
            2);
        GameState largePotGame = CreateDealerResponseGame(
            20,
            9,
            2,
            4,
            7,
            2);
        Assert.That(largePotGame.Pot.TryAdd(40), Is.True);
        var dealerAi = new DealerAi();

        Assert.That(
            dealerAi.TryEvaluate(smallPotGame, out DealerHandOdds smallPotOdds),
            Is.True);
        Assert.That(
            dealerAi.TryEvaluate(largePotGame, out DealerHandOdds largePotOdds),
            Is.True);

        DealerActionPlan smallPotPlan = dealerAi.Decide(
            smallPotGame,
            50,
            0);
        DealerActionPlan largePotPlan = dealerAi.Decide(
            largePotGame,
            50,
            0);

        AssertOddsEqual(smallPotOdds, largePotOdds);
        Assert.That(smallPotPlan.Decision, Is.EqualTo(DealerDecision.Fold));
        Assert.That(largePotPlan.Decision, Is.EqualTo(DealerDecision.Call));
    }

    [Test]
    public void Decide_RaiseAmountVariesWithSizingRoll()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            1,
            2,
            4,
            7,
            2);
        var dealerAi = new DealerAi();

        DealerActionPlan smallPlan = dealerAi.Decide(gameState, 99, 0);
        DealerActionPlan largePlan = dealerAi.Decide(gameState, 99, 60);

        Assert.That(smallPlan.Decision, Is.EqualTo(DealerDecision.Raise));
        Assert.That(largePlan.Decision, Is.EqualTo(DealerDecision.Raise));
        Assert.That(smallPlan.RaiseBy, Is.GreaterThan(1));
        Assert.That(largePlan.RaiseBy, Is.GreaterThan(smallPlan.RaiseBy));
    }

    [TestCase(0)]
    [TestCase(25)]
    [TestCase(55)]
    [TestCase(75)]
    [TestCase(99)]
    public void Execute_SelectedRaiseSizeIsValidForGameState(int raiseRoll)
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            1,
            2,
            4,
            7,
            2);
        var dealerAi = new DealerAi();
        int callAmount =
            gameState.Betting.GetCallAmount(TurnOwner.Dealer);
        int maxRaiseBy = gameState.DealerChips.Count - callAmount;

        DealerActionPlan plan = dealerAi.Decide(
            gameState,
            99,
            raiseRoll);

        Assert.That(
            plan.Decision,
            Is.EqualTo(DealerDecision.Raise)
                .Or.EqualTo(DealerDecision.AllIn));
        Assert.That(plan.RaiseBy, Is.InRange(1, maxRaiseBy));

        if (plan.Decision == DealerDecision.Raise)
        {
            Assert.That(plan.RaiseBy, Is.LessThan(maxRaiseBy));
        }
        else
        {
            Assert.That(plan.RaiseBy, Is.EqualTo(maxRaiseBy));
        }

        Assert.That(dealerAi.TryExecute(gameState, plan), Is.True);
    }

    [Test]
    public void Execute_ExactStackCallCanSpendDealerToZero()
    {
        GameState gameState = CreateDealerResponseGame(
            4,
            1,
            2,
            4,
            7,
            4);
        var dealerAi = new DealerAi();

        DealerActionPlan plan = dealerAi.Decide(gameState, 99, 50);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.Call));
        Assert.That(dealerAi.TryExecute(gameState, plan), Is.True);
        Assert.That(gameState.DealerChips.Count, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Execute_ShortAllInUsesExistingRefundAndShowdownFlow()
    {
        GameState gameState = CreateDealerResponseGame(
            3,
            1,
            2,
            4,
            7,
            5);
        var dealerAi = new DealerAi();

        DealerActionPlan plan = dealerAi.Decide(gameState, 99, 50);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.AllIn));
        Assert.That(dealerAi.TryExecute(gameState, plan), Is.True);
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(17));
        Assert.That(gameState.DealerChips.Count, Is.Zero);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(3));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void DecideAndExecute_RefusePlayerTurn()
    {
        GameState gameState = CreateDealerTurnGame(
            20,
            20,
            1,
            2,
            4,
            7,
            TurnOwner.Player);
        var dealerAi = new DealerAi();
        var foldPlan = new DealerActionPlan(DealerDecision.Fold);

        DealerActionPlan plan = dealerAi.Decide(gameState, 99, 99);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.None));
        Assert.That(dealerAi.TryExecute(gameState, foldPlan), Is.False);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
    }

    [Test]
    public void DecideAndExecute_RefuseNonBettingPhase()
    {
        GameState gameState = CreateDealerTurnGame(
            20,
            20,
            1,
            2,
            4,
            7);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);
        var dealerAi = new DealerAi();
        var foldPlan = new DealerActionPlan(DealerDecision.Fold);

        DealerActionPlan plan = dealerAi.Decide(gameState, 99, 99);

        Assert.That(plan.Decision, Is.EqualTo(DealerDecision.None));
        Assert.That(dealerAi.TryExecute(gameState, foldPlan), Is.False);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
    }

    private static void AssertOddsEqual(
        DealerHandOdds expected,
        DealerHandOdds actual)
    {
        Assert.That(
            actual.WinProbability,
            Is.EqualTo(expected.WinProbability).Within(0.000001d));
        Assert.That(
            actual.DrawProbability,
            Is.EqualTo(expected.DrawProbability).Within(0.000001d));
        Assert.That(
            actual.LossProbability,
            Is.EqualTo(expected.LossProbability).Within(0.000001d));
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
        Assert.That(gameState.TrySetPlayerCard(new Card(playerRank)), Is.True);
        Assert.That(gameState.TrySetDealerCard(new Card(dealerRank)), Is.True);
        Assert.That(
            gameState.TrySetCommunityCards(
                new Card(communityRank1),
                new Card(communityRank2)),
            Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.Turn.TrySet(turn), Is.True);
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
