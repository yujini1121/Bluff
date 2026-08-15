using System;

public sealed class GameState
{
    public GamePhase Phase { get; private set; }
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

    private bool TryAddBetToPot(int amount)
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        ChipStack chips = GetChips(CurrentTurn);

        if (amount <= 0 ||
            amount > chips.Count ||
            amount > int.MaxValue - Pot.Amount ||
            !Betting.CanAddToTotalBet(CurrentTurn, amount))
        {
            return false;
        }

        chips.TrySpend(amount);
        Pot.TryAdd(amount);
        Betting.TryAddToTotalBet(CurrentTurn, amount);
        return true;
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

        Phase = GamePhase.Showdown;
        Turn.Reset();
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

        int allInAmount = GetChips(CurrentTurn).Count;

        if (!TryAddBetToPot(allInAmount))
        {
            return false;
        }

        TurnOwner opponent = GetOpponent(CurrentTurn);
        bool opponentCanRespond = Betting.GetCallAmount(opponent) > 0 &&
                                  GetChips(opponent).Count > 0;

        if (opponentCanRespond)
        {
            Turn.TrySwitch();
        }
        else
        {
            Phase = GamePhase.Showdown;
            Turn.Reset();
        }

        return true;
    }

    public bool TryFold()
    {
        if (Phase != GamePhase.Betting || !IsActiveTurn(CurrentTurn))
        {
            return false;
        }

        TurnOwner winner = GetOpponent(CurrentTurn);
        ChipStack winnerChips = GetChips(winner);
        int potAmount = Pot.Amount;

        if (potAmount > int.MaxValue - winnerChips.Count)
        {
            return false;
        }

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
