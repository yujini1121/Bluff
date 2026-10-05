using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public sealed class IntroFlowController : MonoBehaviour
{
    public const string GameplaySceneName = "Dev_Yujin";
    public enum IntroState { Corridor, BarCutscene, DealerInteraction, Dialogue, Transition }

    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private CinemachineVirtualCamera playerViewCamera;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private PlayableDirector barDirector;
    [SerializeField] private IntroBarSequenceCamera barCamera;
    [SerializeField] private Transform dealerLookTarget;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField, Min(0.1f)] private float interactionDistance = 4f;
    [SerializeField, Range(1f, 90f)] private float interactionAngle = 40f;
    [SerializeField] private DialogueController dialogueController;
    [SerializeField, TextArea] private string[] dialogueLines;
    [SerializeField] private IntroFadeController fadeController;
    [SerializeField] private IntroHoldToSkip holdToSkip;
    [SerializeField] private PlayableDirector doorDirector;

    public IntroState State { get; private set; } = IntroState.Corridor;
    public bool CanInteract => State == IntroState.DealerInteraction && playerViewCamera != null &&
        dealerLookTarget != null &&
        Vector3.Distance(playerViewCamera.transform.position, dealerLookTarget.position) <= interactionDistance &&
        Vector3.Angle(playerViewCamera.transform.forward,
            dealerLookTarget.position - playerViewCamera.transform.position) <= interactionAngle;

    private bool initialized;
    private int dialogueStartFrame;

    private void OnEnable()
    {
        if (dialogueController != null) dialogueController.DialogueCompleted += HandleDialogueCompleted;
    }

    private void OnDisable()
    {
        if (dialogueController != null) dialogueController.DialogueCompleted -= HandleDialogueCompleted;
        SetPrompt(false);
        if (holdToSkip != null) holdToSkip.SetAvailable(false);
    }

    private void Start() => StartIntro();

    public void StartIntro()
    {
        if (initialized) return;
        initialized = true;
        State = IntroState.Corridor;
        SetPrompt(false);
        if (holdToSkip != null) holdToSkip.SetAvailable(true);
        playerMove.SetMoveInputEnabled(true);
    }

    private void Update()
    {
        if (State == IntroState.BarCutscene && barDirector.time >= barDirector.duration - 0.001d)
            CompleteBarSequence();

        if (State == IntroState.DealerInteraction)
        {
            SetPrompt(CanInteract);
            if (Input.GetKeyDown(KeyCode.E)) TryInteract();
        }
        else if (State == IntroState.Dialogue && Time.frameCount > dialogueStartFrame &&
                 (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)))
        {
            dialogueController.Next();
        }
    }

    public bool BeginBarSequence()
    {
        if (State != IntroState.Corridor || barDirector == null || barCamera == null ||
            playerMove == null || playerViewCamera == null || outputCamera == null) return false;
        State = IntroState.BarCutscene;
        playerMove.SetMoveInputEnabled(false);
        SetPrompt(false);
        barDirector.time = 0d;
        barDirector.Play();
        barDirector.Evaluate();
        playerMove.WarpViewPose(barCamera.AuthoredPosition, outputCamera.transform.rotation);
        barCamera.CaptureEntry(outputCamera);
        barCamera.ApplyPose();
        return true;
    }

    private void CompleteBarSequence()
    {
        if (State != IntroState.BarCutscene) return;
        State = IntroState.DealerInteraction;
        barDirector.time = barDirector.duration;
        barDirector.Evaluate();
        barCamera.ApplyPose();
        Transform finalView = barCamera.VirtualCamera.transform;
        playerMove.WarpViewPose(finalView.position, finalView.rotation);
        playerViewCamera.m_Lens = barCamera.VirtualCamera.m_Lens;
        playerViewCamera.ForceCameraPosition(finalView.position, finalView.rotation);
        barCamera.Release();
        barDirector.Stop();
        playerMove.SetMoveInputEnabled(true);
        SetPrompt(CanInteract);
    }

    public bool TryInteract()
    {
        if (!CanInteract || dialogueController == null) return false;
        State = IntroState.Dialogue;
        dialogueStartFrame = Time.frameCount;
        SetPrompt(false);
        playerMove.SetMoveInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        dialogueController.StartDialogue(dialogueLines);
        return true;
    }

    private void HandleDialogueCompleted()
    {
        if (State == IntroState.Dialogue) BeginTransition();
    }

    public void SkipIntro() => BeginTransition();

    private void BeginTransition()
    {
        if (State == IntroState.Transition || fadeController == null) return;
        State = IntroState.Transition;
        SetPrompt(false);
        if (holdToSkip != null) holdToSkip.SetAvailable(false);
        if (barDirector != null && barDirector.state == PlayState.Playing) barDirector.Pause();
        if (doorDirector != null && doorDirector.state == PlayState.Playing) doorDirector.Pause();
        playerMove.SetMoveInputEnabled(false);
        fadeController.TransitionToGameplay();
    }

    private void SetPrompt(bool visible)
    {
        if (interactionPrompt != null && interactionPrompt.activeSelf != visible)
            interactionPrompt.SetActive(visible);
    }
}
