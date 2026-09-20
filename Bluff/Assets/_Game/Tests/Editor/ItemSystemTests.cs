using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ItemSystemTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> createdObjects = new List<Object>();

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

    [TestCase(TurnOwner.Player, true)]
    [TestCase(TurnOwner.Dealer, false)]
    public void ItemUse_PlayerClickRespectsTurnAndConsumesOnlyOnSuccess(
        TurnOwner turn,
        bool expectedSuccess)
    {
        GameState gameState = CreateBettingGame(turn);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Player, ItemType.chipPocket);
        Item itemComponent = item.GetComponent<Item>();
        itemComponent.itemSystem = itemSystem;

        itemComponent.Use();

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(expectedSuccess ? 22 : 20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(inventory.playerItemInventory[0] == null, Is.EqualTo(expectedSuccess));
        Assert.That(item == null, Is.EqualTo(expectedSuccess));
    }

    [Test]
    public void UseItem_CannotUseOtherOwnersItem()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Dealer, ItemType.chipPocket);

        Assert.That(itemSystem.UseItem(TurnOwner.Player, item), Is.False);

        Assert.That(inventory.HasItem(TurnOwner.Dealer, item), Is.True);
        Assert.That(item == null, Is.False);
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UseItem_InvalidItemInformationDoesNotConsumeItem(bool hasItemComponent)
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        var item = Track(new GameObject("Invalid Item"));
        if (hasItemComponent)
        {
            item.AddComponent<Item>();
        }
        Assert.That(inventory.AddItem(TurnOwner.Player, item), Is.True);

        Assert.That(itemSystem.UseItem(TurnOwner.Player, item), Is.False);

        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
        Assert.That(item == null, Is.False);
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
    }

    [Test]
    public void TryUseItem_PlayerCannotUseItemOnDealerTurn()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Player, ItemType.chipPocket);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Player, ItemType.chipPocket),
            Is.False);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(inventory.HasItem(TurnOwner.Player, item), Is.True);
    }

    [Test]
    public void TryUseItem_DealerCannotUseItemOnPlayerTurn()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Dealer, ItemType.chipPocket);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.chipPocket),
            Is.False);

        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(inventory.HasItem(TurnOwner.Dealer, item), Is.True);
    }

    [Test]
    public void TryUseItem_DealerChipPocketAddsDealerChipsAndConsumesItem()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Dealer, ItemType.chipPocket);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.chipPocket),
            Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(22));
        Assert.That(inventory.dealerItemInventory, Is.All.Null);
        Assert.That(item == null, Is.True);
    }

    [Test]
    public void TryUseItem_DealerPrizmChipFoldsWithoutPenalty()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        SetRoundCards(gameState, 9, 4, 4, 4);
        Assert.That(gameState.Pot.TryAdd(4), Is.True);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        AddItem(inventory, TurnOwner.Dealer, ItemType.prizmChip);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.prizmChip),
            Is.True);

        Assert.That(gameState.FoldedBy, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.FoldPenaltyAmount, Is.Zero);
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(24));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.RoundEnd));
        Assert.That(inventory.dealerItemInventory, Is.All.Null);
    }

    [Test]
    public void TryUseItem_DealerDefyIgnoresPlayerRaise()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Player);
        Assert.That(gameState.TryRaise(4), Is.True);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        AddItem(inventory, TurnOwner.Dealer, ItemType.defy);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.defy),
            Is.True);

        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(20));
        Assert.That(gameState.Pot.Amount, Is.Zero);
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.None));
        Assert.That(inventory.dealerItemInventory, Is.All.Null);
    }

    [Test]
    public void TryUseItem_DealerRefreshCardUsesExistingRefreshFlow()
    {
        var deck = new Deck(new[] { new Card(10), new Card(9) });
        var gameState = new GameState(20, 20, deck);
        SetRoundCards(gameState, 1, 2, 3, 4);
        Assert.That(gameState.Pot.TryAdd(2), Is.True);
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.Turn.TrySet(TurnOwner.Dealer), Is.True);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        AddItem(inventory, TurnOwner.Dealer, ItemType.refreshCard);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.refreshCard),
            Is.True);

        Assert.That(gameState.PlayerCard, Is.Not.Null);
        Assert.That(gameState.DealerCard, Is.Not.Null);
        Assert.That(gameState.CommunityCard1, Is.Not.Null);
        Assert.That(gameState.CommunityCard2, Is.Not.Null);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(inventory.dealerItemInventory, Is.All.Null);
    }

    [Test]
    public void TryUseItem_CannotUseUnownedItemType()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject ownedItem = AddItem(
            inventory,
            TurnOwner.Dealer,
            ItemType.chipPocket);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.prizmChip),
            Is.False);

        Assert.That(inventory.HasItem(TurnOwner.Dealer, ownedItem), Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
    }

    [Test]
    public void TryUseItem_FailedEffectDoesNotRemoveItem()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        Assert.That(gameState.Pot.TryAdd(3), Is.True);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Dealer, ItemType.defy);

        Assert.That(
            itemSystem.TryUseItem(TurnOwner.Dealer, ItemType.defy),
            Is.False);

        Assert.That(inventory.HasItem(TurnOwner.Dealer, item), Is.True);
        Assert.That(item == null, Is.False);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
    }

    [Test]
    public void GetOwnedItemTypes_ReturnsOnlySelectedOwnersValidItems()
    {
        GameState gameState = CreateBettingGame(TurnOwner.Dealer);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        AddItem(inventory, TurnOwner.Player, ItemType.prizmChip);
        AddItem(inventory, TurnOwner.Dealer, ItemType.chipPocket);
        AddItem(inventory, TurnOwner.Dealer, ItemType.defy);
        var invalidItem = Track(new GameObject("Item Without Data"));
        Assert.That(inventory.AddItem(TurnOwner.Dealer, invalidItem), Is.True);

        Assert.That(
            itemSystem.GetOwnedItemTypes(TurnOwner.Player),
            Is.EqualTo(new[] { ItemType.prizmChip }));
        Assert.That(
            itemSystem.GetOwnedItemTypes(TurnOwner.Dealer),
            Is.EqualTo(new[] { ItemType.chipPocket, ItemType.defy }));
        Assert.That(itemSystem.GetOwnedItemTypes(TurnOwner.None), Is.Empty);
    }

    [TestCase(TurnOwner.Player, false)]
    [TestCase(TurnOwner.Dealer, false)]
    [TestCase(TurnOwner.Player, true)]
    [TestCase(TurnOwner.Dealer, true)]
    public void TryUseRefreshCard_BeforeBettingSucceedsRegardlessOfCarriedPot(
        TurnOwner owner,
        bool carriedPot)
    {
        GameState gameState = CreateRefreshRound(owner, carriedPot);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, owner, ItemType.refreshCard);
        int potBefore = gameState.Pot.Amount;
        int playerChipsBefore = gameState.PlayerChips.Count;
        int dealerChipsBefore = gameState.DealerChips.Count;

        Assert.That(itemSystem.TryUseItem(owner, ItemType.refreshCard), Is.True);

        Assert.That(item == null, Is.True);
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potBefore));
        Assert.That(gameState.PlayerChips.Count, Is.EqualTo(playerChipsBefore));
        Assert.That(gameState.DealerChips.Count, Is.EqualTo(dealerChipsBefore));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(owner));
        Assert.That(itemSystem.GetOwnedItemTypes(owner), Is.Empty);
    }

    [TestCase(TurnOwner.Player, false)]
    [TestCase(TurnOwner.Dealer, false)]
    [TestCase(TurnOwner.Player, true)]
    [TestCase(TurnOwner.Dealer, true)]
    public void TryUseRefreshCard_AfterVoluntaryRaiseDoesNotConsumeItem(
        TurnOwner owner,
        bool carriedPot)
    {
        TurnOwner firstTurn = owner == TurnOwner.Player
            ? TurnOwner.Dealer
            : TurnOwner.Player;
        GameState gameState = CreateRefreshRound(firstTurn, carriedPot);
        Assert.That(gameState.TryRaise(1), Is.True);
        Assert.That(gameState.CurrentTurn, Is.EqualTo(owner));
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, owner, ItemType.refreshCard);
        Card playerCardBefore = gameState.PlayerCard;
        int deckCountBefore = gameState.Deck.RemainingCount;
        int potBefore = gameState.Pot.Amount;

        Assert.That(itemSystem.TryUseItem(owner, ItemType.refreshCard), Is.False);

        Assert.That(item == null, Is.False);
        Assert.That(inventory.HasItem(owner, item), Is.True);
        Assert.That(gameState.PlayerCard, Is.SameAs(playerCardBefore));
        Assert.That(gameState.Deck.RemainingCount, Is.EqualTo(deckCountBefore));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(potBefore));
    }

    [Test]
    public void TryUseRefreshCard_SelectedByDealerItemAiInCarriedPotRoundSucceeds()
    {
        GameState gameState = CreateRefreshRound(TurnOwner.Dealer, true);
        ItemSystem itemSystem = CreateItemSystem(gameState, out Inventory inventory);
        GameObject item = AddItem(inventory, TurnOwner.Dealer, ItemType.refreshCard);
        DealerActionPlan actionPlan = new DealerAi().Decide(gameState, 20, 0);
        DealerItemPlan itemPlan = new DealerItemAi().Decide(
            gameState,
            itemSystem.GetOwnedItemTypes(TurnOwner.Dealer),
            actionPlan);

        Assert.That(itemPlan.SelectedItem, Is.EqualTo(ItemType.refreshCard));
        Assert.That(itemSystem.TryUseItem(TurnOwner.Dealer, itemPlan.SelectedItem.Value), Is.True);

        Assert.That(item == null, Is.True);
        Assert.That(gameState.Phase, Is.EqualTo(GamePhase.Betting));
        Assert.That(gameState.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.Zero);
        Assert.That(gameState.Betting.DealerTotalBet, Is.Zero);
    }

    private static GameState CreateRefreshRound(TurnOwner owner, bool carriedPot)
    {
        var gameState = new GameState(20, 20, Deck.CreateIndianHoldemDeck());
        Assert.That(gameState.TryStartRound(carriedPot ? TurnOwner.Player : owner), Is.True);

        if (carriedPot)
        {
            SetRoundCards(gameState, 1, 1, 4, 7);
            Assert.That(gameState.TryRaise(2), Is.True);
            Assert.That(gameState.TryCall(), Is.True);
            Assert.That(gameState.TrySettleShowdown(out RoundWinner winner), Is.True);
            Assert.That(winner, Is.EqualTo(RoundWinner.Draw));
            Assert.That(gameState.Pot.Amount, Is.EqualTo(6));
            Assert.That(gameState.TryPrepareNextRound(), Is.True);
            Assert.That(gameState.TryStartRound(owner), Is.True);
            // 이월 라운드에서는 추가 Ante 없이 이전 라운드의 칩과 Pot을 유지한다.
            Assert.That(gameState.PlayerChips.Count, Is.EqualTo(17));
            Assert.That(gameState.DealerChips.Count, Is.EqualTo(17));
        }

        Assert.That(gameState.Pot.Amount, Is.EqualTo(carriedPot ? 6 : 2));
        Assert.That(gameState.Betting.PlayerTotalBet, Is.EqualTo(carriedPot ? 0 : 1));
        Assert.That(gameState.Betting.DealerTotalBet, Is.EqualTo(carriedPot ? 0 : 1));
        Assert.That(gameState.Betting.GetCallAmount(owner), Is.Zero);
        SetRoundCards(gameState, 4, 1, 4, 2);
        return gameState;
    }

    private ItemSystem CreateItemSystem(
        GameState gameState,
        out Inventory inventory)
    {
        inventory = Track(ScriptableObject.CreateInstance<Inventory>());
        var gameObject = Track(new GameObject("Item System Test"));
        ItemSystem itemSystem = gameObject.AddComponent<ItemSystem>();
        FieldInfo inventoryField = typeof(ItemSystem).GetField(
            "inventory",
            PrivateInstance);
        Assert.That(inventoryField, Is.Not.Null);
        inventoryField.SetValue(itemSystem, inventory);
        itemSystem.Initialize(new ItemGameApi(gameState));
        return itemSystem;
    }

    private GameObject AddItem(
        Inventory inventory,
        TurnOwner owner,
        ItemType type)
    {
        var itemData = Track(ScriptableObject.CreateInstance<ItemData>());
        itemData.itemType = type;
        var gameObject = Track(new GameObject(type.ToString()));
        Item item = gameObject.AddComponent<Item>();
        item.itemData = itemData;
        Assert.That(inventory.AddItem(owner, gameObject), Is.True);
        return gameObject;
    }

    private static GameState CreateBettingGame(TurnOwner turn)
    {
        var gameState = new GameState(20, 20, CreateDeck());
        Assert.That(gameState.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(gameState.Turn.TrySet(turn), Is.True);
        return gameState;
    }

    private static void SetRoundCards(
        GameState gameState,
        int playerRank,
        int dealerRank,
        int communityRank1,
        int communityRank2)
    {
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
    }

    private static Deck CreateDeck()
    {
        return new Deck(new[] { new Card(1), new Card(2) });
    }

    private T Track<T>(T value) where T : Object
    {
        createdObjects.Add(value);
        return value;
    }
}
