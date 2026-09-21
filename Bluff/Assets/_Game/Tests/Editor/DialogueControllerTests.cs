using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class DialogueControllerTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject testObject;
    private DialogueController controller;
    private TMP_Text dialogueText;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject(
            "DialogueController Test",
            typeof(RectTransform));
        dialogueText = testObject.AddComponent<TextMeshProUGUI>();
        controller = testObject.AddComponent<DialogueController>();

        SetField("dialogueText", dialogueText);
        SetField(
            "dialogueLines",
            new[] { "First", "Second", "Third" });
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObject);
    }

    [Test]
    public void ThreeLines_AdvanceThenCompleteExactlyOnce()
    {
        int completionCount = 0;
        controller.DialogueCompleted += () => completionCount++;

        controller.StartDialogue();

        Assert.That(controller.IsRunning, Is.True);
        Assert.That(controller.CurrentLineIndex, Is.Zero);
        Assert.That(dialogueText.text, Is.EqualTo("First"));

        controller.Next();

        Assert.That(controller.CurrentLineIndex, Is.EqualTo(1));
        Assert.That(dialogueText.text, Is.EqualTo("Second"));
        Assert.That(completionCount, Is.Zero);

        controller.Next();

        Assert.That(controller.CurrentLineIndex, Is.EqualTo(2));
        Assert.That(dialogueText.text, Is.EqualTo("Third"));
        Assert.That(completionCount, Is.Zero);

        controller.Next();

        Assert.That(controller.IsRunning, Is.False);
        Assert.That(completionCount, Is.EqualTo(1));

        controller.Next();

        Assert.That(controller.IsRunning, Is.False);
        Assert.That(dialogueText.text, Is.EqualTo("Third"));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    [Test]
    public void DifferentLineSets_CanRunAndCompleteSequentially()
    {
        int completionCount = 0;
        controller.DialogueCompleted += () => completionCount++;

        controller.StartDialogue(new[] { "A1", "A2" });

        Assert.That(dialogueText.text, Is.EqualTo("A1"));
        controller.Next();
        Assert.That(dialogueText.text, Is.EqualTo("A2"));
        controller.Next();
        Assert.That(completionCount, Is.EqualTo(1));

        controller.StartDialogue(new[] { "B1" });

        Assert.That(controller.IsRunning, Is.True);
        Assert.That(controller.CurrentLineIndex, Is.Zero);
        Assert.That(dialogueText.text, Is.EqualTo("B1"));
        controller.Next();
        Assert.That(controller.IsRunning, Is.False);
        Assert.That(completionCount, Is.EqualTo(2));

        controller.Next();
        Assert.That(completionCount, Is.EqualTo(2));
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(DialogueController).GetField(
            fieldName,
            PrivateInstance);

        Assert.That(field, Is.Not.Null);
        field.SetValue(controller, value);
    }
}
