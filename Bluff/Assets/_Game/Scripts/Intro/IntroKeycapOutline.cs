using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class IntroKeycapOutline : MaskableGraphic
{
    [SerializeField, Min(0.5f)] private float thickness = 1.5f;
    [SerializeField] private Color outlineColor = new Color(0.68f, 0.74f, 0.82f, 0.65f);
    [SerializeField] private Color progressColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float progress;
    public float Progress
    {
        get => progress;
        set { float next = Mathf.Clamp01(value); if (Mathf.Approximately(progress, next)) return; progress = next; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float half = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.5f) * 0.5f;
        Vector2[] corners = {
            new Vector2(r.xMin + half, r.yMax - half), new Vector2(r.xMax - half, r.yMax - half),
            new Vector2(r.xMax - half, r.yMin + half), new Vector2(r.xMin + half, r.yMin + half)
        };
        float perimeter = 2f * (r.width + r.height - 4f * half);
        float remaining = progress * perimeter;
        for (int i = 0; i < 4; i++) AddEdge(vh, corners[i], corners[(i + 1) % 4], half, outlineColor * color);
        for (int i = 0; i < 4 && remaining > 0f; i++)
        {
            Vector2 start = corners[i];
            Vector2 end = corners[(i + 1) % 4];
            float length = Vector2.Distance(start, end);
            if (length <= 0f) continue;
            AddEdge(vh, start, Vector2.Lerp(start, end, Mathf.Min(remaining / length, 1f)), half, progressColor * color);
            remaining -= length;
        }
    }

    private static void AddEdge(VertexHelper vh, Vector2 a, Vector2 b, float half, Color tint)
    {
        Vector2 delta = (b - a).normalized;
        Vector2 normal = new Vector2(-delta.y, delta.x) * half;
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero);
        vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero);
        vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
