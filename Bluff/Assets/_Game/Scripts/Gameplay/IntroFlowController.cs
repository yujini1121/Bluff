using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class IntroFlowController : MonoBehaviour
{
    private const string GameplaySceneName = "Dev_Yujin";

    public enum IntroStepType
    {
        Cutscene,
        Dialogue,
        Interaction
    }

    [Serializable]
    public sealed class IntroStep
    {
        [SerializeField] private IntroStepType type;
        [SerializeField] private string[] dialogueLines = Array.Empty<string>();

        public IntroStepType Type => type;
        public string[] DialogueLines => dialogueLines;
    }

    [SerializeField] private DialogueController dialogueController;
    [SerializeField] private IntroStep[] steps = Array.Empty<IntroStep>();

    private int currentStepIndex = -1;
    private bool dialogueSubscribed;
    private bool isTransitioning;
    private Action<string> sceneLoader = SceneManager.LoadScene;

    public int CurrentStepIndex => currentStepIndex;
    public IntroStepType? CurrentStepType => HasCurrentStep()
        ? steps[currentStepIndex].Type
        : null;

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
        if (isTransitioning || currentStepIndex >= 0)
        {
            return;
        }

        SubscribeToDialogue();
        currentStepIndex = 0;
        StartCurrentStep();
    }

    public void OnCutsceneCompleted()
    {
        if (isTransitioning ||
            CurrentStepType != IntroStepType.Cutscene)
        {
            return;
        }

        AdvanceStep();
    }

    public void OnInteractionCompleted()
    {
        if (isTransitioning ||
            CurrentStepType != IntroStepType.Interaction)
        {
            return;
        }

        AdvanceStep();
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
        UnsubscribeFromDialogue();
        sceneLoader(GameplaySceneName);
    }

    private void HandleDialogueCompleted()
    {
        if (isTransitioning ||
            CurrentStepType != IntroStepType.Dialogue)
        {
            return;
        }

        AdvanceStep();
    }

    private void StartCurrentStep()
    {
        if (isTransitioning)
        {
            return;
        }

        if (!HasCurrentStep())
        {
            FinishIntro();
            return;
        }

        IntroStep currentStep = steps[currentStepIndex];

        switch (currentStep.Type)
        {
            case IntroStepType.Cutscene:
            case IntroStepType.Interaction:
                return;

            case IntroStepType.Dialogue:
                if (dialogueController == null)
                {
                    Debug.LogError(
                        "Dialogue Step에 DialogueController가 연결되지 않았습니다.",
                        this);
                    return;
                }

                dialogueController.StartDialogue(currentStep.DialogueLines);
                return;
        }
    }

    private void AdvanceStep()
    {
        if (isTransitioning)
        {
            return;
        }

        currentStepIndex++;

        if (HasCurrentStep())
        {
            StartCurrentStep();
            return;
        }

        FinishIntro();
    }

    private bool HasCurrentStep()
    {
        return steps != null &&
               currentStepIndex >= 0 &&
               currentStepIndex < steps.Length &&
               steps[currentStepIndex] != null;
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
