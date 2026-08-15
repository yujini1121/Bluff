using System;

public sealed class GameState
{
    public GamePhase Phase { get; private set; }
    public RoundEndReason RoundEndReason { get; private set; }
    public TurnOwner FoldedBy { get; private set; }
    public TurnState Turn { get; }
    public TurnOwner CurrentTurn => Turn.Owner;
    public ChipStack PlayerChips { get; }
    public ChipStack DealerChips { get; }
    public Pot Pot { get; }
    public BettingState Betting { get; }
    public Deck Deck { get; }
    public Card PlayerCard { get; private set; }
    public Card DealerCard { get; private set; }
    public Card CommunityCard1 { get; private set; }
    public Card CommunityCard2 { get; private set; }

    public GameState(int playerStartingChips, int dealerStartingChips, Deck deck)
    {
        Deck = deck ?? throw new ArgumentNullException(nameof(deck));
        PlayerChips = new ChipStack(playerStartingChips);
        DealerChips = new ChipStack(dealerStartingChips);
        Pot = new Pot();
        Betting = new BettingState();
        Turn = new TurnState();
        Phase = GamePhase.Setup;
        ResetRoundResult();
    }

    public bool TrySetPhase(GamePhase phase)
    {
        if (!IsValid(phase))
        {
            return false;
        }

        Phase = phase;
        return true;
    }

    public bool TrySetPlayerCard(Card card)
    {
        if (card == null)
        {
            return false;
        }

        PlayerCard = card;
        return true;
    }

    public bool TrySetDealerCard(Card card)
    {
        if (card == null)
        {
            return false;
        }

        DealerCard = card;
        return true;
    }

    public bool TrySetCommunityCards(Card communityCard1, Card communityCard2)
    {
        if (communityCard1 == null || communityCard2 == null)
        {
            return false;
        }

        CommunityCard1 = communityCard1;
        CommunityCard2 = communityCard2;
        return true;
    }

    public void ClearCards()
    {
        PlayerCard = null;
        DealerCard = null;
        CommunityCard1 = null;
        CommunityCard2 = null;
    }

    public void ResetRoundResult()
    {
        RoundEndReason = global::RoundEndReason.None;
        FoldedBy = TurnOwner.None;
    }

    private bool TryAddBetToPot(int amount)
    {
        if (!CanAddBetToPot(amount))
        {
            return false;
        }

        AddBetToPot(amount);
        return true;
    }

    private bool CanAddBetToPot(int amount)
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        ChipStack ownerChips = GetChips(CurrentTurn);

