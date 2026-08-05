public sealed class Card
{
    public int Rank { get; }

    public Card(int rank)
    {
        Rank = rank;
    }

    public override string ToString()
    {
        return Rank.ToString();
    }
}
