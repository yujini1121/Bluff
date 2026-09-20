using NUnit.Framework;

public sealed class DealerItemAiTests
{
    private static readonly ItemType[] AllItems =
    {
        ItemType.refreshCard,
        ItemType.prizmChip,
        ItemType.chipPocket,
        ItemType.defy
    };

    [Test]
    public void Decide_PlayerTurnSelectsNoItem()
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            4,
            1,
            4,
            2,
            TurnOwner.Player);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            AllItems,
            new DealerActionPlan(DealerDecision.Fold));

        AssertNoItem(plan);
    }

    [TestCase(GamePhase.Setup)]
    [TestCase(GamePhase.Showdown)]
    [TestCase(GamePhase.RoundEnd)]
    [TestCase(GamePhase.GameOver)]
    public void Decide_NonBettingPhaseSelectsNoItem(GamePhase phase)
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            4,
            1,
            4,
            2);
        Assert.That(gameState.TrySetPhase(phase), Is.True);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            AllItems,
            new DealerActionPlan(DealerDecision.Fold));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_DoesNotSelectAnItemDealerDoesNotOwn()
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            4,
            1,
            4,
            2);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.defy },
            new DealerActionPlan(DealerDecision.Fold));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_ChangingOnlyDealerCardKeepsItemPlanIdentical()
    {
        GameState lowDealerCardGame = CreateDealerResponseGame(
            20,
            4,
            1,
            4,
            2,
            2);
        GameState highDealerCardGame = CreateDealerResponseGame(
            20,
            4,
            10,
            4,
            2,
            2);
        var dealerAi = new DealerAi();
        var itemAi = new DealerItemAi();
        DealerActionPlan lowCardAction = dealerAi.Decide(
            lowDealerCardGame,
            0,
            0);
        DealerActionPlan highCardAction = dealerAi.Decide(
            highDealerCardGame,
            0,
            0);

        DealerItemPlan lowCardPlan = itemAi.Decide(
            lowDealerCardGame,
            AllItems,
            lowCardAction);
        DealerItemPlan highCardPlan = itemAi.Decide(
            highDealerCardGame,
            AllItems,
            highCardAction);

        Assert.That(lowCardAction.Decision, Is.EqualTo(DealerDecision.Fold));
        Assert.That(highCardAction.Decision, Is.EqualTo(lowCardAction.Decision));
        Assert.That(lowCardPlan.SelectedItem, Is.EqualTo(ItemType.prizmChip));
        Assert.That(
            highCardPlan.ShouldUseItem,
            Is.EqualTo(lowCardPlan.ShouldUseItem));
        Assert.That(highCardPlan.SelectedItem, Is.EqualTo(lowCardPlan.SelectedItem));
    }

    [Test]
    public void Decide_LowEquityBeforeVoluntaryBettingSelectsRefreshCard()
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            4,
            1,
            4,
            2);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.refreshCard },
            new DealerActionPlan(DealerDecision.Fold));

        AssertSelected(plan, ItemType.refreshCard);
    }

    [Test]
    public void Decide_HighEquityDoesNotSelectRefreshCard()
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            1,
            2,
            4,
            7);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.refreshCard },
            new DealerActionPlan(DealerDecision.Raise, 1));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_LowEquityAfterPlayerRaiseDoesNotSelectRefreshCard()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            4,
            1,
            4,
            2,
            2);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.refreshCard },
            new DealerActionPlan(DealerDecision.Fold));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_FoldFacingPlayerRaiseSelectsPrizmChip()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            4,
            1,
            4,
            2,
            2);
        DealerActionPlan actionPlan = new DealerAi().Decide(
            gameState,
            0,
            0);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.prizmChip },
            actionPlan);

        Assert.That(actionPlan.Decision, Is.EqualTo(DealerDecision.Fold));
        AssertSelected(plan, ItemType.prizmChip);
    }

    [Test]
    public void Decide_FoldWithoutCallAmountDoesNotSelectPrizmChip()
    {
        GameState gameState = CreateBettingGame(
            20,
            20,
            4,
            1,
            4,
            2);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.prizmChip },
            new DealerActionPlan(DealerDecision.Fold));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_LowDealerChipCountSelectsChipPocket()
    {
        GameState gameState = CreateBettingGame(
            20,
            5,
            1,
            2,
            4,
            7);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.chipPocket },
            new DealerActionPlan(DealerDecision.Raise, 1));

        AssertSelected(plan, ItemType.chipPocket);
    }

    [Test]
    public void Decide_DealerAboveLowChipThresholdDoesNotSelectChipPocket()
    {
        GameState gameState = CreateBettingGame(
            20,
            6,
            1,
            2,
            4,
            7);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.chipPocket },
            new DealerActionPlan(DealerDecision.Raise, 1));

        AssertNoItem(plan);
    }

    [Test]
    public void Decide_StrongEnoughDealerFacingBurdensomeCallSelectsDefy()
    {
        GameState gameState = CreateDealerResponseGame(
            6,
            1,
            2,
            4,
            7,
            3);
        DealerActionPlan actionPlan = new DealerAi().Decide(
            gameState,
            10,
            0);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.defy },
            actionPlan);

        Assert.That(actionPlan.Decision, Is.Not.EqualTo(DealerDecision.Fold));
        AssertSelected(plan, ItemType.defy);
    }

    [Test]
    public void Decide_SmallCallBurdenDoesNotSelectDefy()
    {
        GameState gameState = CreateDealerResponseGame(
            20,
            1,
            2,
            4,
            7,
            3);
        DealerActionPlan actionPlan = new DealerAi().Decide(
            gameState,
            10,
            0);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.defy },
            actionPlan);

        Assert.That(actionPlan.Decision, Is.Not.EqualTo(DealerDecision.Fold));
        AssertNoItem(plan);
    }

    [Test]
    public void Decide_LowEquityBluffDoesNotSelectDefy()
    {
        GameState gameState = CreateDealerResponseGame(
            5,
            4,
            1,
            4,
            2,
            3);
        DealerActionPlan actionPlan = new DealerAi().Decide(
            gameState,
            99,
            0);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            new[] { ItemType.defy },
            actionPlan);

        Assert.That(actionPlan.Decision, Is.Not.EqualTo(DealerDecision.Fold));
        AssertNoItem(plan);
    }

    [Test]
    public void Decide_MultipleEligibleItemsReturnsOnlyOneItem()
    {
        GameState gameState = CreateDealerResponseGame(
            5,
            4,
            1,
            4,
            2,
            3);
        var itemAi = new DealerItemAi();

        DealerItemPlan plan = itemAi.Decide(
            gameState,
            AllItems,
            new DealerActionPlan(DealerDecision.Fold));

        AssertSelected(plan, ItemType.prizmChip);
    }

    private static void AssertNoItem(DealerItemPlan plan)
    {
        Assert.That(plan.ShouldUseItem, Is.False);
        Assert.That(plan.SelectedItem, Is.Null);
    }

    private static void AssertSelected(
        DealerItemPlan plan,
        ItemType expectedItem)
    {
        Assert.That(plan.ShouldUseItem, Is.True);
        Assert.That(plan.SelectedItem, Is.EqualTo(expectedItem));
    }

    private static GameState CreateDealerResponseGame(
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        int raiseBy)
    {
        GameState gameState = CreateBettingGame(
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

    private static GameState CreateBettingGame(
        int playerChips,
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        TurnOwner turn = TurnOwner.Dealer)
    {
        var gameState = new GameState(
            playerChips,
            dealerChips,
            CreateDeck());
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
