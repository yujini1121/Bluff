using UnityEngine;

public sealed class WorldToScreenUIFollower : MonoBehaviour
{
    [SerializeField] private Transform worldTarget;
    [SerializeField] private RectTransform uiTarget;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera targetCamera;

    private void LateUpdate()
    {
        if (worldTarget == null ||
            uiTarget == null ||
            canvas == null ||
            targetCamera == null)
        {
            return;
        }

        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(worldTarget.position);
        RectTransform parentRect = uiTarget.parent as RectTransform;

        if (parentRect == null)
        {
            uiTarget.position = screenPosition;
            return;
        }

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null
                ? canvas.worldCamera
                : targetCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out Vector3 uiWorldPosition))
        {
            uiTarget.position = uiWorldPosition;
        }
    }
}
