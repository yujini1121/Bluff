using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
[ExecuteAlways, DisallowMultipleComponent]
public sealed class DigitalScreenTearing : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private DefeatCutsceneController owner;
    [SerializeField] private VictoryCutsceneController victoryCutscene;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private AnimationTrack animationTrack;
    public bool effectEnabled = true;
    public bool previewInEditMode = true;
    [Range(0, 1)] public float intensity;
    [Range(-0.1f, 0.1f)] public float horizontalOffset = 0.025f;
    [HideInInspector] public float bandHeight = 0.055f;
    [HideInInspector] public float bandCenter = 0.58f;
    [HideInInspector] public float secondBandCenter = 0.44f;
    [HideInInspector] public float secondBandStrength;
    [Range(0, 0.01f)] public float rgbSeparation = 0.0015f;

    [Header("Glitch Spread")]
    [Range(0, 1)] public float glitchDensity;
    [Range(1, 30)] public int maxGlitches = 30;
    [Range(0, 1)] public float glitchCoverage;
    [Range(0, 4)] public float glitchDisplacement = 1;
    public float glitchPatternPhase;
    [Range(0.1f, 3)] public float glitchPatternSpeed = 1;
    [Range(0, 1)] public float glitchSizeVariation = 0.8f;

    private static readonly List<DigitalScreenTearing> instances = new List<DigitalScreenTearing>();
    private bool captured;

    public void Capture() { ResetEffect(); captured = true; }
    public void Release() { captured = false; ResetEffect(); }
    private void ResetEffect()
    {
        intensity = 0;
        secondBandStrength = 0;
        glitchDensity = glitchCoverage = glitchPatternPhase = 0;
    }
    private void OnEnable() { if (!instances.Contains(this)) instances.Add(this); }
    private void OnDisable() { Release(); instances.Remove(this); }
    private void OnDestroy() { Release(); instances.Remove(this); }

    public static DigitalScreenTearing ForCamera(Camera camera)
    {
        if (camera == null || camera.cameraType != CameraType.Game) return null;
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var effect = instances[i];
            if (effect != null && effect.targetCamera == camera && effect.CanRender()) return effect;
        }
        return null;
    }

    private bool CanRender()
    {
        if (!isActiveAndEnabled || !effectEnabled || intensity <= 0 || glitchDensity <= 0 || glitchCoverage <= 0 ||
            (victoryCutscene != null && victoryCutscene.IsPlaying) ||
            director == null || !director.isActiveAndEnabled || !director.playableGraph.IsValid() ||
            animationTrack == null || animationTrack.mutedInHierarchy ||
            director.GetGenericBinding(animationTrack) != GetComponent<Animator>() ||
            animationTrack.timelineAsset != director.playableAsset) return false;
        bool insideClip = false;
        foreach (var clip in animationTrack.GetClips())
            if (director.time >= clip.start && director.time < clip.end) { insideClip = true; break; }
        if (!insideClip) return false;
        if (Application.isPlaying)
            return captured && owner != null && owner.isActiveAndEnabled && owner.IsPlaying &&
                !owner.IsWaitingForDialogue;
#if UNITY_EDITOR
        return previewInEditMode && UnityEditor.AnimationMode.InAnimationMode();
#else
        return false;
#endif
    }
}
