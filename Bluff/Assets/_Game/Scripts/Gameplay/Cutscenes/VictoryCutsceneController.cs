using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;

public sealed class VictoryCutsceneController : MonoBehaviour
{
    public enum VictoryBeat { None, Stillness, Dealer, Dialogue, RevealChip, TransferChip,
        Unlock, MemoryTransition, MemoryDealer, LastWords, ReturnToResult }

    [Header("Timeline presentation / dialogue gate")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CutsceneCameraRig cameraRig;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private CutsceneOverlay overlay;
    [SerializeField, TextArea] private string[] dealerLines =
        { "……좋습니다.", "이번 판은 손님의 승리군요.", "약속은 지켜드려야겠죠." };

    [Header("Restored on completion and disable")]
    [SerializeField] private CanvasGroup[] gameplayHud = Array.Empty<CanvasGroup>();
    [SerializeField] private Behaviour[] suspendedInputs = Array.Empty<Behaviour>();

    [Header("Replaceable memory chip visual (never a betting chip / Item)")]
    [SerializeField] private Transform memoryChip;
    [SerializeField] private Renderer chipGlow;

    public bool HasPlayed { get; private set; }
    public bool IsPlaying { get; private set; }
    public VictoryBeat Beat { get; private set; }
    public event Action Completed;
    public event Action MemoryPlaybackStarted;

    private bool finishRequested;
    private bool resumeRequested;
    private bool dialogueStarted;
    private bool[] inputStates;
    private HudState[] hudStates;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private GameObject previousSelection;
    private int dialogueStartFrame;
    private bool memoryStarted;
    private MaterialPropertyBlock originalGlow;
    private Vector3 chipPosition;
    private Quaternion chipRotation;
    private bool chipWasActive;
    private bool dialogueWasActive;
    private Vector3 chipScale;
    private float glowIntensity;
    public bool IsWaitingForDialogue { get; private set; }

    private struct HudState { public float Alpha; public bool Interactable; public bool Raycasts; }

    public bool IsConfigured => director != null && director.playableAsset != null &&
        cameraRig != null && cameraRig.IsConfigured && dialogue != null && dialoguePanel != null &&
        overlay != null && overlay.IsConfigured && memoryChip != null &&
        gameplayHud.Length > 0 &&
        Array.TrueForAll(gameplayHud, group => group != null);

    public bool TryPlay(GamePhase phase, GameWinner winner, bool settlementComplete)
    {
        if (HasPlayed || IsPlaying || phase != GamePhase.GameOver || winner != GameWinner.Player ||
            !settlementComplete) return false;
        HasPlayed = true;
        if (!isActiveAndEnabled || !IsConfigured)
        {
            Debug.LogError("Victory cutscene is not configured. Showing the normal final result.", this);
            return false;
        }

        IsPlaying = true;
        CapturePresentation();
        director.stopped += OnDirectorStopped;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.extrapolationMode = DirectorWrapMode.None;
        director.time = 0d;
        director.Play();
        // Install the Entry override before the Brain's next update, even during a gameplay blend.
        director.Evaluate();
        return true;
    }

    private void CapturePresentation()
    {
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        inputStates = new bool[suspendedInputs.Length];
        for (int i = 0; i < suspendedInputs.Length; i++)
        {
            if (suspendedInputs[i] == null) continue;
            inputStates[i] = suspendedInputs[i].enabled;
            suspendedInputs[i].enabled = false;
        }
        hudStates = new HudState[gameplayHud.Length];
        for (int i = 0; i < gameplayHud.Length; i++)
        {
            CanvasGroup group = gameplayHud[i];
            if (group == null) continue;
            hudStates[i] = new HudState { Alpha = group.alpha, Interactable = group.interactable,
                Raycasts = group.blocksRaycasts };
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }
        chipPosition = memoryChip.position;
        chipRotation = memoryChip.rotation;
        chipScale = memoryChip.localScale;
        chipWasActive = memoryChip.gameObject.activeSelf;
        var glow = memoryChip.GetComponent<MemoryChipGlow>();
        if (glow != null) glowIntensity = glow.intensity;
        memoryChip.gameObject.SetActive(false);
        if (chipGlow != null)
        {
            originalGlow = new MaterialPropertyBlock();
            chipGlow.GetPropertyBlock(originalGlow);
        }
        dialogueWasActive = dialogue.gameObject.activeSelf;
        dialogue.gameObject.SetActive(true);
        dialogue.DialogueCompleted += OnDialogueCompleted;
        overlay.Show();
        cameraRig.Capture();
    }

    // Timeline SignalReceiver callbacks; edit-mode scrubbing previews visuals only.
    public void BeginDialogue()
    {
        if (!Application.isPlaying || !IsPlaying || dialogueStarted || finishRequested) return;
        dialogueStarted = true;
        IsWaitingForDialogue = true;
        Beat = VictoryBeat.Dialogue;
        dialogueStartFrame = Time.frameCount;
        director.Pause();
        dialogue.StartDialogue(dealerLines);
    }

    public void BeginMemoryPlayback()
    {
        if (!Application.isPlaying || !IsPlaying || memoryStarted) return;
        memoryStarted = true;
        Beat = VictoryBeat.MemoryDealer;
        MemoryPlaybackStarted?.Invoke();
    }

    public void ReturnToGameplayCamera()
    {
        if (!Application.isPlaying || !IsPlaying) return;
        Beat = VictoryBeat.ReturnToResult;
        cameraRig.Release();
    }

    public void CompleteTimeline()
    {
        if (Application.isPlaying && IsPlaying) finishRequested = true;
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector) => CompleteTimeline();

    private void Update()
    {
        if (!IsPlaying) return;
        if (finishRequested) { Finish(); return; }
        if (resumeRequested)
        {
            resumeRequested = false;
            director.Resume();
        }
        if (IsWaitingForDialogue && Time.frameCount > dialogueStartFrame &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))) dialogue.Next();
    }

    private void OnDialogueCompleted()
    {
        if (!IsPlaying || !IsWaitingForDialogue) return;
        IsWaitingForDialogue = false;
        Beat = VictoryBeat.None;
        // Resume outside the notification callback / dialogue coroutine.
        resumeRequested = true;
    }

    public void Cancel()
    {
        if (!IsPlaying) return;
        Finish();
    }

    [ContextMenu("Skip Victory Cutscene (Play Mode)")]
    public void Skip() => Cancel();

    public void ResetForGame()
    {
        Cancel();
        HasPlayed = false;
        Beat = VictoryBeat.None;
    }

    private void Finish()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        finishRequested = resumeRequested = dialogueStarted = false;
        IsWaitingForDialogue = false;
        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
            director.Stop();
        }
        if (cameraRig != null) cameraRig.Release();
        if (dialogue != null)
        {
            dialogue.DialogueCompleted -= OnDialogueCompleted;
            dialogue.gameObject.SetActive(false);
            dialogue.gameObject.SetActive(dialogueWasActive);
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (overlay != null)
        {
            overlay.Hide();
        }
        if (memoryChip != null)
        {
            memoryChip.SetPositionAndRotation(chipPosition, chipRotation);
            memoryChip.localScale = chipScale;
            var glow = memoryChip.GetComponent<MemoryChipGlow>();
            if (glow != null) glow.intensity = glowIntensity;
            memoryChip.gameObject.SetActive(chipWasActive);
        }
        if (chipGlow != null && originalGlow != null) chipGlow.SetPropertyBlock(originalGlow);
        for (int i = 0; i < gameplayHud.Length; i++)
        {
            if (gameplayHud[i] == null) continue;
            gameplayHud[i].alpha = hudStates[i].Alpha;
            gameplayHud[i].interactable = hudStates[i].Interactable;
            gameplayHud[i].blocksRaycasts = hudStates[i].Raycasts;
        }
        for (int i = 0; i < suspendedInputs.Length; i++)
            if (suspendedInputs[i] != null) suspendedInputs[i].enabled = inputStates[i];
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        memoryStarted = false;
        Beat = VictoryBeat.None;
        Completed?.Invoke();
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
}
