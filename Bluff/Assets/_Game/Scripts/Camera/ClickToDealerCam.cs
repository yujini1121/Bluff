using UnityEngine;

public class ClickToDealerCam : MonoBehaviour
{
    SwitchCamera switchCamera;

    void Awake()
    {
        switchCamera = FindObjectOfType<SwitchCamera>();
    }

    void OnMouseDown()
    {
        switchCamera.SwitchToDealerCam();
    }

    void OnMouseUp()
    {
        switchCamera.SwitchToPlayerCam();
    }
}
