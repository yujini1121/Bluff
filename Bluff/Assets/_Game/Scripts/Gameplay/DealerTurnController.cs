using System;
using System.Collections.Generic;

public sealed class DealerTurnController
{
    private readonly DealerAi dealerAi = new DealerAi();
    private readonly DealerItemAi dealerItemAi = new DealerItemAi();

    private GameState gameState;
    private ItemSystem itemSystem;
    private Action<string> addLog;

    public void Initialize(
        GameState gameState,
        ItemSystem itemSystem,
        Action<string> addLog)
    {
        this.gameState = gameState;
        this.itemSystem = itemSystem;
        this.addLog = addLog;
    }

    public bool TryPrepareAction(
        int actionRoll,
        int raiseRoll,
        out DealerActionPlan actionPlan)
    {
        actionPlan = dealerAi.Decide(gameState, actionRoll, raiseRoll);
        IReadOnlyList<ItemType> ownedItems = itemSystem != null
            ? itemSystem.GetOwnedItemTypes(TurnOwner.Dealer)
            : Array.Empty<ItemType>();
        DealerItemPlan itemPlan = dealerItemAi.Decide(gameState, ownedItems, actionPlan);

        if (!itemPlan.ShouldUseItem)
        {
            addLog("DEALER ITEM NONE - 아이템을 사용하지 않음");
            return true;
        }

        ItemType selectedItemType = itemPlan.SelectedItem.Value;
        bool itemUsed = itemSystem.TryUseItem(TurnOwner.Dealer, selectedItemType);
        addLog(itemUsed
            ? $"DEALER ITEM {selectedItemType} 사용"
            : $"DEALER ITEM {selectedItemType} 사용 실패");

        // 사용 요청이 실패했더라도 효과가 상태를 일부 변경했을 수 있으므로 다시 확인
        if (gameState.Phase != GamePhase.Betting ||
            gameState.CurrentTurn != TurnOwner.Dealer)
        {
            addLog($"DEALER ITEM 이후 추가 행동 없음 - Phase: {gameState.Phase}, Turn: {gameState.CurrentTurn}");
            actionPlan = DealerActionPlan.None;
            return false;
        }

        addLog("DEALER ITEM 이후 일반 행동 재계산");
        actionPlan = dealerAi.Decide(gameState, actionRoll, raiseRoll);
        return true;
    }

    public bool TryExecute(DealerActionPlan actionPlan)
    {
        return dealerAi.TryExecute(gameState, actionPlan);
    }
}
