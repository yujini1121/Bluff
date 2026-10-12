using Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class CutsceneCameraRig : MonoBehaviour
{
    [SerializeField] private Camera outputCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform authoredPath;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private LensSettings previousLens;
    private int previousPriority;
    private bool previousEnabled;
    private bool captured;

    public bool IsConfigured => outputCamera != null && virtualCamera != null && authoredPath != null &&
        outputCamera.GetComponent<CinemachineBrain>() != null;

    public void Capture()
    {
        entryPosition = outputCamera.transform.position;
        entryRotation = outputCamera.transform.rotation;
        previousLens = virtualCamera.m_Lens;
        previousPriority = virtualCamera.Priority;
        previousEnabled = virtualCamera.enabled;
        captured = true;
        virtualCamera.m_Lens = LensSettings.FromCamera(outputCamera);
        virtualCamera.transform.SetPositionAndRotation(entryPosition, entryRotation);
        virtualCamera.ForceCameraPosition(entryPosition, entryRotation);
        virtualCamera.enabled = true;
        virtualCamera.Priority = 1000;
    }

    public void Apply(double timelineTime)
    {
        if (!captured) return;
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 4f, (float)timelineTime));
        virtualCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(entryPosition, authoredPath.position, blend),
            Quaternion.Slerp(entryRotation, authoredPath.rotation, blend));
    }

    public void Release()
    {
        if (!captured) return;
        captured = false;
        if (virtualCamera == null) return;
        virtualCamera.Priority = previousPriority;
        virtualCamera.m_Lens = previousLens;
        virtualCamera.enabled = previousEnabled;
    }

    private void OnDisable() => Release();
}
