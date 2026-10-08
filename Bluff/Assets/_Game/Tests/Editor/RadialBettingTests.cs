using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RadialBettingTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(174, 1)]
    [TestCase(135, 6)]
    [TestCase(96, 11)]
    [TestCase(180, 1)]
    [TestCase(90, 11)]
    public void ArcMapping_EndpointsMidpointAndOutsideClamp(float angle, int expected)
    {
        Assert.That(BettingArcSlider.ValueAtPoint(BettingArcGraphic.Point(angle, 302), 1, 11), Is.EqualTo(expected));
    }

    [Test]
    public void ArcMapping_SingleChipAndLargeRangesStayInBounds()
    {
        Assert.That(BettingArcSlider.ValueAtPoint(new Vector2(-1, 1), 1, 1), Is.EqualTo(1));
        Assert.That(BettingArcSlider.ValueAtPoint(new Vector2(0, 1), 1, int.MaxValue), Is.EqualTo(int.MaxValue));
        for (int angle = 90; angle <= 180; angle++)
            Assert.That(BettingArcSlider.ValueAtPoint(BettingArcGraphic.Point(angle, 100), 1, 20), Is.InRange(1, 20));
    }

    [UnityTest]
    public IEnumerator ActualScene_SelectionCancelConfirmAndTurnLock()
    {
        EditorSceneManager.OpenScene(RadialBettingSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        var controller = Object.FindObjectOfType<GameplayController>();
        Assert.That(controller, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 25;
        while (!(bool)Invoke(controller, "CanAcceptPlayerBettingInput") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That((bool)Invoke(controller, "CanAcceptPlayerBettingInput"), Is.True, "Player can act after deal presentation");
        var ui = Object.FindObjectOfType<RadialBettingView>();
        Assert.That(ui, Is.Not.Null);
        Assert.That(Read<GameObject>(Read<GameplayView>(controller, "gameplayView"), "playerActionBar").activeSelf, Is.False);
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .Single(r => r.name == "Player Test Area").activeSelf, Is.False);
        var game = Read<GameState>(controller, "gameState");
        // Establish the requested concrete example through the real betting model.
        game.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(game.TryRaise(4), Is.True);
        Invoke(controller, "RefreshView");
        int before = game.PlayerChips.Count;
        int pot = game.Pot.Amount;
        Capture("01_Actions", 1920, 1080);
        Click(ui, "raiseButton");
        Click(ui, "confirmButton"); // Must be ignored during transition.
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before));
        ui.Back(); // Rapid cancellation interrupts the opening tween.
        yield return Seconds(.3f);
        Assert.That(ui.IsChoosingRaise, Is.False);
        Assert.That(Read<RadialBettingAnimator>(ui, "animator").IsTransitioning, Is.False);
        Click(ui, "raiseButton");
        yield return Seconds(.3f);
        var slider = Read<BettingArcSlider>(ui, "slider");
        controller.OnRaiseAmountSelected(3);
        Assert.That(Read<TMP_Text>(ui, "totalText").text, Is.EqualTo("TOTAL  7  CHIPS"));
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before));
        Assert.That(game.Pot.Amount, Is.EqualTo(pot));
        slider.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up });
        Assert.That(slider.Value, Is.EqualTo(4));
        slider.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down });
        Assert.That(slider.Value, Is.EqualTo(3));
        Click(ui, "maxButton");
        Assert.That(slider.Value, Is.EqualTo(before - 4));
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before));
        // Drag through the actual EventSystem coordinate path to select +3.
        float t = 2f / (before - 5);
        var world = slider.transform.TransformPoint(BettingArcGraphic.Point(Mathf.Lerp(174, 96, t), 302));
        var pointer = new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(null, world) };
        slider.OnPointerDown(pointer);
        slider.OnDrag(pointer);
        slider.OnPointerUp(pointer);
        Assert.That(slider.Value, Is.EqualTo(3));
        Capture("02_Raise", 1920, 1080);
        Capture("03_Raise_4x3", 1024, 768);
        Capture("04_Raise_Ultrawide", 2560, 1080);
        Click(ui, "backButton");
        yield return Seconds(.3f);
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before));
        Click(ui, "raiseButton");
        ui.gameObject.SetActive(false);
        ui.gameObject.SetActive(true);
        Invoke(controller, "RefreshView");
        Assert.That(ui.IsChoosingRaise, Is.False);
        Assert.That(Read<RadialBettingAnimator>(ui, "animator").IsTransitioning, Is.False);
        Click(ui, "raiseButton");
        yield return Seconds(.3f);
        Click(ui, "confirmButton");
        Click(ui, "confirmButton");
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before - 7));
        Assert.That(game.Pot.Amount, Is.EqualTo(pot + 7));
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(ui.gameObject.activeSelf, Is.False);
        Assert.That(ui.IsChoosingRaise, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualScene_LiveLimitsPauseFoldNextRoundAndShortAllIn()
    {
        EditorSceneManager.OpenScene(RadialBettingSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        var controller = Object.FindObjectOfType<GameplayController>();
        yield return WaitForInput(controller);
        var ui = Object.FindObjectOfType<RadialBettingView>();
        var game = Read<GameState>(controller, "gameState");
        int before = game.PlayerChips.Count;
        Click(ui, "raiseButton");
        yield return Seconds(.3f);
        Click(ui, "maxButton");
        game.PlayerChips.TrySpend(before - 2);
        Invoke(controller, "RefreshView");
        Assert.That(Read<BettingArcSlider>(ui, "slider").Value, Is.EqualTo(2));
        Assert.That(Read<TMP_Text>(ui, "totalText").text, Is.EqualTo("TOTAL  2  CHIPS"));
        Time.timeScale = 0;
        Invoke(controller, "RefreshView");
        Click(ui, "confirmButton");
        Assert.That(game.PlayerChips.Count, Is.EqualTo(2));
        Assert.That(ui.IsChoosingRaise, Is.False);
        Time.timeScale = 1;
        game.PlayerChips.TryAdd(before - 2);
        int dealerChips = game.DealerChips.Count;
        game.DealerChips.TrySpend(dealerChips);
        Invoke(controller, "RefreshView");
        Assert.That(Read<Button>(ui, "raiseButton").interactable, Is.False);
        Click(ui, "raiseButton");
        Assert.That(ui.IsChoosingRaise, Is.False);
        game.DealerChips.TryAdd(dealerChips);
        Invoke(controller, "RefreshView");
        Click(ui, "foldButton");
        Assert.That(game.FoldedBy, Is.EqualTo(TurnOwner.Player));
        Assert.That(ui.gameObject.activeSelf, Is.False);
        var view = Read<GameplayView>(controller, "gameplayView");
        yield return WaitForNextRound(view);
        // Keep this review deterministic while testing the actual next-round path.
        typeof(GameplayController).GetField("minDealerThinkDelay", Hidden).SetValue(controller, 50f);
        typeof(GameplayController).GetField("maxDealerThinkDelay", Hidden).SetValue(controller, 50f);
        int round = game.CurrentRound;
        controller.OnNextRoundClicked();
        yield return WaitForPresentation(controller);
        Assert.That(game.CurrentRound, Is.EqualTo(round + 1));
        Assert.That(ui.IsChoosingRaise, Is.False);
        Invoke(controller, "CancelDealerAction");
        game.Turn.TrySet(TurnOwner.Dealer);
        Assert.That(game.TryRaise(4), Is.True);
        if (game.PlayerChips.Count > 2) game.PlayerChips.TrySpend(game.PlayerChips.Count - 2);
        Invoke(controller, "RefreshView");
        Assert.That(ui.gameObject.activeSelf, Is.True);
        Assert.That(Read<Button>(ui, "raiseButton").interactable, Is.False);
        Assert.That(Read<TMP_Text>(ui, "callText").text, Does.StartWith("ALL IN"));
        Click(ui, "callButton");
        Assert.That(game.PlayerChips.Count, Is.Zero);
        Assert.That(game.Phase, Is.EqualTo(GamePhase.Showdown));
        Assert.That(ui.gameObject.activeSelf, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualScene_ConfirmMaximumUsesExistingAllIn()
    {
        EditorSceneManager.OpenScene(RadialBettingSetupEditor.ScenePath);
        yield return new EnterPlayMode();
        var controller = Object.FindObjectOfType<GameplayController>();
        yield return WaitForInput(controller);
        typeof(GameplayController).GetField("minDealerThinkDelay", Hidden).SetValue(controller, 50f);
        typeof(GameplayController).GetField("maxDealerThinkDelay", Hidden).SetValue(controller, 50f);
        var ui = Object.FindObjectOfType<RadialBettingView>();
        var game = Read<GameState>(controller, "gameState");
        int before = game.PlayerChips.Count;
        Click(ui, "raiseButton");
        yield return Seconds(.3f);
        Click(ui, "maxButton");
        Assert.That(game.PlayerChips.Count, Is.EqualTo(before));
        Assert.That(Read<TMP_Text>(ui, "confirmText").text, Is.EqualTo("CONFIRM ALL IN"));
        Click(ui, "confirmButton");
        Assert.That(game.PlayerChips.Count, Is.Zero);
        Assert.That(game.CurrentTurn, Is.EqualTo(TurnOwner.Dealer));
        Assert.That(ui.gameObject.activeSelf, Is.False);
        yield return WaitForPresentation(controller);
        yield return null;
        yield return new ExitPlayMode();
    }

    private static IEnumerator WaitForInput(GameplayController controller)
    {
        float deadline = Time.realtimeSinceStartup + 25;
        while (!(bool)Invoke(controller, "CanAcceptPlayerBettingInput") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That((bool)Invoke(controller, "CanAcceptPlayerBettingInput"), Is.True);
    }
    private static IEnumerator WaitForNextRound(GameplayView view)
    {
        float deadline = Time.realtimeSinceStartup + 25;
        while (!Read<Button>(view, "nextRoundButton").interactable && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Read<Button>(view, "nextRoundButton").interactable, Is.True);
    }
    private static IEnumerator WaitForPresentation(GameplayController controller)
    {
        float deadline = Time.realtimeSinceStartup + 25;
        while (Read<GameplayPresentationController>(controller, "presentation").IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Read<GameplayPresentationController>(controller, "presentation").IsBusy, Is.False);
    }
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    private static object Invoke(object target, string name) => target.GetType().GetMethod(name, Hidden).Invoke(target, null);
    private static void Click(RadialBettingView ui, string field) => Read<Button>(ui, field).onClick.Invoke();
    private static IEnumerator Seconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
    private static void Capture(string name, int width, int height)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Directory.CreateDirectory("Logs/RadialReview");
        var camera = Camera.main;
        var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previousCameras = canvases.Select(c => c.worldCamera).ToArray();
        var previousDistances = canvases.Select(c => c.planeDistance).ToArray();
        var texture = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        float aspect = camera.aspect;
        try
        {
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .05f;
            }
            camera.targetTexture = texture;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/RadialReview/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = aspect;
            RenderTexture.active = previousActive;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = previousCameras[i];
                canvases[i].planeDistance = previousDistances[i];
            }
            Canvas.ForceUpdateCanvases();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(pixels);
        }
    }
}
