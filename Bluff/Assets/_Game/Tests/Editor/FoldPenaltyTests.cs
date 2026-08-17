using NUnit.Framework;

public sealed class FoldPenaltyTests
{
    [Test]
    public void PlayerStraightFold_PaysTenChipsToDealer()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            6,
            9,
            4,
            5);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(30));
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(10));
        AssertFoldEndedRound(gameState, TurnOwner.Player);
    }

    [Test]
    public void DealerStraightFold_PaysTenChipsToPlayer()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Dealer,
            20,
            20,
            9,
            6,
            4,
            5);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(30));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(10));
        AssertFoldEndedRound(gameState, TurnOwner.Dealer);
    }

    [Test]
    public void TripleFold_PaysTenChipPenalty()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            4,
            9,
            4,
            4);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(30));
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(10));
    }

    [Test]
    public void DoubleFold_DoesNotPayPenalty()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            4,
            9,
            4,
            7);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
    }

    [Test]
    public void NumberFold_DoesNotPayPenalty()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            1,
            9,
            4,
            7);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
    }

    [Test]
    public void StraightFold_WithFewerThanTenChips_PaysAllRemainingChips()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            6,
            20,
            6,
            9,
            4,
            5);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(26));
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(6));
    }

    [Test]
    public void FoldPenalty_ThatEmptiesChipStack_EndsGameThroughExistingFlow()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            5,
            20,
            6,
            9,
            4,
            5);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.Zero);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(25));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Player));
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(5));
    }

    [Test]
    public void StraightFold_AwardsExistingPotAndPenaltyToOpponent()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            6,
            9,
            4,
            5);
        Assert.That(gameState.Pot.TryAdd(6), Is.True);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(36));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(10));
    }

    [Test]
    public void StraightFold_WithPot_PreservesTotalChipCount()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Dealer,
            20,
            20,
            9,
            10,
            1,
            2);
        Assert.That(gameState.Pot.TryAdd(8), Is.True);
        long totalChipsBefore = GetTotalChips(gameState);

        Assert.That(gameState.TryFold(), Is.True);

        Assert.That(GetTotalChips(gameState), Is.EqualTo(totalChipsBefore));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(38));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(10));
        Assert.That(gameState.Pot.Amount, Is.Zero);
    }

    [Test]
    public void Fold_FailsAtomicallyWhenPotAndPenaltyWouldOverflowWinnerChips()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            int.MaxValue - 5,
            6,
            9,
            4,
            5);
        Assert.That(gameState.Pot.TryAdd(1), Is.True);

        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int potAmount = gameState.Pot.Amount;
        GamePhase phase = gameState.Phase;
        TurnOwner currentTurn = gameState.CurrentTurn;

        Assert.That(gameState.TryFold(), Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potAmount));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.None));
        Assert.That(gameState.Phase, Is.EqualTo(phase));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(currentTurn));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
    }

    [Test]
    public void FoldPenaltyWaiver_SkipsOnlyPenaltyAndStillAwardsPot()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            6,
            9,
            4,
            5);
        Assert.That(gameState.Pot.TryAdd(6), Is.True);

        Assert.That(
            gameState.TryFold(isFoldPenaltyWaived: true),
            Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(26));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
        AssertFoldEndedRound(gameState, TurnOwner.Player);
    }

    [Test]
    public void PrepareNextRound_ClearsAppliedFoldPenaltyAmount()
    {
        GameState gameState = CreateFoldGame(
            TurnOwner.Player,
            20,
            20,
            6,
            9,
            4,
            5);
        Assert.That(gameState.TryFold(), Is.True);
        Assert.That(gameState.FoldPenaltyAmount, Is.EqualTo(10));

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.None));
    }

    private static GameState CreateFoldGame(
        TurnOwner foldedBy,
        int playerChips,
        int dealerChips,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2)
    {
        var gameState = new GameState(playerChips, dealerChips, CreateDeck());
        gameState.TrySetPlayerCard(new Card(playerRank));
        gameState.TrySetDealerCard(new Card(dealerRank));
        gameState.TrySetCommunityCards(
            new Card(communityRank1),
            new Card(communityRank2));
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(foldedBy);
        return gameState;
    }

    private static void AssertFoldEndedRound(
        GameState gameState,
        TurnOwner foldedBy)
    {
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.FoldedBy, Is.EqualTo(foldedBy));
        Assert.That(gameState.RoundEndReason, Is.EqualTo(RoundEndReason.Fold));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
    }

    private static long GetTotalChips(GameState gameState)
    {
        return (long)gameState.PlayerChips.Count +
               gameState.DealerChips.Count +
               gameState.Pot.Amount;
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }
}
