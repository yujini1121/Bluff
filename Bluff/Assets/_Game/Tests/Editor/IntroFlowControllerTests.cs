using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class IntroFlowControllerTests
{
    private GameObject root;
    private IntroFlowController flow;
    private PlayerMove player;
    private DialogueController dialogue;
    private PlayableDirector director;
    private Transform view;
    private Transform path;
    private GameObject prompt;
    private IntroHoldToSkip hold;
    private GameObject skipPrompt;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Intro test");
        root.SetActive(false);
        var playerObject = Child("Player");
        playerObject.AddComponent<Rigidbody>().useGravity = false;
        player = playerObject.AddComponent<PlayerMove>();
        view = Child("View", playerObject.transform).transform;
        var playerCamera = view.gameObject.AddComponent<CinemachineVirtualCamera>();
        Set(player, "cameraHolder", view);
        var output = Child("Output").AddComponent<Camera>();
        director = Child("Bar").AddComponent<PlayableDirector>();
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 2;
        director.playableAsset = timeline;
        director.extrapolationMode = DirectorWrapMode.Hold;
        var rig = director.gameObject.AddComponent<IntroBarSequenceCamera>();
        path = Child("Path").transform;
        path.position = new Vector3(2, 1, 3);
        path.rotation = Quaternion.identity;
        var cutsceneCamera = Child("Cutscene View").AddComponent<CinemachineVirtualCamera>();
        Set(rig, "authoredPath", path);
        Set(rig, "director", director);
        Set(rig, "virtualCamera", cutsceneCamera);
        var dealer = Child("Dealer").transform;
        dealer.position = new Vector3(2, 1, 5);
        dialogue = root.AddComponent<DialogueController>();
        flow = root.AddComponent<IntroFlowController>();
        prompt = Child("E");
        Set(flow, "playerMove", player);
        Set(flow, "playerViewCamera", playerCamera);
        Set(flow, "outputCamera", output);
        Set(flow, "barDirector", director);
        Set(flow, "barCamera", rig);
        Set(flow, "dealerLookTarget", dealer);
        Set(flow, "dialogueController", dialogue);
        Set(flow, "dialogueLines", new[] { "Proposal", "Let's play" });
        Set(flow, "interactionPrompt", prompt);
        hold = director.gameObject.AddComponent<IntroHoldToSkip>();
        skipPrompt = Child("ESC");
        Set(hold, "flow", flow);
        Set(hold, "keycap", skipPrompt);
        Set(flow, "holdToSkip", hold);
        root.SetActive(true);
        flow.StartIntro();
    }

    [TearDown]
    public void TearDown()
    {
        var timeline = director.playableAsset;
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(timeline);
    }

    [Test]
    public void DoorwayAndDealerAreSeparateGates_SequenceReturnsFinalPose()
    {
        Assert.That(player.IsMoveInputEnabled, Is.True);
        Assert.That(flow.TryInteract(), Is.False);
        Assert.That(flow.BeginBarSequence(), Is.True);
        Assert.That(flow.BeginBarSequence(), Is.False);
        Assert.That(player.IsMoveInputEnabled, Is.False);
        Assert.That(dialogue.IsRunning, Is.False);
        director.time = 2;
        Invoke(flow, "Update");
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.DealerInteraction));
        Assert.That(player.IsMoveInputEnabled, Is.True);
        Assert.That(Vector3.Distance(view.position, path.position), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(view.rotation, path.rotation), Is.LessThan(0.001f));
        Assert.That(prompt.activeSelf, Is.True);
        Assert.That(dialogue.IsRunning, Is.False, "No automatic dialogue on arrival");
        Assert.That(flow.TryInteract(), Is.True);
        Assert.That(flow.TryInteract(), Is.False);
        Assert.That(prompt.activeSelf, Is.False);
        Assert.That(player.IsMoveInputEnabled, Is.False);
        Assert.That(dialogue.CurrentLineIndex, Is.Zero);
    }

    [Test]
    public void HoldIsHiddenUntilPressed_ReleaseCancels_AndAvailabilitySurvivesPhases()
    {
        Assert.That(hold.IsAvailable, Is.True);
        Assert.That(skipPrompt.activeSelf, Is.False);
        hold.Tick(true, 0.7f);
        Assert.That(skipPrompt.activeSelf, Is.True);
        float partial = hold.Progress;
        hold.Tick(false, 0.02f);
        Assert.That(hold.Progress, Is.InRange(0.01f, partial - 0.01f));
        hold.Tick(true, 0.7f);
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.Corridor));
        hold.Tick(false, 0.2f);
        Assert.That(hold.Progress, Is.Zero);
        Assert.That(skipPrompt.activeSelf, Is.False);
        flow.BeginBarSequence();
        director.time = 2;
        Invoke(flow, "Update");
        Assert.That(hold.IsAvailable, Is.True);
        Assert.That(skipPrompt.activeSelf, Is.False);
        Assert.That(flow.TryInteract(), Is.True);
        hold.Tick(true, 0.3f);
        Assert.That(skipPrompt.activeSelf, Is.True);
    }

    [Test]
    public void InteractionRequiresFacingDealerAndBeingNearby()
    {
        flow.BeginBarSequence();
        director.time = 2;
        Invoke(flow, "Update");
        view.rotation = Quaternion.Euler(0, 180, 0);
        Assert.That(flow.TryInteract(), Is.False);
        view.rotation = Quaternion.identity;
        view.position = new Vector3(2, 1, -20);
        Assert.That(flow.TryInteract(), Is.False);
    }

    private GameObject Child(string name, Transform parent = null)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent != null ? parent : root.transform);
        return child;
    }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
