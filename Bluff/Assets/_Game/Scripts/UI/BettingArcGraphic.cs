using UnityEngine;
using UnityEngine.UI;

// A real annular hit area: transparent space never intercepts table/item input.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BettingArcGraphic : MaskableGraphic
{
    [SerializeField] private float radius = 302;
    [SerializeField] private float thickness = 4;
    [SerializeField] private float startAngle = 96;
    [SerializeField] private float endAngle = 174;

    public void Configure(float outerRadius, float width, float start, float end)
    {
        radius = outerRadius;
        thickness = width;
        startAngle = start;
        endAngle = end;
        SetVerticesDirty();
    }

    public static Vector2 Point(float angle, float distance)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(endAngle - startAngle) / 2));
        float inner = Mathf.Max(0, radius - thickness);
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, (float)i / segments);
            vh.AddVert(Point(angle, inner), color, Vector2.zero);
            vh.AddVert(Point(angle, radius), color, Vector2.one);
            if (i == 0) continue;
            int v = i * 2;
            vh.AddTriangle(v - 2, v - 1, v);
            vh.AddTriangle(v, v - 1, v + 1);
        }
    }

    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (!base.Raycast(screenPoint, eventCamera) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 p)) return false;
        float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        return p.magnitude >= Mathf.Max(0, radius - thickness) &&
               p.magnitude <= radius && angle >= Mathf.Min(startAngle, endAngle) &&
               angle <= Mathf.Max(startAngle, endAngle);
    }
}
