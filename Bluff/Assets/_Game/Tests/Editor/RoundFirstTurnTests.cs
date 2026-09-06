using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoundFirstTurnTests
{
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void PlayerFirst_PlayerWins_PlayerStartsNextRound()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Player);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);
            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));

            SettleShowdown(ui, 6, 4, 4, 5, RoundWinner.Player);
            InvokePrepareAndStartNextRound(ui);

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void PlayerFirst_DealerWins_DealerStartsNextRound()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Player);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);
            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));

            SettleShowdown(ui, 4, 6, 4, 5, RoundWinner.Dealer);
            InvokePrepareAndStartNextRound(ui);

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void DealerFirst_Draw_DealerStartsNextRound()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Dealer);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);
            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));

            SettleShowdown(ui, 9, 9, 4, 7, RoundWinner.Draw);
            InvokePrepareAndStartNextRound(ui);

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void PlayerFolds_DealerStartsNextRound()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Player);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);
            Assert.That(gameState.TryFold(), Is.True);

            InvokePrepareAndStartNextRound(ui);

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void DealerFolds_PlayerStartsNextRound()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Dealer);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);
            Assert.That(gameState.TryFold(), Is.True);

            InvokePrepareAndStartNextRound(ui);

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void ConsecutiveDraws_KeepOriginalFirstTurn()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Dealer);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            InvokeStartRound(ui);

            for (int index = 0; index < 2; index++)
            {
                Assert.That(
                    gameState.CurrentTurn,
                    Is.EqualTo(TurnOwner.Dealer));
                SettleShowdown(ui, 9, 9, 4, 7, RoundWinner.Draw);
                InvokePrepareAndStartNextRound(ui);
            }

            Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void StartRound_FailureDoesNotChangeNextFirstTurn()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Player);

        try
        {
            InvokeStartRound(ui);
            Assert.That(
                GetField<TurnOwner>(ui, "nextRoundFirstTurn"),
                Is.EqualTo(TurnOwner.Player));

            InvokeStartRound(ui);

            Assert.That(
                GetField<TurnOwner>(ui, "nextRoundFirstTurn"),
                Is.EqualTo(TurnOwner.Player));
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    private static IndianHoldemDebugUI CreateUi(TurnOwner firstTurn)
    {
        var gameObject = new GameObject("Round First Turn Test");
        gameObject.SetActive(false);
        ItemSystem itemSystem = gameObject.AddComponent<ItemSystem>();
        IndianHoldemDebugUI ui =
            gameObject.AddComponent<IndianHoldemDebugUI>();

        SetField(ui, "firstTurn", firstTurn);
        SetField(ui, "playerStartingChips", 100);
        SetField(ui, "dealerStartingChips", 100);
        SetField(ui, "itemSystem", itemSystem);
        InvokePrivate(ui, "CreateDebugGame");
        return ui;
    }

    private static void InvokeStartRound(IndianHoldemDebugUI ui)
    {
        InvokePrivate(ui, "StartRound");
    }

    private static void InvokePrepareAndStartNextRound(
        IndianHoldemDebugUI ui)
    {
        InvokePrivate(ui, "PrepareAndStartNextRound");
    }

    private static void SettleShowdown(
        IndianHoldemDebugUI ui,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2,
        RoundWinner expectedWinner)
    {
        GameState gameState = GetField<GameState>(ui, "gameState");
        Assert.That(
            gameState.TrySetPlayerCard(new Card(playerRank)),
            Is.True);
        Assert.That(
            gameState.TrySetDealerCard(new Card(dealerRank)),
            Is.True);
        Assert.That(
            gameState.TrySetCommunityCards(
                new Card(communityRank1),
                new Card(communityRank2)),
            Is.True);
        Assert.That(gameState.TryRaise(1), Is.True);
        Assert.That(gameState.TryCall(), Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));

        InvokePrivate(ui, "ResolveShowdown");

        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(
            GetField<RoundWinner>(ui, "roundWinner"),
            Is.EqualTo(expectedWinner));
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

    private static T GetField<T>(
        IndianHoldemDebugUI ui,
        string fieldName)
    {
        FieldInfo field = typeof(IndianHoldemDebugUI).GetField(
            fieldName,
            PrivateInstance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(ui);
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
