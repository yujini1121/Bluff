using System;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

// Timeline owns every presentation time. This component only owns the gameplay session
// and the two user-paced dialogue gates. It never samples camera/effect curves itself.
public sealed class DefeatCutsceneController : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CutsceneCameraRig cameraRig;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private Volume effectsVolume;
    [SerializeField] private DefeatAudioMuffle audioMuffle;
    [SerializeField] private VictoryCutsceneController victoryCutscene;
    [SerializeField] private CanvasGroup[] gameplayHud = Array.Empty<CanvasGroup>();
    [SerializeField] private Behaviour[] suspendedInputs = Array.Empty<Behaviour>();
    [SerializeField, TextArea] private string[] dealerLines =
        { "……유감입니다.", "이번 판은 제 승리군요." };
    [SerializeField, TextArea] private string[] warningLines =
        { "처음에 말씀드렸죠. 패배에도 대가는 있다고.", "……그럼, 약속대로 하겠습니다." };

    public bool HasPlayed { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsWaitingForDialogue { get; private set; }
    public event Action Completed;

    private struct HudState { public float Alpha; public bool Interactable, Raycasts; }
    private HudState[] hudStates;
    private bool[] inputStates;
    private bool finishRequested, resumeRequested, dealerStarted, warningStarted;
    private bool dialogueWasActive, volumeWasEnabled;
    private float volumeWeight;
    private int dialogueStartFrame;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private GameObject previousSelection;

    public bool IsConfigured
    {
        get
        {
            if (director == null || !(director.playableAsset is TimelineAsset timeline) ||
                cameraRig == null || !cameraRig.IsConfigured || dialogue == null || !dialogue.enabled ||
                dialoguePanel == null || screenRoot == null || effectsVolume == null ||
                effectsVolume.sharedProfile == null || audioMuffle == null || gameplayHud.Length == 0 ||
                gameplayHud.Any(g => g == null)) return false;
            var tracks = timeline.GetOutputTracks().Where(t => !t.mutedInHierarchy).ToArray();
            if (tracks.Any(t => director.GetGenericBinding(t) == null)) return false;
            var shots = tracks.OfType<CinemachineTrack>().SelectMany(t => t.GetClips()).ToArray();
            if (shots.Length < 3 || shots.Any(c => !(((CinemachineShot)c.asset).VirtualCamera.Resolve(director))))
                return false;
            return HasSignal(tracks, nameof(BeginDealerDialogue)) &&
                HasSignal(tracks, nameof(BeginFinalWarning)) && HasSignal(tracks, nameof(CompleteTimeline));
        }
    }

    private bool HasSignal(TrackAsset[] tracks, string method)
    {
        foreach (var track in tracks.OfType<SignalTrack>())
        {
            var receiver = director.GetGenericBinding(track) as SignalReceiver;
            if (receiver == null) continue;
            foreach (var marker in track.GetMarkers().OfType<SignalEmitter>())
            {
                if (marker.asset == null || marker.time > director.duration + 1e-6) continue;
                var reaction = receiver.GetReaction(marker.asset);
                if (reaction == null) continue;
                for (int i = 0; i < reaction.GetPersistentEventCount(); i++)
                    if (reaction.GetPersistentTarget(i) == this && reaction.GetPersistentMethodName(i) == method &&
                        reaction.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off) return true;
            }
        }
        return false;
    }

    private void Awake()
    {
        if (screenRoot != null) screenRoot.SetActive(false);
    }

    public bool TryPlay(GamePhase phase, GameWinner winner, bool settlementComplete)
    {
        if (HasPlayed || IsPlaying || phase != GamePhase.GameOver || winner != GameWinner.Dealer ||
            !settlementComplete || (victoryCutscene != null && victoryCutscene.IsPlaying)) return false;
        HasPlayed = true; // A broken setup must not permanently suppress the existing result UI.
        if (!isActiveAndEnabled || !IsConfigured)
        {
            Debug.LogError("Defeat cutscene is not configured. Showing the normal final result.", this);
            return false;
        }
        IsPlaying = true;
        try
        {
            Capture();
            director.stopped += OnDirectorStopped;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0;
            director.Play();
            director.Evaluate(); // Snapshot the actual output, including an in-progress gameplay blend.
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Finish();
            return false;
        }
    }

    private void Capture()
    {
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        dialogueWasActive = dialogue.gameObject.activeSelf;
        volumeWasEnabled = effectsVolume.enabled; volumeWeight = effectsVolume.weight;
        hudStates = gameplayHud.Select(g => new HudState
            { Alpha = g.alpha, Interactable = g.interactable, Raycasts = g.blocksRaycasts }).ToArray();
        inputStates = suspendedInputs.Select(b => b != null && b.enabled).ToArray();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        foreach (var input in suspendedInputs) if (input != null) input.enabled = false;
        foreach (var hud in gameplayHud) { hud.alpha = 0; hud.interactable = hud.blocksRaycasts = false; }
        dialogue.gameObject.SetActive(true);
        dialogue.DialogueCompleted += OnDialogueCompleted;
        screenRoot.SetActive(true);
        effectsVolume.enabled = true;
        audioMuffle.Capture(transform);
        cameraRig.Capture();
    }

    public void BeginDealerDialogue()
    {
        if (!Application.isPlaying || !IsPlaying || dealerStarted || finishRequested) return;
        dealerStarted = true;
        BeginDialogue(dealerLines);
    }

    public void BeginFinalWarning()
    {
        if (!Application.isPlaying || !IsPlaying || warningStarted || finishRequested) return;
        warningStarted = true;
        BeginDialogue(warningLines);
    }

    private void BeginDialogue(string[] lines)
    {
        IsWaitingForDialogue = true;
        dialogueStartFrame = Time.frameCount;
        director.Pause();
        dialogue.StartDialogue(lines);
    }

    private void OnDialogueCompleted()
    {
        if (!IsPlaying || !IsWaitingForDialogue) return;
        IsWaitingForDialogue = false;
        resumeRequested = true;
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (finishRequested || Input.GetKeyDown(KeyCode.Escape)) { Finish(); return; }
        // Destroyed/disabled dependencies must not strand a paused director and hidden HUD.
        if (director == null || !director.isActiveAndEnabled || dialogue == null ||
            !dialogue.isActiveAndEnabled || screenRoot == null || !screenRoot.activeInHierarchy)
        { Finish(); return; }
        if (resumeRequested) { resumeRequested = false; director.Resume(); }
        if (IsWaitingForDialogue && Time.frameCount > dialogueStartFrame &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))) dialogue.Next();
    }

    public void CompleteTimeline() { if (Application.isPlaying && IsPlaying) finishRequested = true; }
    private void OnDirectorStopped(PlayableDirector source) => CompleteTimeline();
    public void Cancel() => Finish();
    [ContextMenu("Skip Defeat Cutscene (Play Mode)")]
    public void Skip() => Finish();
    public void ResetForGame() { Finish(); HasPlayed = false; }

    private void Finish()
    {
        if (!IsPlaying) return;
        IsPlaying = IsWaitingForDialogue = false;
        finishRequested = resumeRequested = dealerStarted = warningStarted = false;
        if (director != null) { director.stopped -= OnDirectorStopped; director.Stop(); }
        if (cameraRig != null) cameraRig.Release();
        if (audioMuffle != null) audioMuffle.Release();
        if (dialogue != null)
        {
            dialogue.DialogueCompleted -= OnDialogueCompleted;
            dialogue.gameObject.SetActive(false); // Stop the existing typing coroutine/SFX.
            dialogue.gameObject.SetActive(dialogueWasActive);
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (screenRoot != null) screenRoot.SetActive(false);
        if (effectsVolume != null) { effectsVolume.weight = volumeWeight; effectsVolume.enabled = volumeWasEnabled; }
        for (int i = 0; hudStates != null && i < hudStates.Length; i++)
        {
            if (gameplayHud[i] == null) continue;
            gameplayHud[i].alpha = hudStates[i].Alpha;
            gameplayHud[i].interactable = hudStates[i].Interactable;
            gameplayHud[i].blocksRaycasts = hudStates[i].Raycasts;
        }
        for (int i = 0; inputStates != null && i < inputStates.Length; i++)
            if (suspendedInputs[i] != null) suspendedInputs[i].enabled = inputStates[i];
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        Completed?.Invoke();
    }
    private void OnDisable() => Finish();
    private void OnDestroy() => Finish();
}
