using UnityEngine;

public sealed class CardVisualController : MonoBehaviour
{
    [Header("Community Card")]
    [SerializeField] private CardVisual communityCardVisual1;
    [SerializeField] private CardVisual communityCardVisual2;

    [Header("Dealer Card")]
    [SerializeField] private CardVisual dealerCardVisual;

    private GameState gameState;

    public void Initialize(GameState state)
    {
        gameState = state;
        RefreshCards();
    }

    public void RefreshCards()
    {
        SetCard(communityCardVisual1, gameState?.CommunityCard1);
        SetCard(communityCardVisual2, gameState?.CommunityCard2);
        SetCard(dealerCardVisual, gameState?.DealerCard);
    }

    private static void SetCard(CardVisual cardVisual, Card card)
    {
        if (cardVisual != null)
        {
            cardVisual.SetCard(card);
        }
    }
}
