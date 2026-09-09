using Cinemachine;
using System;
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

    public CameraView CurrentView { get; private set; }

    private void Start()
    {
        SwitchToPlayerCam();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SwitchToPlayerCam();
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            SwitchToCommunityCam();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            SwitchToDealerCam();
        }
    }

    public void SwitchToPlayerCam()
    {
        playerCam.Priority = 10;
        communityCam.Priority = 0;
        dealerCam.Priority = 0;
        CurrentView = CameraView.Player;
    }

    public void SwitchToCommunityCam()
    {
        playerCam.Priority = 0;
        communityCam.Priority = 10;
        dealerCam.Priority = 0;
        CurrentView = CameraView.Community;
    }

    public void SwitchToDealerCam()
    {
        playerCam.Priority = 0;
        communityCam.Priority = 0;
        dealerCam.Priority = 10;
        CurrentView = CameraView.Dealer;
    }
}
