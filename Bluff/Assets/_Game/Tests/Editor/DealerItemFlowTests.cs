using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DealerItemFlowTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> createdObjects = new List<Object>();
    private readonly List<string> logs = new List<string>();
    private ItemSystem itemSystem;

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
        logs.Clear();
        itemSystem = null;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NoSelectedItem_KeepsExistingAction(bool ownsIneligibleItem)
    {
        GameState gameState = CreateGame(20);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject item = ownsIneligibleItem
            ? AddItem(inventory, ItemType.chipPocket)
            : null;
        DealerActionPlan expected = new DealerAi().Decide(gameState, 99, 0);

        Assert.That(dealerTurn.TryPrepareAction(99, 0, out DealerActionPlan actual), Is.True);

        AssertSamePlan(actual, expected);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        if (ownsIneligibleItem)
        {
            Assert.That(item == null, Is.False);
            Assert.That(inventory.HasItem(TurnOwner.Dealer, item), Is.True);
        }
        AssertLog("DEALER ITEM NONE");
        Assert.That(dealerTurn.TryExecute(actual), Is.True);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void RefreshCard_RecalculatesActionFromNewCards()
    {
        // 셔플 순서를 고정해 기존 Fold 계획과 새 카드의 Raise 계획을 구분한다.
        var deck = new Deck(new[] { new Card(9) }, new NoSwapRandom());
        GameState gameState = CreateGame(20, deck);
        Assert.That(gameState.TrySetPlayerCard(new Card(4)), Is.True);
        Assert.That(gameState.TrySetDealerCard(new Card(1)), Is.True);
        Assert.That(gameState.TrySetCommunityCards(new Card(4), new Card(2)), Is.True);
        Assert.That(gameState.Pot.TryAdd(2), Is.True);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, ItemType.refreshCard);
        DealerActionPlan previous = new DealerAi().Decide(gameState, 20, 0);
        Assert.That(previous.Decision, Is.EqualTo(DealerDecision.Fold));

        Assert.That(dealerTurn.TryPrepareAction(20, 0, out DealerActionPlan actual), Is.True);

        Assert.That(item == null, Is.True);
        Assert.That(inventory.dealerItemInventory, Is.All.Null);
        Assert.That(gameState.PlayerCard.Rank, Is.EqualTo(2));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        AssertSamePlan(actual, new DealerAi().Decide(gameState, 20, 0));
        Assert.That(actual.Decision, Is.EqualTo(DealerDecision.Raise));
        AssertLog("일반 행동 재계산");
        Assert.That(dealerTurn.TryExecute(actual), Is.True);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.None));
    }

    [Test]
    public void ChipPocket_RecalculatesBetFromIncreasedChips()
    {
        GameState gameState = CreateGame(5);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, ItemType.chipPocket);
        DealerActionPlan previous = new DealerAi().Decide(gameState, 99, 99);

        Assert.That(dealerTurn.TryPrepareAction(99, 99, out DealerActionPlan actual), Is.True);

        Assert.That(item == null, Is.True);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(7));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        AssertSamePlan(actual, new DealerAi().Decide(gameState, 99, 99));
        Assert.That(actual.RaiseBy, Is.Not.EqualTo(previous.RaiseBy));
        AssertLog("일반 행동 재계산");
        Assert.That(dealerTurn.TryExecute(actual), Is.True);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Player));
    }

    [Test]
    public void PrizmChip_FoldStopsAdditionalAction()
    {
        GameState gameState = CreateGame(20);
        Assert.That(gameState.TrySetPlayerCard(new Card(4)), Is.True);
        Assert.That(gameState.TrySetDealerCard(new Card(1)), Is.True);
        Assert.That(gameState.TrySetCommunityCards(new Card(4), new Card(2)), Is.True);
        Assert.That(gameState.Turn.TrySet(TurnOwner.Player), Is.True);
        Assert.That(gameState.TryRaise(2), Is.True);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, ItemType.prizmChip);
        AddItem(inventory, ItemType.chipPocket);

        Assert.That(dealerTurn.TryPrepareAction(0, 0, out DealerActionPlan actual), Is.False);

        Assert.That(item == null, Is.True);
        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(actual.Decision, Is.EqualTo(DealerDecision.None));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(inventory.dealerItemInventory[1] == null, Is.False);
        AssertLog("추가 행동 없음");
        Assert.That(logs, Has.None.Contains("일반 행동 재계산"));
    }

    [Test]
    public void Defy_ShowdownStopsAdditionalAction()
    {
        GameState gameState = CreateGame(6);
        Assert.That(gameState.Turn.TrySet(TurnOwner.Player), Is.True);
        Assert.That(gameState.TryRaise(3), Is.True);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, ItemType.defy);

        Assert.That(dealerTurn.TryPrepareAction(10, 0, out DealerActionPlan actual), Is.False);

        Assert.That(item == null, Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(actual.Decision, Is.EqualTo(DealerDecision.None));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(6));
        AssertLog("추가 행동 없음");
        Assert.That(logs, Has.None.Contains("일반 행동 재계산"));
    }

    [Test]
    public void OneOpportunity_ConsumesOnlyOneOfTwoEligibleItems()
    {
        GameState gameState = CreateGame(2);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        GameObject first = AddItem(inventory, ItemType.chipPocket);
        GameObject second = AddItem(inventory, ItemType.chipPocket);

        Assert.That(dealerTurn.TryPrepareAction(99, 0, out DealerActionPlan actual), Is.True);

        Assert.That(first == null, Is.True);
        Assert.That(second == null, Is.False);
        Assert.That(inventory.HasItem(TurnOwner.Dealer, second), Is.True);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(4));
        Assert.That(new DealerItemAi().Decide(
            gameState, new[] { ItemType.chipPocket }, actual).ShouldUseItem, Is.True);
        Assert.That(dealerTurn.TryExecute(actual), Is.True);
        Assert.That(inventory.HasItem(TurnOwner.Dealer, second), Is.True);
    }

    [Test]
    public void FailedItemRequest_RecalculatesWithoutConsumingItem()
    {
        GameState gameState = CreateGame(5);
        DealerTurnController dealerTurn = CreateDealerTurn(gameState, out Inventory inventory);
        SetField(itemSystem, "chipPocketAmount", 0);
        GameObject item = AddItem(inventory, ItemType.chipPocket);

        Assert.That(dealerTurn.TryPrepareAction(99, 0, out DealerActionPlan actual), Is.True);

        Assert.That(item == null, Is.False);
        Assert.That(inventory.HasItem(TurnOwner.Dealer, item), Is.True);
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(5));
        AssertSamePlan(actual, new DealerAi().Decide(gameState, 99, 0));
        AssertLog("사용 실패");
        AssertLog("일반 행동 재계산");
    }

    private DealerTurnController CreateDealerTurn(GameState gameState, out Inventory inventory)
    {
        inventory = Track(ScriptableObject.CreateInstance<Inventory>());
        var gameObject = Track(new GameObject("Dealer Item Flow Test"));
        gameObject.SetActive(false);
        itemSystem = gameObject.AddComponent<ItemSystem>();
        SetField(itemSystem, "inventory", inventory);
        itemSystem.Initialize(new ItemGameApi(gameState));
        var dealerTurn = new DealerTurnController();
        dealerTurn.Initialize(gameState, itemSystem, logs.Add);
        return dealerTurn;
    }

    private GameObject AddItem(Inventory inventory, ItemType type)
    {
        var data = Track(ScriptableObject.CreateInstance<ItemData>());
        data.itemType = type;
        var gameObject = Track(new GameObject(type.ToString()));
        gameObject.AddComponent<Item>().itemData = data;
        Assert.That(inventory.AddItem(TurnOwner.Dealer, gameObject), Is.True);
        return gameObject;
    }

    private static GameState CreateGame(int dealerChips, Deck deck = null)
    {
        var gameState = new GameState(20, dealerChips,
            deck ?? new Deck(new[] { new Card(9), new Card(10) }));
        Assert.That(gameState.TrySetPlayerCard(new Card(1)), Is.True);
        Assert.That(gameState.TrySetDealerCard(new Card(2)), Is.True);
        Assert.That(gameState.TrySetCommunityCards(new Card(4), new Card(7)), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.Turn.TrySet(TurnOwner.Dealer), Is.True);
        return gameState;
    }

    private static void AssertSamePlan(DealerActionPlan actual, DealerActionPlan expected)
    {
        Assert.That(actual.Decision, Is.EqualTo(expected.Decision));
        Assert.That(actual.RaiseBy, Is.EqualTo(expected.RaiseBy));
    }

    private void AssertLog(string fragment)
    {
        Assert.That(logs, Has.Some.Contains(fragment));
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }

    private sealed class NoSwapRandom : System.Random
    {
        public override int Next(int maxValue) => maxValue - 1;
    }
}
