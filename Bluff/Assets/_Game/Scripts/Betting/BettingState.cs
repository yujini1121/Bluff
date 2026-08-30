public sealed class BettingState
{
    public int PlayerTotalBet { get; private set; }
    public int DealerTotalBet { get; private set; }

    public int GetTotalBet(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return PlayerTotalBet;
            case TurnOwner.Dealer:
                return DealerTotalBet;
            default:
                return 0;
        }
    }

    public int GetCallAmount(TurnOwner owner)
    {
        switch (owner)
        {
            case TurnOwner.Player:
                return DealerTotalBet > PlayerTotalBet
                    ? DealerTotalBet - PlayerTotalBet
                    : 0;
            case TurnOwner.Dealer:
                return PlayerTotalBet > DealerTotalBet
                    ? PlayerTotalBet - DealerTotalBet
                    : 0;
            default:
                return 0;
        }
    }

    public void Reset()
    {
        PlayerTotalBet = 0;
        DealerTotalBet = 0;
    }

    internal bool CanAddToTotalBet(TurnOwner owner, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        switch (owner)
        {
            case TurnOwner.Player:
                return amount <= int.MaxValue - PlayerTotalBet;
            case TurnOwner.Dealer:
                return amount <= int.MaxValue - DealerTotalBet;
            default:
                return false;
        }
    }

    internal bool TryAddToTotalBet(TurnOwner owner, int amount)
    {
        if (!CanAddToTotalBet(owner, amount))
        {
            return false;
        }

        if (owner == TurnOwner.Player)
        {
            PlayerTotalBet += amount;
        }
        else
        {
            DealerTotalBet += amount;
        }

        return true;
    }

    internal bool TryRemoveFromTotalBet(TurnOwner owner, int amount)
    {
        if (amount <= 0 || amount > GetTotalBet(owner))
        {
            return false;
        }

        if (owner == TurnOwner.Player)
        {
            PlayerTotalBet -= amount;
        }
        else if (owner == TurnOwner.Dealer)
        {
            DealerTotalBet -= amount;
        }
        else
        {
            return false;
        }

        return true;
    }
}
