using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class EndingContinuationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Find<T>() where T : Object => Object.FindObjectOfType<T>(true);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        GameModeSelection.Select(GameMode.RoundLimited);
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static GameState CompletedMatch(int playerChips, int dealerChips, bool fold = false, int cardCount = 4)
    {
        var state = new GameState(playerChips, dealerChips,
            new Deck(Enumerable.Range(0, cardCount).Select(_ => new Card(5))), GameMode.RoundLimited);
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 9);
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        if (fold) Assert.That(state.TryFold(true), Is.True);
        else
        {
            state.TrySetPhase(GamePhase.Showdown);
            Assert.That(state.TrySettleShowdown(out _), Is.True);
        }
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        return state;
    }

    [TestCase(40, 40, false)]
    [TestCase(42, 38, false)]
    [TestCase(38, 42, false)]
    [TestCase(40, 40, true)]
    public void Continue_PreservesObjectsChipsPotAndRound_ThenRefillsDeckAndStartsEleven(int player, int dealer, bool fold)
    {
        var state = CompletedMatch(player, dealer, fold);
        var playerStack = state.PlayerChips;
        var dealerStack = state.DealerChips;
        var deck = state.Deck;
        var playerCard = state.PlayerCard;
        int playerBefore = state.PlayerChips.Count, dealerBefore = state.DealerChips.Count, potBefore = state.Pot.Amount;
        Assert.That(state.TryContinueAsEndless(), Is.True);
        Assert.That(state.GameMode, Is.EqualTo(GameMode.Endless));
        Assert.That(state.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.None));
        Assert.That(state.PlayerChips, Is.SameAs(playerStack));
        Assert.That(state.DealerChips, Is.SameAs(dealerStack));
        Assert.That(state.PlayerCard, Is.SameAs(playerCard));
        Assert.That(state.Deck, Is.SameAs(deck));
        Assert.That(state.PlayerChips.Count, Is.EqualTo(playerBefore));
        Assert.That(state.DealerChips.Count, Is.EqualTo(dealerBefore));
        Assert.That(state.Pot.Amount, Is.EqualTo(potBefore));
        Assert.That(state.CurrentRound, Is.EqualTo(10));
        Assert.That(state.TryContinueAsEndless(), Is.False);
        Assert.That(state.TryPrepareNextRound(), Is.True);
        Assert.That(state.Deck, Is.Not.SameAs(deck));
        Assert.That(state.RoundEndReason, Is.EqualTo(RoundEndReason.None));
        Assert.That(state.TryStartRound(TurnOwner.Dealer), Is.True);
        Assert.That(state.CurrentRound, Is.EqualTo(11));
        Assert.That(state.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(state.PlayerChips.Count, Is.EqualTo(playerBefore - (potBefore == 0 ? 1 : 0)));
        Assert.That(state.DealerChips.Count, Is.EqualTo(dealerBefore - (potBefore == 0 ? 1 : 0)));
    }

    [TestCase(0, 20)]
    [TestCase(20, 0)]
    public void Continue_RejectsBankruptcyWithoutGrantingChips(int player, int dealer)
    {
        var state = new GameState(player, dealer, Deck.CreateIndianHoldemDeck());
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        typeof(GameState).GetProperty("FinalWinner").SetValue(state, GameWinner.Player);
        state.TrySetPhase(GamePhase.GameOver);
        Assert.That(state.CanContinueAsEndless, Is.False);
        Assert.That(state.TryContinueAsEndless(), Is.False);
        Assert.That(state.PlayerChips.Count, Is.EqualTo(player));
        Assert.That(state.DealerChips.Count, Is.EqualTo(dealer));
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
    }

    [Test]
    public void Continue_KeepsExistingDeckWhenFourCardsRemain()
    {
        var state = CompletedMatch(40, 40, cardCount: 8);
        var deck = state.Deck;
        Assert.That(deck.RemainingCount, Is.EqualTo(4));
        Assert.That(state.TryContinueAsEndless(), Is.True);
        Assert.That(state.TryPrepareNextRound(), Is.True);
        Assert.That(state.Deck, Is.SameAs(deck));
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        Assert.That(state.CurrentRound, Is.EqualTo(11));
        Assert.That(deck.RemainingCount, Is.Zero);
    }

    [Test]
    public void Continue_RejectsActiveMatchAndAlreadyEndedEndless()
    {
        var active = new GameState(20, 20, Deck.CreateIndianHoldemDeck());
        Assert.That(active.TryContinueAsEndless(), Is.False);
        var state = CompletedMatch(20, 20);
        Assert.That(state.TryContinueAsEndless(), Is.True);
        state.TrySetPhase(GamePhase.GameOver);
        typeof(GameState).GetProperty("FinalWinner").SetValue(state, GameWinner.Draw);
        Assert.That(state.TryContinueAsEndless(), Is.False);
    }

    [UnityTest]
    public IEnumerator ContinueButton_RetainsLiveSessionAndItems_StartsNextRound_AndNeverReplaysCutscene()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        var controller = Find<GameplayController>();
        var presentation = Find<GameplayPresentationController>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (presentation.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        var state = Read<GameState>(controller, "gameState");
        var items = Read<ItemSystem>(controller, "itemSystem");
        var inventory = Read<Inventory>(items, "inventory");
        var retainedItems = inventory.playerItemInventory.Concat(inventory.dealerItemInventory).Where(i => i != null).ToArray();
        Assert.That(retainedItems.Length, Is.GreaterThan(0));
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.TrySetPhase(GamePhase.Showdown);
        state.TrySetPlayerCard(new Card(5)); state.TrySetDealerCard(new Card(5));
        state.TrySetCommunityCards(new Card(5), new Card(5));
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(controller, "roundWinner", winner);
        Set(controller, "isShowdownResultVisible", true);
        int playerBefore = state.PlayerChips.Count, dealerBefore = state.DealerChips.Count, carriedPot = state.Pot.Amount;
        controller.OnViewEndingClicked(); // Draw takes the neutral path.
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").gameObject.activeInHierarchy, Is.True);
        Assert.That(Read<Button>(screen, "titleButton").gameObject.activeInHierarchy, Is.True);
        typeof(GameplayUiSceneTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Ending-Korean-ContinueAvailable" });
        Read<Button>(screen, "continueButton").onClick.Invoke();
        Assert.That(Read<GameState>(controller, "gameState"), Is.SameAs(state));
        Assert.That(Read<ItemSystem>(controller, "itemSystem"), Is.SameAs(items));
        Assert.That(state.GameMode, Is.EqualTo(GameMode.Endless));
        Assert.That(state.CurrentRound, Is.EqualTo(11));
        Assert.That(state.PlayerChips.Count, Is.EqualTo(playerBefore)); // Draw carries its ante/pot.
        Assert.That(state.DealerChips.Count, Is.EqualTo(dealerBefore));
        Assert.That(state.Pot.Amount, Is.EqualTo(carriedPot));
        foreach (var item in retainedItems)
            Assert.That(inventory.playerItemInventory.Concat(inventory.dealerItemInventory), Does.Contain(item));
        Assert.That(screen.IsVisible || screen.IsActive, Is.False);
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.None));
        deadline = Time.realtimeSinceStartup + 30;
        while (presentation.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        Assert.That(state.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(Find<GameplayView>().GameplayHud.alpha, Is.GreaterThan(0.99f));
        // Even a different winner at the end of Endless must not replay a final cutscene.
        state.TrySetPhase(GamePhase.Showdown);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        state.DealerChips.TrySpend(state.DealerChips.Count);
        Assert.That(state.TrySettleShowdown(out winner), Is.True);
        Set(controller, "roundWinner", winner); Set(controller, "isShowdownResultVisible", true);
        controller.OnViewEndingClicked();
        Assert.That(Find<VictoryCutsceneController>().IsPlaying, Is.False);
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.False);
        Assert.That(Read<Button>(screen, "continueButton").gameObject.activeInHierarchy, Is.True);
        typeof(GameplayUiSceneTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Ending-Korean-ContinueDisabled" });
    }
}
