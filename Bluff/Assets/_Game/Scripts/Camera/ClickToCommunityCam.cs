using UnityEngine;

public class ClickToCommunityCam : MonoBehaviour
{
    SwitchCamera switchCamera;

    void Awake()
    {
        switchCamera = FindObjectOfType<SwitchCamera>();
    }

    void OnMouseDown()
    {
        switchCamera.SwitchToCommunityCam();
    }

    void OnMouseUp()
    {
        switchCamera.SwitchToPlayerCam();
    }
}
