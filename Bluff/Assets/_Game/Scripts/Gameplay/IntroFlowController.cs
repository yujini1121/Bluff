using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class IntroFlowController : MonoBehaviour
{
    private const string GameplaySceneName = "Dev_Yujin";

    public enum IntroStage
    {
        NotStarted,
        Dialogue,
        Interaction,
        Finished
    }

    [SerializeField] private DialogueController dialogueController;

    private bool dialogueSubscribed;
    private bool isTransitioning;
    private Action<string> sceneLoader = SceneManager.LoadScene;

    public event Action<IntroStage> StageChanged;

    public IntroStage CurrentStage { get; private set; } =
        IntroStage.NotStarted;

    private void OnEnable()
    {
        SubscribeToDialogue();
    }

    private void Start()
    {
        StartIntro();
    }

    private void OnDisable()
    {
        UnsubscribeFromDialogue();
    }

    public void StartIntro()
    {
        if (isTransitioning || CurrentStage != IntroStage.NotStarted)
        {
            return;
        }

        if (dialogueController == null)
        {
            Debug.LogError(
                "IntroFlowController에 DialogueController가 연결되지 않았습니다.",
                this);
            return;
        }

        SubscribeToDialogue();
        SetStage(IntroStage.Dialogue);
        dialogueController.StartDialogue();
    }

    public void OnInteractionCompleted()
    {
        if (isTransitioning || CurrentStage != IntroStage.Interaction)
        {
            return;
        }

        FinishIntro();
    }

    public void SkipIntro()
    {
        if (isTransitioning)
        {
            return;
        }

        FinishIntro();
    }

    public void FinishIntro()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        SetStage(IntroStage.Finished);
        UnsubscribeFromDialogue();
        sceneLoader(GameplaySceneName);
    }

    private void HandleDialogueCompleted()
    {
        if (isTransitioning || CurrentStage != IntroStage.Dialogue)
        {
            return;
        }

        SetStage(IntroStage.Interaction);
    }

    private void SetStage(IntroStage stage)
    {
        CurrentStage = stage;
        StageChanged?.Invoke(stage);
    }

    private void SubscribeToDialogue()
    {
        if (dialogueController == null || dialogueSubscribed)
        {
            return;
        }

        dialogueController.DialogueCompleted += HandleDialogueCompleted;
        dialogueSubscribed = true;
    }

    private void UnsubscribeFromDialogue()
    {
        if (dialogueController == null || !dialogueSubscribed)
        {
            return;
        }

        dialogueController.DialogueCompleted -= HandleDialogueCompleted;
        dialogueSubscribed = false;
    }
}
