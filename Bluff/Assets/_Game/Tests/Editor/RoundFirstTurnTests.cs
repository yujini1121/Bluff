using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoundFirstTurnTests
{
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(
        TurnOwner.Player,
        TurnOwner.Player,
        TurnOwner.Dealer,
        TurnOwner.Player)]
    [TestCase(
        TurnOwner.Dealer,
        TurnOwner.Dealer,
        TurnOwner.Player,
        TurnOwner.Dealer)]
    public void StartRound_AlternatesFirstTurnAfterEachSuccessfulRound(
        TurnOwner configuredFirstTurn,
        TurnOwner firstRoundTurn,
        TurnOwner secondRoundTurn,
        TurnOwner thirdRoundTurn)
    {
        IndianHoldemDebugUI ui = CreateUi(configuredFirstTurn);

        try
        {
            GameState gameState = GetField<GameState>(ui, "gameState");
            TurnOwner[] expectedTurns =
            {
                firstRoundTurn,
                secondRoundTurn,
                thirdRoundTurn
            };

            for (int index = 0; index < expectedTurns.Length; index++)
            {
                InvokeStartRound(ui);
                Assert.That(
                    gameState.CurrentTurn,
                    Is.EqualTo(expectedTurns[index]));

                if (index < expectedTurns.Length - 1)
                {
                    Assert.That(gameState.TryFold(), Is.True);
                    Assert.That(gameState.TryPrepareNextRound(), Is.True);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(ui.gameObject);
        }
    }

    [Test]
    public void StartRound_FailureDoesNotAdvanceNextFirstTurn()
    {
        IndianHoldemDebugUI ui = CreateUi(TurnOwner.Player);

        try
        {
            InvokeStartRound(ui);
            Assert.That(
                GetField<TurnOwner>(ui, "nextRoundFirstTurn"),
                Is.EqualTo(TurnOwner.Dealer));

            InvokeStartRound(ui);

            Assert.That(
                GetField<TurnOwner>(ui, "nextRoundFirstTurn"),
                Is.EqualTo(TurnOwner.Dealer));
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
