using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DealerBetPresentationTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private ChipVisualController chips;
    private DealerAnimationController dealer;
    private GameplayPresentationController presentation;
    private Animator animator;
    private Transform socket;
    private GameState game;
    private int notifications;
    private bool previousTweenInitialized;
    private static readonly FieldInfo TweenInitialized = typeof(DOTween).GetField(
        "initialized", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        previousTweenInitialized = (bool)TweenInitialized.GetValue(null);
        TweenInitialized.SetValue(null, true);
        root = new GameObject("Dealer bet presentation test");
        chips = root.AddComponent<ChipVisualController>();
        foreach (string field in new[] { "playerChipArea", "dealerChipArea", "playerBetAreaPoint", "dealerBetAreaPoint", "potArea" })
            Set(chips, field, Child(field));
        Read<Transform>(chips, "dealerChipArea").position = new Vector3(0, .8f, 0);
        Read<Transform>(chips, "dealerChipArea").localScale = new Vector3(1, 1.5f, 1);
        Set(chips, "chipPrefabs", new[] { AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Art/Chips/PF_Chip_Red_01.prefab") });
        animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Game/Art/Characters/Animations/AC_Dealer.controller");
        animator.Rebind();
        dealer = root.AddComponent<DealerAnimationController>();
        socket = Child("Chip socket");
        socket.position = new Vector3(1, 2, 3);
        Set(dealer, "animator", animator);
        Set(dealer, "chipSocket", socket);
        Set(dealer, "allInChipOffset", new Vector3(.3f, 0, .2f));
        presentation = root.AddComponent<GameplayPresentationController>();
        Set(presentation, "chipVisualController", chips);
        Set(presentation, "dealerAnimationController", dealer);
        notifications = 0;
    }

    [TearDown]
    public void TearDown()
    {
        dealer?.CancelBetChipAnimation();
        if (root != null) Object.DestroyImmediate(root);
        TweenInitialized.SetValue(null, previousTweenInitialized);
    }

    [TestCase(DealerDecision.AllIn, 3, 10, false)]
    [TestCase(DealerDecision.Call, 38, 10, true)]
    [TestCase(DealerDecision.Raise, 5, 10, false)]
    [TestCase(DealerDecision.Raise, 20, 10, true)]
    [TestCase(DealerDecision.Call, 1, 10, false)]
    [TestCase(DealerDecision.Call, 9, 10, false)]
    [TestCase(DealerDecision.Call, 10, 10, true)]
    [TestCase(DealerDecision.AllIn, 10, 10, true)]
    [TestCase(DealerDecision.AllIn, 38, 10, true)]
    [TestCase(DealerDecision.Call, 13, 10, true)]
    [TestCase(DealerDecision.Raise, 11, 10, true)]
    [TestCase(DealerDecision.Raise, 5, 5, true)]
    [TestCase(DealerDecision.Raise, 10, 11, false)]
    public void ActualAdditionalBet_SelectsExistingTriggerAndCompletesChipMove(
        DealerDecision decision, int amount, int threshold, bool twoHands)
    {
        game = NewGame(decision == DealerDecision.AllIn ? amount : 100,
            decision == DealerDecision.Call ? TurnOwner.Player : TurnOwner.Dealer);
        if (decision == DealerDecision.Call) Assert.That(game.TryRaise(amount), Is.True);
        presentation.Initialize(game, () => notifications++);
        var serialized = new SerializedObject(presentation);
        Assert.That(serialized.FindProperty("dealerTwoHandBetChipThreshold").intValue, Is.EqualTo(10));
        serialized.FindProperty("dealerTwoHandBetChipThreshold").intValue = threshold;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        int before = game.DealerChips.Count;
        bool accepted = decision == DealerDecision.AllIn ? game.TryAllIn() :
            decision == DealerDecision.Call ? game.TryCall() : game.TryRaise(amount);
        Assert.That(accepted, Is.True);
        int moved = before - game.DealerChips.Count;
        Assert.That(moved, Is.EqualTo(amount));
        Assert.That(presentation.PlayDealerBet(moved), Is.True);
        AssertMoveAndComplete(amount, twoHands);
    }

    [Test]
    public void Raise_UsesFiveNewChipsEvenWhenCumulativeBetIsTwelve()
    {
        game = NewGame(100, TurnOwner.Dealer);
        Assert.That(game.TryRaise(7), Is.True);
        Assert.That(game.TryRaise(2), Is.True);
        presentation.Initialize(game, () => notifications++);
        int before = game.DealerChips.Count;
        Assert.That(game.TryRaise(3), Is.True); // Call two, then raise three.
        Assert.That(game.Betting.DealerTotalBet, Is.EqualTo(12));
        Assert.That(presentation.PlayDealerBet(before - game.DealerChips.Count), Is.True);
        AssertMoveAndComplete(5, false);
    }

    [TestCase(3)]
    [TestCase(20)]
    public void CancelMove_RecoversChipVisualsAndNotifiesOnce(int amount)
    {
        game = NewGame(100, TurnOwner.Dealer);
        presentation.Initialize(game, () => notifications++);
        Assert.That(game.TryRaise(amount), Is.True);
        Assert.That(presentation.PlayDealerBet(amount), Is.True);
        dealer.GrabChip();
        dealer.CancelBetChipAnimation();
        dealer.CancelBetChipAnimation();
        Assert.That(notifications, Is.EqualTo(1));
        AssertSynchronized();
    }

    [TestCase(13, 5)]
    [TestCase(24, 9)]
    [TestCase(40, 1)]
    public void SingleHandCarry_GathersOneStackAndKeepsItAttachedWhenHandRotates(
        int balance, int amount)
    {
        game = NewGame(balance, TurnOwner.Dealer);
        presentation.Initialize(game, () => notifications++);
        Set(dealer, "singleHandChipSpacing", .017f);
        Vector3 gripOffset = new Vector3(.01f, .02f, -.03f);
        Set(dealer, "singleHandChipOffset", gripOffset);
        socket.rotation = Quaternion.Euler(15, 35, 20);
        Assert.That(game.TryRaise(amount), Is.True);
        Assert.That(presentation.PlayDealerBet(amount), Is.True);
        Transform[] moving = Read<Transform[]>(dealer, "activeBetChips");
        if (amount > 1)
            Assert.That(Mathf.Abs(moving[0].position.x - moving[amount - 1].position.x),
                Is.GreaterThan(.01f), "Exercise chips taken across two source stacks.");

        dealer.GrabChip();
        var offsets = new Vector3[amount];
        var rotations = new Quaternion[amount];
        Vector3 grip = socket.TransformPoint(gripOffset);
        for (int i = 0; i < amount; i++)
        {
            Vector3 expected = grip + Vector3.up * ((amount - 1) * .5f - i) * .017f;
            Assert.That(Vector3.Distance(moving[i].position, expected), Is.LessThan(.0001f));
            offsets[i] = socket.InverseTransformPoint(moving[i].position);
            rotations[i] = Quaternion.Inverse(socket.rotation) * moving[i].rotation;
        }

        socket.position += new Vector3(.4f, .2f, -.3f);
        socket.rotation = Quaternion.Euler(-35, 70, 60);
        typeof(DealerAnimationController).GetMethod("LateUpdate", Hidden).Invoke(dealer, null);
        for (int i = 0; i < amount; i++)
        {
            Assert.That(Vector3.Distance(moving[i].position, socket.TransformPoint(offsets[i])),
                Is.LessThan(.0001f), "Every chip must remain at its grip-relative position.");
            Assert.That(Quaternion.Angle(moving[i].rotation, socket.rotation * rotations[i]),
                Is.LessThan(.01f));
        }

        dealer.ReleaseChip();
        foreach (Tween tween in Read<List<Tween>>(dealer, "betMoveTweens")) tween.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(1, 1);
        Assert.That(notifications, Is.EqualTo(1));
        AssertSynchronized();
    }

    [Test]
    public void UnavailableAnimator_ClearsPendingMoveWithoutChangingBet()
    {
        game = NewGame(100, TurnOwner.Dealer);
        presentation.Initialize(game, () => notifications++);
        Assert.That(game.TryRaise(20), Is.True);
        animator.enabled = false;
        Assert.That(presentation.PlayDealerBet(20), Is.False);
        Assert.That(presentation.IsChipAnimating, Is.False);
        Assert.That(Read<List<GameObject>>(chips, "pendingChips"), Is.Empty);
        Assert.That(game.DealerChips.Count, Is.EqualTo(80));
        Assert.That(game.Pot.Amount, Is.EqualTo(20));
    }

    private void AssertMoveAndComplete(int count, bool twoHands)
    {
        Assert.That(animator.GetBool(twoHands ? "All-In" : "Call"), Is.True);
        Assert.That(animator.GetBool(twoHands ? "Call" : "All-In"), Is.False);
        Assert.That(Read<bool>(dealer, "keepChipWorldY"), Is.EqualTo(twoHands));
        Transform[] moving = Read<Transform[]>(dealer, "activeBetChips");
        Assert.That(moving.Length, Is.EqualTo(count));
        float[] originalY = new float[count];
        for (int i = 0; i < count; i++) originalY[i] = moving[i].position.y;
        dealer.GrabChip();
        Vector3[] grabbed = new Vector3[count];
        Quaternion[] rotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            grabbed[i] = moving[i].position;
            rotations[i] = moving[i].rotation;
        }
        if (twoHands) AssertGroundedStacks(moving, originalY);
        Vector3 delta = new Vector3(.5f, .6f, .7f);
        socket.position += delta;
        typeof(DealerAnimationController).GetMethod("LateUpdate", Hidden).Invoke(dealer, null);
        for (int i = 0; i < count; i++)
        {
            Vector3 expected = grabbed[i] + (twoHands ? new Vector3(0, 0, delta.z) : delta);
            Assert.That(Vector3.Distance(moving[i].position, expected), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(moving[i].rotation, rotations[i]), Is.LessThan(.01f));
        }
        int balance = game.DealerChips.Count;
        int pot = game.Pot.Amount;
        dealer.ReleaseChip();
        dealer.ReleaseChip(); // Duplicate events must not start another move.
        foreach (Tween tween in Read<List<Tween>>(dealer, "betMoveTweens")) tween.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(1, 1);
        Assert.That(notifications, Is.EqualTo(1));
        Assert.That(game.DealerChips.Count, Is.EqualTo(balance));
        Assert.That(game.Pot.Amount, Is.EqualTo(pot));
        AssertSynchronized();
    }

    private void AssertGroundedStacks(Transform[] moving, float[] originalY)
    {
        float surfaceY = Read<Transform>(chips, "dealerChipArea").position.y;
        for (int i = 0; i < moving.Length; i++)
        {
            float bottomY = moving[i].position.y;
            for (int j = 0; j < moving.Length; j++)
            {
                if (Mathf.Abs(moving[i].position.x - moving[j].position.x) > .0001f ||
                    Mathf.Abs(moving[i].position.z - moving[j].position.z) > .0001f) continue;
                bottomY = Mathf.Min(bottomY, moving[j].position.y);
                Assert.That(moving[i].position.y - moving[j].position.y,
                    Is.EqualTo(originalY[i] - originalY[j]).Within(.0001f), "Stack spacing must be preserved.");
            }
            Assert.That(bottomY, Is.EqualTo(surfaceY).Within(.0001f),
                "Every pushed stack must rest on the chip-area surface, including a partially taken stack.");
        }
    }

    private void AssertSynchronized()
    {
        Assert.That(presentation.IsChipAnimating, Is.False);
        Assert.That(Read<List<GameObject>>(chips, "pendingChips"), Is.Empty);
        Assert.That(Read<List<GameObject>>(chips, "dealerChipInstances").Count, Is.EqualTo(game.DealerChips.Count));
        Assert.That(Read<List<GameObject>>(chips, "dealerBetChipInstances").Count, Is.EqualTo(game.Betting.DealerTotalBet));
    }

    private static GameState NewGame(int dealerChips, TurnOwner turn)
    {
        var state = new GameState(100, dealerChips, new Deck(new[] { new Card(1), new Card(2), new Card(3), new Card(4) }));
        Assert.That(state.TrySetPhase(GamePhase.Betting), Is.True);
        Assert.That(state.Turn.TrySet(turn), Is.True);
        return state;
    }
    private Transform Child(string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(root.transform);
        return child;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
}
