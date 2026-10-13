using System.Collections.Generic;
using UnityEngine;

// An animation-driven adapter: no clock, no scheduled effects, no shared mixer edits.
// The Timeline keys amount; only filters borrowed for this session are touched.
public sealed class DefeatAudioMuffle : MonoBehaviour
{
    [Range(0, 1)] public float amount;
    [SerializeField, Range(10, 22000)] private float muffledCutoff = 850;
    private struct State
    {
        public AudioLowPassFilter Filter;
        public bool Owned, Enabled;
        public float Cutoff;
    }
    private readonly List<State> states = new List<State>();

    public void Capture(Transform excludedRoot)
    {
        Release();
        foreach (var source in FindObjectsOfType<AudioSource>())
        {
            if (source.transform.IsChildOf(excludedRoot) || !source.isPlaying) continue;
            var filter = source.GetComponent<AudioLowPassFilter>();
            // Multiple AudioSources can share one GameObject/filter.
            if (filter != null && states.Exists(s => s.Filter == filter)) continue;
            bool owned = filter == null;
            if (owned) filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            states.Add(new State { Filter = filter, Owned = owned, Enabled = filter.enabled, Cutoff = filter.cutoffFrequency });
        }
        Apply();
    }
    private void LateUpdate() => Apply();
    private void OnDidApplyAnimationProperties() => Apply();
    private void Apply()
    {
        foreach (var state in states)
        {
            if (state.Filter == null) continue;
            state.Filter.enabled = amount > 0.001f || (!state.Owned && state.Enabled);
            float normal = !state.Owned && state.Enabled ? state.Cutoff : 22000;
            state.Filter.cutoffFrequency = Mathf.Lerp(normal, Mathf.Min(normal, muffledCutoff), Mathf.Clamp01(amount));
        }
    }
    public void Release()
    {
        foreach (var state in states)
        {
            if (state.Filter == null) continue;
            state.Filter.enabled = state.Owned ? false : state.Enabled;
            state.Filter.cutoffFrequency = state.Cutoff;
            if (state.Owned) Destroy(state.Filter);
        }
        states.Clear();
        amount = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
