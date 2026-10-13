using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Michsky.UI.Shift;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GameplayUiSceneTests
{
    private const string ScenePath = "Assets/_Game/Scenes/Dev_Yujin.unity";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (!Application.isPlaying)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void Scene_BettingAndProgressEventsTargetItsOwnController()
    {
        OpenScene();
        var view = Find<GameplayView>();
        var controller = Find<GameplayController>();
        Assert.That(view.HasReferences(), Is.True);
        AssertEvent(Read<Slider>(view, "raiseSlider").onValueChanged,
            controller, nameof(GameplayController.OnRaiseSliderChanged));
        AssertEvent(Read<Button>(view, "raiseExecuteButton").onClick,
            controller, nameof(GameplayController.OnRaiseClicked));
        foreach (string method in new[] { "OnCallClicked", "OnFoldClicked" })
            Assert.That(Read<Button[]>(view, "bettingActionButtons").Any(button =>
                HasEvent(button.onClick, controller, method)), Is.True, method);
        foreach (var pair in new[] {
            ("resolveShowdownButton", "OnResolveShowdownClicked"),
            ("nextRoundButton", "OnNextRoundClicked"),
            ("restartButton", "OnRestartClicked") })
            AssertEvent(Read<Button>(view, pair.Item1).onClick, controller, pair.Item2);
        Assert.That(Read<int>(controller, "playerStartingChips"), Is.EqualTo(20));
        Assert.That(Read<TurnOwner>(controller, "firstTurn"), Is.EqualTo(TurnOwner.Player));
        Assert.That(Read<VictoryCutsceneController>(controller, "victoryCutscene"), Is.Not.Null);
        Assert.That(Read<DefeatCutsceneController>(controller, "defeatCutscene"), Is.Not.Null);
        Assert.That(Read<VictoryCutsceneController>(controller, "victoryCutscene").IsConfigured, Is.True);
        Assert.That(Read<DefeatCutsceneController>(controller, "defeatCutscene").IsConfigured, Is.True);
    }

    [Test]
    public void Scene_SettingsSlidersBelongToTheNewPauseMenu()
    {
        OpenScene();
        var menu = Read<GameObject>(Find<Pause>(), "pauseMenu");
        Assert.That(menu.name, Is.EqualTo("Settings"));
        foreach (string field in new[] { "MasterSlider", "BGMVolumeSlider", "SFXVolumeSlider" })
        {
            var slider = Read<Slider>(Find<SoundSystem>(), field);
            Assert.That(slider, Is.Not.Null, field);
            Assert.That(slider.transform.IsChildOf(menu.transform), Is.True, field);
        }
    }

    [Test]
    public void Scene_HandRankAndTutorialButtonsToggleTheirPanels()
    {
        OpenScene();
        var root = Find<GameplayView>().transform.Find("GameRoot");
        foreach (var pair in new[] { ("Hand Rank Button", "Hand Rank Area"), ("Tutorial Button", "Tutorial Area") })
        {
            var button = root.Find(pair.Item1).GetComponent<Button>();
            var panel = root.Find(pair.Item2).gameObject;
            Assert.That(panel.activeSelf, Is.False);
            InvokeInEditor(button.onClick);
            Assert.That(panel.activeSelf, Is.True);
            InvokeInEditor(button.onClick);
            Assert.That(panel.activeSelf, Is.False);
        }
        var tutorial = root.Find("Tutorial Area").GetComponent<Tutorial>();
        var steps = Read<GameObject[]>(tutorial, "tutorialSteps");
        InvokeInEditor(Read<Button>(tutorial, "nextButton").onClick);
        Assert.That(steps[0].activeSelf, Is.False);
        Assert.That(steps[1].activeSelf, Is.True);
        InvokeInEditor(Read<Button>(tutorial, "prevButton").onClick);
        Assert.That(steps[0].activeSelf, Is.True);
        Assert.That(steps[1].activeSelf, Is.False);
    }

    [Test]
    public void View_RefreshUsesTheMergedSliderAndRaiseButton()
    {
        OpenScene();
        var view = Find<GameplayView>();
        var slider = Read<Slider>(view, "raiseSlider");
        slider.maxValue = 10;
        slider.SetValueWithoutNotify(1);
        view.Initialize();
        view.RefreshBetting(true, true, true, false, 4, 10);
        Assert.That(slider.value, Is.EqualTo(4));
        Assert.That(Read<Button>(view, "raiseExecuteButton").GetComponent<MainButton>().buttonText,
            Is.EqualTo("Raise (+4)"));
    }

    [Test]
    public void View_RaiseAllInAndTurnLocksUseTheMergedBehaviour()
    {
        OpenScene();
        var view = Find<GameplayView>();
        view.Initialize();
        view.RefreshBetting(true, true, true, true, 5, 5);
        var call = Read<Button[]>(view, "bettingActionButtons").Single(button =>
            HasEvent(button.onClick, Find<GameplayController>(), "OnCallClicked"));
        Assert.That(Read<Button>(view, "raiseExecuteButton").GetComponent<MainButton>().buttonText,
            Is.EqualTo("All in"));
        view.RefreshBetting(true, true, false, false, 3, 5);
        Assert.That(call.interactable, Is.False);
        Assert.That(Read<Button>(view, "raiseExecuteButton").GetComponent<MainButton>().buttonText,
            Is.EqualTo("Raise (+3)"));
        view.RefreshBetting(false, false, false, false, 0, 0);
        Assert.That(Read<GameObject>(view, "playerActionBar").activeSelf, Is.False);
        Assert.That(Read<Slider>(view, "raiseSlider").interactable, Is.False);
        Assert.That(Read<Button>(view, "raiseExecuteButton").interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator ActualScene_SliderRaiseDealerResponseAndCallReachShowdown()
    {
        OpenScene();
        DelayDealer();
        yield return new EnterPlayMode();
        yield return RunRaiseAndCall();
        yield return new ExitPlayMode();
    }

    // Keep lambda captures inside a scope created after the Play Mode domain reload.
    private static IEnumerator RunRaiseAndCall()
    {
        var controller = Find<GameplayController>();
        var view = Find<GameplayView>();
        yield return WaitUntil(() => CanBet(controller), "Initial card and chip presentation");
        Capture("01-Actions");
        var game = Read<GameState>(controller, "gameState");
        var slider = Read<Slider>(view, "raiseSlider");
        int chips = game.PlayerChips.Count;
        slider.value = 3.4f;
        Assert.That(Read<int>(controller, "selectedRaiseAmount"), Is.EqualTo(3));
        Assert.That(slider.value, Is.EqualTo(3));
        Assert.That(game.PlayerChips.Count, Is.EqualTo(chips), "Selection must not spend chips");
        yield return null;
        Capture("02-RaiseSelection");
        Read<Button>(view, "raiseExecuteButton").onClick.Invoke();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(chips - 3));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(Read<GameObject>(view, "playerActionBar").activeSelf, Is.False);
        Read<Button>(view, "raiseExecuteButton").onClick.Invoke();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(chips - 3), "Duplicate input must be blocked");
        var presentation = Find<GameplayPresentationController>();
        yield return WaitUntil(() => !presentation.IsBusy, "Player raise chip presentation");
        Invoke(controller, "CancelDealerAction");
        Assert.That(game.TryRaise(2), Is.True, "Controlled dealer response");
        Invoke(controller, "RefreshView");
        var call = Read<Button[]>(view, "bettingActionButtons").Single(button =>
            HasEvent(button.onClick, controller, "OnCallClicked"));
        Assert.That(call.interactable, Is.True);
        call.onClick.Invoke();
        Assert.That(game.PlayerChips.Count, Is.EqualTo(chips - 5));
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Showdown));
        yield return WaitUntil(() => !presentation.IsBusy, "Call chip presentation");
        Assert.That(Read<Button>(view, "resolveShowdownButton").gameObject.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator ActualScene_FoldResultAndNextRoundWorkWithNewButtons()
    {
        OpenScene();
        DelayDealer();
        yield return new EnterPlayMode();
        yield return RunFoldAndNextRound();
        yield return new ExitPlayMode();
    }

    private static IEnumerator RunFoldAndNextRound()
    {
        var controller = Find<GameplayController>();
        var view = Find<GameplayView>();
        yield return WaitUntil(() => CanBet(controller), "Initial round presentation");
        var fold = Read<Button[]>(view, "bettingActionButtons").Single(button =>
            HasEvent(button.onClick, controller, "OnFoldClicked"));
        fold.onClick.Invoke();
        var game = Read<GameState>(controller, "gameState");
        Assert.That(game.Phase, Is.EqualTo(GamePhase.RoundEnd));
        var next = Read<Button>(view, "nextRoundButton");
        yield return WaitUntil(() => next.interactable, "Fold reveal presentation");
        Assert.That(Read<GameObject>(view, "resultOverlay").activeSelf, Is.True);
        Capture("03-FoldResult");
        next.onClick.Invoke();
        Assert.That(game.CurrentRound, Is.EqualTo(2));
        Assert.That(Read<GameObject>(view, "resultOverlay").activeSelf, Is.False);
        yield return WaitUntil(() => !Find<GameplayPresentationController>().IsBusy, "Next round presentation");
    }

    private static void OpenScene() => EditorSceneManager.OpenScene(ScenePath);
    private static T Find<T>() where T : Object => Object.FindObjectOfType<T>(true);
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Hidden).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static object Invoke(object owner, string method) => owner.GetType().GetMethod(method, Hidden).Invoke(owner, null);
    private static bool CanBet(GameplayController controller) => (bool)Invoke(controller, "CanAcceptPlayerBettingInput");
    private static void DelayDealer()
    {
        Set(Find<GameplayController>(), "minDealerThinkDelay", 60f);
        Set(Find<GameplayController>(), "maxDealerThinkDelay", 60f);
    }
    private static IEnumerator WaitUntil(Func<bool> condition, string context)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), context);
            yield return null;
        }
    }
    private static bool HasEvent(UnityEventBase events, Object target, string method) =>
        Enumerable.Range(0, events.GetPersistentEventCount()).Any(index =>
            events.GetPersistentTarget(index) == target && events.GetPersistentMethodName(index) == method);
    private static void AssertEvent(UnityEventBase events, Object target, string method) =>
        Assert.That(HasEvent(events, target, method), Is.True, method);

    private static void InvokeInEditor(UnityEvent events)
    {
        for (int index = 0; index < events.GetPersistentEventCount(); index++)
            events.SetPersistentListenerState(index, UnityEventCallState.EditorAndRuntime);
        events.Invoke();
    }

    private static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Logs/UIParity/Frames");
        var camera = Camera.main;
        var canvases = Object.FindObjectsOfType<Canvas>()
            .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var canvasState = canvases.Select(canvas => (canvas.worldCamera, canvas.planeDistance)).ToArray();
        var texture = new RenderTexture(1920, 1080, 24);
        var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
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
            camera.aspect = 1920f / 1080f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/UIParity/Frames/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            for (int index = 0; index < canvases.Length; index++)
            {
                canvases[index].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[index].worldCamera = canvasState[index].worldCamera;
                canvases[index].planeDistance = canvasState[index].planeDistance;
            }
            Object.Destroy(texture);
            Object.Destroy(pixels);
        }
    }
}
