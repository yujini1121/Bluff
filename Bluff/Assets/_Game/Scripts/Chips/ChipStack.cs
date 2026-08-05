public sealed class ChipStack
{
    public int Count { get; private set; }

    public ChipStack(int initialCount = 0)
    {
        Count = initialCount < 0 ? 0 : initialCount;
    }

    public bool TryAdd(int amount)
    {
        if (amount <= 0 || amount > int.MaxValue - Count)
        {
            return false;
        }

        Count += amount;
        return true;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Count)
        {
            return false;
        }

        Count -= amount;
        return true;
    }
}
