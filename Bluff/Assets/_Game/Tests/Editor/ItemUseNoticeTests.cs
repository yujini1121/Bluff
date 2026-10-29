using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ItemUseNoticeTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Initialized = typeof(DOTween).GetField(
        "initialized", BindingFlags.Static | BindingFlags.NonPublic);
    private bool previousInitialized;
    private GameObject canvasObject;
    private ItemUseNotice notice;

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
        notice = ItemUseNotice.Create(canvasObject.GetComponent<Canvas>(), font);
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
    public void Notice_AppearsBelowRoundHudWithoutBlockingInput()
    {
        Assert.That(notice.gameObject.activeSelf, Is.False);
        notice.Show(ItemUseFailureReason.NoRaiseToDefy);
        var rect = (RectTransform)notice.transform;
        Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
        Assert.That(rect.anchoredPosition.y, Is.EqualTo(-165f));
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
    public void Notice_HoldsThenFadesOutWithinThreeSeconds()
    {
        notice.Show(ItemUseFailureReason.NotYourTurn);
        Sequence sequence = Animation;
        Assert.That(sequence.Duration(), Is.EqualTo(3f).Within(0.001f));
        sequence.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.2f, 0.2f);
        Assert.That(Group.alpha, Is.EqualTo(1f));
        DOTween.ManualUpdate(2.2f, 2.2f);
        Assert.That(Group.alpha, Is.EqualTo(1f));
        DOTween.ManualUpdate(0.35f, 0.35f);
        Assert.That(Group.alpha, Is.GreaterThan(0f).And.LessThan(1f));
        DOTween.ManualUpdate(0.4f, 0.4f);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(Animation, Is.Null);
    }

    [Test]
    public void RepeatedFailure_ReplacesMessageAndRestartsOneNotice()
    {
        notice.Show(ItemUseFailureReason.NotYourTurn);
        Animation.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(2.8f, 2.8f);
        notice.Show(ItemUseFailureReason.RefreshAfterBetting);
        Animation.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(0.3f, 0.3f);
        Assert.That(canvasObject.transform.childCount, Is.EqualTo(1));
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

    private Sequence Animation => (Sequence)typeof(ItemUseNotice).GetField("sequence", Hidden).GetValue(notice);
    private TMP_Text Text => notice.GetComponentInChildren<TMP_Text>(true);
    private CanvasGroup Group => notice.GetComponent<CanvasGroup>();
}
