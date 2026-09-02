using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GameModeTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        GameModeSelection.Select(GameMode.RoundLimited);
    }

    [Test]
    public void RoundLimited_RoundsOneThroughNineCanPrepareNextRound()
    {
        GameState gameState = CreateDrawGame(
            100,
            100,
            GameMode.RoundLimited,
            10);

        for (int round = 1; round <= 9; round++)
        {
            CompleteDrawRound(gameState);

            Assert.That(gameState.CurrentRound, Is.EqualTo(round));
            Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
            Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
            Assert.That(gameState.TryPrepareNextRound(), Is.True);
            Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Setup));
        }

        Assert.That(gameState.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(gameState.CurrentRound, Is.EqualTo(10));
    }

    [Test]
    public void RoundLimited_TenthRoundEndsWithPlayerChipLead()
    {
        GameState gameState = CompleteRoundLimitedMatch(42, 38);

        AssertFinalRoundLimitedResult(gameState, GameWinner.Player);
    }

    [Test]
    public void RoundLimited_TenthRoundEndsWithDealerChipLead()
    {
        GameState gameState = CompleteRoundLimitedMatch(38, 42);

        AssertFinalRoundLimitedResult(gameState, GameWinner.Dealer);
    }

    [Test]
    public void RoundLimited_TenthRoundEndsInDrawWithEqualChips()
    {
        GameState gameState = CompleteRoundLimitedMatch(40, 40);

        AssertFinalRoundLimitedResult(gameState, GameWinner.Draw);
    }

    [Test]
    public void RoundLimited_TenthRoundDrawKeepsPotAndExcludesItFromWinner()
    {
        GameState gameState = CreateDrawGame(
            42,
            38,
            GameMode.RoundLimited,
            10);
        Assert.That(gameState.Pot.TryAdd(20), Is.True);

        CompleteRounds(gameState, 10);

        AssertFinalRoundLimitedResult(gameState, GameWinner.Player);
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(42));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(38));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(20));
    }

    [Test]
    public void RoundLimited_ZeroChipsBeforeRoundTenKeepsExistingGameOver()
    {
        var gameState = new GameState(
            1,
            10,
            CreateDeckWithRepeatedRank(40, 5),
            GameMode.RoundLimited);

        Assert.That(gameState.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(gameState.CurrentRound, Is.EqualTo(1));
        gameState.TrySetPlayerCard(new Card(1));
        gameState.TrySetDealerCard(new Card(10));
        gameState.TrySetCommunityCards(new Card(3), new Card(4));

        Assert.That(
            gameState.TrySettleShowdown(out RoundWinner winner),
            Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Dealer));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
    }

    [Test]
    public void Endless_ExhaustedDeckIsReplacedAndNextRoundStarts()
    {
        GameState gameState = CreateDrawGame(
            10,
            10,
            GameMode.Endless,
            1);
        Deck originalDeck = gameState.Deck;

        CompleteDrawRound(gameState);
        Assert.That(originalDeck.RemainingCount, Is.Zero);
        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.Deck, Is.Not.SameAs(originalDeck));
        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(40));
        Assert.That(gameState.TryStartRound(TurnOwner.Dealer), Is.True);
        Assert.That(gameState.CurrentRound, Is.EqualTo(2));
        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(36));
    }

    [Test]
    public void Endless_DeckReplacementPreservesCarriedDrawPotAndMatchState()
    {
        GameState gameState = CreateDrawGame(
            10,
            10,
            GameMode.Endless,
            1);

        CompleteDrawRound(gameState);
        int playerChips = gameState.PlayerChips.Count;
        int dealerChips = gameState.DealerChips.Count;
        int carriedPot = gameState.Pot.Amount;

        Assert.That(gameState.TryPrepareNextRound(), Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChips));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChips));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(carriedPot));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));
        Assert.That(gameState.TryStartRound(TurnOwner.Dealer), Is.True);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(carriedPot));
    }

    [Test]
    public void Endless_UsesMultipleDecksWithoutRoundLimitGameOver()
    {
        var gameState = new GameState(
            100,
            100,
            Deck.CreateIndianHoldemDeck(),
            GameMode.Endless);

        for (int round = 1; round <= 25; round++)
        {
            Assert.That(gameState.TryStartRound(TurnOwner.Player), Is.True);
            Assert.That(gameState.TryFold(true), Is.True);
            Assert.That(gameState.CurrentRound, Is.EqualTo(round));
            Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
            Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.None));

            if (round < 25)
            {
                Assert.That(gameState.TryPrepareNextRound(), Is.True);
            }
        }
    }

    [Test]
    public void Endless_ZeroChipsStillEndsGame()
    {
        var gameState = new GameState(
            0,
            10,
            Deck.CreateIndianHoldemDeck(),
            GameMode.Endless);
        gameState.TrySetPlayerCard(new Card(1));
        gameState.TrySetDealerCard(new Card(2));
        gameState.TrySetCommunityCards(new Card(3), new Card(4));
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);

        Assert.That(gameState.TryFold(true), Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(GameWinner.Dealer));
    }

    [Test]
    public void GameplayCreationUsesModeSelectedOnTitle()
    {
        GameModeSelection.Select(GameMode.Endless);
        IndianHoldemDebugUI ui = CreateGameplayUi();

        try
        {
            Assert.That(ui.CurrentGameMode, Is.EqualTo(GameMode.Endless));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void GameplayRecreationKeepsModeAndStartsNewMatch()
    {
        GameModeSelection.Select(GameMode.Endless);
        IndianHoldemDebugUI firstUi = CreateGameplayUi();
        InvokePrivate(firstUi, "StartRound");
        Assert.That(firstUi.CurrentRound, Is.EqualTo(1));
        Object.DestroyImmediate(firstUi.gameObject);

        IndianHoldemDebugUI restartedUi = CreateGameplayUi();

        try
        {
            Assert.That(restartedUi.CurrentGameMode, Is.EqualTo(GameMode.Endless));
            Assert.That(restartedUi.CurrentRound, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(restartedUi.gameObject);
        }
    }

    private static GameState CompleteRoundLimitedMatch(
        int playerChips,
        int dealerChips)
    {
        GameState gameState = CreateDrawGame(
            playerChips,
            dealerChips,
            GameMode.RoundLimited,
            10);
        CompleteRounds(gameState, 10);
        return gameState;
    }

    private static void CompleteRounds(GameState gameState, int roundCount)
    {
        for (int round = 1; round <= roundCount; round++)
        {
            CompleteDrawRound(gameState);

            if (round < roundCount)
            {
                Assert.That(gameState.TryPrepareNextRound(), Is.True);
            }
        }
    }

    private static void CompleteDrawRound(GameState gameState)
    {
        Assert.That(gameState.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Showdown), Is.True);
        Assert.That(
            gameState.TrySettleShowdown(out RoundWinner winner),
            Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
    }

    private static void AssertFinalRoundLimitedResult(
        GameState gameState,
        GameWinner expectedWinner)
    {
        Assert.That(
            gameState.CurrentRound,
            Is.EqualTo(GameState.MaximumRoundCount));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(gameState.FinalWinner, Is.EqualTo(expectedWinner));
        Assert.That(gameState.TryPrepareNextRound(), Is.False);
    }

    private static GameState CreateDrawGame(
        int playerChips,
        int dealerChips,
        GameMode gameMode,
        int roundCount)
    {
        return new GameState(
            playerChips,
            dealerChips,
            CreateDeckWithRepeatedRank(roundCount * 4, 5),
            gameMode);
    }

    private static Deck CreateDeckWithRepeatedRank(int cardCount, int rank)
    {
        var cards = new Card[cardCount];

        for (int index = 0; index < cards.Length; index++)
        {
            cards[index] = new Card(rank);
        }

        return new Deck(cards);
    }

    private static IndianHoldemDebugUI CreateGameplayUi()
    {
        var gameObject = new GameObject("Game Mode Test");
        gameObject.SetActive(false);
        ItemSystem itemSystem = gameObject.AddComponent<ItemSystem>();
        IndianHoldemDebugUI ui =
            gameObject.AddComponent<IndianHoldemDebugUI>();
        SetField(ui, "itemSystem", itemSystem);
        InvokePrivate(ui, "CreateDebugGame");
        return ui;
    }

    private static void InvokePrivate(
        IndianHoldemDebugUI ui,
        string methodName)
    {
        MethodInfo method = typeof(IndianHoldemDebugUI).GetMethod(
            methodName,
            PrivateInstance);
        Assert.That(method, Is.Not.Null);
        method.Invoke(ui, null);
    }

    private static void SetField<T>(
        IndianHoldemDebugUI ui,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(IndianHoldemDebugUI).GetField(
            fieldName,
            PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(ui, value);
    }
}
