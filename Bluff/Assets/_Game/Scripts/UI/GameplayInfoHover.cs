using UnityEngine;

public enum GameplayInfoHoverTarget
{
    PlayerChips,
    DealerChips,
    Deck
}

public sealed class GameplayInfoHover : MonoBehaviour
{
    private const float TooltipAnchorGizmoRadius = 0.05f;

    [SerializeField] private GameplayInfoHoverTarget target;
    [SerializeField] private GameplayInfoTooltip tooltip;
    [SerializeField] private IndianHoldemDebugUI gameUi;
    [SerializeField] private Transform tooltipAnchor;

    private bool isHovered;
    private Camera worldCamera;

    private void OnMouseEnter()
    {
        if (tooltip == null || gameUi == null)
        {
            return;
        }

        isHovered = true;
        worldCamera = Camera.main;
        tooltip.Show(BuildInfoText());
        UpdateTooltipPosition();
    }

    private void OnMouseExit()
    {
        StopHover();
    }

    private void LateUpdate()
    {
        if (isHovered)
        {
            UpdateTooltipPosition();
        }
    }

    private void OnDisable()
    {
        StopHover();
    }

    private void OnDrawGizmosSelected()
    {
        if (tooltipAnchor == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, tooltipAnchor.position);
        Gizmos.DrawWireSphere(
            tooltipAnchor.position,
            TooltipAnchorGizmoRadius);
    }

    private void UpdateTooltipPosition()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        Transform anchor = tooltipAnchor != null
            ? tooltipAnchor
            : transform;
        tooltip?.SetWorldPosition(anchor.position, worldCamera);
    }

    private void StopHover()
    {
        isHovered = false;
        worldCamera = null;
        tooltip?.Hide();
    }

    private string BuildInfoText()
    {
        switch (target)
        {
            case GameplayInfoHoverTarget.PlayerChips:
                return gameUi.CurrentPlayerChipCount.ToString();
            case GameplayInfoHoverTarget.DealerChips:
                return gameUi.CurrentDealerChipCount.ToString();
            case GameplayInfoHoverTarget.Deck:
                return gameUi.CurrentDeckRemainingCount.ToString();
            default:
                return string.Empty;
        }
    }
}
