using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerItemPresentationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> createdObjects = new List<Object>();
    private GameState game;
    private ItemSystem items;
    private Inventory inventory;
    private GameplayController ui;
    private GameplayView view;
    private ChipVisualController chips;
    private CardVisualController cards;
    private Random.State previousRandom;

    [SetUp]
    public void SetUp()
    {
        previousRandom = Random.state;
        inventory = Track(ScriptableObject.CreateInstance<Inventory>());
        cards = CreateObject("Card presentation").AddComponent<CardVisualController>();
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Cards/PF_Card.prefab");
        Assert.That(cardPrefab, Is.Not.Null);
        for (int index = 0; index < 4; index++)
        {
            Transform root = CreateObject($"Card root {index}", cards.transform).transform;
            CardVisual visual = Object.Instantiate(cardPrefab, root).GetComponentInChildren<CardVisual>();
            if (index < 2)
            {
                string prefix = index == 0 ? "player" : "dealer";
                Set(cards, prefix + "CardVisual", visual);
                Set(cards, prefix + "CardRoot", root);
                Set(cards, prefix + "CardPoint", CreateObject(prefix + " normal", cards.transform).transform);
                Transform revealPoint = CreateObject(prefix + " reveal", cards.transform).transform;
                revealPoint.position = new Vector3(index == 0 ? -1 : 1, 0, 0);
                Set(cards, prefix + "ShowdownCardPoint", revealPoint);
            }
            else
            {
                Set(cards, "communityCardVisual" + (index - 1), visual);
            }
        }
        GameObject deckObject = CreateObject("Deck presentation");
        deckObject.SetActive(false);
        deckObject.transform.position = new Vector3(6f, 0f, 0f);
        DeckStackVisual deckVisual = deckObject.AddComponent<DeckStackVisual>();
        Set(cards, "deckStackVisual", deckVisual);
        Set(cards, "cardDealPoint", deckObject.transform);
        Set(cards, "dealDuration", 0.1f);
        Set(cards, "dealInterval", 0.02f);
        Set(cards, "refreshCollectDuration", 0.1f);
        Set(cards, "refreshCollectInterval", 0.02f);
        Set(cards, "refreshBeforeShuffleDelay", 0.01f);
        Set(cards, "refreshShuffleDuration", 0.1f);

        chips = CreateObject("Chip presentation").AddComponent<ChipVisualController>();
        foreach (string field in new[] { "playerChipArea", "dealerChipArea", "playerBetAreaPoint", "dealerBetAreaPoint", "potArea" })
            Set(chips, field, CreateObject(field, chips.transform).transform);
        GameObject chipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Chips/PF_Chip_Red_01.prefab");
        Assert.That(chipPrefab, Is.Not.Null);
        Set(chips, "chipPrefabs", new[] { chipPrefab });

        GameObject uiObject = CreateObject("Player item gameplay UI");
        uiObject.SetActive(false);
        items = uiObject.AddComponent<ItemSystem>();
        Set(items, "inventory", inventory);
        ui = uiObject.AddComponent<GameplayController>();
        view = uiObject.AddComponent<GameplayView>();
        Set(ui, "gameplayView", view);
        Set(ui, "itemSystem", items);
        Set(ui, "cardVisualController", cards);
        Set(ui, "chipVisualController", chips);
        Set(ui, "maxLogLines", 30);
        foreach (string field in new[] { "roundText", "debugInfoText", "messageText", "raiseAmountText", "raiseExecuteText", "resultTitleText", "resultDetailText" })
            Set(view, field, CreateObject(field, ui.transform).AddComponent<TextMeshProUGUI>());
        foreach (string field in new[] { "raiseDecreaseButton", "raiseIncreaseButton", "raiseMaxButton", "raiseExecuteButton", "resolveShowdownButton", "nextRoundButton", "restartButton", "debugToggleButton" })
            Set(view, field, CreateObject(field, ui.transform).AddComponent<Button>());
        foreach (string field in new[] { "debugPanel", "playerActionBar", "contextActionArea", "resultOverlay" })
            Set(view, field, CreateObject(field, ui.transform));
        Set(view, "bettingActionButtons", new[] { CreateObject("Betting button", ui.transform).AddComponent<Button>() });
        uiObject.SetActive(true);
        Invoke(ui, "SubscribeToItemSystemEvents");
        Bind(NewRound(TurnOwner.Player));
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
            if (createdObjects[index] != null) Object.DestroyImmediate(createdObjects[index]);
        createdObjects.Clear();
        Random.state = previousRandom;
    }

    [Test]
    public void PlayerPocket_ImmediatelyAddsTwoVisualChipsAndConsumesOnlyOneItem()
    {
        Invoke(ui, "OnEnable");
        Invoke(ui, "OnEnable");
        GameObject first = Add(TurnOwner.Player, ItemType.chipPocket);
        GameObject second = Add(TurnOwner.Player, ItemType.chipPocket);
        first.GetComponent<Item>().Use();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(21));
        Assert.That(first == null, Is.True);
        Assert.That(inventory.HasItem(TurnOwner.Player, second), Is.True);
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Betting));
        AssertChipVisuals();
    }

    [Test]
    public void PlayerRefresh_LocksInputUntilPresentationCompletes()
    {
        GameObject first = Add(TurnOwner.Player, ItemType.refreshCard);
        GameObject second = Add(TurnOwner.Player, ItemType.refreshCard);

        first.GetComponent<Item>().Use();

        Assert.That(first == null, Is.True);
        Assert.That(Get(ui, "isCardAnimating"), Is.EqualTo(true));
        Assert.That(Get(cards, "refreshSequence"), Is.Not.Null);
        second.GetComponent<Item>().Use();
        Assert.That(inventory.HasItem(TurnOwner.Player, second), Is.True);

        CompleteRefreshPresentation();

        Assert.That(Get(ui, "isCardAnimating"), Is.EqualTo(false));
        Assert.That(Get(cards, "refreshSequence"), Is.Null);
        Assert.That(Get(cards, "dealSequence"), Is.Null);
    }

    [Test]
    public void DealerRefresh_WaitsBeforeExecutingPreparedAction()
    {
        var state = new GameState(
            20,
            20,
            new Deck(new[] { new Card(9) }, new NoSwapRandom()));
        state.TrySetPlayerCard(new Card(4));
        state.TrySetDealerCard(new Card(1));
        state.TrySetCommunityCards(new Card(4), new Card(2));
        state.Pot.TryAdd(2);
        state.TrySetPhase(GamePhase.Betting);
        state.Turn.TrySet(TurnOwner.Dealer);
        Bind(state);
        Add(TurnOwner.Dealer, ItemType.refreshCard);
        Set(ui, "minDealerThinkDelay", 0f);
        Set(ui, "maxDealerThinkDelay", 0f);
        Random.InitState(FindDealerSeed(ItemType.refreshCard));
        var routine = (IEnumerator)Invoke(ui, "PerformDealerActionAfterDelay");

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(Get(ui, "isCardAnimating"), Is.EqualTo(true));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));

        CompleteRefreshPresentation();

        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(
            game.Phase != GamePhase.Betting ||
            game.CurrentTurn != TurnOwner.Dealer,
            Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PlayerPrizm_UsesPreEffectPotCompletesFoldRevealAndShowsResult(bool finalRound)
    {
        if (finalRound) Bind(CreateFinalRound());
        int potBefore = game.Pot.Amount;
        int playerBefore = game.PlayerChips.Count;
        int dealerBefore = game.DealerChips.Count;
        GameObject item = Add(TurnOwner.Player, ItemType.prizmChip);
        item.GetComponent<Item>().Use();
        Assert.That(item == null, Is.True);
        Assert.That(game.FoldPenaltyAmount, Is.Zero);
        Assert.That(game.PlayerChips.Count, Is.EqualTo(playerBefore));
        Assert.That(game.DealerChips.Count, Is.EqualTo(dealerBefore + potBefore));
        Assert.That(Get(ui, "isChipAnimating"), Is.EqualTo(true));
        Assert.That(((List<GameObject>)Get(chips, "pendingChips")).Count, Is.EqualTo(potBefore));
        CompleteChipMoves();
        CompleteCardReveal();
        AssertChipVisuals();
        Assert.That(Get(ui, "isFoldResultVisible"), Is.EqualTo(true));
        Assert.That(((GameObject)Get(view, "resultOverlay")).activeSelf, Is.True);
        Assert.That(Get(ui, "isChipAnimating"), Is.EqualTo(false));
        Assert.That(Get(ui, "isCardAnimating"), Is.EqualTo(false));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(game.Phase, Is.EqualTo(finalRound ? GamePhase.GameOver : GamePhase.RoundEnd));
        Button restart = (Button)Get(view, "restartButton");
        Assert.That(restart.gameObject.activeSelf, Is.EqualTo(finalRound));
        if (finalRound) Assert.That(restart.interactable, Is.True);
        else Assert.That(((TMP_Text)Get(view, "resultDetailText")).text, Is.EqualTo("PLAYER FOLD"));
    }

    [Test]
    public void PlayerDefy_ImmediatelySynchronizesRefundThenCompletesExistingShowdown()
    {
        game.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(game.TryRaise(3), Is.True);
        RefreshChips();
        GameObject item = Add(TurnOwner.Player, ItemType.defy);
        ExpectDestroyLogs(3);
        item.GetComponent<Item>().Use();
        Assert.That(item == null, Is.True);
        Assert.That(game.PlayerChips.Count, Is.EqualTo(19));
        Assert.That(game.DealerChips.Count, Is.EqualTo(19));
        Assert.That(game.Pot.Amount, Is.EqualTo(2));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.None));
        AssertChipVisuals();
        Assert.That(((Button)Get(view, "resolveShowdownButton")).interactable, Is.True);
        ui.OnResolveShowdownClicked();
        CompleteCardReveal();
        Assert.That(Get(ui, "roundWinner"), Is.EqualTo(RoundWinner.Player));
        Assert.That(Get(ui, "isShowdownResultVisible"), Is.EqualTo(true));
        var scheduledDelay = (Coroutine)Get(ui, "showdownPresentationCoroutine");
        if (scheduledDelay != null) ui.StopCoroutine(scheduledDelay);
        var delay = (IEnumerator)Invoke(ui, "ContinueShowdownAfterResultDelay", 19, 19, 2);
        Assert.That(delay.MoveNext(), Is.True);
        Assert.That(delay.MoveNext(), Is.False);
        CompleteChipMoves();
        AssertChipVisuals();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(21));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.RoundEnd));
    }

    [TestCase(ItemType.chipPocket)]
    [TestCase(ItemType.defy)]
    public void FailedPlayerEffect_DoesNotConsumeItemOrChangePresentation(ItemType type)
    {
        if (type == ItemType.chipPocket) Set(items, "chipPocketAmount", 0);
        else
        {
            // 이월 Pot이 있어도 미지급 Call이 없으면 Defy 효과가 실패한다.
            Assert.That(game.Pot.TryAdd(4), Is.True);
            RefreshChips();
        }
        GameObject item = Add(TurnOwner.Player, type);
        item.GetComponent<Item>().Use();
        Assert.That(item == null, Is.False);
        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
        Assert.That(game.PlayerChips.Count, Is.EqualTo(19));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Betting));
        AssertChipVisuals();
    }

    [TestCase(ItemType.prizmChip, "isCardAnimating")]
    [TestCase(ItemType.chipPocket, "isChipAnimating")]
    [TestCase(ItemType.defy, "isActionProcessing")]
    public void BusyPlayerInput_DoesNotApplyEffectOrConsumeItem(ItemType type, string flag)
    {
        if (type == ItemType.defy)
        {
            game.Turn.TrySet(TurnOwner.Dealer);
            game.TryRaise(1);
            RefreshChips();
        }
        int potBefore = game.Pot.Amount;
        int dealerBefore = game.DealerChips.Count;
        GameObject item = Add(TurnOwner.Player, type);
        Set(ui, flag, true);
        item.GetComponent<Item>().Use();
        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
        Assert.That(game.Pot.Amount, Is.EqualTo(potBefore));
        Assert.That(game.DealerChips.Count, Is.EqualTo(dealerBefore));
        Assert.That(game.PlayerChips.Count, Is.EqualTo(19));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Player));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Betting));
        AssertChipVisuals();
    }

    [Test]
    public void DisabledUi_RejectsPlayerRequestAndReenableRestoresSingleHandler()
    {
        GameObject item = Add(TurnOwner.Player, ItemType.chipPocket);
        ui.enabled = false;
        item.GetComponent<Item>().Use();
        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
        Assert.That(game.PlayerChips.Count, Is.EqualTo(19));
        ui.enabled = true;
        item.GetComponent<Item>().Use();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(21));
        Assert.That(item == null, Is.True);
        AssertChipVisuals();
    }

    [TestCase(ItemType.prizmChip)]
    [TestCase(ItemType.chipPocket)]
    [TestCase(ItemType.defy)]
    public void DealerCoroutine_PreservesOneItemUseAndExistingPresentation(ItemType type)
    {
        var state = new GameState(20, type == ItemType.defy ? 7 : type == ItemType.chipPocket ? 6 : 20, Deck.CreateIndianHoldemDeck());
        Assert.That(state.TryStartRound(type == ItemType.chipPocket ? TurnOwner.Dealer : TurnOwner.Player), Is.True);
        state.TrySetPlayerCard(new Card(type == ItemType.defy ? 1 : 4));
        state.TrySetDealerCard(new Card(type == ItemType.defy ? 2 : 1));
        state.TrySetCommunityCards(new Card(4), new Card(type == ItemType.defy ? 7 : 2));
        if (type != ItemType.chipPocket) Assert.That(state.TryRaise(type == ItemType.defy ? 3 : 2), Is.True);
        Bind(state);
        GameObject first = Add(TurnOwner.Dealer, type);
        GameObject second = Add(TurnOwner.Dealer, ItemType.chipPocket);
        Set(ui, "minDealerThinkDelay", 0f);
        Set(ui, "maxDealerThinkDelay", 0f);
        int seed = FindDealerSeed(type);
        Random.InitState(seed);
        if (type == ItemType.defy) ExpectDestroyLogs(3);
        var routine = (IEnumerator)Invoke(ui, "PerformDealerActionAfterDelay");
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(first == null, Is.True);
        Assert.That(inventory.HasItem(TurnOwner.Dealer, second), Is.True);
        var logs = (List<string>)Get(ui, "logs");
        if (type == ItemType.defy)
        {
            Assert.That(game.Phase, Is.EqualTo(GamePhase.Showdown));
            Assert.That(logs, Has.Some.Contains("추가 행동 없음"));
        }
        else
        {
            CompleteChipMoves();
            CompleteCardReveal();
            Assert.That(Get(ui, "isFoldResultVisible"), Is.EqualTo(true));
            Assert.That(((GameObject)Get(view, "resultOverlay")).activeSelf, Is.True);
            if (type == ItemType.chipPocket)
            {
                Assert.That(game.DealerChips.Count, Is.EqualTo(7));
                Assert.That(logs, Has.Some.Contains("일반 행동 재계산"));
            }
            else
            {
                Assert.That(game.FoldPenaltyAmount, Is.Zero);
                Assert.That(logs, Has.Some.Contains("추가 행동 없음"));
            }
        }
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.None));
        AssertChipVisuals();
    }

    private void Bind(GameState state)
    {
        game = state;
        Set(ui, "gameState", game);
        items.Initialize(new ItemGameApi(game));
        var dealerTurn = new DealerTurnController();
        dealerTurn.Initialize(game, items, message => Invoke(ui, "AddLog", message));
        Set(ui, "dealerTurn", dealerTurn);
        cards.Initialize(game);
        ExpectChipRemovals();
        chips.Initialize(game);
    }
    private void RefreshChips() { ExpectChipRemovals(); chips.RefreshChips(); }
    private void ExpectChipRemovals()
    {
        ExpectDestroyLogs(Mathf.Max(0, Count("playerChipInstances") - game.PlayerChips.Count)
            + Mathf.Max(0, Count("dealerChipInstances") - game.DealerChips.Count)
            + Mathf.Max(0, Count("playerBetChipInstances") - game.Betting.PlayerTotalBet)
            + Mathf.Max(0, Count("dealerBetChipInstances") - game.Betting.DealerTotalBet)
            + Mathf.Max(0, Count("potChipInstances") - Mathf.Max(0, game.Pot.Amount - game.Betting.PlayerTotalBet - game.Betting.DealerTotalBet)));
    }
    private static void ExpectDestroyLogs(int count)
    {
        // ChipVisualController의 PlayMode용 Destroy가 EditMode에서 내는 로그만 예상한다.
        for (int index = 0; index < count; index++)
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode!"));
    }
    private int Count(string field) => ((List<GameObject>)Get(chips, field)).Count;
    private void AssertChipVisuals()
    {
        Assert.That(Count("playerChipInstances"), Is.EqualTo(game.PlayerChips.Count));
        Assert.That(Count("dealerChipInstances"), Is.EqualTo(game.DealerChips.Count));
        Assert.That(Count("playerBetChipInstances"), Is.EqualTo(game.Betting.PlayerTotalBet));
        Assert.That(Count("dealerBetChipInstances"), Is.EqualTo(game.Betting.DealerTotalBet));
        Assert.That(Count("potChipInstances") + Count("playerBetChipInstances") + Count("dealerBetChipInstances"), Is.EqualTo(game.Pot.Amount));
    }
    private void CompleteChipMoves()
    {
        var moves = ((List<Tween>)Get(chips, "playerMoveTweens")).ToArray();
        Assert.That(moves, Is.Not.Empty);
        foreach (Tween move in moves) move.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);
        Assert.That(((List<GameObject>)Get(chips, "pendingChips")), Is.Empty);
    }
    private void CompleteCardReveal()
    {
        var reveal = (Sequence)Get(cards, "showdownRevealSequence");
        var completed = (System.Action)Get(cards, "showdownRevealCompleted");
        Assert.That(reveal, Is.Not.Null);
        Assert.That(completed, Is.Not.Null);
        reveal.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);
        Assert.That(Get(cards, "showdownRevealSequence"), Is.Null);
        foreach (string prefix in new[] { "player", "dealer" })
            Assert.That(((Transform)Get(cards, prefix + "CardRoot")).position,
                Is.EqualTo(((Transform)Get(cards, prefix + "ShowdownCardPoint")).position));
        // CardVisualController는 PlayMode에서만 완료 알림을 보내므로 연결된 콜백을 진행한다.
        if (!Application.isPlaying) completed();
    }

    private void CompleteRefreshPresentation()
    {
        var refresh = (Sequence)Get(cards, "refreshSequence");
        Assert.That(refresh, Is.Not.Null);
        refresh.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);
        var deal = (Sequence)Get(cards, "dealSequence");
        Assert.That(deal, Is.Not.Null);
        deal.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);
        Assert.That(Get(cards, "dealSequence"), Is.Null);
        if (!Application.isPlaying)
        {
            Invoke(cards, "CompleteRefreshDeal");
            Invoke(ui, "OnRefreshPresentationCompleted");
        }
    }
    private int FindDealerSeed(ItemType type)
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            Random.InitState(seed);
            Random.Range(0f, 0f);
            DealerActionPlan plan = new DealerAi().Decide(game, Random.Range(0, 100), Random.Range(0, 100));
            DealerItemPlan itemPlan = new DealerItemAi().Decide(game, items.GetOwnedItemTypes(TurnOwner.Dealer), plan);
            if (itemPlan.SelectedItem == type && (type != ItemType.chipPocket || plan.Decision == DealerDecision.Fold)) return seed;
        }
        Assert.Fail("아이템을 선택하는 Dealer roll을 찾을 수 없습니다.");
        return -1;
    }
    private GameObject Add(TurnOwner owner, ItemType type)
    {
        var data = Track(ScriptableObject.CreateInstance<ItemData>());
        data.itemType = type;
        GameObject itemObject = CreateObject(type.ToString());
        Item item = itemObject.AddComponent<Item>();
        item.itemSystem = items;
        item.itemData = data;
        Assert.That(inventory.AddItem(owner, itemObject), Is.True);
        return itemObject;
    }
    private static GameState NewRound(TurnOwner owner)
    {
        var state = new GameState(20, 20, Deck.CreateIndianHoldemDeck());
        Assert.That(state.TryStartRound(owner), Is.True);
        state.TrySetPlayerCard(new Card(4));
        state.TrySetDealerCard(new Card(1));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        return state;
    }
    private static GameState CreateFinalRound()
    {
        var state = new GameState(100, 100, Deck.CreateIndianHoldemDeck());
        for (int round = 0; round < 9; round++)
        {
            Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
            state.TrySetPlayerCard(new Card(1));
            state.TrySetDealerCard(new Card(1));
            state.TrySetCommunityCards(new Card(4), new Card(7));
            state.TryRaise(1);
            state.TryCall();
            Assert.That(state.TrySettleShowdown(out RoundWinner winner), Is.True);
            Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(state.TryPrepareNextRound(), Is.True);
        }
        Assert.That(state.TryStartRound(TurnOwner.Player), Is.True);
        return state;
    }
    private GameObject CreateObject(string name, Transform parent = null)
    {
        GameObject obj = Track(new GameObject(name));
        if (parent != null) obj.transform.SetParent(parent);
        return obj;
    }
    private T Track<T>(T obj) where T : Object { createdObjects.Add(obj); return obj; }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    private static object Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);

    private sealed class NoSwapRandom : System.Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
