using System;
using System.Collections.Generic;

public sealed class Deck
{
    private readonly List<Card> cards;
    private readonly Random random;

    public int RemainingCount => cards.Count;

    public Deck(IEnumerable<Card> cards)
        : this(cards, new Random())
    {
    }

    public Deck(IEnumerable<Card> cards, Random random)
    {
        if (cards == null)
        {
            throw new ArgumentNullException(nameof(cards));
        }

        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.cards = new List<Card>();

        foreach (Card card in cards)
        {
            if (card == null)
            {
                throw new ArgumentException("The deck cannot contain a null card.", nameof(cards));
            }

            this.cards.Add(card);
        }
    }

    public void Shuffle()
    {
        for (int index = cards.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            Card card = cards[index];
            cards[index] = cards[swapIndex];
            cards[swapIndex] = card;
        }
    }

    public bool TryDraw(out Card card)
    {
        if (cards.Count == 0)
        {
            card = null;
            return false;
        }

        int lastIndex = cards.Count - 1;
        card = cards[lastIndex];
        cards.RemoveAt(lastIndex);
        return true;
    }
}
