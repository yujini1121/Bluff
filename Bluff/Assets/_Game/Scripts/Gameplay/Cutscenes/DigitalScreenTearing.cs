using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("Bluff/Cutscenes/Screen Split")]
public sealed class DigitalScreenTearing : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private DefeatCutsceneController owner;
    [SerializeField] private VictoryCutsceneController victoryCutscene;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private AnimationTrack animationTrack;
    public bool effectEnabled = true;
    [Tooltip("Timeline 미리보기 중 연결된 Game 카메라에만 적용합니다.")]
    public bool previewInEditMode = true;
    [Range(0, 1)] public float intensity;
    [Tooltip("세 개의 대각선 균열이 순서대로 자라는 진행도입니다.")]
    [Range(0, 1)] public float crackProgress;
    [Tooltip("렌더링 해상도 기준 검은 균열의 두께입니다. 단위: 픽셀.")]
    [Range(0, 12)] public float crackWidth = 8f;
    [Tooltip("분할 영역이 어긋나는 진행도입니다. Timeline에서 조절합니다.")]
    [Range(0, 1)] public float separationProgress;
    [Tooltip("화면 높이에 대한 영역 이동 거리 비율입니다.")]
    [Range(0, 0.06f)] public float displacement = 0.028f;

    private static readonly List<DigitalScreenTearing> instances = new List<DigitalScreenTearing>();
    private bool captured;

    public void Capture() { ResetEffect(); captured = true; }
    public void Release() { captured = false; ResetEffect(); }
    private void ResetEffect() { intensity = 0; crackProgress = 0; separationProgress = 0; }
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
