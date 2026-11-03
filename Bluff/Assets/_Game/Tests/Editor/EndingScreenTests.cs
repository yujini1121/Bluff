using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.Shift;
using UnityEngine.EventSystems;

public sealed class EndingScreenTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Find<T>() where T : Object => Object.FindObjectOfType<T>(true);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TestCase(GameWinner.Player, false)]
    [TestCase(GameWinner.Dealer, false)]
    [TestCase(GameWinner.Draw, false)]
    [TestCase(GameWinner.Player, true)]
    [TestCase(GameWinner.Dealer, true)]
    [TestCase(GameWinner.Draw, true)]
    public void Screen_PreservesCaptionAndLocksAndRestoresGameplay(GameWinner winner, bool canContinue)
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        var owner = Find<GameplayController>();
        var victory = Find<VictoryCutsceneController>();
        var defeat = Find<DefeatCutsceneController>();
        var hud = Find<GameplayView>().GameplayHud;
        var input = Find<InputSystem>();
        bool enabledBefore = input.enabled;
        hud.alpha = 0.73f;
        var screen = Read<EndingScreenController>(owner, "endingScreen");
        try
        {
            int continueCalls = 0;
            int restartCalls = 0;
            screen.Show(winner, canContinue, () => continueCalls++, () => restartCalls++);
            Assert.That(screen.IsVisible, Is.True);
            Assert.That(hud.alpha, Is.Zero);
            Assert.That(hud.interactable || hud.blocksRaycasts, Is.False);
            Assert.That(input.enabled, Is.False);
            Assert.That(Cursor.visible, Is.True);
            Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
            Assert.That(Read<MainButton>(screen, "continueButtonLabel").buttonText,
                Is.EqualTo(canContinue ? "이어서 계속하기" : "다시하기"));
            Assert.That(Read<Button>(screen, "continueButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(Read<Button>(screen, "titleButton").gameObject.activeInHierarchy, Is.True);
            if (EventSystem.current != null) Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            screen.OnContinueClicked();
            Assert.That(continueCalls, Is.EqualTo(canContinue ? 1 : 0));
            Assert.That(restartCalls, Is.EqualTo(canContinue ? 0 : 1));
            Assert.That(Read<Button>(screen, "titleButton").interactable,
                Is.EqualTo(Application.CanStreamedLevelBeLoaded("Title")));
            Assert.That(Read<TMP_Text>(screen, "caption").text, Is.EqualTo(winner == GameWinner.Draw ? "MATCH DRAW" :
                winner == GameWinner.Player ? victory.EndingCaption.text : defeat.EndingCaption.text));
            screen.Release(); screen.Release();
            Assert.That(input.enabled, Is.EqualTo(enabledBefore));
            Assert.That(hud.alpha, Is.EqualTo(0.73f));
            Assert.That(screen.IsVisible, Is.False);
        }
        finally { screen.Release(); }
    }

    [UnityTest]
    public IEnumerator CutsceneSkipAndMissingSetup_AlwaysReachPersistentEnding()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        var owner = Find<GameplayController>();
        var state = Read<GameState>(owner, "gameState");
        var presentation = Find<GameplayPresentationController>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (presentation.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4));
        state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(owner, "roundWinner", winner);
        Set(owner, "isShowdownResultVisible", true);
        owner.OnViewEndingClicked();
        var flow = Find<VictoryCutsceneController>();
        Assert.That(flow.IsPlaying, Is.True);
        flow.Skip();
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible, Is.True);
        yield return null;
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Find<GameplayView>().GameplayHud.alpha, Is.Zero);
        Assert.That(flow.GetComponent<PlayableDirector>().state, Is.Not.EqualTo(PlayState.Playing));
        Assert.That(Read<GameObject>(Find<GameplayView>(), "resultOverlay").activeSelf, Is.False);
        Capture("Ending-Victory-Skipped");
        owner.enabled = false;
        Assert.That(screen.IsActive, Is.False);
        owner.enabled = true;
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(flow.IsPlaying, Is.False);
        screen.gameObject.SetActive(false);
        yield return null;
        Assert.That(screen.gameObject.activeInHierarchy && screen.IsVisible, Is.True);
        Assert.That(Find<GameplayView>().GameplayHud.alpha, Is.Zero);
        var titleButton = Read<Button>(screen, "titleButton");
        titleButton.GetComponent<UIElementSound>().OnPointerEnter(new PointerEventData(EventSystem.current));
        titleButton.GetComponent<UIElementSound>().OnPointerClick(new PointerEventData(EventSystem.current));
        titleButton.onClick.Invoke();
        yield return null;
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo("Title"));
        Assert.That(Time.timeScale, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator MissingVictoryDirector_FallsBackToEndingWithWorkingTitleButton()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        var owner = Find<GameplayController>();
        var state = Read<GameState>(owner, "gameState");
        float deadline = Time.realtimeSinceStartup + 30;
        while (Find<GameplayPresentationController>().IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null;
        }
        typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
        state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
        state.TrySetCommunityCards(new Card(4), new Card(4)); state.TrySetPhase(GamePhase.Showdown);
        Assert.That(state.TrySettleShowdown(out var winner), Is.True);
        Set(owner, "roundWinner", winner); Set(owner, "isShowdownResultVisible", true);
        Set(Find<VictoryCutsceneController>(), "director", null);
        LogAssert.Expect(LogType.Error, "Victory cutscene is not configured. Showing the normal final result.");
        owner.OnViewEndingClicked();
        var screen = Find<EndingScreenController>();
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Find<VictoryCutsceneController>().IsPlaying, Is.False);
        Assert.That(Read<Button>(screen, "titleButton").interactable, Is.True);
        screen.GoToTitle(); yield return null;
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo("Title"));
    }

    private static void Capture(string name) => typeof(GameplayUiSceneTests)
        .GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { name });

    [Test]
    public void SecondaryAction_BlocksReentryAndCanRetryAfterCallbackFailure()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        var screen = Find<EndingScreenController>();
        int calls = 0;
        screen.Show(GameWinner.Dealer, false, null, () =>
        {
            calls++;
            screen.OnContinueClicked();
            if (calls == 1) throw new System.InvalidOperationException("Test action failed");
            screen.Release();
        });
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Test action failed"));
        screen.OnContinueClicked();
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(screen.IsVisible, Is.True);
        Assert.That(Read<Button>(screen, "continueButton").interactable, Is.True);
        screen.OnContinueClicked(); screen.OnContinueClicked();
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(screen.IsVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator InitialFocus_IsEmpty_AndNavigationStartsOnlyOnInput()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        yield return new EnterPlayMode();
        yield return RunEndingNavigation();
    }

    private static IEnumerator RunEndingNavigation()
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (Find<GameplayPresentationController>().IsBusy)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        var events = EventSystem.current;
        var module = events.GetComponent<StandaloneInputModule>();
        var originalInput = module.inputOverride;
        var input = events.gameObject.AddComponent<EndingNavigationInput>();
        input.horizontalAxis = module.horizontalAxis;
        input.verticalAxis = module.verticalAxis;
        input.pointer = new Vector2(-10, -10);
        module.inputOverride = input;
        var screen = Find<EndingScreenController>();
        var title = Read<Button>(screen, "titleButton");
        var next = Read<Button>(screen, "continueButton");
        int submits = 0;
        try
        {
            typeof(EventSystem).GetField("m_HasFocus", Hidden).SetValue(events, true);
            screen.Show(GameWinner.Player, true, () => submits++, () => { });
            yield return null; yield return null;
            Assert.That(events.currentSelectedGameObject, Is.Null);
            Assert.That(ButtonState(title), Is.EqualTo("Normal"));
            Assert.That(ButtonState(next), Is.EqualTo("Normal"));
            Capture("Ending-Initial-Unselected");
            input.move = Vector2.down;
            yield return null; yield return null;
            Assert.That(events.currentSelectedGameObject, Is.SameAs(title.gameObject));
            input.move = Vector2.zero;
            yield return null; yield return null;
            ExecuteEvents.Execute(events.currentSelectedGameObject,
                new AxisEventData(events) { moveVector = Vector2.down, moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(next.gameObject));
            ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            Assert.That(submits, Is.EqualTo(1));
            input.move = Vector2.zero;
            input.pointer = RectTransformUtility.WorldToScreenPoint(null, title.transform.position);
            yield return null; yield return null;
            Assert.That(events.currentSelectedGameObject, Is.Null);
            Assert.That(ButtonState(title), Is.EqualTo("Highlighted"));
            Assert.That(ButtonState(next), Is.EqualTo("Normal"));
            Capture("Ending-Pointer-Hover");
            input.pointer = new Vector2(-10, -10);
            yield return null; yield return null;
            Assert.That(ButtonState(title), Is.EqualTo("Normal"));
            screen.Show(GameWinner.Player, false, null, () => { });
            input.move = Vector2.down;
            yield return null; yield return null;
            Assert.That(events.currentSelectedGameObject, Is.Null);
            input.move = Vector2.zero;
            yield return null; yield return null;
            input.move = Vector2.down;
            yield return null; yield return null;
            Assert.That(events.currentSelectedGameObject, Is.SameAs(title.gameObject));
        }
        finally
        {
            module.inputOverride = originalInput;
            screen.Release();
            Object.Destroy(input);
        }
    }

    private static string ButtonState(Button button) => typeof(Selectable)
        .GetProperty("currentSelectionState", Hidden).GetValue(button).ToString();
}

public sealed class EndingNavigationInput : BaseInput
{
    public string horizontalAxis, verticalAxis;
    public Vector2 move, pointer;
    public override float GetAxisRaw(string axisName) => axisName == horizontalAxis ? move.x : axisName == verticalAxis ? move.y : 0f;
    public override bool GetButtonDown(string buttonName) => false;
    public override bool GetMouseButton(int button) => false;
    public override bool GetMouseButtonDown(int button) => false;
    public override bool GetMouseButtonUp(int button) => false;
    public override bool mousePresent => true;
    public override Vector2 mousePosition => pointer;
    public override Vector2 mouseScrollDelta => Vector2.zero;
    public override int touchCount => 0;
}
