using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;

public sealed class VictoryCutsceneController : MonoBehaviour
{
    public enum VictoryBeat { None, Stillness, Dealer, Dialogue, RevealChip, TransferChip,
        Unlock, MemoryTransition, MemoryDealer, LastWords, ReturnToResult }

    [Header("Sequence (0–4 entrance, pause for NEXT, 4–14 delivery/memory)")]
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
    [SerializeField] private Transform chipRevealPoint;
    [SerializeField] private Transform chipReceivePoint;
    [SerializeField] private Renderer chipGlow;

    public bool HasPlayed { get; private set; }
    public bool IsPlaying { get; private set; }
    public VictoryBeat Beat { get; private set; }
    public event Action Completed;
    public event Action MemoryPlaybackStarted;

    private Coroutine routine;
    private bool[] inputStates;
    private HudState[] hudStates;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private GameObject previousSelection;
    private bool dialogueFinished;
    private int dialogueStartFrame;
    private bool memoryStarted;
    private MaterialPropertyBlock glowProperties;
    private MaterialPropertyBlock originalGlow;
    private Vector3 chipPosition;
    private Quaternion chipRotation;
    private bool chipWasActive;
    private bool dialogueWasActive;

    private struct HudState { public float Alpha; public bool Interactable; public bool Raycasts; }

    public bool IsConfigured => director != null && director.playableAsset != null &&
        cameraRig != null && cameraRig.IsConfigured && dialogue != null && dialoguePanel != null &&
        overlay != null && overlay.IsConfigured && memoryChip != null &&
        chipRevealPoint != null && chipReceivePoint != null && gameplayHud.Length > 0 &&
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
        routine = StartCoroutine(Run());
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
        chipWasActive = memoryChip.gameObject.activeSelf;
        memoryChip.gameObject.SetActive(false);
        if (chipGlow != null)
        {
            originalGlow = new MaterialPropertyBlock();
            chipGlow.GetPropertyBlock(originalGlow);
            glowProperties = new MaterialPropertyBlock();
        }
        dialogueWasActive = dialogue.gameObject.activeSelf;
        dialogue.gameObject.SetActive(true);
        dialogue.DialogueCompleted += OnDialogueCompleted;
        overlay.Show();
        cameraRig.Capture();
    }

    private IEnumerator Run()
    {
        try
        {
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.time = 0d;
            director.Play();
            yield return PlayTo(4d);
            director.Pause();
            Beat = VictoryBeat.Dialogue;
            dialogueFinished = false;
            dialogueStartFrame = Time.frameCount;
            dialogue.StartDialogue(dealerLines);
            while (!dialogueFinished) yield return null;
            director.Resume();
            yield return PlayTo(14d);
            Beat = VictoryBeat.ReturnToResult;
            director.Stop();
            cameraRig.Release();
            yield return new WaitForSecondsRealtime(0.35f);
            yield return overlay.ClearScreen(0.65f);
        }
        finally { Finish(); }
    }

    private IEnumerator PlayTo(double end)
    {
        while (director.time < end)
        {
            director.time = Math.Min(end, director.time + Time.unscaledDeltaTime);
            director.Evaluate();
            cameraRig.Apply(director.time);
            PresentBeat((float)director.time);
            yield return null;
        }
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (Beat == VictoryBeat.Dialogue && Time.frameCount > dialogueStartFrame &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))) dialogue.Next();
    }

    private void OnDialogueCompleted() => dialogueFinished = true;

    private void PresentBeat(float time)
    {
        if (time < 1f) Beat = VictoryBeat.Stillness;
        else if (time <= 4f) Beat = VictoryBeat.Dealer;
        else if (time < 6f) Beat = VictoryBeat.RevealChip;
        else if (time < 8f) Beat = VictoryBeat.TransferChip;
        else if (time < 9f) Beat = VictoryBeat.Unlock;
        else if (time < 9.6f) Beat = VictoryBeat.MemoryTransition;
        else if (time < 10.4f) Beat = VictoryBeat.MemoryDealer;
        else Beat = VictoryBeat.LastWords;

        bool chipVisible = time >= 5f && time < 9.6f;
        memoryChip.gameObject.SetActive(chipVisible);
        float transfer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6f, 8f, time));
        Vector3 position = Vector3.Lerp(chipRevealPoint.position, chipReceivePoint.position, transfer);
        position += Vector3.up * (Mathf.Sin(transfer * Mathf.PI) * 0.08f);
        position -= Vector3.up * (0.12f * (1f - Mathf.InverseLerp(5f, 5.7f, time)));
        memoryChip.SetPositionAndRotation(position,
            Quaternion.Slerp(chipRevealPoint.rotation, chipReceivePoint.rotation, transfer));
        if (chipGlow != null)
        {
            float glow = Mathf.InverseLerp(8f, 8.6f, time);
            glowProperties.SetColor("_EmissionColor", new Color(0.1f, 0.85f, 1f) * Mathf.Lerp(0.25f, 4f, glow));
            chipGlow.SetPropertyBlock(glowProperties);
        }
        if (time >= 9f && !memoryStarted)
        {
            memoryStarted = true;
            MemoryPlaybackStarted?.Invoke();
        }
        float flash = (time >= 9f && time < 9.15f) || (time >= 9.38f && time < 9.47f) ? 0.8f : 0f;
        float darkness = Mathf.InverseLerp(10.4f, 11f, time);
        bool lastWords = time >= 11f;
        overlay.Present(darkness, flash,
            lastWords, time >= 8f && time < 9f,
            lastWords ? 1f - Mathf.InverseLerp(12.6f, 13.7f, time) : 1f);
    }


    public void Cancel()
    {
        if (!IsPlaying) return;
        if (routine != null) StopCoroutine(routine);
        Finish();
    }

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
        routine = null;
        if (director != null) director.Stop();
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
