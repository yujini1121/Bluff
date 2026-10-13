using Cinemachine;
using UnityEngine;

public enum CameraView
{
    Player,
    Community,
    Dealer
}

public class SwitchCamera : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera playerCam;
    [SerializeField] private CinemachineVirtualCamera communityCam;
    [SerializeField] private CinemachineVirtualCamera dealerCam;

    private bool canCameraMove = true;

    public CameraView CurrentView { get; private set; }

    public bool IsDealerCameraSettled
    {
        get
        {
            if (CurrentView != CameraView.Dealer || dealerCam == null)
            {
                return false;
            }

            CinemachineBrain brain = CinemachineCore.Instance.FindPotentialTargetBrain(dealerCam);
            return brain != null &&
                   brain.isActiveAndEnabled &&
                   !brain.IsBlending &&
                   brain.ActiveVirtualCamera == (ICinemachineCamera)dealerCam;
        }
    }

    private void Start()
    {
        SwitchToPlayerCam();
    }

    public void SwitchToPlayerCam()
    {
        if (!canCameraMove)
        {
            return;
        }

        playerCam.Priority = 10;
        communityCam.Priority = 0;
        dealerCam.Priority = 0;
        CurrentView = CameraView.Player;
    }

    public void SwitchToCommunityCam()
    {
        if (!canCameraMove)
        {
            return;
        }

        playerCam.Priority = 0;
        communityCam.Priority = 10;
        dealerCam.Priority = 0;
        CurrentView = CameraView.Community;
    }

    public void SwitchToDealerCam()
    {
        if (!canCameraMove)
        {
            return;
        }

        playerCam.Priority = 0;
        communityCam.Priority = 0;
        dealerCam.Priority = 10;
        CurrentView = CameraView.Dealer;
    }

    public void CanCameraMovement(bool canMove)
    {
        canCameraMove = canMove;
    }
}
