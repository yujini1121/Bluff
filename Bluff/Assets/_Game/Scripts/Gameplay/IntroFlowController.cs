using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public sealed class IntroFlowController : MonoBehaviour
{
    public const string GameplaySceneName = "Dev_Yujin";
    public enum IntroState { Corridor, BarCutscene, DealerInteraction, Dialogue, Transition, Seating }

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
    [SerializeField] private Transform seatedView;
    [SerializeField, Min(0.01f)] private float seatingSpeed = 0.7f;
    [SerializeField, Min(1f)] private float seatingTurnSpeed = 60f;
    private Vector3 seatingPosition;
    private Quaternion seatingRotation;

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
        if (barDirector != null) barDirector.stopped += HandleBarStopped;
        if (initialized && holdToSkip != null) holdToSkip.SetAvailable(State != IntroState.Transition);
    }

    private void OnDisable()
    {
        if (dialogueController != null) dialogueController.DialogueCompleted -= HandleDialogueCompleted;
        if (barDirector != null) barDirector.stopped -= HandleBarStopped;
        if (State == IntroState.BarCutscene)
        {
            ReturnViewToPlayer();
            State = IntroState.DealerInteraction;
            if (barDirector != null) barDirector.Stop();
        }
        if (State == IntroState.Seating)
        {
            State = IntroState.DealerInteraction;
            playerMove.SetMoveInputEnabled(true);
        }
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
        if (State == IntroState.Seating)
        {
            TickSeating(Time.deltaTime);
            return;
        }
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
        ReturnViewToPlayer();
        barDirector.Stop();
        SetPrompt(CanInteract);
    }

    private void HandleBarStopped(PlayableDirector stopped)
    {
        if (State != IntroState.BarCutscene) return;
        // 외부에서 중단해도 마지막 키로 점프하지 않고 현재 시점을 돌려준다.
        State = IntroState.DealerInteraction;
        ReturnViewToPlayer();
    }

    private void ReturnViewToPlayer()
    {
        if (barCamera == null || playerMove == null || playerViewCamera == null) return;
        Transform finalView = barCamera.VirtualCamera.transform;
        playerMove.WarpViewPose(finalView.position, finalView.rotation);
        playerViewCamera.m_Lens = barCamera.VirtualCamera.m_Lens;
        playerViewCamera.ForceCameraPosition(finalView.position, finalView.rotation);
        barCamera.Release();
        playerMove.SetMoveInputEnabled(true);
    }

    public bool TryInteract()
    {
        if (!CanInteract || dialogueController == null) return false;
        SetPrompt(false);
        playerMove.SetMoveInputEnabled(false);
        if (seatedView != null)
        {
            State = IntroState.Seating;
            Transform visible = outputCamera != null ? outputCamera.transform : playerViewCamera.transform;
            seatingPosition = visible.position;
            seatingRotation = visible.rotation;
            playerViewCamera.transform.SetPositionAndRotation(seatingPosition, seatingRotation);
            playerViewCamera.ForceCameraPosition(seatingPosition, seatingRotation);
            var brain = outputCamera != null ? outputCamera.GetComponent<CinemachineBrain>() : null;
            if (brain != null) brain.ActiveBlend = null;
        }
        else StartSeatedDialogue();
        return true;
    }

    private void TickSeating(float deltaTime)
    {
        if (State != IntroState.Seating) return;
        seatingPosition = Vector3.MoveTowards(seatingPosition, seatedView.position,
            Mathf.Max(0.01f, seatingSpeed) * deltaTime);
        Vector3 direction = dealerLookTarget.position - seatingPosition;
        Quaternion facing = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction) : seatingRotation;
        seatingRotation = Quaternion.RotateTowards(seatingRotation, facing,
            Mathf.Max(1f, seatingTurnSpeed) * deltaTime);
        playerViewCamera.transform.SetPositionAndRotation(seatingPosition, seatingRotation);
        playerViewCamera.ForceCameraPosition(seatingPosition, seatingRotation);
        if (Vector3.Distance(seatingPosition, seatedView.position) < 0.0001f &&
            Quaternion.Angle(seatingRotation, facing) < 0.1f)
            StartSeatedDialogue();
    }

    private void StartSeatedDialogue()
    {
        State = IntroState.Dialogue;
        dialogueStartFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        dialogueController.StartDialogue(dialogueLines);
    }

    private void HandleDialogueCompleted()
    {
        if (State == IntroState.Dialogue) BeginTransition();
    }

    public void SkipIntro() => BeginTransition();

    private void BeginTransition()
    {
        if (State == IntroState.Transition || fadeController == null || !fadeController.CanTransition) return;
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
