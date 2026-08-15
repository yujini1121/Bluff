public sealed class Pot
{
    public int Amount { get; private set; }

    public bool TryAdd(int amount)
    {
        if (amount <= 0 || amount > int.MaxValue - Amount)
        {
            return false;
        }

        Amount += amount;
        return true;
    }

    internal bool TryRemove(int amount)
    {
        if (amount <= 0 || amount > Amount)
        {
            return false;
        }

        Amount -= amount;
        return true;
    }

    public int TakeAll()
    {
        int amount = Amount;
        Amount = 0;
        return amount;
    }

    public void Reset()
    {
        Amount = 0;
    }
}
