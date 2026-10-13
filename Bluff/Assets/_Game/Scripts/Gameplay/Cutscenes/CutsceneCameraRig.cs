using System;
using Cinemachine;
using UnityEngine;

public sealed class CutsceneCameraRig : MonoBehaviour
{
    [SerializeField] private Camera outputCamera;
    // The original Victory Camera becomes the runtime entry snapshot.
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private CinemachineVirtualCamera[] shotCameras = Array.Empty<CinemachineVirtualCamera>();
    private CameraState[] states;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private bool captured;

    private struct CameraState
    {
        public LensSettings Lens;
        public int Priority;
        public bool Enabled, Active;
    }

    public bool IsConfigured => outputCamera != null && virtualCamera != null &&
        outputCamera.GetComponent<CinemachineBrain>() != null && shotCameras.Length > 0 &&
        Array.IndexOf(shotCameras, virtualCamera) >= 0 &&
        Array.TrueForAll(shotCameras, camera => camera != null);

    public void Capture()
    {
        if (captured) return;
        states = new CameraState[shotCameras.Length];
        entryPosition = virtualCamera.transform.position;
        entryRotation = virtualCamera.transform.rotation;
        for (int i = 0; i < shotCameras.Length; i++)
        {
            var camera = shotCameras[i];
            states[i] = new CameraState { Lens = camera.m_Lens, Priority = camera.Priority,
                Enabled = camera.enabled, Active = camera.gameObject.activeSelf };
            camera.gameObject.SetActive(true);
            camera.enabled = true;
        }
        captured = true;
        // Includes an in-progress gameplay blend. Timeline owns all later shots.
        virtualCamera.m_Lens = LensSettings.FromCamera(outputCamera);
        virtualCamera.ForceCameraPosition(outputCamera.transform.position, outputCamera.transform.rotation);
    }

    public void Release()
    {
        if (!captured) return;
        captured = false;
        for (int i = 0; i < shotCameras.Length; i++)
        {
            var camera = shotCameras[i];
            if (camera == null) continue;
            camera.Priority = states[i].Priority;
            camera.m_Lens = states[i].Lens;
            camera.enabled = states[i].Enabled;
            camera.gameObject.SetActive(states[i].Active);
        }
        if (virtualCamera != null) virtualCamera.ForceCameraPosition(entryPosition, entryRotation);
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
