using System.Linq;
using System.Reflection;
using Michsky.UI.Shift;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ResultFlowAssetTests
{
    private const string EndingPrefab = "Assets/_Game/Prefabs/UI/EndingScreen.prefab";
    private const string SciFiButton = "Assets/Shift - Complete Sci-Fi UI/Prefabs/Button/Main Button.prefab";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Hidden).GetValue(owner);
    private static T Find<T>() where T : Object => Object.FindObjectOfType<T>(true);

    [TearDown]
    public void Cleanup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Time.timeScale = 1;
    }

    [TestCase("Dev_Yujin")]
    [TestCase("Dev_Hwijun")]
    public void GameplayScenes_ReferenceAuthoredPrefabAndTextAndInputScope(string sceneName)
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + sceneName + ".unity");
        var owner = Find<GameplayController>();
        var view = Find<GameplayView>();
        var ending = Read<EndingScreenController>(owner, "endingScreen");
        Assert.That(ending, Is.Not.Null);
        Assert.That(ending.HasReferences, Is.True);
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ending.gameObject), Is.EqualTo(EndingPrefab));
        Assert.That(Read<Canvas>(ending, "canvas").enabled, Is.False);
        Assert.That(Read<CanvasGroup[]>(ending, "hud"), Does.Contain(view.GameplayHud));
        Assert.That(Read<Behaviour[]>(ending, "inputs"), Does.Contain(Find<InputSystem>()));
        Assert.That(Read<Behaviour[]>(ending, "inputs").All(b => b != null && b.gameObject.scene == owner.gameObject.scene), Is.True);
        Assert.That(Read<ResultFlowText>(view, "resultText"), Is.Not.Null);
        Assert.That(Read<GameObject>(view, "finalResultContent"), Is.Not.Null);
        Assert.That(Read<TMP_Text>(view, "finalResultTitleText"), Is.Not.SameAs(Read<TMP_Text>(view, "resultTitleText")));
        Assert.That(Read<TMP_Text>(view, "finalResultDetailText"), Is.Not.SameAs(Read<TMP_Text>(view, "resultDetailText")));
    }

    [Test]
    public void EndingButtons_ReuseNestedSciFiAssetAndSerializedClickEvents()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefab).GetComponent<EndingScreenController>();
        Assert.That(prefab, Is.Not.Null);
        Assert.That(Read<MainButton>(prefab, "continueButtonLabel"),
            Is.SameAs(Read<Button>(prefab, "continueButton").GetComponent<MainButton>()));
        Assert.That(Read<string>(prefab, "continueLabel"), Is.EqualTo("이어서 계속하기"));
        Assert.That(Read<string>(prefab, "restartLabel"), Is.EqualTo("다시하기"));
        foreach (var entry in new[] { ("titleButton", "GoToTitle"), ("continueButton", "OnContinueClicked") })
        {
            var button = Read<Button>(prefab, entry.Item1);
            var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(button.gameObject);
            Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo(SciFiButton));
            Assert.That(button.GetComponent<MainButton>(), Is.Not.Null);
            string label = entry.Item1 == "titleButton" ? "메인화면으로 돌아가기" : "이어서 계속하기";
            var main = button.GetComponent<MainButton>();
            Assert.That(main.buttonText, Is.EqualTo(label));
            foreach (var text in new[] { main.normalText, main.highlightedText, main.pressedText })
            {
                Assert.That(text.text, Is.EqualTo(label));
                Assert.That(label.Where(c => !char.IsWhiteSpace(c)).All(c => text.font.HasCharacter(c, true)),
                    Is.True, "The authored button font must support its Korean label");
            }
            Assert.That(button.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
            var feedback = button.GetComponent<UIElementSound>();
            Assert.That(feedback.audioObject, Is.Not.Null, "Use an authored source rather than a scene-name lookup");
            Assert.That(feedback.audioObject.playOnAwake, Is.False);
            Assert.That(feedback.audioObject.outputAudioMixerGroup, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Audio/SFX/Click SFX.prefab").GetComponent<AudioSource>().outputAudioMixerGroup));
            Assert.That(Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
                button.onClick.GetPersistentTarget(i) == prefab &&
                button.onClick.GetPersistentMethodName(i) == entry.Item2), Is.True);
        }
    }

    [Test]
    public void ResultDisplay_UsesEditedAssetTextAndPreservesAuthoredLayouts()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity");
        var view = Find<GameplayView>();
        var settings = Object.Instantiate(Read<ResultFlowText>(view, "resultText"));
        try
        {
            settings.roundPlayerWin = "CUSTOM ROUND";
            settings.matchDealerWin = "CUSTOM MATCH";
            settings.finalTitleFormat = "CUSTOM TITLE {0}";
            settings.finalSummaryFormat = "{0}|{1}|{2}|{3}";
            settings.viewEndingLabel = "CUSTOM ENDING";
            view.GetType().GetField("resultText", Hidden).SetValue(view, settings);
            var finalTitle = Read<TMP_Text>(view, "resultTitleText");
            var finalDetail = Read<TMP_Text>(view, "resultDetailText");
            finalTitle.rectTransform.anchoredPosition = new Vector2(20, 110);
            finalDetail.rectTransform.sizeDelta = new Vector2(530, 150);
            finalDetail.fontSize = 21;
            var state = new GameState(5, 30, Deck.CreateIndianHoldemDeck());
            typeof(GameState).GetProperty("CurrentRound").SetValue(state, 10);
            state.TrySetPlayerCard(new Card(4)); state.TrySetDealerCard(new Card(7));
            state.TrySetCommunityCards(new Card(4), new Card(4)); state.TrySetPhase(GamePhase.Showdown);
            Assert.That(state.TrySettleShowdown(out var winner), Is.True);
            view.RefreshResult(state, winner, HandRank.Triple, HandRank.Double, true, false, true);
            view.RefreshProgress(false, true, false, true, true);
            Assert.That(finalTitle.text, Is.EqualTo("CUSTOM TITLE CUSTOM MATCH"));
            Assert.That(Read<GameObject>(view, "finalResultContent").activeSelf, Is.False);
            Assert.That(finalDetail.text, Does.Contain("CUSTOM MATCH"));
            Assert.That(finalTitle.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(20, 110)));
            Assert.That(finalDetail.rectTransform.sizeDelta, Is.EqualTo(new Vector2(530, 150)));
            Assert.That(finalDetail.fontSize, Is.EqualTo(21));
            Assert.That(Read<Button>(view, "nextRoundButton").GetComponent<MainButton>().buttonText, Is.EqualTo("CUSTOM ENDING"));
            state.TrySetPhase(GamePhase.RoundEnd);
            view.RefreshResult(state, winner, HandRank.Triple, HandRank.Double, true, false, false);
            Assert.That(Read<GameObject>(view, "finalResultContent").activeSelf, Is.False);
            Assert.That(Read<TMP_Text>(view, "resultTitleText").gameObject.activeSelf, Is.True);
            Assert.That(finalTitle.text, Is.EqualTo("CUSTOM ROUND"));
            Assert.That(finalTitle.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(20, 110)));
        }
        finally { Object.DestroyImmediate(settings); }
    }
}
