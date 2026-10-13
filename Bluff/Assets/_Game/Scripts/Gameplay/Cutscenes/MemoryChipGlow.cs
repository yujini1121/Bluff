using UnityEngine;

// Animation keys intensity; the shared material is never modified.
[ExecuteAlways]
public sealed class MemoryChipGlow : MonoBehaviour
{
    [SerializeField] private Renderer target;
    [Min(0f)] public float intensity = 0.25f;
    private MaterialPropertyBlock properties;

    private void Apply()
    {
        if (target == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        target.GetPropertyBlock(properties);
        properties.SetColor("_EmissionColor", new Color(0.1f, 0.85f, 1f) * Mathf.Max(0f, intensity));
        target.SetPropertyBlock(properties);
    }

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();
    private void OnDidApplyAnimationProperties() => Apply();
}
