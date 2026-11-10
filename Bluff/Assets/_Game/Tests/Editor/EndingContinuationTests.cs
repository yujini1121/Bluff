using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Michsky.UI.Shift;
using System.Text.RegularExpressions;

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
        Assert.That(inventory.playerItemInventory.Concat(inventory.dealerItemInventory).Count(i => i != null), Is.Zero);
        items.GetItem();
        var retainedItems = inventory.playerItemInventory.Concat(inventory.dealerItemInventory).Where(i => i != null).ToArray();
        Assert.That(retainedItems.Length, Is.EqualTo(2));
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.TrySetPhase(GamePhase.Showdown);
        state.TrySetPlayerCard(new Card(5)); state.TrySetDealerCard(new Card(5));
        state.TrySetCommunityCards(new Card(5), new Card(5));
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(controller, "roundWinner", winner);
        Set(controller, "isShowdownResultVisible", true);
        int playerBefore = state.PlayerChips.Count, dealerBefore = state.DealerChips.Count, carriedPot = state.Pot.Amount;
        Time.timeScale = 0f;
        controller.OnViewEndingClicked();
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").gameObject.activeInHierarchy, Is.True);
        Assert.That(Read<Button>(screen, "titleButton").gameObject.activeInHierarchy, Is.True);
        typeof(GameplayUiSceneTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Ending-Korean-ContinueAvailable" });
        Read<Button>(screen, "continueButton").onClick.Invoke();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Read<GameState>(controller, "gameState"), Is.SameAs(state));
        Assert.That(Read<ItemSystem>(controller, "itemSystem"), Is.SameAs(items));
        Assert.That(state.GameMode, Is.EqualTo(GameMode.Endless));
        Assert.That(state.CurrentRound, Is.EqualTo(11));
        Assert.That(state.PlayerChips.Count, Is.EqualTo(playerBefore));
        Assert.That(state.DealerChips.Count, Is.EqualTo(dealerBefore));
        Assert.That(state.Pot.Amount, Is.EqualTo(carriedPot));
        foreach (var item in retainedItems)
            Assert.That(inventory.playerItemInventory.Concat(inventory.dealerItemInventory), Does.Contain(item));
        Assert.That(inventory.playerItemInventory.Count(i => i != null), Is.EqualTo(1));
        Assert.That(inventory.dealerItemInventory.Count(i => i != null), Is.EqualTo(1));
        var view = Find<GameplayView>();
        Assert.That(Read<TMPro.TMP_Text>(view, "endlessRoundText").text, Is.EqualTo("Round 11"));
        Assert.That(Read<RoundProgressView>(view, "roundProgress").gameObject.activeSelf, Is.False);
        Assert.That(screen.IsVisible || screen.IsActive, Is.False);
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.None));
        deadline = Time.realtimeSinceStartup + 30;
        while (presentation.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        Assert.That(state.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(view.GameplayHud.alpha, Is.GreaterThan(0.99f));
        state.TrySetPhase(GamePhase.Showdown);
        state.TrySetPlayerCard(new Card(5)); state.TrySetDealerCard(new Card(5));
        state.TrySetCommunityCards(new Card(5), new Card(5));
        Assert.That(state.TrySettleShowdown(out winner), Is.True);
        Set(controller, "roundWinner", winner); Set(controller, "isShowdownResultVisible", true);
        controller.OnNextRoundClicked();
        Assert.That(state.CurrentRound, Is.EqualTo(12));
        Assert.That(inventory.playerItemInventory.Count(i => i != null), Is.EqualTo(2));
        Assert.That(inventory.dealerItemInventory.Count(i => i != null), Is.EqualTo(2));
        yield return Ready();
        state.TrySetPhase(GamePhase.Showdown);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        state.DealerChips.TrySpend(state.DealerChips.Count);
        Assert.That(state.TrySettleShowdown(out winner), Is.True);
        Set(controller, "roundWinner", winner); Set(controller, "isShowdownResultVisible", true);
        controller.OnViewEndingClicked();
        Assert.That(Find<VictoryCutsceneController>().IsPlaying, Is.False);
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
        Assert.That(Read<MainButton>(screen, "continueButtonLabel").buttonText, Is.EqualTo("다시하기"));
        Assert.That(Read<Button>(screen, "continueButton").gameObject.activeInHierarchy, Is.True);
        typeof(GameplayUiSceneTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Ending-Korean-RestartEndless" });
    }

    [TestCase(GameMode.RoundLimited, 10, 20, 30, false, true)]
    [TestCase(GameMode.RoundLimited, 10, 0, 30, false, false)]
    [TestCase(GameMode.RoundLimited, 10, 30, 0, false, false)]
    [TestCase(GameMode.RoundLimited, 3, 0, 30, false, false)]
    [TestCase(GameMode.RoundLimited, 10, 20, 20, true, true)]
    [TestCase(GameMode.Endless, 12, 0, 30, false, false)]
    public void EndingButton_UsesCompletedMatchRules(GameMode mode, int round, int player, int dealer, bool draw, bool canContinue)
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        var state = new GameState(player, dealer, Deck.CreateIndianHoldemDeck(), mode);
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, round);
        state.TrySetPlayerCard(new Card(draw || dealer == 0 ? 4 : 7));
        state.TrySetDealerCard(new Card(4));
        if (dealer == 0 && !draw) state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out _), Is.True);
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(state.CanContinueAsEndless, Is.EqualTo(canContinue));
        var screen = Find<EndingScreenController>();
        int continues = 0, restarts = 0;
        screen.Show(state.FinalWinner, state.CanContinueAsEndless, () => continues++, () => restarts++);
        var label = Read<MainButton>(screen, "continueButtonLabel");
        Assert.That(label.buttonText, Is.EqualTo(canContinue ? "이어서 계속하기" : "다시하기"));
        Assert.That(new[] { label.normalText, label.highlightedText, label.pressedText }.All(t => t.text == label.buttonText), Is.True);
        screen.OnContinueClicked();
        Assert.That(continues, Is.EqualTo(canContinue ? 1 : 0));
        Assert.That(restarts, Is.EqualTo(canContinue ? 0 : 1));
        screen.Release();
    }

    [UnityTest]
    public IEnumerator ClassicBankruptEndings_RestartFreshInClassic()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        yield return VerifyRestart(10, true);
        yield return VerifyRestart(10, false);
        yield return VerifyRestart(3, true);
    }

    [UnityTest]
    public IEnumerator EndlessBankruptEnding_RestartsFreshInEndless()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        GameModeSelection.Select(GameMode.Endless);
        SceneManager.LoadScene("Dev_Yujin");
        yield return null;
        yield return VerifyRestart(12, true);
    }

    private static IEnumerator Ready()
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (Find<GameplayPresentationController>().IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
    }

    private static IEnumerator VerifyRestart(int round, bool playerBankrupt)
    {
        yield return Ready();
        var controller = Find<GameplayController>();
        var state = Read<GameState>(controller, "gameState");
        var mode = state.GameMode;
        var items = Read<ItemSystem>(controller, "itemSystem");
        items.GetItem();
        var inventory = Read<Inventory>(items, "inventory");
        var oldItems = inventory.playerItemInventory.Concat(inventory.dealerItemInventory).Where(i => i != null).ToArray();
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, round);
        var bankrupt = playerBankrupt ? state.PlayerChips : state.DealerChips;
        Assert.That(bankrupt.TrySpend(bankrupt.Count), Is.True);
        state.TrySetPlayerCard(new Card(playerBankrupt ? 7 : 4));
        state.TrySetDealerCard(new Card(playerBankrupt ? 4 : 7));
        state.TrySetCommunityCards(new Card(4), new Card(4)); state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        Set(controller, "roundWinner", winner); Set(controller, "isShowdownResultVisible", true);
        controller.OnViewEndingClicked();
        Find<VictoryCutsceneController>().Skip(); Find<DefeatCutsceneController>().Skip();
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<MainButton>(screen, "continueButtonLabel").buttonText, Is.EqualTo("다시하기"));
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
        GameModeSelection.Select(mode == GameMode.Endless ? GameMode.RoundLimited : GameMode.Endless);
        var click = Read<Button>(screen, "continueButton").onClick;
        click.Invoke(); click.Invoke();
        yield return null;
        var freshController = Find<GameplayController>();
        var fresh = Read<GameState>(freshController, "gameState");
        Assert.That(fresh, Is.Not.SameAs(state));
        Assert.That(fresh.GameMode, Is.EqualTo(mode));
        Assert.That(GameModeSelection.SelectedMode, Is.EqualTo(mode));
        Assert.That(fresh.CurrentRound, Is.EqualTo(1));
        Assert.That(fresh.PlayerChips.Count, Is.EqualTo(Read<int>(freshController, "playerStartingChips") - 1));
        Assert.That(fresh.DealerChips.Count, Is.EqualTo(Read<int>(freshController, "dealerStartingChips") - 1));
        Assert.That(fresh.Pot.Amount, Is.EqualTo(2));
        Assert.That(fresh.Deck.RemainingCount, Is.EqualTo(36));
        Assert.That(oldItems.All(item => item == null), Is.True, "Scene reload must remove all old items");
        var freshItems = Read<ItemSystem>(freshController, "itemSystem");
        Assert.That(freshItems, Is.Not.SameAs(items));
        var freshInventory = Read<Inventory>(freshItems, "inventory");
        Assert.That(freshInventory.playerItemInventory.Count(i => i != null), Is.Zero);
        Assert.That(freshInventory.dealerItemInventory.Count(i => i != null), Is.Zero);
        Assert.That(Find<EndingScreenController>().IsActive, Is.False);
        Assert.That(Find<VictoryCutsceneController>().HasPlayed, Is.False);
        Assert.That(Find<DefeatCutsceneController>().HasPlayed, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1));
        yield return Ready();
        Assert.That(fresh.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(Find<GameplayView>().GameplayHud.alpha, Is.GreaterThan(0.99f));
    }

    [UnityTest]
    public IEnumerator FailedContinuation_KeepsEndingAndOffersWorkingRestart()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        yield return Ready();
        var controller = Find<GameplayController>();
        var state = Read<GameState>(controller, "gameState");
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.PlayerChips.TryAdd(5);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(4));
        state.TrySetCommunityCards(new Card(4), new Card(4)); state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(controller, "roundWinner", winner); Set(controller, "isShowdownResultVisible", true);
        controller.OnViewEndingClicked();
        Find<VictoryCutsceneController>().Skip();
        string endingCaption = Read<TMPro.TMP_Text>(Find<EndingScreenController>(), "caption").text;
        Set(controller, "nextRoundFirstTurn", TurnOwner.None);
        LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Endless round could not be started"));
        Read<Button>(Find<EndingScreenController>(), "continueButton").onClick.Invoke();
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible && screen.IsActive, Is.True);
        controller.enabled = false; controller.enabled = true;
        Assert.That(Read<TMPro.TMP_Text>(screen, "caption").text, Is.EqualTo(endingCaption));
        Assert.That(Read<MainButton>(screen, "continueButtonLabel").buttonText, Is.EqualTo("다시하기"));
        Assert.That(Read<Button>(screen, "titleButton").interactable, Is.True);
        var gameplayScene = SceneManager.GetActiveScene();
        var invalidScene = SceneManager.CreateScene("Unregistered restart target");
        SceneManager.SetActiveScene(invalidScene);
        LogAssert.Expect(LogType.Error, "현재 활성 Scene이 Build Settings에 등록되지 않아 게임을 Restart할 수 없습니다.");
        Read<Button>(screen, "continueButton").onClick.Invoke();
        Assert.That(screen.IsVisible && Read<Button>(screen, "titleButton").interactable, Is.True);
        Assert.That(Read<bool>(controller, "isRestarting"), Is.False);
        SceneManager.SetActiveScene(gameplayScene);
        yield return SceneManager.UnloadSceneAsync(invalidScene);
        Read<Button>(screen, "continueButton").onClick.Invoke();
        yield return null;
        Assert.That(Read<GameState>(Find<GameplayController>(), "gameState"), Is.Not.SameAs(state));
        Assert.That(Find<GameplayController>().CurrentRound, Is.EqualTo(1));
        Assert.That(Find<GameplayController>().CurrentGameMode, Is.EqualTo(GameMode.RoundLimited));
        Assert.That(Find<EndingScreenController>().IsVisible, Is.False);
        yield return Ready();
    }
}
