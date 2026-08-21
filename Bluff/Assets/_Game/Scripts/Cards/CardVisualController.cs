using UnityEngine;

public sealed class CardVisualController : MonoBehaviour
{
    [Header("커뮤니티 카드 Visual")]
    [SerializeField] private CardVisual communityCardVisual1;
    [SerializeField] private CardVisual communityCardVisual2;

    [Header("딜러 카드 Visual (선택)")]
    [SerializeField]
    [Tooltip("향후 딜러의 Head/Forehead Transform 아래에 배치한 CardVisual을 연결합니다.")]
    private CardVisual dealerCardVisual;

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
