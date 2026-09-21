using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IntroFlowControllerTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject testObject;
    private DialogueController dialogueController;
    private IntroFlowController flowController;
    private int sceneLoadCount;
    private string loadedSceneName;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("IntroFlowController Test");
        dialogueController = testObject.AddComponent<DialogueController>();
        flowController = testObject.AddComponent<IntroFlowController>();

        SetField(flowController, "dialogueController", dialogueController);
        SetField(flowController, "steps", CreateStandardSteps());
        SetField(
            flowController,
            "sceneLoader",
            (Action<string>)(sceneName =>
            {
                sceneLoadCount++;
                loadedSceneName = sceneName;
            }));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(testObject);
    }

    [Test]
    public void StandardSteps_AdvanceInOrderAndLoadGameplayOnce()
    {
        flowController.StartIntro();

        AssertCurrentStep(0, IntroFlowController.IntroStepType.Cutscene);

        flowController.OnInteractionCompleted();
        AssertCurrentStep(0, IntroFlowController.IntroStepType.Cutscene);

        flowController.OnCutsceneCompleted();
        AssertCurrentDialogue(1, "Dialogue A");

        flowController.OnCutsceneCompleted();
        AssertCurrentDialogue(1, "Dialogue A");

        dialogueController.Next();
        AssertCurrentStep(2, IntroFlowController.IntroStepType.Interaction);

        flowController.OnCutsceneCompleted();
        AssertCurrentStep(2, IntroFlowController.IntroStepType.Interaction);

        flowController.OnInteractionCompleted();
        AssertCurrentDialogue(3, "Dialogue B");

        flowController.OnInteractionCompleted();
        AssertCurrentDialogue(3, "Dialogue B");

        dialogueController.Next();
        AssertCurrentStep(4, IntroFlowController.IntroStepType.Interaction);

        flowController.OnInteractionCompleted();

        Assert.That(flowController.CurrentStepIndex, Is.EqualTo(5));
        Assert.That(flowController.CurrentStepType, Is.Null);
        Assert.That(loadedSceneName, Is.EqualTo("Dev_Yujin"));
        Assert.That(sceneLoadCount, Is.EqualTo(1));

        flowController.OnCutsceneCompleted();
        flowController.OnInteractionCompleted();
        flowController.SkipIntro();
        flowController.FinishIntro();

        Assert.That(sceneLoadCount, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void SkipAtAnyConfiguredStep_LoadsGameplayOnce(int stepIndex)
    {
        AdvanceToStep(stepIndex);

        flowController.SkipIntro();

        Assert.That(loadedSceneName, Is.EqualTo("Dev_Yujin"));
        Assert.That(sceneLoadCount, Is.EqualTo(1));

        flowController.SkipIntro();
        flowController.FinishIntro();

        Assert.That(sceneLoadCount, Is.EqualTo(1));
    }

    [Test]
    public void EmptyStepList_FinishesSafely()
    {
        SetField(
            flowController,
            "steps",
            Array.Empty<IntroFlowController.IntroStep>());

        flowController.StartIntro();

        Assert.That(flowController.CurrentStepIndex, Is.Zero);
        Assert.That(flowController.CurrentStepType, Is.Null);
        Assert.That(loadedSceneName, Is.EqualTo("Dev_Yujin"));
        Assert.That(sceneLoadCount, Is.EqualTo(1));

        flowController.SkipIntro();
        Assert.That(sceneLoadCount, Is.EqualTo(1));
    }

    [Test]
    public void EmptyDialogue_ImmediatelyAdvancesToFollowingStep()
    {
        SetField(
            flowController,
            "steps",
            new[]
            {
                CreateStep(IntroFlowController.IntroStepType.Dialogue),
                CreateStep(IntroFlowController.IntroStepType.Interaction)
            });

        flowController.StartIntro();

        AssertCurrentStep(1, IntroFlowController.IntroStepType.Interaction);
        Assert.That(sceneLoadCount, Is.Zero);
    }

    private void AdvanceToStep(int targetStepIndex)
    {
        flowController.StartIntro();

        while (flowController.CurrentStepIndex < targetStepIndex)
        {
            switch (flowController.CurrentStepType)
            {
                case IntroFlowController.IntroStepType.Cutscene:
                    flowController.OnCutsceneCompleted();
                    break;

                case IntroFlowController.IntroStepType.Dialogue:
                    dialogueController.Next();
                    break;

                case IntroFlowController.IntroStepType.Interaction:
                    flowController.OnInteractionCompleted();
                    break;

                default:
                    Assert.Fail("목표 Step에 도달하기 전에 Intro가 종료되었습니다.");
                    break;
            }
        }

        Assert.That(flowController.CurrentStepIndex, Is.EqualTo(targetStepIndex));
    }

    private void AssertCurrentDialogue(int stepIndex, string expectedLine)
    {
        AssertCurrentStep(
            stepIndex,
            IntroFlowController.IntroStepType.Dialogue);
        Assert.That(dialogueController.IsRunning, Is.True);
        Assert.That(dialogueController.CurrentLine, Is.EqualTo(expectedLine));
    }

    private void AssertCurrentStep(
        int stepIndex,
        IntroFlowController.IntroStepType stepType)
    {
        Assert.That(flowController.CurrentStepIndex, Is.EqualTo(stepIndex));
        Assert.That(flowController.CurrentStepType, Is.EqualTo(stepType));
    }

    private static IntroFlowController.IntroStep[] CreateStandardSteps()
    {
        return new[]
        {
            CreateStep(IntroFlowController.IntroStepType.Cutscene),
            CreateStep(
                IntroFlowController.IntroStepType.Dialogue,
                "Dialogue A"),
            CreateStep(IntroFlowController.IntroStepType.Interaction),
            CreateStep(
                IntroFlowController.IntroStepType.Dialogue,
                "Dialogue B"),
            CreateStep(IntroFlowController.IntroStepType.Interaction)
        };
    }

    private static IntroFlowController.IntroStep CreateStep(
        IntroFlowController.IntroStepType type,
        params string[] dialogueLines)
    {
        var step = new IntroFlowController.IntroStep();
        SetField(step, "type", type);
        SetField(step, "dialogueLines", dialogueLines);
        return step;
    }

    private static void SetField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
