using System;

public sealed class Card
{
    public int Rank { get; }

    public Card(int rank)
    {
        if (rank < 1 || rank > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "카드 숫자는 1부터 10까지여야 합니다.");
        }

        Rank = rank;
    }

    public override string ToString()
    {
        return Rank.ToString();
    }
}
