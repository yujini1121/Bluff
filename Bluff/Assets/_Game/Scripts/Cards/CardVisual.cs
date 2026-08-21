using UnityEngine;

public sealed class CardVisual : MonoBehaviour
{
    [SerializeField, Tooltip("카드 모델의 Renderer입니다.")]
    private Renderer cardRenderer;
    [SerializeField, Tooltip("0번부터 Rank 1, 2, ... 10 순서로 연결합니다.")]
    private Material[] rankMaterials = new Material[10];

    private bool hasAppliedRank;

    public int CurrentRank { get; private set; }

    private void Awake()
    {
        EnsureRenderer();
    }

    private void OnValidate()
    {
        EnsureRenderer();
    }

    public void SetCard(Card card)
    {
        int rank = card == null ? 0 : card.Rank;

        if (hasAppliedRank && rank == CurrentRank)
        {
            return;
        }

        hasAppliedRank = true;
        CurrentRank = rank;

        if (rank == 0)
        {
            SetRendererVisible(false);
            return;
        }

        if (!TryGetRankMaterial(rank, out Material rankMaterial))
        {
            SetRendererVisible(false);
            Debug.LogWarning(
                $"{name}: Rank {rank} 카드 Material이 연결되지 않았습니다.",
                this);
            return;
        }

        if (!EnsureRenderer())
        {
            Debug.LogWarning(
                $"{name}: 카드 Renderer가 연결되지 않았습니다.",
                this);
            return;
        }

        cardRenderer.sharedMaterial = rankMaterial;
        cardRenderer.enabled = true;
    }

    public void Clear()
    {
        SetCard(null);
    }

    private bool TryGetRankMaterial(int rank, out Material rankMaterial)
    {
        int materialIndex = rank - 1;
        bool hasMaterial = rankMaterials != null &&
                           materialIndex >= 0 &&
                           materialIndex < rankMaterials.Length;
        rankMaterial = hasMaterial ? rankMaterials[materialIndex] : null;
        return rankMaterial != null;
    }

    private bool EnsureRenderer()
    {
        if (cardRenderer == null)
        {
            cardRenderer = GetComponentInChildren<Renderer>(true);
        }

        return cardRenderer != null;
    }

    private void SetRendererVisible(bool isVisible)
    {
        if (EnsureRenderer())
        {
            cardRenderer.enabled = isVisible;
        }
    }
}
