using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public sealed class VictoryCutsceneTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator RestoreEditorAfterTest()
    {
        SceneManager.sceneLoaded -= ConfigureTestMatch;
        GameModeSelection.Select(GameMode.RoundLimited);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [TestCase(GamePhase.RoundEnd, GameWinner.Player, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Dealer, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Draw, true)]
    [TestCase(GamePhase.GameOver, GameWinner.None, true)]
    [TestCase(GamePhase.GameOver, GameWinner.Player, false)]
    public void Gate_RejectsNonFinalWinsAndUnfinishedSettlement(GamePhase phase, GameWinner winner, bool settled)
    {
        var root = new GameObject("Victory gate test");
        try
        {
            var flow = root.AddComponent<VictoryCutsceneController>();
            Assert.That(flow.TryPlay(phase, winner, settled), Is.False);
            Assert.That(flow.HasPlayed, Is.False);
            Assert.That(flow.IsPlaying, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void MissingSetup_ConsumesAttemptSoRestartIsNotLocked()
    {
        var root = new GameObject("Missing victory setup");
        try
        {
            var flow = root.AddComponent<VictoryCutsceneController>();
            LogAssert.Expect(LogType.Error, "Victory cutscene is not configured. Showing the normal final result.");
            Assert.That(flow.TryPlay(GamePhase.GameOver, GameWinner.Player, true), Is.False);
            Assert.That(flow.HasPlayed, Is.True);
            Assert.That(flow.IsPlaying, Is.False);
            flow.ResetForGame();
            Assert.That(flow.HasPlayed, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void Scene_HasPlayableBinding_AndPreservesIntroDialogueTuning()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        VictoryCutsceneSetupEditor.ValidateOpenScene();
        var flow = Find<VictoryCutsceneController>();
        var dialogue = Read<DialogueController>(flow, "dialogue");
        var director = flow.GetComponent<PlayableDirector>();
        Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.UnscaledGameTime));
        Assert.That(((TimelineAsset)director.playableAsset).durationMode, Is.EqualTo(TimelineAsset.DurationMode.BasedOnClips));
        Assert.That(director.duration + 1e-9, Is.GreaterThanOrEqualTo(Marker(director,"Finish").time));
        Assert.That(Read<Transform>(flow, "memoryChip").GetComponent<Item>(), Is.Null);
        var intro = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity", OpenSceneMode.Additive);
        try
        {
            var source = intro.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueController>(true)).Single();
            foreach (string name in new[] { "characterInterval", "commaDelay", "sentenceDelay", "ellipsisDelay",
                         "soundVolume", "minimumSoundInterval", "charactersPerSound", "pitchRange",
                         "dialogueSound", "dialogueSfxMixerGroup" })
                Assert.That(Read<object>(dialogue, name), Is.EqualTo(Read<object>(source, name)), name);
        }
        finally { EditorSceneManager.CloseScene(intro, true); }
        var panel = Read<GameObject>(flow, "dialoguePanel");
        var next = panel.GetComponentInChildren<Button>(true);
        Assert.That(next.onClick.GetPersistentTarget(0), Is.EqualTo(dialogue));
        Assert.That(next.onClick.GetPersistentMethodName(0), Is.EqualTo("Next"));
        Assert.That(flow.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1), "Only dialogue NEXT remains");
        var timeline = (UnityEngine.Timeline.TimelineAsset)director.playableAsset;
        var shots = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().ToArray();
        Assert.That(shots.Select(c => ((CinemachineShot)c.asset).VirtualCamera.Resolve(director)).Distinct().Count(), Is.EqualTo(5));
        var memoryCamera = ((CinemachineShot)shots.Single(c=>c.displayName == "Memory Dealer").asset).VirtualCamera.Resolve(director);
        Assert.That(memoryCamera.Follow, Is.Null);
        Assert.That(memoryCamera.LookAt, Is.Null, "Memory placeholder must be a stable authored pose");
        var chip = Read<Transform>(flow,"memoryChip");
        var track = timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>director.GetGenericBinding(t) == chip.parent.GetComponent<Animator>());
        Assert.That(track.GetClips().Count(), Is.EqualTo(3));
        foreach (var clip in track.GetClips())
        {
            var bindings = AnimationUtility.GetCurveBindings(((AnimationPlayableAsset)clip.asset).clip);
            Assert.That(bindings.Count(b=>b.type == typeof(Transform) && b.propertyName.StartsWith("m_LocalPosition")), Is.EqualTo(3));
            Assert.That(bindings.Any(b=>b.type == typeof(MemoryChipGlow) && b.propertyName == "intensity"), Is.True);
        }
    }

    [UnityTest]
    public IEnumerator ActualShowdown_WaitsForCardsAndChips_PlaysAllBeats_ThenRestarts()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        // Create captured-lambda locals after EnterPlayMode's domain reload.
        yield return RunShowdownScenario();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunShowdownScenario()
    {
        yield return LoadReadyGame();
        var game = Find<GameplayController>();
        Assert.That(game, Is.Not.Null, "Gameplay controller exists");
        var state = Read<GameState>(game, "gameState");
        Assert.That(state, Is.Not.Null, "GameState initialized");
        var presentation = Read<GameplayPresentationController>(game, "presentation");
        var flow = Find<VictoryCutsceneController>();
        var view = Read<GameplayView>(game, "gameplayView");
        string authoredLastWords = Read<TMP_Text>(Read<CutsceneOverlay>(flow, "overlay"), "caption").text;
        var result = Read<GameObject>(view, "resultOverlay");
        var restart = Read<Button>(view, "restartButton");
        var switcher = Find<SwitchCamera>();
        Assert.That(flow, Is.Not.Null, "Victory controller exists");
        Assert.That(view, Is.Not.Null, "Gameplay view exists");
        Assert.That(result, Is.Not.Null, "Result overlay exists");
        Assert.That(switcher, Is.Not.Null, "Camera switcher exists");
        Assert.That(Read<CinemachineVirtualCamera>(switcher, "dealerCam"), Is.Not.Null, "Dealer camera exists");
        switcher.SwitchToDealerCam();
        yield return Seconds(1.1f);

        // Use the actual rule engine to reach a final win, then the normal UI entry point.
        SetWinningCards(state);
        state.DealerChips.TrySpend(state.DealerChips.Count - 2);
        state.Turn.TrySet(TurnOwner.Player);
        Assert.That(state.TryRaise(5), Is.True);
        Assert.That(state.TryAllIn(), Is.True);
        presentation.RefreshChips();
        presentation.RefreshCards();
        game.OnResolveShowdownClicked();
        Assert.That(state.Phase, Is.EqualTo(GamePhase.GameOver));
        Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Player));
        Assert.That(flow.IsPlaying, Is.False);
        bool sawCards = false, sawChips = false;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!flow.IsPlaying)
        {
            sawCards |= presentation.IsCardAnimating;
            sawChips |= presentation.IsChipAnimating;
            Assert.That(result.activeSelf, Is.False, "No final result flash before the sequence");
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        Assert.That(sawCards, Is.True);
        Assert.That(sawChips, Is.True);
        Assert.That(presentation.IsBusy, Is.False, "The entire settlement must finish first");
        Assert.That(switcher.enabled, Is.False);
        Assert.That(Find<Pause>().enabled, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(flow.TryPlay(GamePhase.GameOver, GameWinner.Player, true), Is.False);
        int chipsBefore = state.PlayerChips.Count;
        game.OnCallClicked(); game.OnFoldClicked(); game.OnRaiseClicked();
        game.OnNextRoundClicked(); game.OnRestartClicked();
        Assert.That(Read<bool>(game, "isRestarting"), Is.False);
        Assert.That(state.PlayerChips.Count, Is.EqualTo(chipsBefore));
        Assert.That(Read<Coroutine>(game, "dealerActionCoroutine"), Is.Null);

        yield return Until(() => flow.Beat == VictoryCutsceneController.VictoryBeat.Dialogue, 6);
        Capture("02_DealerDialogue");
        var dialogue = Read<DialogueController>(flow, "dialogue");
        var director = flow.GetComponent<PlayableDirector>();
        double pausedTime = director.time;
        Assert.That(pausedTime, Is.InRange(Marker(director,"Dialogue_Start").time, Marker(director,"Dialogue_Start").time + 0.2));
        yield return Seconds(0.7f);
        Assert.That(director.time, Is.EqualTo(pausedTime).Within(0.001), "Wait indefinitely for NEXT");
        Assert.That(director.state, Is.EqualTo(PlayState.Paused));
        Assert.That(result.activeSelf, Is.False);
        Assert.That(dialogue.IsRunning, Is.True);
        Assert.That(dialogue.CurrentLine, Is.EqualTo("……좋습니다."));
        var next = Read<GameObject>(flow, "dialoguePanel").GetComponentInChildren<Button>(true);
        yield return CompleteDialogue(dialogue, next);
        yield return Until(() => director.state == PlayState.Playing && director.time > pausedTime, 2);
        int memoryEvents = 0;
        flow.MemoryPlaybackStarted += () => memoryEvents++;
        var chipTrack = ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>()
            .Single(t=>director.GetGenericBinding(t) == Read<Transform>(flow,"memoryChip").parent.GetComponent<Animator>());
        var revealClip = chipTrack.GetClips().First();
        yield return Until(() => director.time >= revealClip.start + revealClip.duration * 0.8, 3);
        Capture("04_MemoryChipReveal");
        var activateClip = chipTrack.GetClips().Last();
        yield return Until(() => director.time >= activateClip.start + activateClip.duration * 0.3, 4);
        Capture("06_Unlock");
        Assert.That(Read<Transform>(flow, "memoryChip").gameObject.activeSelf, Is.True);
        yield return Until(() => director.time >= Marker(director,"Memory_Playback").time + 0.8, 3);
        Capture("08_MemoryDealerPlaceholder");
        var lastWords = ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AnimationTrack>()
            .Single(t=>director.GetGenericBinding(t) == Read<TMP_Text>(Read<CutsceneOverlay>(flow,"overlay"),"caption").GetComponent<Animator>()).GetClips().Single();
        yield return Until(() => director.time >= lastWords.start + lastWords.duration * 0.15, 3);
        Capture("09_LastWords");
        var overlay = Read<CutsceneOverlay>(flow,"overlay");
        Assert.That(Read<Image>(overlay,"shade").GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.99f));
        Assert.That(Read<TMP_Text>(overlay,"caption").GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.99f));
        Assert.That(Read<TMP_Text>(overlay,"unlockCaption").GetComponent<CanvasGroup>().alpha, Is.LessThan(0.01f));
        Assert.That(Read<TMP_Text>(Read<CutsceneOverlay>(flow, "overlay"), "caption").text,
            Is.EqualTo(authoredLastWords));
        yield return Until(() => !flow.IsPlaying, 5);
        yield return null;
        Capture("10_FinalResult");
        Assert.That(memoryEvents, Is.EqualTo(1));
        AssertResultRestored(game, flow);
        Assert.That(switcher.CurrentView, Is.EqualTo(CameraView.Dealer));
        for (int i = 0; i < 10; i++) { Invoke(game, "RefreshView"); yield return null; }
        Assert.That(flow.IsPlaying, Is.False);
        Assert.That(restart.interactable, Is.True);
        game.OnRestartClicked();
        yield return null;
        yield return Until(() => Find<VictoryCutsceneController>() != flow, 4);
        Assert.That(Find<VictoryCutsceneController>().HasPlayed, Is.False);
        Assert.That(Read<Slider>(SoundSystem.Instance, "MasterSlider").gameObject.scene,
            Is.EqualTo(SceneManager.GetActiveScene()), "Persistent audio must bind the new scene's slider");
        var restartedGame = Find<GameplayController>();
        yield return Until(() => !Read<GameplayPresentationController>(restartedGame, "presentation").IsBusy, 15);
        Assert.That(Read<GameState>(restartedGame, "gameState").Phase, Is.EqualTo(GamePhase.Betting));
    }

    [UnityTest]
    public IEnumerator ActualResults_RoundWin_DealerWin_Draw_AndLimitedFinalWin()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        for (int outcome = 0; outcome < 4; outcome++)
        {
            GameModeSelection.Select(outcome == 0 ? GameMode.Endless : GameMode.RoundLimited);
            yield return LoadReadyGame();
            var game = Find<GameplayController>();
            Assert.That(game, Is.Not.Null, "Gameplay controller exists");
            var state = Read<GameState>(game, "gameState");
            Assert.That(state, Is.Not.Null, "GameState initialized");
            var presentation = Read<GameplayPresentationController>(game, "presentation");
            var flow = Find<VictoryCutsceneController>();
            var view = Read<GameplayView>(game, "gameplayView");
            SetWinningCards(state);
            if (outcome > 0)
            {
                // Exercise the real round-limit settlement at round ten without nine repeated visual deals.
                Set(state, "<CurrentRound>k__BackingField", GameState.MaximumRoundCount);
                if (outcome == 1) { state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(6)); }
                if (outcome == 2) state.TrySetDealerCard(new Card(6));
            }
            state.Turn.TrySet(TurnOwner.Player);
            Assert.That(state.TryRaise(1), Is.True);
            Assert.That(state.TryCall(), Is.True);
            presentation.RefreshChips();
            presentation.RefreshCards();
            game.OnResolveShowdownClicked();
            yield return Until(() => !presentation.IsBusy && Read<Coroutine>(game, "showdownPresentationCoroutine") == null, 20);
            yield return null;
            if (outcome == 3)
            {
                Assert.That(state.PlayerChips.Count > 0 && state.DealerChips.Count > 0, Is.True);
                Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Player));
                Assert.That(flow.IsPlaying, Is.True, "Limited match ends even when both players retain chips");
                flow.Cancel();
            }
            else
            {
                Assert.That(state.Phase, Is.EqualTo(outcome == 0 ? GamePhase.RoundEnd : GamePhase.GameOver));
                Assert.That(flow.HasPlayed, Is.False);
                Assert.That(flow.IsPlaying, Is.False);
                Assert.That(Read<GameObject>(view, "resultOverlay").activeSelf, Is.True);
            }
        }
        GameModeSelection.Select(GameMode.RoundLimited);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualDealerFold_FinalWin_CancelDisableAndSceneUnloadRestoreState()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        GameModeSelection.Select(GameMode.Endless);
        for (int path = 0; path < 3; path++)
        {
            yield return LoadReadyGame();
            var game = Find<GameplayController>();
            Assert.That(game, Is.Not.Null, "Gameplay controller exists");
            var state = Read<GameState>(game, "gameState");
            Assert.That(state, Is.Not.Null, "GameState initialized");
            var presentation = Read<GameplayPresentationController>(game, "presentation");
            var flow = Find<VictoryCutsceneController>();
            SetWinningCards(state);
            // A straight (4/5/6) folds with a penalty. One dealer chip makes this a final win.
            state.TrySetDealerCard(new Card(6));
            state.DealerChips.TrySpend(state.DealerChips.Count - 1);
            presentation.RefreshChips();
            int playerBefore = state.PlayerChips.Count, dealerBefore = state.DealerChips.Count, pot = state.Pot.Amount;
            state.Turn.TrySet(TurnOwner.Dealer);
            Assert.That(state.TryFold(), Is.True);
            Assert.That(state.FinalWinner, Is.EqualTo(GameWinner.Player));
            presentation.PlayFold(TurnOwner.Dealer, pot, state.FoldPenaltyAmount, playerBefore, dealerBefore, pot,
                () => Invoke(game, "CompleteFoldCardReveal"));
            Assert.That(flow.IsPlaying, Is.False);
            yield return Until(() => flow.IsPlaying, 20);
            Assert.That(presentation.IsBusy, Is.False);
            Assert.That(Read<bool>(game, "isFoldResultVisible"), Is.True);
            if (path == 0)
            {
                yield return Until(() => flow.Beat == VictoryCutsceneController.VictoryBeat.Dialogue, 6);
                flow.Cancel();
                yield return null;
                Assert.That(Read<DialogueController>(flow, "dialogue").IsRunning, Is.False);
                AssertResultRestored(game, flow);
                flow.Cancel(); // Repeated cleanup is harmless.
            }
            else if (path == 1)
            {
                game.enabled = false;
                yield return null;
                game.enabled = true;
                yield return null;
                AssertResultRestored(game, flow);
                Assert.That(flow.IsPlaying, Is.False, "Re-enable must not replay the same win");
            }
            else
            {
                SceneManager.LoadScene("Dev_Yujin");
                yield return null;
                yield return null;
                Assert.That(Find<VictoryCutsceneController>().HasPlayed, Is.False);
                Assert.That(Find<SwitchCamera>().enabled, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                var reloadedGame = Find<GameplayController>();
                Set(reloadedGame, "minDealerThinkDelay", 100f);
                Set(reloadedGame, "maxDealerThinkDelay", 100f);
                yield return Until(() => !Read<GameplayPresentationController>(reloadedGame, "presentation").IsBusy, 15);
                Assert.That(Read<GameState>(reloadedGame, "gameState").Phase, Is.EqualTo(GamePhase.Betting));
                Invoke(reloadedGame, "CancelDealerAction");
            }
        }
        GameModeSelection.Select(GameMode.RoundLimited);
        yield return new ExitPlayMode();
    }

    private static void AssertResultRestored(GameplayController game, VictoryCutsceneController flow)
    {
        var view = Read<GameplayView>(game, "gameplayView");
        Assert.That(flow.IsPlaying, Is.False);
        Assert.That(flow.HasPlayed, Is.True);
        Assert.That(Read<GameObject>(view, "resultOverlay").activeSelf, Is.True);
        Assert.That(Read<Button>(view, "restartButton").interactable, Is.True);
        Assert.That(Read<TMP_Text>(view, "resultTitleText").text, Does.Contain("PLAYER WINS"));
        Assert.That(Find<SwitchCamera>().enabled, Is.True);
        Assert.That(Find<Pause>().enabled, Is.True);
        Assert.That(Read<Transform>(flow, "memoryChip").gameObject.activeSelf, Is.False);
        Assert.That(Read<CanvasGroup[]>(flow, "gameplayHud").All(g => g.alpha > 0.99f), Is.True);
    }

    [Test]
    public void Setup_RepeatedlyPreservesAuthoredCamerasClipsAndKeys()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        var flow = Find<VictoryCutsceneController>();
        var director = flow.GetComponent<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var shot = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().Single(c=>c.displayName == "Dealer");
        var camera = ((CinemachineShot)shot.asset).VirtualCamera.Resolve(director);
        var transfer = timeline.GetOutputTracks().OfType<AnimationTrack>().Where(t=>!t.mutedInHierarchy)
            .SelectMany(t=>t.GetClips()).Single(c=>c.displayName.StartsWith("Transfer"));
        var motion = ((AnimationPlayableAsset)transfer.asset).clip;
        var binding = AnimationUtility.GetCurveBindings(motion).First(b=>b.propertyName == "m_LocalPosition.x");
        var curve = AnimationUtility.GetEditorCurve(motion,binding);
        var original = new AnimationCurve(curve.keys);
        var pose = camera.transform.position;
        double duration = shot.duration;
        int count = flow.GetComponentsInChildren<Transform>(true).Length;
        string guid = AssetDatabase.AssetPathToGUID(VictoryCutsceneSetupEditor.TimelinePath);
        try
        {
            camera.transform.position += Vector3.right * 0.123f;
            shot.duration += 0.25;
            curve.MoveKey(2,new Keyframe(curve.keys[2].time,curve.keys[2].value + 0.234f));
            AnimationUtility.SetEditorCurve(motion,binding,curve);
            VictoryCutsceneSetupEditor.SetupOpenScene();
            VictoryCutsceneSetupEditor.SetupOpenScene();
            Assert.That(camera.transform.position,Is.EqualTo(pose + Vector3.right * 0.123f));
            Assert.That(shot.duration,Is.EqualTo(duration + 0.25));
            Assert.That(AnimationUtility.GetEditorCurve(motion,binding).keys[2].value,Is.EqualTo(curve.keys[2].value));
            Assert.That(flow.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
            Assert.That(AssetDatabase.AssetPathToGUID(VictoryCutsceneSetupEditor.TimelinePath),Is.EqualTo(guid));
        }
        finally
        {
            camera.transform.position = pose; shot.duration = duration;
            AnimationUtility.SetEditorCurve(motion,binding,original);
            EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }

    [Test]
    public void EditorPreview_AuthoredCameraKeyAndEffectEditsDriveTheGraph()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        var flow = Find<VictoryCutsceneController>();
        var director = flow.GetComponent<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var shots = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().ToArray();
        var dealerShot = shots.Single(c=>c.displayName == "Dealer");
        var camera = ((CinemachineShot)dealerShot.asset).VirtualCamera.Resolve(director);
        var brain = Read<Camera>(Read<CutsceneCameraRig>(flow,"cameraRig"),"outputCamera").GetComponent<CinemachineBrain>();
        var pose = camera.transform.position;
        double dealerEnd = dealerShot.duration;
        var chip = Read<Transform>(flow,"memoryChip");
        var chipTrack = timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>director.GetGenericBinding(t) == chip.parent.GetComponent<Animator>());
        var transfer = chipTrack.GetClips().Single(c=>c.displayName.StartsWith("Transfer"));
        var motion = ((AnimationPlayableAsset)transfer.asset).clip;
        var binding = AnimationUtility.GetCurveBindings(motion).First(b=>b.propertyName == "m_LocalPosition.x");
        var curve = AnimationUtility.GetEditorCurve(motion,binding);
        var originalCurve = new AnimationCurve(curve.keys);
        var flash = Read<Image>(Read<CutsceneOverlay>(flow,"overlay"),"flash").GetComponent<CanvasGroup>();
        var flashClip = timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>director.GetGenericBinding(t) == flash.GetComponent<Animator>()).GetClips().Single();
        double flashStart = flashClip.start;
        try
        {
            camera.ForceCameraPosition(pose + Vector3.right * 0.2f,camera.transform.rotation);
            // Shortening a Shot changes the native blend without a controller pose override.
            dealerShot.duration -= 0.3;
            curve.MoveKey(2,new Keyframe(curve.keys[2].time,curve.keys[2].value + 0.3f));
            AnimationUtility.SetEditorCurve(motion,binding,curve);
            flashClip.start += 0.5;
            director.Play();
            director.time = dealerShot.start + dealerShot.duration - 0.15;
            director.Evaluate(); brain.ManualUpdate();
            Assert.That(Vector3.Distance(brain.transform.position,pose),Is.GreaterThan(0.05f));
            director.time = transfer.start + curve.keys[2].time;
            director.Evaluate();
            Assert.That(chip.localPosition.x,Is.EqualTo(curve.keys[2].value).Within(0.005f));
            Assert.That(chip.gameObject.activeSelf,Is.True);
            director.time = flashStart + 0.05; director.Evaluate();
            Assert.That(flash.alpha,Is.LessThan(0.01f),"Effect no longer starts at its old time");
            director.time = flashClip.start + 0.05; director.Evaluate();
            Assert.That(flash.alpha,Is.EqualTo(0.8f).Within(0.005f));
            director.time = flashStart + 0.05; director.Evaluate();
            Assert.That(flash.alpha,Is.LessThan(0.01f),"Scrubbing backward also restores the hidden state");
            Assert.That(flow.HasPlayed,Is.False,"Editor preview cannot trigger gameplay gates");
        }
        finally
        {
            director.Stop(); camera.ForceCameraPosition(pose,camera.transform.rotation);
            dealerShot.duration = dealerEnd; flashClip.start = flashStart;
            AnimationUtility.SetEditorCurve(motion,binding,originalCurve);
            EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        }
    }

    [UnityTest]
    public IEnumerator ChangedSignals_PauseResumeMemoryAndFinishFollowAuthoredTimes()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        yield return RunChangedSignals();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunChangedSignals()
    {
        yield return LoadReadyGame();
        var game = Find<GameplayController>();
        game.enabled = false;
        var flow = Find<VictoryCutsceneController>();
        var director = flow.GetComponent<PlayableDirector>();
        var dialogueMarker = Marker(director,"Dialogue_Start");
        var memoryMarker = Marker(director,"Memory_Playback");
        var finishMarker = Marker(director,"Finish");
        double dialogueTime = dialogueMarker.time, memoryTime = memoryMarker.time, finishTime = finishMarker.time;
        int memoryEvents = 0, completions = 0;
        flow.MemoryPlaybackStarted += ()=>memoryEvents++;
        flow.Completed += ()=>completions++;
        try
        {
            dialogueMarker.time = 0.6; memoryMarker.time = 1.1; finishMarker.time = 1.6;
            Assert.That(flow.TryPlay(GamePhase.GameOver,GameWinner.Player,true),Is.True);
            yield return Until(()=>flow.IsWaitingForDialogue,2);
            double pause = director.time;
            Assert.That(pause,Is.InRange(0.6,0.8));
            yield return Seconds(0.8f);
            Assert.That(director.time,Is.EqualTo(pause).Within(0.001));
            Assert.That(memoryEvents,Is.Zero);
            var dialogue = Read<DialogueController>(flow,"dialogue");
            var next = Read<GameObject>(flow,"dialoguePanel").GetComponentInChildren<Button>(true);
            yield return CompleteDialogue(dialogue,next);
            yield return Until(()=>memoryEvents == 1,2);
            Assert.That(director.time,Is.InRange(1.1,1.4));
            yield return Until(()=>!flow.IsPlaying,2);
            Assert.That(completions,Is.EqualTo(1));
            Assert.That(memoryEvents,Is.EqualTo(1));
            flow.ResetForGame();
            Assert.That(flow.HasPlayed,Is.False);
            Assert.That(flow.TryPlay(GamePhase.GameOver,GameWinner.Player,true),Is.True);
            yield return Until(()=>flow.IsWaitingForDialogue,2);
            yield return CompleteDialogue(dialogue,next);
            yield return Until(()=>memoryEvents == 2,2);
            Assert.That(flow.IsPlaying,Is.True);
            flow.ResetForGame(); // Also clean an active presentation, beyond the dialogue gate.
            Assert.That(flow.IsPlaying,Is.False);
            Assert.That(flow.HasPlayed,Is.False);
            Assert.That(completions,Is.EqualTo(2));
            Assert.That(Read<Transform>(flow,"memoryChip").gameObject.activeSelf,Is.False);
            Assert.That(Find<SwitchCamera>().enabled,Is.True);
            Assert.That(flow.TryPlay(GamePhase.GameOver,GameWinner.Player,true),Is.True);
            yield return Until(()=>flow.IsWaitingForDialogue,2);
            flow.Skip();
            Assert.That(director.state,Is.EqualTo(PlayState.Paused));
            Assert.That(flow.IsWaitingForDialogue,Is.False);
            Assert.That(Read<DialogueController>(flow,"dialogue").IsRunning,Is.False);
            Assert.That(completions,Is.EqualTo(3));
            flow.ResetForGame();
            Assert.That(flow.HasPlayed,Is.False);
        }
        finally
        {
            flow.Cancel(); dialogueMarker.time = dialogueTime; memoryMarker.time = memoryTime; finishMarker.time = finishTime;
        }
    }

    [UnityTest]
    public IEnumerator EveryGameplayView_CapturesActualOutputAndRestoresCameraAndInputState()
    {
        EditorSceneManager.OpenScene(VictoryCutsceneSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        yield return RunCameraEntryChecks();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunCameraEntryChecks()
    {
        yield return LoadReadyGame();
        Find<GameplayController>().enabled = false;
        var flow = Find<VictoryCutsceneController>();
        var rig = Read<CutsceneCameraRig>(flow,"cameraRig");
        Assert.That(Read<GameObject>(Read<CutsceneOverlay>(flow,"overlay"),"uiRoot").activeSelf,Is.False,
            "The preview Canvas cannot intercept gameplay input before a cutscene");
        var output = Read<Camera>(rig,"outputCamera");
        var entry = Read<CinemachineVirtualCamera>(rig,"virtualCamera");
        var shots = Read<CinemachineVirtualCamera[]>(rig,"shotCameras");
        var switcher = Find<SwitchCamera>();
        var pause = Find<Pause>();
        foreach (CameraView view in Enum.GetValues(typeof(CameraView)))
        {
            switcher.enabled = true;
            if(view == CameraView.Player) switcher.SwitchToPlayerCam();
            if(view == CameraView.Community) switcher.SwitchToCommunityCam();
            if(view == CameraView.Dealer) switcher.SwitchToDealerCam();
            yield return Seconds(0.2f); // Deliberately capture a gameplay blend in progress.
            var position = output.transform.position; var rotation = output.transform.rotation;
            float fov = output.fieldOfView;
            var priorities = shots.Select(c=>c.Priority).ToArray();
            var active = shots.Select(c=>c.gameObject.activeSelf).ToArray();
            var enabled = shots.Select(c=>c.enabled).ToArray();
            pause.enabled = false;
            flow.ResetForGame();
            Assert.That(flow.TryPlay(GamePhase.GameOver,GameWinner.Player,true),Is.True);
            Assert.That(Vector3.Distance(entry.transform.position,position),Is.LessThan(0.001));
            Assert.That(Quaternion.Angle(entry.transform.rotation,rotation),Is.LessThan(0.001));
            Assert.That(entry.m_Lens.FieldOfView,Is.EqualTo(fov).Within(0.001));
            output.GetComponent<CinemachineBrain>().ManualUpdate();
            Assert.That(Vector3.Distance(output.transform.position,position),Is.LessThan(0.01),
                $"No entry jump ({view}, active={output.GetComponent<CinemachineBrain>().ActiveVirtualCamera?.Name}, director={directorState(flow)})");
            yield return null;
            flow.Cancel();
            Assert.That(switcher.enabled,Is.True);
            Assert.That(pause.enabled,Is.False,"Previously disabled input stays disabled");
            Assert.That(shots.Select(c=>c.Priority),Is.EqualTo(priorities));
            Assert.That(shots.Select(c=>c.enabled),Is.EqualTo(enabled));
            Assert.That(shots.Select(c=>c.gameObject.activeSelf),Is.EqualTo(active));
            Assert.That(switcher.CurrentView,Is.EqualTo(view));
        }
        pause.enabled = true;
    }

    private static string directorState(VictoryCutsceneController flow) => flow.GetComponent<PlayableDirector>().state.ToString();

    private static SignalEmitter Marker(PlayableDirector director,string name) => ((TimelineAsset)director.playableAsset)
        .GetOutputTracks().OfType<SignalTrack>().SelectMany(t=>t.GetMarkers()).OfType<SignalEmitter>().Single(m=>m.asset.name == name);

    private static IEnumerator LoadReadyGame()
    {
        SceneManager.sceneLoaded -= ConfigureTestMatch;
        SceneManager.sceneLoaded += ConfigureTestMatch;
        SceneManager.LoadScene("Dev_Yujin");
        yield return null;
        yield return null;
        var game = Find<GameplayController>();
        Set(game, "minDealerThinkDelay", 100f);
        Set(game, "maxDealerThinkDelay", 100f);
        yield return Until(() => !Read<GameplayPresentationController>(game, "presentation").IsBusy, 15);
        Invoke(game, "CancelDealerAction");
    }

    private static void ConfigureTestMatch(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Dev_Yujin") return;
        // Keep artist/debug Inspector chip counts untouched; prepare a repeatable test match
        // after Awake and before Start begins the first real card/chip presentation.
        var game = Find<GameplayController>();
        Set(game, "playerStartingChips", 10);
        Set(game, "dealerStartingChips", 10);
        Set(game, "firstTurn", TurnOwner.Player);
        Set(game, "minDealerThinkDelay", 100f);
        Set(game, "maxDealerThinkDelay", 100f);
        Invoke(game, "CreateGame");
    }

    private static void SetWinningCards(GameState state)
    {
        state.TrySetPlayerCard(new Card(6));
        state.TrySetDealerCard(new Card(4));
        state.TrySetCommunityCards(new Card(4), new Card(5));
    }

    private static IEnumerator CompleteDialogue(DialogueController dialogue, Button next)
    {
        for (int line = 0; line < 3; line++)
        {
            Assert.That(dialogue.CurrentLineIndex, Is.EqualTo(line));
            if (dialogue.IsTyping) { next.onClick.Invoke(); yield return null; }
            Capture("03_DialogueLine" + (line + 1));
            next.onClick.Invoke();
            yield return null;
        }
        Assert.That(dialogue.IsRunning, Is.False);
    }

    private static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Timed out"); yield return null; }
    }
    private static IEnumerator Seconds(float seconds) { yield return new WaitForSecondsRealtime(seconds); }
    private static T Find<T>() where T : Component => Object.FindObjectsOfType<T>(true).Single();
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Logs/Victory/Frames");
        Camera camera = Camera.main;
        var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active, previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        try
        {
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.05f;
            }
            camera.targetTexture = texture;
            camera.aspect = 1280f / 720f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/Victory/Frames/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            Object.Destroy(texture); Object.Destroy(pixels);
        }
    }
}

