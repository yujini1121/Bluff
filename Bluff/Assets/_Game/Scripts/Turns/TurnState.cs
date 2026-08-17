public sealed class TurnState
{
    public TurnOwner Owner { get; private set; } = TurnOwner.None;

    public bool TrySet(TurnOwner owner)
    {
        if (!IsValid(owner))
        {
            return false;
        }

        Owner = owner;
        return true;
    }

    public bool TrySwitch()
    {
        switch (Owner)
        {
            case TurnOwner.Player:
                Owner = TurnOwner.Dealer;
                return true;
            case TurnOwner.Dealer:
                Owner = TurnOwner.Player;
                return true;
            default:
                return false;
        }
    }

    public void Reset()
    {
        Owner = TurnOwner.None;
    }

    private static bool IsValid(TurnOwner owner)
    {
        return owner == TurnOwner.None ||
               owner == TurnOwner.Player ||
               owner == TurnOwner.Dealer;
    }
}
