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
    [Tooltip("Preview only the bound Game camera while Timeline animation preview is active.")]
    public bool previewInEditMode = true;
    [Range(0, 1)] public float intensity;
    [Tooltip("Fraction of screen width. Positive moves the image right.")]
    [Range(-0.1f, 0.1f)] public float horizontalOffset = 0.025f;
    [Range(0.005f, 0.25f)] public float bandHeight = 0.055f;
    [Range(0, 1)] public float bandCenter = 0.58f;
    [Range(0, 1)] public float secondBandCenter = 0.44f;
    [Range(0, 1)] public float secondBandStrength;
    [Tooltip("RGB sampling offset as a fraction of screen width, multiplied by intensity.")]
    [Range(0, 0.01f)] public float rgbSeparation = 0.0015f;

    private static readonly List<DigitalScreenTearing> instances = new List<DigitalScreenTearing>();
    private bool captured;

    public void Capture() { intensity = 0; secondBandStrength = 0; captured = true; }
    public void Release() { captured = false; intensity = 0; secondBandStrength = 0; }
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
        if (!isActiveAndEnabled || !effectEnabled || intensity <= 0 ||
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
                !owner.IsWaitingForDialogue && director.state == PlayState.Playing;
#if UNITY_EDITOR
        return previewInEditMode && UnityEditor.AnimationMode.InAnimationMode();
#else
        return false;
#endif
    }
}