[UnityEditor.InitializeOnLoad]
public static class VictoryCutsceneTestRunner
{
    private const string Running = "VictoryTimeline.TestRun";
    private const string Destination = "VictoryTimeline.TestOutput";
    private static UnityEditor.TestTools.TestRunner.Api.TestRunnerApi api;
    static VictoryCutsceneTestRunner()
    {
        api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        if (SessionState.GetBool("VictoryTimeline.Isolated",false))
        {
            SessionState.SetBool("VictoryTimeline.Isolated",false);
            EditorApplication.delayCall += RunIsolatedGameplay;
        }
    }
    [MenuItem("Tools/Victory/Run Victory Regression Tests")]
    public static void RunVictory() => Run(new[] { "VictoryCutsceneTests" }, "Victory");
    [MenuItem("Tools/Victory/Run Gameplay Regression Tests")]
    public static void RunRegression()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            SessionState.GetBool(Running, false)) return;
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("Save the open scene before running gameplay regressions.");
            return;
        }
        // A fresh domain avoids destroyed singletons/tweens left by tests entering Play Mode.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("VictoryTimeline.Isolated", true);
        EditorUtility.RequestScriptReload();
    }
    private static void RunIsolatedGameplay() => Run(new[] { "CoreStateTests",
        "BettingActionTests", "DealerAiTests", "DealerItemAiTests", "DealerItemFlowTests", "FoldPenaltyTests",
        "GameModeTests", "GameOverTests", "ItemGameApiTests", "ItemSystemTests", "RoundFirstTurnTests",
        "RoundStartTests", "RoundTransitionTests", "ShowdownResultTests", "ShowdownSettlementTests",
        "PlayerItemPresentationTests", "RefreshCardVisualTests" }, "GameplayIsolated");
    private static void Run(string[] names,string output)
    {
        if (SessionState.GetBool(Running, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Running,true);
        SessionState.SetString(Destination,"Logs/VictoryTimeline/" + output + ".xml");
        api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter
            { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode, testNames = names }));
    }
    private sealed class Results : UnityEditor.TestTools.TestRunner.Api.ICallbacks
    {
        public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor testsToRun) { }
        public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
        public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }
        public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Running,false)) return;
            Directory.CreateDirectory("Logs/VictoryTimeline");
            File.WriteAllText(SessionState.GetString(Destination,"Logs/VictoryTimeline/Tests.xml"),result.ToXml().OuterXml);
            SessionState.SetBool(Running,false);
            Debug.Log($"Victory test run: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
