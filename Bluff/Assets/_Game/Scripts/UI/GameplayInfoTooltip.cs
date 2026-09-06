using TMPro;
using UnityEngine;

public sealed class GameplayInfoTooltip : MonoBehaviour
{
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private TMP_Text infoText;

    private RectTransform tooltipRect;
    private Canvas canvas;

    private void Awake()
    {
        CacheUiReferences();
        Hide();
    }

    public void Show(string text)
    {
        if (tooltipRoot == null || infoText == null)
        {
            return;
        }

        infoText.text = text;
        tooltipRoot.SetActive(true);
    }

    public void SetWorldPosition(Vector3 worldPosition, Camera worldCamera)
    {
        if (worldCamera == null || !CacheUiReferences())
        {
            return;
        }

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(worldPosition);
        RectTransform parentRect = tooltipRect.parent as RectTransform;

        if (parentRect == null)
        {
            tooltipRect.position = screenPosition;
            return;
        }

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null
                ? canvas.worldCamera
                : worldCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out Vector3 uiWorldPosition))
        {
            tooltipRect.position = uiWorldPosition;
        }
    }

    public void Hide()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }
    }

    private bool CacheUiReferences()
    {
        if (tooltipRoot == null)
        {
            return false;
        }

        tooltipRect ??= tooltipRoot.transform as RectTransform;
        canvas ??= tooltipRoot.GetComponentInParent<Canvas>(true);
        return tooltipRect != null && canvas != null;
    }
}
