public sealed class BettingState
{
    public int PlayerBet { get; private set; }
    public int DealerBet { get; private set; }

    public int GetBet(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return PlayerBet;
            case TurnOwner.Dealer:
                return DealerBet;
            default:
                return 0;
        }
    }

    public int GetCallAmount(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return DealerBet > PlayerBet
                    ? DealerBet - PlayerBet
                    : 0;
            case TurnOwner.Dealer:
                return PlayerBet > DealerBet
                    ? PlayerBet - DealerBet
                    : 0;
            default:
                return 0;
        }
    }

    public void Reset()
    {
        PlayerBet = 0;
        DealerBet = 0;
    }

    internal bool CanAddBet(TurnOwner owner, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        switch (owner)
        {
            case TurnOwner.Player:
                return amount <= int.MaxValue - PlayerBet;
            case TurnOwner.Dealer:
                return amount <= int.MaxValue - DealerBet;
            default:
                return false;
        }
    }

    internal bool TryAddBet(TurnOwner owner, int amount)
    {
        if (!CanAddBet(owner, amount))
        {
            return false;
        }

        if (owner == TurnOwner.Player)
        {
            PlayerBet += amount;
        }
        else
        {
            DealerBet += amount;
        }

        return true;
    }
}
