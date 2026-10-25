using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class TitleMenuArrow : MonoBehaviour
{
    [SerializeField] private RectTransform defaultTarget;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    private RectTransform arrowRect;
    private RectTransform currentTarget;
    private Vector2 velocity;

    private void Awake()
    {
        arrowRect = GetComponent<RectTransform>();
        GetComponent<Image>().raycastTarget = false;
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        if (currentTarget == null)
        {
            currentTarget = defaultTarget;
        }

        if (currentTarget != null)
        {
            SetLocalPosition(GetTargetPosition());
        }
    }

    public void MoveTo(RectTransform target)
    {
        if (isActiveAndEnabled && target != null)
        {
            currentTarget = target;
        }
    }

    private void LateUpdate()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector2 targetPosition = GetTargetPosition();
        Vector2 position = Vector2.SmoothDamp(
            arrowRect.localPosition,
            targetPosition,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if ((position - targetPosition).sqrMagnitude < 0.0001f)
        {
            position = targetPosition;
            velocity = Vector2.zero;
        }

        SetLocalPosition(position);
    }

    private Vector2 GetTargetPosition()
    {
        Rect buttonRect = currentTarget.rect;
        Vector3 worldPosition = currentTarget.TransformPoint(
            new Vector3(buttonRect.xMin, buttonRect.center.y, 0f));
        Vector3 localPosition = arrowRect.parent.InverseTransformPoint(worldPosition);
        localPosition.x -= arrowRect.rect.xMax * arrowRect.localScale.x;
        return localPosition;
    }

    private void SetLocalPosition(Vector2 position)
    {
        arrowRect.localPosition = new Vector3(position.x, position.y, arrowRect.localPosition.z);
    }

    private void OnDisable()
    {
        velocity = Vector2.zero;
    }
}
