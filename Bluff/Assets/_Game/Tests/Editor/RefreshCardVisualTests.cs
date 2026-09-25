using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RefreshCardVisualTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> createdObjects = new List<Object>();
    private readonly CardVisual[] visuals = new CardVisual[4];
    private readonly Renderer[] renderers = new Renderer[4];
    private Material[] materials;
    private GameState gameState;
    private Inventory inventory;
    private ItemSystem itemSystem;
    private GameplayController ui;
    private GameplayView view;
    private GameplayPresentationController presentation;
    private CardVisualController controller;
    private DeckStackVisual deckStackVisual;
    private Transform playerRoot;

    [SetUp]
    public void SetUp()
    {
        materials = new Material[10];
        for (int index = 0; index < materials.Length; index++)
        {
            materials[index] = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/_Game/Art/Cards/Materials/M_Card_Dot_{index + 1:00}.mat");
            Assert.That(materials[index], Is.Not.Null);
        }

        var visualObject = CreateObject("Refresh Card Visuals");
        visualObject.SetActive(false);
        controller = visualObject.AddComponent<CardVisualController>();
        for (int index = 0; index < visuals.Length; index++)
        {
            var root = CreateObject($"Card Root {index}");
            root.transform.SetParent(visualObject.transform);
            var cardObject = CreateObject($"Card {index}");
            cardObject.transform.SetParent(root.transform);
            renderers[index] = cardObject.AddComponent<MeshRenderer>();
            visuals[index] = cardObject.AddComponent<CardVisual>();
            SetField(visuals[index], "cardRenderer", renderers[index]);
            SetField(visuals[index], "rankMaterials", materials);
            if (index < 2)
            {
                string prefix = index == 0 ? "player" : "dealer";
                SetField(controller, prefix + "CardVisual", visuals[index]);
                SetField(controller, prefix + "CardRoot", root.transform);
                SetField(controller, prefix + "CardPoint", CreateAnchor(visualObject.transform));
                SetField(controller, prefix + "ShowdownCardPoint", CreateAnchor(visualObject.transform));
                if (index == 0)
                {
                    playerRoot = root.transform;
                }
            }
        }
        SetField(controller, "communityCardVisual1", visuals[2]);
        SetField(controller, "communityCardVisual2", visuals[3]);

        var deckObject = CreateObject("Refresh Deck");
        deckObject.SetActive(false);
        deckObject.transform.position = new Vector3(6f, 0f, 0f);
        deckStackVisual = deckObject.AddComponent<DeckStackVisual>();
        SetField(controller, "deckStackVisual", deckStackVisual);
        SetField(controller, "cardDealPoint", deckObject.transform);
        SetField(controller, "dealDuration", 0.1f);
        SetField(controller, "dealInterval", 0.02f);
        SetField(controller, "refreshCollectDuration", 0.28f);
        SetField(controller, "refreshCollectInterval", 0.06f);
        SetField(controller, "refreshBeforeShuffleDelay", 0.01f);
        SetField(controller, "refreshShuffleDuration", 0.1f);

        var uiObject = CreateObject("Refresh Card UI");
        uiObject.SetActive(false);
        inventory = Track(ScriptableObject.CreateInstance<Inventory>());
        itemSystem = uiObject.AddComponent<ItemSystem>();
        SetField(itemSystem, "inventory", inventory);
        ui = uiObject.AddComponent<GameplayController>();
        view = uiObject.AddComponent<GameplayView>();
        presentation = uiObject.AddComponent<GameplayPresentationController>();
        SetField(ui, "gameplayView", view);
        SetField(ui, "presentation", presentation);
        SetField(ui, "itemSystem", itemSystem);
        SetField(presentation, "cardVisualController", controller);
        Invoke(ui, "CreateGame");

        // P4/D1/C4/C2에서 P2/D4/C1/C4로 교체해 개인·공개 카드 변경을 보장한다.
        gameState = new GameState(20, 20,
            new Deck(new[] { new Card(9) }, new NoSwapRandom()));
        gameState.TrySetPlayerCard(new Card(4));
        gameState.TrySetDealerCard(new Card(1));
        gameState.TrySetCommunityCards(new Card(4), new Card(2));
        gameState.Pot.TryAdd(2);
        gameState.TrySetPhase(GamePhase.Betting);
        gameState.Turn.TrySet(TurnOwner.Player);
        SetField(ui, "gameState", gameState);
        itemSystem.Initialize(new ItemGameApi(gameState));
        presentation.Initialize(gameState, () => Invoke(ui, "RefreshView"));
        AssertVisualsMatchCurrentCards();
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }
        }
        createdObjects.Clear();
    }

    [TestCase(TurnOwner.Player)]
    [TestCase(TurnOwner.Dealer)]
    public void RefreshAfterDeal_ImmediatelySynchronizesAllRanksAndMaterials(TurnOwner owner)
    {
        UseRefresh(owner);

        Assert.That(gameState.PlayerCard.Rank, Is.EqualTo(2));
        Assert.That(gameState.CommunityCard1.Rank, Is.EqualTo(1));
        AssertVisualsMatchCurrentCards();
    }

    [TestCase(TurnOwner.Player)]
    [TestCase(TurnOwner.Dealer)]
    public void Refresh_DoesNotSynchronizeAgainAfterSuccessNotification(TurnOwner owner)
    {
        // 성공 통지 뒤 적용한 위치를 두 번째 RefreshCards 호출이 다시 초기화하는지 검사한다.
        Vector3 markerPosition = new Vector3(7, 8, 9);
        int notifications = 0;
        itemSystem.RefreshCardSucceeded += () =>
        {
            notifications++;
            AssertVisualsMatchCurrentCards();
            playerRoot.position = markerPosition;
        };

        UseRefresh(owner);

        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(playerRoot.position, Is.EqualTo(markerPosition));
        AssertVisualsMatchCurrentCards();
    }

    [Test]
    public void RefreshPresentation_CollectsShufflesAndReusesDeal()
    {
        int[] previousRanks = GetVisualRanks();
        int deckCount = gameState.Deck.RemainingCount;
        Vector3 deckPosition = deckStackVisual.transform.localPosition;
        controller.gameObject.SetActive(true);

        UseRefresh(TurnOwner.Player);

        Assert.That(presentation.IsCardAnimating, Is.True);
        Assert.That(GetVisualRanks(), Is.EqualTo(previousRanks));
        var refresh = (Sequence)GetField(controller, "refreshSequence");
        Assert.That(refresh, Is.Not.Null);
        refresh.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);

        Assert.That(GetField(controller, "refreshSequence"), Is.Null);
        Assert.That(deckStackVisual.transform.localPosition, Is.EqualTo(deckPosition));
        Assert.That(
            GetField(deckStackVisual, "currentCardCount"),
            Is.EqualTo(deckCount + 4));
        var deal = (Sequence)GetField(controller, "dealSequence");
        Assert.That(deal, Is.Not.Null);
        foreach (Renderer renderer in renderers)
        {
            Assert.That(renderer.enabled, Is.False);
        }

        deal.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);

        Assert.That(GetField(controller, "dealSequence"), Is.Null);
        Assert.That(
            GetField(deckStackVisual, "currentCardCount"),
            Is.EqualTo(deckCount));
        AssertVisualsMatchCurrentCards();
        Invoke(controller, "CompleteRefreshDeal");
    }

    [Test]
    public void RefreshPresentation_BlocksDealAndShowdownUntilCollectionEnds()
    {
        controller.gameObject.SetActive(true);
        UseRefresh(TurnOwner.Player);

        Assert.That(
            controller.TryPlayDeal(() => { }, () => { }),
            Is.False);
        Assert.That(
            controller.TryPlayShowdownReveal(() => { }, () => { }),
            Is.False);
    }

    [Test]
    public void RefreshPresentation_DisableRestoresLatestCardsAndDeck()
    {
        int deckCount = gameState.Deck.RemainingCount;
        Vector3 deckPosition = deckStackVisual.transform.localPosition;
        controller.gameObject.SetActive(true);
        UseRefresh(TurnOwner.Player);
        var refresh = (Sequence)GetField(controller, "refreshSequence");
        Assert.That(refresh, Is.Not.Null);
        refresh.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.12f, 0.12f);

        controller.gameObject.SetActive(false);

        Assert.That(GetField(controller, "refreshSequence"), Is.Null);
        Assert.That(deckStackVisual.transform.localPosition, Is.EqualTo(deckPosition));
        Assert.That(
            GetField(deckStackVisual, "currentCardCount"),
            Is.EqualTo(deckCount));
        AssertVisualsMatchCurrentCards();
    }

    [Test]
    public void RefreshThenShowdown_DisplayedCardsMatchCurrentWinner()
    {
        UseRefresh(TurnOwner.Player);
        Assert.That(gameState.TryRaise(1), Is.True);
        Assert.That(gameState.TryCall(), Is.True);
        Assert.That(gameState.TryDetermineWinner(out RoundWinner expected), Is.True);
        Assert.That(expected, Is.EqualTo(RoundWinner.Dealer));
        controller.gameObject.SetActive(true);

        Invoke(ui, "ResolveShowdown");
        var reveal = (Sequence)GetField(controller, "showdownRevealSequence");
        Assert.That(reveal, Is.Not.Null);
        reveal.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(10f, 10f);

        Assert.That(GetField(ui, "roundWinner"), Is.EqualTo(expected));
        Assert.That(GetField(ui, "playerHandRank"), Is.EqualTo(HandRank.Number));
        Assert.That(GetField(ui, "dealerHandRank"), Is.EqualTo(HandRank.Double));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(GetField(controller, "showdownRevealSequence"), Is.Null);
        AssertVisualsMatchCurrentCards();
    }

    [TestCase(ItemType.chipPocket)]
    [TestCase(ItemType.prizmChip)]
    [TestCase(ItemType.defy)]
    public void OtherItems_DoNotNotifyOrResynchronizeCardVisuals(ItemType type)
    {
        if (type == ItemType.defy)
        {
            gameState.Turn.TrySet(TurnOwner.Dealer);
            Assert.That(gameState.TryRaise(1), Is.True);
        }
        int notifications = 0;
        itemSystem.RefreshCardSucceeded += () => notifications++;
        Vector3 markerPosition = new Vector3(7, 8, 9);
        playerRoot.position = markerPosition;
        AddItem(TurnOwner.Player, type);

        Assert.That(itemSystem.TryUseItem(TurnOwner.Player, type), Is.True);

        Assert.That(notifications, Is.Zero);
        Assert.That(playerRoot.position, Is.EqualTo(markerPosition));
        AssertVisualsMatchCurrentCards();
    }

    [Test]
    public void FailedRefresh_DoesNotNotifyOrConsumeItem()
    {
        Assert.That(gameState.Deck.TryDraw(out _), Is.True);
        int notifications = 0;
        itemSystem.RefreshCardSucceeded += () => notifications++;
        GameObject item = AddItem(TurnOwner.Player, ItemType.refreshCard);

        Assert.That(itemSystem.TryUseItem(TurnOwner.Player, ItemType.refreshCard), Is.False);

        Assert.That(notifications, Is.Zero);
        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
        AssertVisualsMatchCurrentCards();
    }

    [TestCase("isCardAnimating")]
    [TestCase("isChipAnimating")]
    public void RefreshDuringRoundStart_UsesExistingDealCompletionSynchronization(string flag)
    {
        ConfigureView();
        SetField(presentation, flag, true);
        foreach (CardVisual visual in visuals)
        {
            visual.SetVisible(false);
        }

        UseRefresh(TurnOwner.Player);

        Assert.That(visuals[0].CurrentRank, Is.EqualTo(4));
        foreach (Renderer renderer in renderers)
        {
            Assert.That(renderer.enabled, Is.False);
        }
        SetField(presentation, "isChipAnimating", false);
        Invoke(presentation, "OnCardDealCompleted");
        AssertVisualsMatchCurrentCards();
    }

    [Test]
    public void SubscriptionLifecycle_DoesNotDuplicateOrRetainUiHandlers()
    {
        Invoke(ui, "OnEnable");
        Invoke(ui, "OnEnable");
        Assert.That(SubscriberCount(), Is.EqualTo(1));
        Invoke(ui, "OnDisable");
        Assert.That(SubscriberCount(), Is.Zero);
        Invoke(ui, "OnEnable");
        Assert.That(SubscriberCount(), Is.EqualTo(1));
        Invoke(ui, "OnDestroy");
        Assert.That(SubscriberCount(), Is.Zero);
    }

    private void UseRefresh(TurnOwner owner)
    {
        gameState.Turn.TrySet(owner);
        GameObject item = AddItem(owner, ItemType.refreshCard);
        Assert.That(itemSystem.TryUseItem(owner, ItemType.refreshCard), Is.True);
        Assert.That(item == null, Is.True);
    }

    private int[] GetVisualRanks()
    {
        var ranks = new int[visuals.Length];
        for (int index = 0; index < visuals.Length; index++)
        {
            ranks[index] = visuals[index].CurrentRank;
        }

        return ranks;
    }

    private void AssertVisualsMatchCurrentCards()
    {
        Card[] cards = { gameState.PlayerCard, gameState.DealerCard,
            gameState.CommunityCard1, gameState.CommunityCard2 };
        for (int index = 0; index < cards.Length; index++)
        {
            int rank = cards[index].Rank;
            Assert.That(visuals[index].CurrentRank, Is.EqualTo(rank), $"Card {index}");
            Assert.That(renderers[index].sharedMaterial, Is.SameAs(materials[rank - 1]));
            Assert.That(renderers[index].enabled, Is.True);
        }
    }

    private GameObject AddItem(TurnOwner owner, ItemType type)
    {
        var data = Track(ScriptableObject.CreateInstance<ItemData>());
        data.itemType = type;
        var gameObject = CreateObject(type.ToString());
        Item item = gameObject.AddComponent<Item>();
        item.itemSystem = itemSystem;
        item.itemData = data;
        Assert.That(inventory.AddItem(owner, gameObject), Is.True);
        return gameObject;
    }

    private void ConfigureView()
    {
        foreach (string field in new[] { "roundText", "debugInfoText", "messageText",
            "raiseAmountText", "raiseExecuteText" })
        {
            GameObject textObject = CreateObject(field);
            textObject.transform.SetParent(ui.transform);
            SetField(view, field, textObject.AddComponent<TextMeshProUGUI>());
        }
        foreach (string field in new[] { "raiseDecreaseButton", "raiseIncreaseButton",
            "raiseMaxButton", "raiseExecuteButton", "resolveShowdownButton",
            "nextRoundButton", "restartButton" })
        {
            GameObject buttonObject = CreateObject(field);
            buttonObject.transform.SetParent(ui.transform);
            SetField(view, field, buttonObject.AddComponent<Button>());
        }
        foreach (string field in new[] { "debugPanel", "playerActionBar",
            "contextActionArea", "resultOverlay" })
        {
            GameObject gameObject = CreateObject(field);
            gameObject.transform.SetParent(ui.transform);
            SetField(view, field, gameObject);
        }
        SetField(view, "bettingActionButtons", new Button[0]);
    }

    private int SubscriberCount()
    {
        var handlers = (System.Action)GetField(itemSystem, "RefreshCardSucceeded");
        return handlers?.GetInvocationList().Length ?? 0;
    }

    private Transform CreateAnchor(Transform parent)
    {
        GameObject anchor = CreateObject("Card Anchor");
        anchor.transform.SetParent(parent);
        return anchor.transform;
    }

    private GameObject CreateObject(string name) => Track(new GameObject(name));
    private T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    private static object Invoke(object target, string name, object[] arguments = null)
    {
        MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private sealed class NoSwapRandom : System.Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
