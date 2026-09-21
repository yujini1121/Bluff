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

        SetField(dialogueController, "dialogueLines", new[] { "Dialogue" });
        SetField(flowController, "dialogueController", dialogueController);
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
    public void NormalFlow_AdvancesInOrderAndLoadsGameplayOnce()
    {
        flowController.StartIntro();

        Assert.That(
            flowController.CurrentStage,
            Is.EqualTo(IntroFlowController.IntroStage.Dialogue));
        Assert.That(dialogueController.IsRunning, Is.True);

        dialogueController.Next();

        Assert.That(
            flowController.CurrentStage,
            Is.EqualTo(IntroFlowController.IntroStage.Interaction));

        flowController.OnInteractionCompleted();

        Assert.That(
            flowController.CurrentStage,
            Is.EqualTo(IntroFlowController.IntroStage.Finished));
        Assert.That(loadedSceneName, Is.EqualTo("Dev_Yujin"));
        Assert.That(sceneLoadCount, Is.EqualTo(1));

        flowController.OnInteractionCompleted();
        flowController.FinishIntro();

        Assert.That(sceneLoadCount, Is.EqualTo(1));
    }

    [Test]
    public void InteractionCompletionBeforeDialogueEnds_IsIgnored()
    {
        flowController.StartIntro();

        flowController.OnInteractionCompleted();

        Assert.That(
            flowController.CurrentStage,
            Is.EqualTo(IntroFlowController.IntroStage.Dialogue));
        Assert.That(sceneLoadCount, Is.Zero);
    }

    [Test]
    public void SkipDuringDialogue_LoadsGameplayOnce()
    {
        flowController.StartIntro();

        flowController.SkipIntro();

        Assert.That(
            flowController.CurrentStage,
            Is.EqualTo(IntroFlowController.IntroStage.Finished));
        Assert.That(loadedSceneName, Is.EqualTo("Dev_Yujin"));
        Assert.That(sceneLoadCount, Is.EqualTo(1));

        flowController.SkipIntro();
        flowController.FinishIntro();

        Assert.That(sceneLoadCount, Is.EqualTo(1));
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
