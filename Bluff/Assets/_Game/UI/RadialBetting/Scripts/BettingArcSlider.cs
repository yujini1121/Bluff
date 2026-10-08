using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BettingArcSlider : Selectable, IInitializePotentialDragHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, ICanvasRaycastFilter
{
    [SerializeField] private BettingArcGraphic fill;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float radius = 302;
    [SerializeField] private Image radialFill;
    [SerializeField] private float startAngle = 174, endAngle = 96;
    [SerializeField] private bool limitRaycastToArc;
    [SerializeField, Min(1)] private float hitWidth = 40;
    private int minimum = 1;
    private int maximum = 1;
    private int value = 1;
    private bool dragging;
    public event Action<int> ValueChanged;
    public int Value => value;

    public static int ValueAtPoint(Vector2 point, int min, int max,
        float start = 174, float end = 96)
    {
        if (max <= min) return min;
        // Clamping coordinates keeps drags beyond either endpoint at that endpoint.
        float angle = Mathf.Atan2(Mathf.Max(0, point.y), Mathf.Min(0, point.x)) * Mathf.Rad2Deg;
        double t = Mathf.Approximately(start, end) ? 0 : Mathf.Clamp01((start - angle) / (start - end));
        return (int)Math.Round(min + ((double)max - min) * t, MidpointRounding.AwayFromZero);
    }

    public void SetRangeAndValue(int min, int max, int selected)
    {
        bool rangeChanged = minimum != min || maximum != Mathf.Max(min, max);
        minimum = min;
        maximum = Mathf.Max(min, max);
        int next = Mathf.Clamp(selected, minimum, maximum);
        if (value == next && initialized && !rangeChanged) return;
        value = next;
        UpdateVisual();
    }

    private bool initialized;
    private void UpdateVisual()
    {
        initialized = true;
        float t = maximum == minimum ? 0 : (float)(((double)value - minimum) / ((double)maximum - minimum));
        float angle = Mathf.Lerp(startAngle, endAngle, t);
        if (fill != null) fill.Configure(radius + 2, 4, angle, startAngle);
        // A full-circle Shift sprite: clockwise from Left, only its top-left quarter fills.
        if (radialFill != null) radialFill.fillAmount = t * Mathf.Abs(startAngle - endAngle) / 360f;
        if (handle != null) handle.anchoredPosition = Point(angle, radius);
    }

    private static Vector2 Point(float angle, float distance) =>
        new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * distance;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!limitRaycastToArc) return true;
        if (!CanInput() || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, screenPoint, eventCamera, out Vector2 point)) return false;
        return point.x <= 0 && point.y >= 0 && Mathf.Abs(point.magnitude - radius) <= hitWidth * .5f;
    }

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
    public override void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !CanInput()) return;
        base.OnPointerDown(e);
        dragging = true;
        SelectAt(e);
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && CanInput()) dragging = true;
    }
    public void OnDrag(PointerEventData e)
    {
        if (dragging && CanInput()) SelectAt(e);
    }
    public void OnEndDrag(PointerEventData e) => dragging = false;
    public override void OnPointerUp(PointerEventData e)
    {
        dragging = false;
        base.OnPointerUp(e);
    }
    protected override void OnDisable()
    {
        dragging = false;
        base.OnDisable();
    }
    public void OnScroll(PointerEventData e)
    {
        if (!CanInput() || Mathf.Approximately(e.scrollDelta.y, 0)) return;
        Change((int)Math.Max(minimum, Math.Min(maximum,
            (long)value + (e.scrollDelta.y > 0 ? 1 : -1))));
        e.Use();
    }
    public override void OnMove(AxisEventData e)
    {
        if (!CanInput()) return;
        int step = e.moveDir == MoveDirection.Up || e.moveDir == MoveDirection.Right ? 1 : -1;
        Change((int)Math.Max(minimum, Math.Min(maximum, (long)value + step)));
        e.Use();
    }
    private bool CanInput() => IsActive() && IsInteractable() && Time.timeScale > 0;
    private void SelectAt(PointerEventData e)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, e.position, e.pressEventCamera, out Vector2 point) &&
            point.sqrMagnitude > 16) Change(ValueAtPoint(point, minimum, maximum, startAngle, endAngle));
    }
    private void Change(int next)
    {
        if (next == value) return;
        value = next;
        // Position is synchronous, never tweened away from the actual selected integer.
        UpdateVisual();
        ValueChanged?.Invoke(value);
    }
}
