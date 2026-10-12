using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(-100)]
public sealed class IntroBarSequenceCamera : MonoBehaviour
{
    [SerializeField] private Transform authoredPath;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField, Min(0.01f)] private float entryBlendSeconds = 1.2f;
    [SerializeField] private Transform dealerLookTarget;
    [SerializeField] private Vector2 dealerFocusWindow = new Vector2(4.1f, 5.15f);
    [SerializeField] private Vector2 lensSettleWindow = new Vector2(5.2f, 10.65f);
    [SerializeField, Range(30f, 90f)] private float arrivalFieldOfView = 56f;
    [SerializeField] private Vector2 seatRiseWindow = new Vector2(9.35f, 11.05f);
    [SerializeField, Min(0f)] private float seatRiseHeight;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private float entryFieldOfView;
    private bool running;
    public CinemachineVirtualCamera VirtualCamera => virtualCamera;
    public Vector3 AuthoredPosition => authoredPath.position;
    public float CurrentHeightOffset { get; private set; }

    public void CaptureEntry(Camera output)
    {
        // 우선순위를 바꾸기 전에 실제 화면의 시점을 보존한다.
        entryPosition = output.transform.position;
        CurrentHeightOffset = 0f;
        entryRotation = output.transform.rotation;
        entryFieldOfView = output.fieldOfView;
        virtualCamera.m_Lens = LensSettings.FromCamera(output);
        virtualCamera.transform.SetPositionAndRotation(entryPosition, entryRotation);
        virtualCamera.ForceCameraPosition(entryPosition, entryRotation);
        virtualCamera.Priority = 20;
        running = true;
    }

    private void LateUpdate() => ApplyPose();

    public void ApplyPose()
    {
        if (!running) return;
        float time = (float)director.time;
        float blend = Ease(time / Mathf.Max(0.01f, entryBlendSeconds));
        Vector3 position = Vector3.Lerp(entryPosition, authoredPath.position, blend);
        CurrentHeightOffset = seatRiseHeight * Ease(Mathf.InverseLerp(seatRiseWindow.x, seatRiseWindow.y, time));
        position += Vector3.up * CurrentHeightOffset;
        Quaternion rotation = authoredPath.rotation;
        // 접근 중에는 위치와 회전 키의 보간 차이로 시선이 얼굴에서 벗어나지 않게 한다.
        if (dealerLookTarget != null && (dealerLookTarget.position - position).sqrMagnitude > 0.001f)
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(dealerLookTarget.position - position),
                Ease(Mathf.InverseLerp(dealerFocusWindow.x, dealerFocusWindow.y, time)));
        virtualCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(entryRotation, rotation, blend));
        virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(entryFieldOfView, arrivalFieldOfView,
            Ease(Mathf.InverseLerp(lensSettleWindow.x, lensSettleWindow.y, time)));
    }

    public void Release()
    {
        running = false;
        if (virtualCamera != null) virtualCamera.Priority = 0;
    }

    private void OnDisable() => Release();

    // 양 끝의 속도와 가속도를 0으로 맞춘다.
    private static float Ease(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
