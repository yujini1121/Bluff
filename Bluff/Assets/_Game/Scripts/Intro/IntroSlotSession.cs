using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroSlotSession : MonoBehaviour
{
    [SerializeField] private IntroFlowController flow;
    [SerializeField] private PlayerMove player;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private CinemachineVirtualCamera slotCamera;
    [SerializeField] private IntroSlotStation[] stations;
    [SerializeField] private IntroSlotHud hud;
    [SerializeField] private IntroSlotChips chips = new IntroSlotChips();
    private IntroSlotStation station;
    private SlotMachineController pendingMachine;
    private int generation, pendingTicket, entryFrame;
    private bool exitRequested, savedMovement, savedCursorVisible, paymentPending;
    private CursorLockMode savedCursorLock;
    public bool IsActive { get; private set; }
    public bool IsExitRequested => exitRequested;
    public IntroSlotChips Chips => chips;
    private bool IsPresenting => station && station.CoinPresentation &&
        station.CoinPresentation.isActiveAndEnabled && station.CoinPresentation.IsBusy;
    public bool CanSpin => IsActive && !exitRequested && !paymentPending && !IsPresenting && chips.CanSpin && station &&
        station.Machine && station.Machine.isActiveAndEnabled && station.Machine.IsConfigured && !station.Machine.IsSpinning;

    private void Update()
    {
        if (!IsActive) return;
        if (!flow || !flow.IsFreeRoam || !station || !station.isActiveAndEnabled ||
            !station.Machine || !station.Machine.isActiveAndEnabled)
        {
            CancelPresentation();
            RequestExit();
            return;
        }
        if (paymentPending)
        {
            var presentation = station.CoinPresentation;
            if (!presentation || !presentation.isActiveAndEnabled || !outputCamera ||
                (!presentation.IsBusy && !presentation.PaymentReady))
            {
                CancelPresentation();
                FinishPending();
            }
            else if (presentation.PaymentReady)
            {
                paymentPending = false;
                presentation.Cancel();
                if (!StartAcceptedSpin()) FinishPending();
            }
        }
        if (exitRequested && !paymentPending && !chips.HasPendingSpin && !IsPresenting)
        {
            ReleaseView();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) RequestExit();
        if (!exitRequested && Time.frameCount > entryFrame &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) ||
             (Input.GetMouseButtonDown(0) && outputCamera &&
              station.IsSpinControlHit(outputCamera.ScreenPointToRay(Input.mousePosition))))) TrySpin();
        if (!exitRequested && Input.GetKeyDown(KeyCode.R)) Refill();
    }

    private void LateUpdate()
    {
        if (IsActive && slotCamera && station && station.CameraPose)
        {
            Quaternion rotation = station.CameraPose.rotation;
            if (IsPresenting)
            {
                Vector3 direction = station.CoinPresentation.FocusPosition - slotCamera.transform.position;
                if (direction.sqrMagnitude > .001f) rotation = Quaternion.LookRotation(direction);
            }
            slotCamera.transform.rotation = Quaternion.Slerp(slotCamera.transform.rotation, rotation,
                1 - Mathf.Exp(-8 * Time.deltaTime));
        }
        if (!hud) return;
        hud.Show(IsActive && flow && flow.IsFreeRoam, chips.Balance,
            IsPresenting && station.CoinPresentation.IsDispensing);
    }

    public IntroSlotStation FindNearby()
    {
        if (!isActiveAndEnabled || !flow || !flow.CanUseSlots || flow.CanInteract ||
            chips.HasPendingSpin || !outputCamera || stations == null) return null;
        IntroSlotStation nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var candidate in stations)
        {
            if (!candidate || !candidate.CanApproach(outputCamera.transform)) continue;
            float next = (candidate.InteractionTarget.position - outputCamera.transform.position).sqrMagnitude;
            if (next < distance) { nearest = candidate; distance = next; }
        }
        return nearest;
    }

    public bool TryEnterNearby()
    {
        var candidate = FindNearby();
        if (!candidate || !player || !slotCamera || !candidate.TryClaim(this)) return false;
        station = candidate;
        savedMovement = player.IsMoveInputEnabled;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        IsActive = true;
        entryFrame = Time.frameCount;
        exitRequested = false;
        flow.SuppressSkipUntilRelease();
        player.SetMoveInputEnabled(false);
        slotCamera.transform.SetPositionAndRotation(station.CameraPose.position, station.CameraPose.rotation);
        slotCamera.m_Lens.FieldOfView = station.FieldOfView;
        slotCamera.ForceCameraPosition(station.CameraPose.position, station.CameraPose.rotation);
        slotCamera.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public bool TrySpin()
    {
        if (!CanSpin) return false;
        if (station.CoinPresentation && station.CoinPresentation.isActiveAndEnabled)
        {
            paymentPending = true;
            station.CoinPresentation.BeginPayment(outputCamera);
            return true;
        }
        return StartAcceptedSpin();
    }

    private bool StartAcceptedSpin()
    {
        if (!IsActive || !station || !station.isActiveAndEnabled || !chips.CanSpin) return false;
        int ticket = ++generation;
        var machine = station.Machine;
        if (!machine || !machine.TrySpin(result => Complete(ticket, result))) return false;
        chips.DebitAcceptedSpin(ticket);
        pendingTicket = ticket;
        pendingMachine = machine;
        pendingMachine.SpinCancelled += CancelPendingSpin;
        return true;
    }

    private void Complete(int ticket, SlotSpinResult result)
    {
        if (!this || !chips.Settle(ticket, result, out int reward, out _)) return;
        DetachPending();
        if (IsActive && isActiveAndEnabled && station && station.CoinPresentation)
            station.CoinPresentation.BeginPayout(outputCamera, reward);
        FinishPending();
    }

    private void CancelPendingSpin()
    {
        if (!chips.Refund(pendingTicket)) return;
        DetachPending();
        FinishPending();
    }

    private void DetachPending()
    {
        if (pendingMachine) pendingMachine.SpinCancelled -= CancelPendingSpin;
        pendingMachine = null;
        pendingTicket = 0;
    }

    private void FinishPending()
    {
        if (paymentPending || IsPresenting) return;
        if (exitRequested) ReleaseView();
        if (!IsActive && station) { station.Release(this); station = null; }
    }

    public void Refill()
    {
        if (IsActive && !exitRequested && !paymentPending && !IsPresenting) chips.Refill();
    }

    public void RequestExit()
    {
        if (!IsActive) return;
        exitRequested = true;
        if (!chips.HasPendingSpin && !paymentPending && !IsPresenting) ReleaseView();
    }

    private void CancelPresentation()
    {
        paymentPending = false;
        if (station && station.CoinPresentation) station.CoinPresentation.Cancel();
    }

    public void ReleaseView()
    {
        if (!IsActive) return;
        CancelPresentation();
        IsActive = false;
        if (slotCamera) slotCamera.gameObject.SetActive(false);
        if (player && flow && (flow.State == IntroFlowController.IntroState.Corridor ||
            flow.State == IntroFlowController.IntroState.DealerInteraction)) player.SetMoveInputEnabled(savedMovement);
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        if (flow) flow.SuppressSkipUntilRelease();
        if (hud) hud.Hide();
        if (!chips.HasPendingSpin && station) { station.Release(this); station = null; }
    }

    private void OnDisable() { ReleaseView(); if (hud) hud.Hide(); }
    private void OnDestroy()
    {
        CancelPresentation();
        chips.Refund(pendingTicket);
        DetachPending();
        if (station) station.Release(this);
    }
}

