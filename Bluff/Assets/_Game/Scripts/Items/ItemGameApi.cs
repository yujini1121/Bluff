using System;

public sealed class ItemGameApi
{
    private readonly GameState gameState;

    public ItemGameApi(GameState gameState)
    {
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    }

    public GamePhase GetCurrentPhase()
    {
        return gameState.Phase;
    }

    public int GetPot()
    {
        return gameState.Pot.Amount;
    }

    public bool TryGiveChips(TurnOwner target, int amount)
    {
        switch (target)
        {
            case TurnOwner.Player:
                return gameState.PlayerChips.TryAdd(amount);
            case TurnOwner.Dealer:
                return gameState.DealerChips.TryAdd(amount);
            default:
                return false;
        }
    }

    public bool TryReplaceCard(CardTarget target)
    {
        // Check : 카드 재설정이 가능한 상태인가?
        if (!CanReplaceCard(target) || gameState.Deck.RemainingCount == 0)
        {
            return false;
        }

        // 기존 카드를 덱에 다시 넣음

        // 카드를 뽑음
        if (!gameState.Deck.TryDraw(out Card card))
        {
            return false;
        }

        switch (target)
        {
            case CardTarget.Player:
                return gameState.TrySetPlayerCard(card);
            case CardTarget.Dealer:
                return gameState.TrySetDealerCard(card);
            case CardTarget.CommunityCard1:
                return gameState.TrySetCommunityCards(card, gameState.CommunityCard2);
            case CardTarget.CommunityCard2:
                return gameState.TrySetCommunityCards(gameState.CommunityCard1, card);
            default:
                return false;
        }
    }

    public bool TryCall()
    {
        return gameState.TryCall();
    }

    public bool TryFoldWithoutPenalty()
    {
        return gameState.TryFold(isFoldPenaltyWaived: true);
    }

    private bool CanReplaceCard(CardTarget target)
    {
        switch (target)
        {
            case CardTarget.Player:
                return gameState.PlayerCard != null;
            case CardTarget.Dealer:
                return gameState.DealerCard != null;
            case CardTarget.CommunityCard1:
            case CardTarget.CommunityCard2:
                return gameState.CommunityCard1 != null &&
                       gameState.CommunityCard2 != null;
            default:
                return false;
        }
    }
}
