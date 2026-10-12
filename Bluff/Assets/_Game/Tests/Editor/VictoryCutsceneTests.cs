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
        Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
        Assert.That(director.duration, Is.EqualTo(14d).Within(0.01));
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
        var track = timeline.GetOutputTracks().OfType<UnityEngine.Timeline.AnimationTrack>().Single();
        var motion = ((UnityEngine.Timeline.AnimationPlayableAsset)track.GetClips().Single().asset).clip;
        foreach (var binding in AnimationUtility.GetCurveBindings(motion))
        {
            var curve = AnimationUtility.GetEditorCurve(motion, binding);
            for (float time = 9.6f; time <= 10.4f; time += 0.1f)
                Assert.That(curve.Evaluate(time), Is.EqualTo(curve.Evaluate(9.6f)).Within(0.0001f),
                    "Dealer camera must stay still: " + binding.propertyName);
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
        yield return Seconds(0.7f);
        Assert.That(director.time, Is.EqualTo(4d).Within(0.001), "Wait indefinitely for NEXT");
        Assert.That(result.activeSelf, Is.False);
        Assert.That(dialogue.IsRunning, Is.True);
        Assert.That(dialogue.CurrentLine, Is.EqualTo("……좋습니다."));
        var next = Read<GameObject>(flow, "dialoguePanel").GetComponentInChildren<Button>(true);
        yield return CompleteDialogue(dialogue, next);
        int memoryEvents = 0;
        flow.MemoryPlaybackStarted += () => memoryEvents++;
        yield return Until(() => director.time >= 5.8d, 3);
        Capture("04_MemoryChipReveal");
        yield return Until(() => director.time >= 8.5d, 4);
        Capture("06_Unlock");
        Assert.That(Read<Transform>(flow, "memoryChip").gameObject.activeSelf, Is.True);
        yield return Until(() => director.time >= 9.8d, 3);
        Capture("08_MemoryDealerPlaceholder");
        yield return Until(() => director.time >= 11.4d, 3);
        Capture("09_LastWords");
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
