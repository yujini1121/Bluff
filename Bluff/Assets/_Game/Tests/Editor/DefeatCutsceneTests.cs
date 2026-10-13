using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DefeatCutsceneTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(GamePhase.RoundEnd, GameWinner.Dealer, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Player, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Draw, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Dealer, false)]
    public void Gate_RejectsNonFinalLossAndUnsettledPresentation(GamePhase phase, GameWinner winner, bool settled)
    {
        var go = new GameObject("Defeat gate");
        try
        {
            var flow = go.AddComponent<DefeatCutsceneController>();
            Assert.That(flow.TryPlay(phase, winner, settled), Is.False);
            Assert.That(flow.HasPlayed, Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MissingSetup_ConsumesAttemptAndAllowsRestart()
    {
        var go = new GameObject("Defeat missing setup");
        try
        {
            var flow = go.AddComponent<DefeatCutsceneController>();
            LogAssert.Expect(LogType.Error, "Defeat cutscene is not configured. Showing the normal final result.");
            Assert.That(flow.TryPlay(GamePhase.GameOver, GameWinner.Dealer, true), Is.False);
            Assert.That(flow.HasPlayed, Is.True);
            flow.ResetForGame();
            Assert.That(flow.HasPlayed, Is.False);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void EditorScrubbing_RecoveryBlackoutAndCaptionFollowAnimation()
    {
        EditorSceneManager.OpenScene(DefeatCutsceneSetupEditor.ScenePath);
        DefeatCutsceneSetupEditor.ValidateOpenScene();
        VictoryCutsceneSetupEditor.ValidateOpenScene();
        var flow = Find<DefeatCutsceneController>();
        var director = flow.GetComponent<PlayableDirector>();
        var volume = Read<Volume>(flow, "effectsVolume");
        var screen = Read<GameObject>(flow, "screenRoot");
        var shade = screen.transform.Find("Blackout").GetComponent<CanvasGroup>();
        var caption = screen.transform.Find("YOU LOST").GetComponent<CanvasGroup>();
        var noise = screen.transform.Find("Digital Noise").GetComponent<CanvasGroup>();
        try
        {
            director.Play();
            director.time = 8.04; director.Evaluate(); Assert.That(volume.weight, Is.GreaterThan(0.9));
            foreach (double t in new[] {8.16, 8.4, 8.64})
            {
                director.time = t; director.Evaluate();
                Assert.That(volume.weight, Is.LessThan(0.001));
                Assert.That(noise.alpha, Is.LessThan(0.001));
                Assert.That(shade.alpha, Is.LessThan(0.001));
            }
            director.time = 8.7; director.Evaluate(); Assert.That(shade.alpha, Is.GreaterThan(0.999));
            director.time = 10; director.Evaluate(); Assert.That(caption.alpha, Is.GreaterThan(0.999));
            director.time = 2; director.Evaluate();
            Assert.That(shade.alpha, Is.LessThan(0.001)); Assert.That(volume.weight, Is.LessThan(0.001));
            Assert.That(flow.HasPlayed, Is.False, "Edit-mode Signals never start gameplay dialogue");
        }
        finally { director.Stop(); EditorSceneManager.OpenScene(DefeatCutsceneSetupEditor.ScenePath); }
    }

    [UnityTest]
    public IEnumerator ActualShowdown_TwoDialogueGates_NaturalEnd_ResultAndRestart()
    {
        EditorSceneManager.OpenScene(DefeatCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        yield return RunShowdown();
        yield return new ExitPlayMode();
    }

    // Create captured-lambda locals after the EnterPlayMode domain reload.
    private static IEnumerator RunShowdown()
    {
        yield return Ready();
        var game = Find<GameplayController>(); var state = Read<GameState>(game, "gameState");
        var presentation = Read<GameplayPresentationController>(game, "presentation");
        var flow = Find<DefeatCutsceneController>();
        var result = Read<GameObject>(Read<GameplayView>(game, "gameplayView"), "resultOverlay");
        Set(state, "<CurrentRound>k__BackingField", GameState.MaximumRoundCount);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(6));
        state.TrySetCommunityCards(new Card(4), new Card(5)); state.Turn.TrySet(TurnOwner.Player);
        Assert.That(state.TryRaise(1), Is.True); Assert.That(state.TryCall(), Is.True);
        presentation.RefreshCards(); presentation.RefreshChips(); game.OnResolveShowdownClicked();
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Dealer)); Assert.That(flow.IsPlaying, Is.False);
        bool sawChips = false, sawCards = false;
        float deadline = Time.realtimeSinceStartup + 25;
        while (!flow.IsPlaying)
        {
            sawChips |= presentation.IsChipAnimating; sawCards |= presentation.IsCardAnimating;
            Assert.That(result.activeSelf, Is.False);
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null;
        }
        Assert.That(sawChips && sawCards, Is.True); Assert.That(presentation.IsBusy, Is.False);
        Assert.That(Find<VictoryCutsceneController>().IsPlaying, Is.False);
        Assert.That(flow.TryPlay(GamePhase.GameOver, GameWinner.Dealer, true), Is.False);
        var director = flow.GetComponent<PlayableDirector>();
        var dialogue = Read<DialogueController>(flow, "dialogue");
        foreach (var firstLine in new[] { "……유감입니다.", "처음에 말씀드렸죠. 패배에도 대가는 있다고." })
        {
            yield return Until(() => flow.IsWaitingForDialogue, 6);
            Assert.That(dialogue.CurrentLine, Is.EqualTo(firstLine));
            double time = director.time;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(director.time, Is.EqualTo(time).Within(0.001)); Assert.That(result.activeSelf, Is.False);
            for (int i = 0; i < 2; i++)
            {
                if (dialogue.IsTyping) { dialogue.Next(); yield return null; }
                dialogue.Next(); yield return null;
            }
            Assert.That(flow.IsWaitingForDialogue, Is.False);
        }
        yield return Until(() => !flow.IsPlaying, 10); yield return null;
        Assert.That(result.activeSelf, Is.True); Assert.That(Find<SwitchCamera>().enabled, Is.True);
        Assert.That(Read<Volume>(flow, "effectsVolume").weight, Is.Zero);
        Assert.That(Read<GameObject>(flow, "screenRoot").activeSelf, Is.False);
        game.OnRestartClicked(); yield return null;
        yield return Until(() => Find<DefeatCutsceneController>() != flow, 6);
        Assert.That(Find<DefeatCutsceneController>().HasPlayed, Is.False);
    }

    [UnityTest]
    public IEnumerator ActualPlayerFold_FinalLoss_SkipAndDisableRestoreEntryAndInputs()
    {
        EditorSceneManager.OpenScene(DefeatCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        yield return RunFold();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunFold()
    {
        yield return Ready();
        var game = Find<GameplayController>(); var state = Read<GameState>(game, "gameState");
        var presentation = Read<GameplayPresentationController>(game, "presentation");
        var flow = Find<DefeatCutsceneController>();
        state.TrySetPlayerCard(new Card(6)); state.TrySetDealerCard(new Card(4));
        state.TrySetCommunityCards(new Card(4), new Card(5)); state.Turn.TrySet(TurnOwner.Player);
        state.PlayerChips.TrySpend(state.PlayerChips.Count - 1); presentation.RefreshChips(); presentation.RefreshCards();
        game.OnFoldClicked();
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Dealer)); Assert.That(flow.IsPlaying, Is.False);
        yield return Until(() => flow.IsPlaying, 25);
        Assert.That(presentation.IsBusy, Is.False);
        flow.Skip(); yield return null;
        Assert.That(Read<GameObject>(Read<GameplayView>(game, "gameplayView"), "resultOverlay").activeSelf, Is.True);
        game.enabled = false;
        var switcher = Find<SwitchCamera>(); var pause = Find<Pause>();
        var rig = Read<CutsceneCameraRig>(flow, "cameraRig");
        var output = Read<Camera>(rig, "outputCamera");
        var entry = Read<CinemachineVirtualCamera>(rig, "virtualCamera");
        foreach (CameraView view in Enum.GetValues(typeof(CameraView)))
        {
            if (view == CameraView.Player) switcher.SwitchToPlayerCam();
            if (view == CameraView.Community) switcher.SwitchToCommunityCam();
            if (view == CameraView.Dealer) switcher.SwitchToDealerCam();
            yield return new WaitForSecondsRealtime(0.15f);
            var position = output.transform.position; var rotation = output.transform.rotation;
            pause.enabled = false;
            flow.ResetForGame(); Assert.That(flow.TryPlay(GamePhase.GameOver, GameWinner.Dealer, true), Is.True);
            output.GetComponent<CinemachineBrain>().ManualUpdate();
            Assert.That(Vector3.Distance(entry.transform.position, position), Is.LessThan(0.001));
            Assert.That(Vector3.Distance(output.transform.position, position), Is.LessThan(0.01));
            Assert.That(Quaternion.Angle(output.transform.rotation, rotation), Is.LessThan(0.01));
            if (view == CameraView.Community) flow.enabled = false; else flow.Skip();
            Assert.That(flow.IsPlaying, Is.False); Assert.That(switcher.enabled, Is.True);
            Assert.That(pause.enabled, Is.False); flow.enabled = true;
        }
        pause.enabled = true;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        SceneManager.sceneLoaded -= Configure;
        GameModeSelection.Select(GameMode.RoundLimited);
        if (UnityEditor.EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    private static IEnumerator Ready()
    {
        GameModeSelection.Select(GameMode.RoundLimited);
        SceneManager.sceneLoaded += Configure; SceneManager.LoadScene("Dev_Yujin"); yield return null; yield return null;
        SceneManager.sceneLoaded -= Configure;
        var game = Find<GameplayController>();
        Set(game, "minDealerThinkDelay", 100f); Set(game, "maxDealerThinkDelay", 100f);
        yield return Until(() => !Read<GameplayPresentationController>(game, "presentation").IsBusy, 20);
        Invoke(game, "CancelDealerAction");
    }
    private static void Configure(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Dev_Yujin") return;
        var game = Find<GameplayController>(); Set(game, "playerStartingChips", 10); Set(game, "dealerStartingChips", 10);
        Set(game, "firstTurn", TurnOwner.Player); Set(game, "minDealerThinkDelay", 100f); Set(game, "maxDealerThinkDelay", 100f);
        Invoke(game, "CreateGame");
    }
    private static IEnumerator Until(Func<bool> condition, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Timed out"); yield return null; }
    }
    private static T Find<T>() where T : Component => Object.FindObjectsOfType<T>(true).Single();
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}

// Runs only when explicitly selected from the menu; survives Enter/Exit Play Mode reloads.
[UnityEditor.InitializeOnLoad]
public static class DefeatCutsceneTestRunner
{
    private const string Running = "Defeat.Tests.Running";
    private static UnityEditor.TestTools.TestRunner.Api.TestRunnerApi api;
    static DefeatCutsceneTestRunner()
    {
        api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        api.RegisterCallbacks(new Results());
    }
    [UnityEditor.MenuItem("Tools/Defeat/Run Focused Tests")]
    public static void Run()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.SessionState.GetBool(Running, false)) return;
        if (SceneManager.GetActiveScene().isDirty)
        { Debug.LogWarning("Save the open scene before running Defeat tests."); return; }
        UnityEditor.SessionState.SetBool(Running, true);
        api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter
        { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode, testNames = new[] { "DefeatCutsceneTests" } }));
    }
    private sealed class Results : UnityEditor.TestTools.TestRunner.Api.ICallbacks
    {
        public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor tests) { }
        public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
        public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }
        public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
        {
            if (!UnityEditor.SessionState.GetBool(Running, false)) return;
            System.IO.Directory.CreateDirectory("Logs/Defeat");
            System.IO.File.WriteAllText("Logs/Defeat/Tests.xml", result.ToXml().OuterXml);
            UnityEditor.SessionState.SetBool(Running, false);
            Debug.Log($"Defeat tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
