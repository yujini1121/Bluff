using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputSystem : MonoBehaviour
{
    [Header("연결 스크립트")]
    [SerializeField] private Pause pause;
    [SerializeField] private SwitchCamera switchCamera;
    [SerializeField] private GameplayController gameplayController;

    [Header("키 홀딩 시간")]
    [SerializeField] private float holdStartTime = 0.5f;
    [SerializeField] private float holdInterval = 0.1f;
    private float holdingTime = 0f;
    private int holdingCount = 0;

    // Update is called once per frame
    void Update()
    {
        // Pause
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pause.PauseGame();
        }
        
        if (!pause.IsPaused)
        {
            // Camera Switch : Player
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                switchCamera.SwitchToPlayerCam();
            }
            // Camera Switch : Community
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                switchCamera.SwitchToCommunityCam();
            }
            // Camera Switch : Dealer
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                switchCamera.SwitchToDealerCam();
            }

            // Betting : Call
            if (Input.GetKeyDown(KeyCode.C))
            {
                gameplayController.OnCallClicked();
            }
            // Betting : Raise
            if (Input.GetKeyDown(KeyCode.R))
            {
                gameplayController.OnRaiseClicked();
            }
            // Betting : Fold
            if (Input.GetKeyDown(KeyCode.F))
            {
                gameplayController.OnFoldClicked();
            }
            // Betting : Raise Increase
            if (Input.GetKeyDown(KeyCode.E))
            {
                gameplayController.OnRaiseIncreaseClicked();
            }
            else if (Input.GetKey(KeyCode.E))
            {
                holdingTime += Time.deltaTime;
                if (holdingTime >= holdStartTime)
                {
                    if (holdingTime >= holdStartTime + holdInterval * holdingCount)
                    {
                        gameplayController.OnRaiseIncreaseClicked();
                        holdingCount++;
                    }
                }
            }
            else if (Input.GetKeyUp(KeyCode.E))
            {
                holdingTime = 0f;
                holdingCount = 0;
            }
            // Betting : Raise Decrease
            if (Input.GetKeyDown(KeyCode.Q))
            {
                gameplayController.OnRaiseDecreaseClicked();
            }
            else if (Input.GetKey(KeyCode.Q))
            {
                holdingTime += Time.deltaTime;
                if (holdingTime >= holdStartTime)
                {
                    if (holdingTime >= holdStartTime + holdInterval * holdingCount)
                    {
                        gameplayController.OnRaiseDecreaseClicked();
                        holdingCount++;
                    }
                }
            }
            else if (Input.GetKeyUp(KeyCode.Q))
            {
                holdingTime = 0f;
                holdingCount = 0;
            }
            // Betting : Raise Max
            if (Input.GetKeyDown(KeyCode.A))
            {
                gameplayController.OnRaiseMaxClicked();
            }

            // Showdown & Next Round
            if (Input.GetKeyDown(KeyCode.Space))
            {
                gameplayController.OnResolveShowdownClicked();
                gameplayController.OnNextRoundClicked();
            }
        }
    }
}
