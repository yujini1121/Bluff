using System.Linq;
using NUnit.Framework;

public sealed class MatchStatisticsTests
{
    [Test]
    public void ClassicToEndless_AccumulatesTenDrawsAndFinalWinWithCarriedPot()
    {
        var deck = new Deck(Enumerable.Range(0, 40).Select(_ => new Card(5)));
        var state = new GameState(10, 10, deck);
        var stats = state.Statistics;

        for (int round = 1; round <= 10; round++)
        {
            Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
            if (round == 1)
            {
                state.AdvancePlayTime(3);
                Assert.That(state.TryRaise(4), Is.True);
                Assert.That(state.TryCall(), Is.True);
                state.AdvancePlayTime(2);
            }
            else
            {
                state.AdvancePlayTime(1);
                state.TrySetPhase(GamePhase.Showdown);
            }

            Assert.That(state.TrySettleShowdown(out var winner), Is.True);
            Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
            if (round < 10)
            {
                state.AdvancePlayTime(1);
                Assert.That(state.TryPrepareNextRound(), Is.True);
            }
        }

        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        state.AdvancePlayTime(10);
        Assert.That(stats.PlaySeconds, Is.EqualTo(23));
        Assert.That(stats.PlayerChipsBet, Is.EqualTo(5));
        Assert.That(stats.LargestPotAmount, Is.EqualTo(10));
        Assert.That(stats.LargestPotRound, Is.EqualTo(1));
        Assert.That(stats.RoundsPlayed, Is.EqualTo(10));

        Assert.That(state.TryContinueAsEndless(), Is.True);
        Assert.That(state.TryPrepareNextRound(), Is.True);
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        state.AdvancePlayTime(4);
        Assert.That(state.TryAllIn(), Is.True);
        Assert.That(state.TryCall(), Is.True);
        state.TrySetPlayerCard(new Card(4));
        state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        Assert.That(state.TrySettleShowdown(out var finalWinner), Is.True);
        Assert.That(finalWinner, Is.EqualTo(RoundWinner.Player));
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(state.Statistics, Is.SameAs(stats));
        Assert.That(stats.RoundsWon, Is.EqualTo(1));
        Assert.That(stats.RoundsLost, Is.Zero);
        Assert.That(stats.RoundsDrawn, Is.EqualTo(10));
        Assert.That(stats.RoundsPlayed, Is.EqualTo(11));
        Assert.That(stats.PlaySeconds, Is.EqualTo(27));
        Assert.That(stats.LongestRound, Is.EqualTo(1));
        Assert.That(stats.LongestRoundSeconds, Is.EqualTo(5));
        Assert.That(stats.PlayerChipsBet, Is.EqualTo(10));
        Assert.That(stats.LargestPotAmount, Is.EqualTo(20));
        Assert.That(stats.LargestPotRound, Is.EqualTo(11));
    }

    [Test]
    public void Fold_RecordsBothOutcomesAndExcludesFailedBetsAndFoldPenalty()
    {
        var state = new GameState(30, 30, new Deck(Enumerable.Range(0, 8).Select(_ => new Card(5))));
        var stats = state.Statistics;
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        state.AdvancePlayTime(2);
        Assert.That(state.TryRaise(30), Is.False);
        Assert.That(state.TryFold(), Is.True);
        Assert.That(state.FoldPenaltyAmount, Is.EqualTo(10));
        Assert.That(state.TryFold(), Is.False);
        Assert.That(stats.RoundsLost, Is.EqualTo(1));
        Assert.That(stats.PlayerChipsBet, Is.EqualTo(1));

        Assert.That(state.TryPrepareNextRound(), Is.True);
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(state.TryRaise(3), Is.True);
        state.AdvancePlayTime(4);
        Assert.That(state.TryFold(), Is.True);
        Assert.That(stats.RoundsWon, Is.EqualTo(1));
        Assert.That(stats.RoundsPlayed, Is.EqualTo(2));
        Assert.That(stats.PlayerChipsBet, Is.EqualTo(5));
        Assert.That(stats.LargestPotAmount, Is.EqualTo(5));
        Assert.That(stats.LargestPotRound, Is.EqualTo(2));
        Assert.That(stats.LongestRound, Is.EqualTo(2));
        Assert.That(stats.LongestRoundSeconds, Is.EqualTo(4));
    }

    [Test]
    public void ShortAllIn_SubtractsUnmatchedPlayerBetFromTotal()
    {
        var state = new GameState(20, 4, Deck.CreateIndianHoldemDeck());
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(state.TryRaise(7), Is.True);
        Assert.That(state.TryAllIn(), Is.True);
        Assert.That(state.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(state.Pot.Amount, Is.EqualTo(8));
        Assert.That(state.Statistics.PlayerChipsBet, Is.EqualTo(4));
        Assert.That(state.Statistics.LargestPotAmount, Is.EqualTo(9));
        Assert.That(state.Statistics.RoundsPlayed, Is.Zero);
    }
}
