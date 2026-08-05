using System;
using NUnit.Framework;

public sealed class CoreStateTests
{
    [Test]
    public void GameState_InitializesAllCoreState()
    {
        var deck = new Deck(new[] { new Card(-1), new Card(20) });

        var gameState = new GameState(10, 15, deck);

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(15));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Deck, Is.SameAs(deck));
        Assert.That(gameState.PlayerCard, Is.Null);
        Assert.That(gameState.DealerCard, Is.Null);
    }

    [Test]
    public void Deck_ShufflesDrawsAndFailsSafelyWhenEmpty()
    {
        var deck = new Deck(
            new[] { new Card(1), new Card(2), new Card(3) },
            new Random(1234));

        deck.Shuffle();

        Assert.That(deck.TryDraw(out Card firstCard), Is.True);
        Assert.That(firstCard, Is.Not.Null);
        Assert.That(deck.TryDraw(out _), Is.True);
        Assert.That(deck.TryDraw(out _), Is.True);
        Assert.That(deck.RemainingCount, Is.Zero);
        Assert.That(deck.TryDraw(out Card emptyCard), Is.False);
        Assert.That(emptyCard, Is.Null);
    }

    [Test]
    public void ChipStack_RejectsInvalidOrUnavailableAmounts()
    {
        var chips = new ChipStack(-10);

        Assert.That(chips.Count, Is.Zero);
        Assert.That(chips.TryAdd(-1), Is.False);
        Assert.That(chips.TryAdd(10), Is.True);
        Assert.That(chips.TrySpend(11), Is.False);
        Assert.That(chips.TrySpend(-1), Is.False);
        Assert.That(chips.TrySpend(4), Is.True);
        Assert.That(chips.Count, Is.EqualTo(6));
    }

    [Test]
    public void Pot_TakeAllReturnsAmountAndClearsPot()
    {
        var pot = new Pot();

        Assert.That(pot.TryAdd(-1), Is.False);
        Assert.That(pot.TryAdd(12), Is.True);
        Assert.That(pot.TakeAll(), Is.EqualTo(12));
        Assert.That(pot.Amount, Is.Zero);
    }

    [Test]
    public void TurnState_SetsSwitchesAndResetsOwner()
    {
        var turn = new TurnState();

        Assert.That(turn.TrySwitch(), Is.False);
        Assert.That(turn.TrySet(TurnOwner.Player), Is.True);
        Assert.That(turn.TrySwitch(), Is.True);
        Assert.That(turn.Owner, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(turn.TrySet((TurnOwner)99), Is.False);

        turn.Reset();

        Assert.That(turn.Owner, Is.EqualTo(TurnOwner.None));
    }
}
