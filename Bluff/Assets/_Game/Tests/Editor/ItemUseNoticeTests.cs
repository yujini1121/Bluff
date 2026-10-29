using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ItemUseNoticeTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Initialized = typeof(DOTween).GetField(
        "initialized", BindingFlags.Static | BindingFlags.NonPublic);
    private bool previousInitialized;
    private GameObject canvasObject;
    private ItemUseNotice notice;
    private ItemUseNotice prefab;

    [SetUp]
    public void SetUp()
    {
        previousInitialized = (bool)Initialized.GetValue(null);
        Initialized.SetValue(null, true);
        canvasObject = new GameObject("Notice canvas", typeof(RectTransform), typeof(Canvas));
        ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1920f, 1080f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Game/Art/UI/Cafe24PROSlimMax SDF.asset");
        Assert.That(font, Is.Not.Null);
        GameObject noticePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/UI/ItemUseNotice.prefab");
        Assert.That(noticePrefab, Is.Not.Null);
        prefab = noticePrefab.GetComponent<ItemUseNotice>();
        notice = ItemUseNotice.Create(prefab, canvasObject.GetComponent<Canvas>(), font);
    }

    [TearDown]
    public void TearDown()
    {
        if (notice != null) notice.Hide();
        if (canvasObject != null) Object.DestroyImmediate(canvasObject);
        DOTween.KillAll();
        Initialized.SetValue(null, previousInitialized);
    }

    [Test]
    public void Notice_AppearsAbovePlayerItemsWithoutBlockingInput()
    {
        Assert.That(notice.gameObject.activeSelf, Is.False);
        notice.Show(ItemUseFailureReason.NoRaiseToDefy);
        var rect = (RectTransform)notice.transform;
        var authoredRect = (RectTransform)prefab.transform;
        Assert.That(rect.anchorMin, Is.EqualTo(authoredRect.anchorMin));
        Assert.That(rect.anchoredPosition, Is.EqualTo(authoredRect.anchoredPosition));
        Assert.That(rect.sizeDelta, Is.EqualTo(authoredRect.sizeDelta));
        Assert.That(Text.color, Is.EqualTo(prefab.GetComponentInChildren<TMP_Text>(true).color));
        Assert.That(notice.GetComponent<Image>().color, Is.EqualTo(prefab.GetComponent<Image>().color));
        Assert.That(Text.text, Does.Contain("레이즈"));
        Assert.That(Group.blocksRaycasts, Is.False);
        Assert.That(Group.interactable, Is.False);
        foreach (Graphic graphic in notice.GetComponentsInChildren<Graphic>())
            Assert.That(graphic.raycastTarget, Is.False);
        foreach (char character in Text.text)
            if (!char.IsWhiteSpace(character))
                Assert.That(Text.font.HasCharacter(character), Is.True, character.ToString());
    }

    [Test]
    public void Notice_UsesInspectorFadeAndHoldDurations()
    {
        typeof(ItemUseNotice).GetField("fadeInDuration", Hidden).SetValue(notice, 0.2f);
        typeof(ItemUseNotice).GetField("holdDuration", Hidden).SetValue(notice, 0.6f);
        typeof(ItemUseNotice).GetField("fadeOutDuration", Hidden).SetValue(notice, 0.4f);
        notice.Show(ItemUseFailureReason.NotYourTurn);
        Sequence sequence = Animation;
        Assert.That(sequence.Duration(), Is.EqualTo(1.2f).Within(0.001f));
        sequence.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.25f, 0.25f);
        Assert.That(Group.alpha, Is.EqualTo(1f));
        DOTween.ManualUpdate(0.5f, 0.5f);
        Assert.That(Group.alpha, Is.EqualTo(1f));
        DOTween.ManualUpdate(0.25f, 0.25f);
        Assert.That(Group.alpha, Is.GreaterThan(0f).And.LessThan(1f));
        DOTween.ManualUpdate(0.3f, 0.3f);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(Animation, Is.Null);
    }

    [Test]
    public void RepeatedFailure_ReplacesMessageAndRestartsOneNotice()
    {
        Vector2 position = ((RectTransform)notice.transform).anchoredPosition;
        Vector2 anchor = ((RectTransform)notice.transform).anchorMin;
        notice.Show(ItemUseFailureReason.NotYourTurn);
        Animation.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(2.8f, 2.8f);
        notice.Show(ItemUseFailureReason.RefreshAfterBetting);
        Animation.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.3f, 0.3f);
        Assert.That(canvasObject.transform.childCount, Is.EqualTo(1));
        Assert.That(((RectTransform)notice.transform).anchoredPosition, Is.EqualTo(position));
        Assert.That(((RectTransform)notice.transform).anchorMin, Is.EqualTo(anchor));
        Assert.That(Text.text, Does.Contain("베팅 전에"));
        Assert.That(notice.gameObject.activeSelf, Is.True);
        Assert.That(Group.alpha, Is.EqualTo(1f));
        DOTween.ManualUpdate(3f, 3f);
        Assert.That(notice.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void Hide_CancelsPendingFadeImmediately()
    {
        notice.Show(ItemUseFailureReason.EffectUnavailable);
        Animation.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.2f, 0.2f);
        notice.Hide();
        Assert.That(Animation, Is.Null);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(Group.alpha, Is.Zero);
        DOTween.ManualUpdate(4f, 4f);
        Assert.That(notice.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NarrowCanvas_FitsWithoutChangingTheAuthoredPrefab()
    {
        float authoredWidth = ((RectTransform)prefab.transform).sizeDelta.x;
        ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(400f, 1080f);
        notice.Show(ItemUseFailureReason.NoRaiseToDefy);
        Assert.That(((RectTransform)notice.transform).rect.width, Is.LessThan(400f));
        Assert.That(((RectTransform)prefab.transform).sizeDelta.x, Is.EqualTo(authoredWidth));
        ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1920f, 1080f);
        notice.Show(ItemUseFailureReason.NoRaiseToDefy);
        Assert.That(((RectTransform)notice.transform).rect.width, Is.EqualTo(authoredWidth));
    }

    [TestCase("Dev_Yujin")]
    [TestCase("Dev_Hwijun")]
    public void GameplayScene_ReferencesTheEditableNoticePrefab(string sceneName)
    {
        string path = "Assets/_Game/Scenes/" + sceneName + ".unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            int views = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (GameplayView view in root.GetComponentsInChildren<GameplayView>(true))
                {
                    var settings = new SerializedObject(view);
                    Assert.That(settings.FindProperty("itemUseNoticePrefab").objectReferenceValue, Is.EqualTo(prefab));
                    views++;
                }
            }
            Assert.That(views, Is.GreaterThan(0));
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private Sequence Animation => (Sequence)typeof(ItemUseNotice).GetField("sequence", Hidden).GetValue(notice);
    private TMP_Text Text => notice.GetComponentInChildren<TMP_Text>(true);
    private CanvasGroup Group => notice.GetComponent<CanvasGroup>();
}
