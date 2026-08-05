using System;

public sealed class GameState
{
    public GamePhase Phase { get; private set; }
    public TurnState Turn { get; }
    public TurnOwner CurrentTurn => Turn.Owner;
    public ChipStack PlayerChips { get; }
    public ChipStack DealerChips { get; }
    public Pot Pot { get; }
    public Deck Deck { get; }
    public Card PlayerCard { get; private set; }
    public Card DealerCard { get; private set; }

    public GameState(int playerStartingChips, int dealerStartingChips, Deck deck)
    {
        Deck = deck ?? throw new ArgumentNullException(nameof(deck));
        PlayerChips = new ChipStack(playerStartingChips);
        DealerChips = new ChipStack(dealerStartingChips);
        Pot = new Pot();
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

    public void ClearCards()
    {
        PlayerCard = null;
        DealerCard = null;
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
