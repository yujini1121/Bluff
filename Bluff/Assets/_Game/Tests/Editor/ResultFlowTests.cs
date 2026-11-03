using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ResultFlowTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Find<T>() where T : Object => Object.FindObjectOfType<T>(true);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void FinalResult_SeparatesRoundWinnerFromMatchWinner()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        Assert.That(Read<float>(Find<GameplayController>(), "resultRevealDelay"), Is.EqualTo(1.2f));
        var state = new GameState(5, 30, Deck.CreateIndianHoldemDeck());
        state.TrySetPlayerCard(new Card(4));
        state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Assert.That(winner, Is.EqualTo(RoundWinner.Player));
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Dealer));
        var view = Find<GameplayView>();
        view.RefreshResult(state, winner, HandRank.Triple, HandRank.Double, true, false, true);
        Assert.That(Read<TMP_Text>(view, "resultTitleText").text, Does.Contain("ROUND: PLAYER WIN"));
        Assert.That(Read<TMP_Text>(view, "resultDetailText").text, Does.Contain("TRIPLE").And.Contain("MATCH RESULT: DEALER WINS").And.Contain("PLAYER 5"));
    }

    [UnityTest]
    public IEnumerator RevealDelay_BlocksFoldAndShowdownProgressAndFinalEnding()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        var controller = Find<GameplayController>();
        var view = Find<GameplayView>();
        var state = Read<GameState>(controller, "gameState");
        var presentation = Find<GameplayPresentationController>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (presentation.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        Set(controller, "resultRevealDelay", 0.15f);
        foreach (var foldedBy in new[] { TurnOwner.Player, TurnOwner.Dealer })
        {
            state.TrySetPhase(GamePhase.Betting);
            state.Turn.TrySet(foldedBy);
            Assert.That(state.TryFold(true), Is.True);
            Set(controller, "isFoldResultVisible", false);
            Call(controller, "CompleteFoldCardReveal");
            Call(controller, "CompleteFoldCardReveal"); // Duplicate callbacks cannot start two timers.
            Assert.That(Read<bool>(controller, "isFoldResultVisible"), Is.False);
            controller.OnNextRoundClicked();
            Assert.That(state.Phase, Is.EqualTo(GamePhase.RoundEnd));
            if (foldedBy == TurnOwner.Player)
            {
                controller.enabled = false;
                Assert.That(Read<Coroutine>(controller, "resultRevealCoroutine"), Is.Null);
                controller.enabled = true;
                Assert.That(Read<bool>(controller, "isFoldResultVisible"), Is.False);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Read<bool>(controller, "isFoldResultVisible"), Is.True);
            Assert.That((bool)Call(controller, "CanAcceptProgressInput", GamePhase.RoundEnd), Is.True);
        }
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        state.DealerChips.TryAdd(10); // Force a round win that differs from the chip-count match result.
        state.TrySetPhase(GamePhase.Showdown);
        state.TryGetHandRank(TurnOwner.Player, out var playerRank);
        state.TryGetHandRank(TurnOwner.Dealer, out var dealerRank);
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(controller, "roundWinner", winner);
        Set(controller, "playerHandRank", playerRank);
        Set(controller, "dealerHandRank", dealerRank);
        Set(controller, "showdownResultDelay", 0.1f);
        Call(controller, "ContinueShowdownAfterReveal", state.PlayerChips.Count, state.DealerChips.Count, 0);
        controller.OnViewEndingClicked();
        Assert.That(Find<VictoryCutsceneController>().HasPlayed, Is.False);
        Assert.That(Find<DefeatCutsceneController>().HasPlayed, Is.False);
        Assert.That(Read<bool>(controller, "isShowdownResultVisible"), Is.False);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(Read<bool>(controller, "isShowdownResultVisible"), Is.True);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(Read<GameObject>(view, "resultOverlay").activeSelf, Is.True);
        Assert.That(Read<Button>(view, "nextRoundButton").interactable, Is.True);
        typeof(GameplayUiSceneTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "ResultFlow-FinalResult" });
        Assert.That(Find<VictoryCutsceneController>().HasPlayed, Is.False);
        Assert.That(Find<DefeatCutsceneController>().HasPlayed, Is.False);
    }
}