        return amount > 0 &&
               amount <= ownerChips.Count &&
               amount <= int.MaxValue - Pot.Amount &&
               Betting.CanAddToTotalBet(CurrentTurn, amount);
    }

    private void AddBetToPot(int amount)
    {
        ChipStack ownerChips = GetChips(CurrentTurn);
        ownerChips.TrySpend(amount);
        Pot.TryAdd(amount);
        Betting.TryAddToTotalBet(CurrentTurn, amount);
    }

    public bool TryCall()
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        int callAmount = Betting.GetCallAmount(CurrentTurn);

        if (callAmount <= 0 || !TryAddBetToPot(callAmount))
        {
            return false;
        }

        FinishBetting();
        return true;
    }

    public bool TryRaise(int raiseBy)
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        TurnOwner opponent = GetOpponent(CurrentTurn);

        if (raiseBy <= 0 || GetChips(opponent).Count == 0)
        {
            return false;
        }

        int callAmount = Betting.GetCallAmount(CurrentTurn);

        if (callAmount > int.MaxValue - raiseBy)
        {
            return false;
        }

        int amountToBet = callAmount + raiseBy;

        if (!TryAddBetToPot(amountToBet))
        {
            return false;
        }

        Turn.TrySwitch();
        return true;
    }

    public bool TryAllIn()
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        TurnOwner allInOwner = CurrentTurn;
        int allInAmount = GetChips(allInOwner).Count;

        if (allInAmount <= 0 || !CanAddBetToPot(allInAmount))
        {
            return false;
        }

        TurnOwner opponent = GetOpponent(allInOwner);
        bool shouldEndBetting = ShouldEndBetting(allInOwner, allInAmount);

        if (shouldEndBetting &&
            !CanRefundUnmatchedBet(allInOwner, allInAmount))
        {
            return false;
        }

        AddBetToPot(allInAmount);

        if (shouldEndBetting)
        {
            TryRefundUnmatchedBet();
            FinishBetting();
        }
        else
        {
            Turn.TrySwitch();
        }

        return true;
    }

    private bool ShouldEndBetting(TurnOwner owner, int addedBet)
    {
        TurnOwner opponent = GetOpponent(owner);
        int ownerTotalBetAfterAction = Betting.GetTotalBet(owner) + addedBet;
        bool opponentNeedsToRespond = ownerTotalBetAfterAction >
                                      Betting.GetTotalBet(opponent);
        bool opponentCanRespond = GetChips(opponent).Count > 0;

        return !opponentNeedsToRespond || !opponentCanRespond;
    }

    private void FinishBetting()
    {
        Phase = GamePhase.Showdown;
        Turn.Reset();
    }

    private bool CanRefundUnmatchedBet(
        TurnOwner allInOwner,
        int allInAmount)
    {
        int playerTotalBet = Betting.PlayerTotalBet;
        int dealerTotalBet = Betting.DealerTotalBet;

        if (allInOwner == TurnOwner.Player)
        {
            playerTotalBet += allInAmount;
        }
        else
        {
            dealerTotalBet += allInAmount;
        }

        TurnOwner unmatchedBetOwner;
        int unmatchedBet;

        if (playerTotalBet > dealerTotalBet)
        {
            unmatchedBetOwner = TurnOwner.Player;
            unmatchedBet = playerTotalBet - dealerTotalBet;
        }
        else if (dealerTotalBet > playerTotalBet)
        {
            unmatchedBetOwner = TurnOwner.Dealer;
            unmatchedBet = dealerTotalBet - playerTotalBet;
        }
        else
        {
            return true;
        }

        int chipsAfterAllIn = GetChips(unmatchedBetOwner).Count;

        if (unmatchedBetOwner == allInOwner)
        {
            chipsAfterAllIn -= allInAmount;
        }

        int potAfterAllIn = Pot.Amount + allInAmount;

        return unmatchedBet <= potAfterAllIn &&
               unmatchedBet <= int.MaxValue - chipsAfterAllIn;
    }

    private bool TryRefundUnmatchedBet()
    {
        TurnOwner unmatchedBetOwner;
        int unmatchedBet;

        if (Betting.PlayerTotalBet > Betting.DealerTotalBet)
        {
            unmatchedBetOwner = TurnOwner.Player;
            unmatchedBet = Betting.PlayerTotalBet - Betting.DealerTotalBet;
        }
        else if (Betting.DealerTotalBet > Betting.PlayerTotalBet)
        {
            unmatchedBetOwner = TurnOwner.Dealer;
            unmatchedBet = Betting.DealerTotalBet - Betting.PlayerTotalBet;
        }
        else
        {
            return true;
        }

        ChipStack ownerChips = GetChips(unmatchedBetOwner);

        if (unmatchedBet > Pot.Amount ||
            unmatchedBet > int.MaxValue - ownerChips.Count)
        {
            return false;
        }

        Pot.TryRemove(unmatchedBet);
        Betting.TryRemoveFromTotalBet(unmatchedBetOwner, unmatchedBet);
        ownerChips.TryAdd(unmatchedBet);
        return true;
    }

    public bool TryFold()
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        TurnOwner foldedBy = CurrentTurn;
        TurnOwner winner = GetOpponent(foldedBy);
        ChipStack winnerChips = GetChips(winner);
        int potAmount = Pot.Amount;

        if (potAmount > int.MaxValue - winnerChips.Count)
        {
            return false;
        }

        FoldedBy = foldedBy;
        RoundEndReason = global::RoundEndReason.Fold;

        if (potAmount > 0)
        {
            winnerChips.TryAdd(Pot.TakeAll());
        }

        Betting.Reset();
        Phase = GamePhase.RoundEnd;
        Turn.Reset();
        return true;
    }

    private ChipStack GetChips(TurnOwner owner)
    {
        return owner == TurnOwner.Player ? PlayerChips : DealerChips;
    }

    private static TurnOwner GetOpponent(TurnOwner owner)
    {
        return owner == TurnOwner.Player ? TurnOwner.Dealer : TurnOwner.Player;
    }

    private static bool IsActiveTurn(TurnOwner owner)
    {
        return owner == TurnOwner.Player || owner == TurnOwner.Dealer;
    }

    private static bool IsValid(GamePhase phase)
    {
        return phase == GamePhase.Setup ||
               phase == GamePhase.Betting ||
               phase == GamePhase.Showdown ||
               phase == GamePhase.RoundEnd ||
               phase == GamePhase.GameOver;
    }
}
