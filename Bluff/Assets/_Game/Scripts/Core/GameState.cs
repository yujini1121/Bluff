using System;

public sealed class GameState
{
    private const int AnteAmount = 1;
    private const int TotalAnteAmount = AnteAmount * 2;

    public GamePhase Phase { get; private set; }
    public RoundEndReason RoundEndReason { get; private set; }
    public TurnOwner FoldedBy { get; private set; }
    public GameWinner FinalWinner { get; private set; }
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
        FinalWinner = GameWinner.None;
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

    public bool TryStartRound(TurnOwner firstTurn)
    {
        if (Phase != GamePhase.Setup ||
            !IsActiveTurn(firstTurn) ||
            PlayerCard != null ||
            DealerCard != null ||
            CommunityCard1 != null ||
            CommunityCard2 != null ||
            Deck.RemainingCount < 4 ||
            PlayerChips.Count < AnteAmount ||
            DealerChips.Count < AnteAmount ||
            Pot.Amount > int.MaxValue - TotalAnteAmount ||
            !Betting.CanAddToTotalBet(TurnOwner.Player, AnteAmount) ||
            !Betting.CanAddToTotalBet(TurnOwner.Dealer, AnteAmount))
        {
            return false;
        }

        Deck.TryDraw(out Card playerCard);
        Deck.TryDraw(out Card dealerCard);
        Deck.TryDraw(out Card communityCard1);
        Deck.TryDraw(out Card communityCard2);

        PlayerCard = playerCard;
        DealerCard = dealerCard;
        CommunityCard1 = communityCard1;
        CommunityCard2 = communityCard2;
        Betting.Reset();
        PlayerChips.TrySpend(AnteAmount);
        DealerChips.TrySpend(AnteAmount);
        Pot.TryAdd(TotalAnteAmount);
        Betting.TryAddToTotalBet(TurnOwner.Player, AnteAmount);
        Betting.TryAddToTotalBet(TurnOwner.Dealer, AnteAmount);
        ResetRoundResult();
        Turn.TrySet(firstTurn);
        Phase = GamePhase.Betting;
        return true;
    }

    public bool TryPrepareNextRound()
    {
        if (Phase != GamePhase.RoundEnd)
        {
            return false;
        }

        Betting.Reset();
        ClearCards();
        ResetRoundResult();
        Turn.Reset();
        Phase = GamePhase.Setup;
        return true;
    }

    public bool TryGetHandRank(TurnOwner owner, out HandRank handRank)
    {
        handRank = HandRank.None;

        if (Phase != GamePhase.Showdown ||
            CommunityCard1 == null ||
            CommunityCard2 == null)
        {
            return false;
        }

        Card privateCard;

        if (owner == TurnOwner.Player)
        {
            privateCard = PlayerCard;
        }
        else if (owner == TurnOwner.Dealer)
        {
            privateCard = DealerCard;
        }
        else
        {
            return false;
        }

        if (privateCard == null)
        {
            return false;
        }

        handRank = GetHandRank(privateCard);
        return true;
    }

    public bool TryDetermineWinner(out RoundWinner winner)
    {
        winner = RoundWinner.None;

        if (!TryGetHandRank(TurnOwner.Player, out HandRank playerHandRank) ||
            !TryGetHandRank(TurnOwner.Dealer, out HandRank dealerHandRank))
        {
            return false;
        }

        if (playerHandRank != dealerHandRank)
        {
            winner = (int)playerHandRank > (int)dealerHandRank
                ? RoundWinner.Player
                : RoundWinner.Dealer;
        }
        else if (PlayerCard.Rank > DealerCard.Rank)
        {
            winner = RoundWinner.Player;
        }
        else if (DealerCard.Rank > PlayerCard.Rank)
        {
            winner = RoundWinner.Dealer;
        }
        else
        {
            winner = RoundWinner.Draw;
        }

        return true;
    }

    public bool TrySettleShowdown(out RoundWinner winner)
    {
        winner = RoundWinner.None;

        if (!TryDetermineWinner(out RoundWinner determinedWinner))
        {
            return false;
        }

        ChipStack winnerChips = null;

        if (determinedWinner == RoundWinner.Player)
        {
            winnerChips = PlayerChips;
        }
        else if (determinedWinner == RoundWinner.Dealer)
        {
            winnerChips = DealerChips;
        }

        int potAmount = Pot.Amount;

        if (winnerChips != null &&
            potAmount > int.MaxValue - winnerChips.Count)
        {
            return false;
        }

        if (winnerChips != null && potAmount > 0)
        {
            winnerChips.TryAdd(Pot.TakeAll());
        }

        Betting.Reset();
        FoldedBy = TurnOwner.None;
        RoundEndReason = global::RoundEndReason.Showdown;
        Phase = GamePhase.RoundEnd;
        Turn.Reset();
        winner = determinedWinner;
        EndGameIfNeeded();
        return true;
    }

    private void EndGameIfNeeded()
    {
        if (Pot.Amount > 0)
        {
            return;
        }

        if (PlayerChips.Count == 0 && DealerChips.Count > 0)
        {
            FinalWinner = GameWinner.Dealer;
            Phase = GamePhase.GameOver;
        }
        else if (DealerChips.Count == 0 && PlayerChips.Count > 0)
        {
            FinalWinner = GameWinner.Player;
            Phase = GamePhase.GameOver;
        }
    }

    private HandRank GetHandRank(Card privateCard)
    {
        int privateRank = privateCard.Rank;
        int communityRank1 = CommunityCard1.Rank;
        int communityRank2 = CommunityCard2.Rank;

        if (privateRank == communityRank1 &&
            communityRank1 == communityRank2)
        {
            return HandRank.Triple;
        }

        if (IsStraight(privateRank, communityRank1, communityRank2))
        {
            return HandRank.Straight;
        }

        if (privateRank == communityRank1 ||
            privateRank == communityRank2 ||
            communityRank1 == communityRank2)
        {
            return HandRank.Double;
        }

        return HandRank.Number;
    }

    private static bool IsStraight(int rank1, int rank2, int rank3)
    {
        if (rank1 == rank2 || rank1 == rank3 || rank2 == rank3)
        {
            return false;
        }

        int lowestRank = Math.Min(rank1, Math.Min(rank2, rank3));
        int highestRank = Math.Max(rank1, Math.Max(rank2, rank3));

        if (highestRank - lowestRank == 2)
        {
            return true;
        }

        bool hasOne = rank1 == 1 || rank2 == 1 || rank3 == 1;
        bool hasTen = rank1 == 10 || rank2 == 10 || rank3 == 10;
        bool hasTwoOrNine = rank1 == 2 || rank2 == 2 || rank3 == 2 ||
                            rank1 == 9 || rank2 == 9 || rank3 == 9;

        return hasOne && hasTen && hasTwoOrNine;
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
        EndGameIfNeeded();
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
