using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueControllerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject testObject;
    private DialogueController controller;
    private TMP_Text dialogueText;
    private int runtimeCompletions;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("DialogueController Test", typeof(RectTransform), typeof(Canvas));
        dialogueText = testObject.AddComponent<TextMeshProUGUI>();
        dialogueText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/DungGeunMo SDF.asset");
        dialogueText.rectTransform.sizeDelta = new Vector2(1000, 300);
        controller = testObject.AddComponent<DialogueController>();
        SetField("dialogueText", dialogueText);
        SetField("dialogueLines", new[] { "First", "Second", "Third" });
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null) Object.DestroyImmediate(testObject);
    }

    [Test]
    public void ThreeLines_FinishTypingThenAdvance_CompleteExactlyOnce()
    {
        int completions = 0;
        controller.DialogueCompleted += () => completions++;
        controller.StartDialogue();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.CurrentLineIndex, Is.EqualTo(i));
            Assert.That(controller.IsTyping, Is.True);
            Assert.That(dialogueText.maxVisibleCharacters, Is.Zero);
            controller.Next();
            Assert.That(controller.CurrentLineIndex, Is.EqualTo(i));
            Assert.That(controller.IsTyping, Is.False);
            Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(dialogueText.textInfo.characterCount));
            Assert.That(completions, Is.Zero);
            controller.Next();
        }
        Assert.That(controller.IsRunning, Is.False);
        Assert.That(completions, Is.EqualTo(1));
        for (int i = 0; i < 20; i++) controller.Next();
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(dialogueText.text, Is.EqualTo("Third"));
    }

    [Test]
    public void Reveal_UsesParsedCharactersAndKeepsFullRichTextAndLayout()
    {
        const string line = "<b>Hi</b> 가 나, Z!";
        controller.StartDialogue(new[] { line });
        Assert.That(dialogueText.textInfo.characterCount, Is.EqualTo(10));
        Assert.That(dialogueText.useMaxVisibleDescender, Is.False);
        Vector2 preferredSize = dialogueText.GetPreferredValues();
        IEnumerator typing = TypingSteps();
        Assert.That(typing.MoveNext(), Is.True);
        for (int visible = 1; visible < 10; visible++)
        {
            Assert.That(typing.MoveNext(), Is.True);
            Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(visible));
            Assert.That(dialogueText.text, Is.EqualTo(line));
            Assert.That(dialogueText.GetPreferredValues(), Is.EqualTo(preferredSize));
        }
        Assert.That(typing.MoveNext(), Is.False);
        Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(10));
        Assert.That(controller.IsTyping, Is.False);
        Assert.That(controller.IsRunning, Is.True, "Natural typing completion still waits for NEXT");
    }

    [TestCase("a,b", 1, 0.16f)]
    [TestCase("a.b", 1, 0.24f)]
    [TestCase("a?b", 1, 0.24f)]
    [TestCase("a!b", 1, 0.24f)]
    [TestCase("a…b", 1, 0.39f)]
    [TestCase("a……b", 1, 0.04f)]
    [TestCase("a……b", 2, 0.39f)]
    [TestCase("a...b", 1, 0.04f)]
    [TestCase("a...b", 2, 0.04f)]
    [TestCase("a...b", 3, 0.39f)]
    [TestCase("a.<b>.</b>.b", 2, 0.04f)]
    [TestCase("a.<b>.</b>.b", 3, 0.39f)]
    [TestCase("a, \t", 1, 0f)]
    [TestCase("a. \n", 1, 0f)]
    [TestCase("a?", 1, 0f)]
    [TestCase("a!", 1, 0f)]
    [TestCase("a…… \t", 2, 0f)]
    [TestCase("a...", 3, 0f)]
    public void PunctuationDelay_IsAppliedOnce_AndNeverAtEnd(string line, int index, float expected)
    {
        controller.StartDialogue(new[] { line });
        float delay = (float)typeof(DialogueController).GetMethod("GetCharacterDelay", PrivateInstance)
            .Invoke(controller, new object[] { index });
        Assert.That(delay, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void MissingClip_IsSilent_AndInstantCompletionDoesNotCreateAudioSource()
    {
        controller.StartDialogue(new[] { "Silent dialogue" });
        IEnumerator typing = TypingSteps();
        while (typing.MoveNext()) { }
        Assert.That(controller.GetComponents<AudioSource>(), Is.Empty);
        controller.StartDialogue(new[] { "Complete me" });
        controller.Next();
        Assert.That(controller.GetComponents<AudioSource>(), Is.Empty);
        Assert.That(controller.IsRunning, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void NewDialogue_CancelsPreviousRoutine_AndCanCompleteSequentially()
    {
        int completions = 0;
        controller.DialogueCompleted += () => completions++;
        controller.StartDialogue(new[] { "Old line" });
        controller.StartDialogue(new[] { "New line" });
        Assert.That(controller.IsTyping, Is.True);
        Assert.That(dialogueText.maxVisibleCharacters, Is.Zero);
        Assert.That(controller.CurrentLine, Is.EqualTo("New line"));
        controller.Next();
        controller.Next();
        Assert.That(completions, Is.EqualTo(1));
        controller.StartDialogue(new[] { "Another line" });
        controller.Next();
        controller.Next();
        Assert.That(completions, Is.EqualTo(2));
    }

    [Test]
    public void DisablingComponentOrObject_CancelsTypingWithoutCompletion()
    {
        int completions = 0;
        controller.DialogueCompleted += () => completions++;
        controller.StartDialogue(new[] { "Interrupted" });
        controller.enabled = false;
        InvokeDisable();
        AssertStopped();
        controller.enabled = true;
        controller.StartDialogue(new[] { "Restarted" });
        testObject.SetActive(false);
        InvokeDisable();
        AssertStopped();
        Object.DestroyImmediate(testObject);
        Assert.That(completions, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void EmptyAndNullLines_AndMissingText_AreSafe()
    {
        int completions = 0;
        controller.DialogueCompleted += () => completions++;
        controller.StartDialogue(null);
        controller.Next();
        Assert.That(completions, Is.EqualTo(1));
        controller.StartDialogue(new string[] { null, "" });
        Assert.That(controller.IsTyping, Is.False);
        controller.Next();
        controller.Next();
        Assert.That(completions, Is.EqualTo(2));
        SetField("dialogueText", null);
        controller.StartDialogue(new[] { "Without a text reference" });
        Assert.That(controller.IsTyping, Is.False);
        controller.Next();
        Assert.That(completions, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator RuntimeTyping_Restart_DuplicateNext_AndLifecycleAreSafe()
    {
        Object.DestroyImmediate(testObject);
        yield return new EnterPlayMode();
        SetUp();
        SetField("characterInterval", 0.1f);
        runtimeCompletions = 0;
        controller.DialogueCompleted += HandleRuntimeCompleted;
        controller.StartDialogue(new[] { "ABCDEFGHIJ", "Last sentence" });
        Assert.That(dialogueText.maxVisibleCharacters, Is.Zero);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (dialogueText.maxVisibleCharacters == 0)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(1));
        controller.Next();
        controller.Next();
        Assert.That(controller.CurrentLineIndex, Is.Zero);
        Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(10));
        yield return null;
        controller.Next();
        Assert.That(controller.CurrentLineIndex, Is.EqualTo(1));
        controller.Next();
        Assert.That(controller.IsTyping, Is.True);
        yield return null;
        controller.Next();
        Assert.That(runtimeCompletions, Is.Zero, "Finishing the last line must not complete the dialogue");
        yield return null;
        controller.Next();
        controller.Next();
        Assert.That(runtimeCompletions, Is.EqualTo(1));

        AudioClip runtimeClip = AudioClip.Create("Runtime dialogue tone", 441, 1, 44100, false);
        try
        {
            SetField("dialogueSound", runtimeClip);
            SetField("charactersPerSound", 1);
            controller.StartDialogue(new[] { "Audio after canvas rebuild" });
            yield return new WaitForSecondsRealtime(0.25f);
            AudioSource runtimeSource = controller.GetComponent<AudioSource>();
            Assert.That(runtimeSource, Is.Not.Null, "Hidden TMP glyphs must still trigger sound when revealed");
            Assert.That(runtimeSource.pitch, Is.InRange(0.95f, 1.05f));
            Assert.That(controller.GetComponents<AudioSource>().Length, Is.EqualTo(1));
            controller.Next();
            SetField("dialogueSound", null);
        }
        finally { Object.DestroyImmediate(runtimeClip); }

        controller.StartDialogue(new[] { "Previous typing task" });
        controller.StartDialogue(new[] { "New" });
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(controller.CurrentLine, Is.EqualTo("New"));
            Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(controller.IsTyping, Is.False);
        }
        finally { Time.timeScale = previousTimeScale; }

        controller.StartDialogue(new[] { "Disable during typing" });
        controller.enabled = false;
        int visible = dialogueText.maxVisibleCharacters;
        yield return new WaitForSecondsRealtime(0.2f);
        AssertStopped();
        Assert.That(dialogueText.maxVisibleCharacters, Is.EqualTo(visible));
        controller.enabled = true;
        controller.StartDialogue(new[] { "Deactivate during typing" });
        testObject.SetActive(false);
        AssertStopped();
        testObject.SetActive(true);
        controller.StartDialogue(new[] { "Destroy during typing" });
        Object.Destroy(testObject);
        yield return null;
        Assert.That(runtimeCompletions, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
        yield return new ExitPlayMode();
    }

    [Test]
    public void OptionalSound_ReusesOneSource_ExcludesPunctuation_AndLimitsPlayback()
    {
        AudioClip clip = AudioClip.Create("Dialogue test tone", 441, 1, 44100, false);
        try
        {
            SetField("dialogueSound", clip);
            controller.StartDialogue(new[] { "abcdefghi" });
            MethodInfo play = typeof(DialogueController).GetMethod("PlayCharacterSound", PrivateInstance);
            play.Invoke(controller, new object[] { new TMP_CharacterInfo { character = ' ', isVisible = false } });
            play.Invoke(controller, new object[] { new TMP_CharacterInfo { character = '!', isVisible = true } });
            Assert.That(controller.GetComponents<AudioSource>(), Is.Empty);
            for (int i = 0; i < 3; i++)
                play.Invoke(controller, new object[] { new TMP_CharacterInfo { character = 'a', isVisible = true } });
            AudioSource source = controller.GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.pitch, Is.InRange(0.95f, 1.05f));
            Assert.That(source.spatialBlend, Is.Zero);
            SetField("nextSoundTime", Time.unscaledTime + 100f);
            float pitch = source.pitch;
            for (int i = 0; i < 6; i++)
                play.Invoke(controller, new object[] { new TMP_CharacterInfo { character = 'b', isVisible = true } });
            Assert.That(source.pitch, Is.EqualTo(pitch));
            Assert.That((float)ReadField("nextSoundTime"), Is.GreaterThan(Time.unscaledTime + 90f));
            SetField("nextSoundTime", float.NegativeInfinity);
            play.Invoke(controller, new object[] { new TMP_CharacterInfo { character = 'c', isVisible = true } });
            Assert.That(controller.GetComponents<AudioSource>().Length, Is.EqualTo(1));
            controller.Next();
            Assert.That(controller.GetComponents<AudioSource>().Length, Is.EqualTo(1));
            Assert.That(ReadField("soundCharacterCount"), Is.EqualTo(0), "NEXT does not emit the remaining sounds");
        }
        finally { Object.DestroyImmediate(clip); }
    }

    private IEnumerator TypingSteps() => (IEnumerator)typeof(DialogueController)
        .GetMethod("TypeCurrentLine", PrivateInstance).Invoke(controller, null);
    private object ReadField(string name) => typeof(DialogueController).GetField(name, PrivateInstance).GetValue(controller);
    private void SetField(string name, object value) => typeof(DialogueController).GetField(name, PrivateInstance).SetValue(controller, value);
    private void InvokeDisable() => typeof(DialogueController).GetMethod("OnDisable", PrivateInstance).Invoke(controller, null);
    private void HandleRuntimeCompleted() => runtimeCompletions++;
    private void AssertStopped()
    {
        Assert.That(controller.IsRunning, Is.False);
        Assert.That(controller.IsTyping, Is.False);
        Assert.That(ReadField("typingRoutine"), Is.Null);
    }
}
