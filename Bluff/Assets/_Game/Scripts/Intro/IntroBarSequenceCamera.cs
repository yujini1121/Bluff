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
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private bool running;
    public CinemachineVirtualCamera VirtualCamera => virtualCamera;
    public Vector3 AuthoredPosition => authoredPath.position;

    public void CaptureEntry(Camera output)
    {
        entryPosition = authoredPath.position;
        entryRotation = output.transform.rotation;
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
        float blend = Mathf.SmoothStep(0f, 1f, (float)director.time / entryBlendSeconds);
        virtualCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(entryPosition, authoredPath.position, blend),
            Quaternion.Slerp(entryRotation, authoredPath.rotation, blend));
    }

    public void Release()
    {
        running = false;
        virtualCamera.Priority = 0;
    }
}
