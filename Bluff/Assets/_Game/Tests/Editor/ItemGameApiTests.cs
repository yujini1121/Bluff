using NUnit.Framework;

public sealed class ItemGameApiTests
{
    [Test]
    public void GiveChips_AddsChipsToTheSelectedTarget()
    {
        var gameState = new GameState(10, 15, CreateDeck());
        var itemGameApi = new ItemGameApi(gameState);

        Assert.That(itemGameApi.TryGiveChips(TurnOwner.Player, 3), Is.True);
        Assert.That(itemGameApi.TryGiveChips(TurnOwner.Dealer, 4), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(13));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(19));
        Assert.That(gameState.Pot.Amount, Is.Zero);
    }

    [Test]
    public void GiveChips_RejectsInvalidTargetAmountAndOverflowWithoutChanges()
    {
        var gameState = new GameState(10, 15, CreateDeck());
        var itemGameApi = new ItemGameApi(gameState);

        Assert.That(itemGameApi.TryGiveChips(TurnOwner.None, 3), Is.False);
        Assert.That(itemGameApi.TryGiveChips((TurnOwner)99, 3), Is.False);
        Assert.That(itemGameApi.TryGiveChips(TurnOwner.Player, 0), Is.False);
        Assert.That(itemGameApi.TryGiveChips(TurnOwner.Dealer, -1), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(15));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));

        var fullChipState = new GameState(int.MaxValue, 10, CreateDeck());
        var fullChipApi = new ItemGameApi(fullChipState);

        Assert.That(fullChipApi.TryGiveChips(TurnOwner.Player, 1), Is.False);
        Assert.That(fullChipState.PlayerChips.Count, Is.EqualTo(int.MaxValue));
        Assert.That(fullChipState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(fullChipState.Pot.Amount, Is.Zero);
    }

    [Test]
    public void ReplaceCard_DrawsFromDeckForEveryCardTarget()
    {
        var deck = new Deck(new[]
        {
            new Card(1),
            new Card(2),
            new Card(3),
            new Card(4)
        });
        GameState gameState = CreateGameWithRoundCards(deck);
        var itemGameApi = new ItemGameApi(gameState);

        Assert.That(itemGameApi.TryReplaceCard(CardTarget.Player), Is.True);
        Assert.That(itemGameApi.TryReplaceCard(CardTarget.Dealer), Is.True);
        Assert.That(itemGameApi.TryReplaceCard(CardTarget.CommunityCard1), Is.True);
        Assert.That(itemGameApi.TryReplaceCard(CardTarget.CommunityCard2), Is.True);

        Assert.That(gameState.PlayerCard.Rank, Is.EqualTo(4));
        Assert.That(gameState.DealerCard.Rank, Is.EqualTo(3));
        Assert.That(gameState.CommunityCard1.Rank, Is.EqualTo(2));
        Assert.That(gameState.CommunityCard2.Rank, Is.EqualTo(1));
        Assert.That(deck.RemainingCount, Is.Zero);
    }

    [Test]
    public void ReplaceCard_RejectsInvalidTargetEmptyDeckAndUndealtCardWithoutChanges()
    {
        var deck = new Deck(new[] { new Card(1) });
        GameState gameState = CreateGameWithRoundCards(deck);
        var itemGameApi = new ItemGameApi(gameState);
        Card playerCard = gameState.PlayerCard;
        Card dealerCard = gameState.DealerCard;
        Card communityCard1 = gameState.CommunityCard1;
        Card communityCard2 = gameState.CommunityCard2;

        Assert.That(itemGameApi.TryReplaceCard(CardTarget.None), Is.False);
        Assert.That(itemGameApi.TryReplaceCard((CardTarget)99), Is.False);

        Assert.That(gameState.PlayerCard, Is.SameAs(playerCard));
        Assert.That(gameState.DealerCard, Is.SameAs(dealerCard));
        Assert.That(gameState.CommunityCard1, Is.SameAs(communityCard1));
        Assert.That(gameState.CommunityCard2, Is.SameAs(communityCard2));
        Assert.That(deck.RemainingCount, Is.EqualTo(1));

        var emptyDeck = new Deck(new Card[0]);
        GameState emptyDeckState = CreateGameWithRoundCards(emptyDeck);
        var emptyDeckApi = new ItemGameApi(emptyDeckState);
        Card originalPlayerCard = emptyDeckState.PlayerCard;

        Assert.That(emptyDeckApi.TryReplaceCard(CardTarget.Player), Is.False);
        Assert.That(emptyDeckState.PlayerCard, Is.SameAs(originalPlayerCard));
        Assert.That(emptyDeck.RemainingCount, Is.Zero);

        var undealtDeck = new Deck(new[] { new Card(1) });
        var undealtState = new GameState(10, 10, undealtDeck);
        var undealtApi = new ItemGameApi(undealtState);

        Assert.That(undealtApi.TryReplaceCard(CardTarget.Player), Is.False);
        Assert.That(undealtState.PlayerCard, Is.Null);
        Assert.That(undealtDeck.RemainingCount, Is.EqualTo(1));
    }

    [Test]
    public void Call_UsesExistingBettingRules()
    {
        var gameState = new GameState(10, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(gameState.TryRaise(4), Is.True);
        var itemGameApi = new ItemGameApi(gameState);

        Assert.That(itemGameApi.TryCall(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(8));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(4));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void Call_FailsWithoutChangingStateWhenNormalCallIsNotAllowed()
    {
        var gameState = new GameState(10, 10, CreateDeck());
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        var itemGameApi = new ItemGameApi(gameState);

        Assert.That(itemGameApi.TryCall(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    private static GameState CreateGameWithRoundCards(Deck deck)
    {
        var gameState = new GameState(10, 10, deck);
        gameState.TrySetPlayerCard(new Card(5));
        gameState.TrySetDealerCard(new Card(6));
        gameState.TrySetCommunityCards(new Card(7), new Card(8));
        return gameState;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
