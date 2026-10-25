using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SlotMachineNpcTests
{
    private const string NpcPath = "Assets/_Game/SlotMachine/Npc/SlotMachineNpc.prefab";
    private const string MachinePath = "Assets/_Game/Art/Environment/SlotMachine/MD_SlotMachine/slot machine 1.prefab";

    [Test]
    public void IntroHasMovedPatronAndDevPreservesOnlyOriginalDealer()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity", OpenSceneMode.Additive);
        try {
            var npcs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SlotMachineNpc>(true)).ToArray();
            Assert.That(npcs.Length, Is.EqualTo(1));
            Assert.That(npcs[0].Machine.name, Is.EqualTo("Slot Machine (2)"));
            Assert.That(npcs[0].Machine.IsConfigured, Is.True);
            var animator = npcs[0].GetComponent<Animator>();
            Assert.That(animator.isHuman && animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(npcs[0].GetComponent<DealerAnimationController>(), Is.Null);
            Assert.That(npcs[0].GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(8));
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            Assert.That(controller.layers[0].iKPass, Is.True);
            Assert.That(controller.layers[0].stateMachine.states.Length, Is.EqualTo(11));
            Assert.That(((AnimationClip)controller.layers[0].stateMachine.defaultState.motion).isLooping, Is.True);
            var dealer = scene.GetRootGameObjects().Single(g => g.name == "PF_Dealer");

            Assert.That(dealer.GetComponent<Animator>().runtimeAnimatorController, Is.Not.SameAs(controller));
        } finally { EditorSceneManager.CloseScene(scene, true); }
        var dev=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Dev_Yujin.unity",OpenSceneMode.Additive);
        try {
            Assert.That(dev.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SlotMachineNpc>(true)),Is.Empty);
            Assert.That(dev.GetRootGameObjects().Single(g=>g.name=="PF_Dealer_Animated").GetComponent<DealerAnimationController>(),Is.Not.Null);
        } finally { EditorSceneManager.CloseScene(dev,true); }
    }

    [UnityTest]
    public IEnumerator ContactResultRepetitionAndCancellationUseTheExistingMachine()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return RunRuntimeChecks();
        yield return new ExitPlayMode();
    }

    private IEnumerator RunRuntimeChecks()
    {
        Assert.That(Application.isPlaying, Is.True);
        var fixture = new GameObject("Slot NPC test fixture");
        fixture.SetActive(false);
        var machineAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MachinePath);
        Assert.That(machineAsset, Is.Not.Null);
        var machineObject = Object.Instantiate(machineAsset, fixture.transform);
        Assert.That(machineObject, Is.Not.Null);
        var machine = machineObject.GetComponent<SlotMachineController>();
        Assert.That(machine, Is.Not.Null);
        machine.transform.SetPositionAndRotation(new Vector3(-4.1571f, 2.43f, 3.41006f), Quaternion.Euler(0, 180, 0));
        machine.transform.localScale = Vector3.one * 7.443194f;
        var npc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NpcPath), fixture.transform).GetComponent<SlotMachineNpc>();
        var serialized = new SerializedObject(npc);
        serialized.FindProperty("machine").objectReferenceValue = machine;
        serialized.FindProperty("restSeconds").vector2Value = Vector2.one;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        fixture.SetActive(true);
        yield return null;
        var animator = npc.GetComponent<Animator>();
        var root = npc.transform.position;
        var rotation = npc.transform.rotation;
        var contact = npc.transform.Find("Button Contact");
        var distal = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
        var tip = distal.childCount > 0 ? distal.GetChild(0) : distal;
        int completed = 0, started = 0;
        bool sawWin = false, sawLoss = false, wasSpinning = false, measuredRest = false;
        machine.WinChance = 1;
        machine.SpinCompleted += r => { completed++; machine.WinChance = 0; };
        float deadline = Time.realtimeSinceStartup + 65;
        while (Time.realtimeSinceStartup < deadline && !(sawWin && sawLoss && completed >= 3)) {
            yield return null;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!measuredRest && state.IsName("Rest") && state.normalizedTime > .3f && !animator.IsInTransition(0)) {
                measuredRest = true;
                foreach (bool right in new[] { true, false }) {
                    var hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                    var target = npc.transform.Find(right ? "Right Hand Rest" : "Left Hand Rest");
                    Assert.That(Vector3.Distance(hand.position, target.position), Is.LessThan(.04f), "Resting hands stay on the measured shelf targets.");
                }
                var skin = npc.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s => s.name == "HandNFoot");
                var mesh = new Mesh();
                skin.BakeMesh(mesh);
                float soleY = mesh.vertices.Min(v => skin.transform.TransformPoint(v).y);
                Object.Destroy(mesh);
                Assert.That(soleY, Is.InRange(-.025f, .035f), "Soles must meet the authored floor height (0), not float or sink.");
            }
            if (machine.IsSpinning && !wasSpinning) {
                started++;
                Debug.Log($"Contact: tip={tip.position:F4}, target={contact.position:F4}, wrist={animator.GetBoneTransform(HumanBodyBones.RightHand).position:F4}, error={Vector3.Distance(tip.position,contact.position):F4}");
                Assert.That(state.IsName("Press"), Is.True, "Spin must start during the button gesture.");
                Assert.That(Vector3.Distance(tip.position, contact.position), Is.LessThan(.04f), "Fingertip must meet the control panel at spin start.");
                Assert.That(machine.TrySpin(), Is.False, "The existing machine rejects overlap.");
            }
            wasSpinning = machine.IsSpinning;
            bool win = state.IsName("Win");
            bool loss = state.IsName("Lose1") || state.IsName("Lose2") || state.IsName("Frustrate");
            if (win || loss) {
                Assert.That(machine.IsSpinning, Is.False, "No reaction before the reels stop.");
                Assert.That(machine.LastResult, Is.Not.Null);
                Assert.That(win, Is.EqualTo(machine.LastResult.IsWin));
                sawWin |= win; sawLoss |= loss;
            }
            Assert.That(Vector3.Distance(root, npc.transform.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(rotation, npc.transform.rotation), Is.LessThan(.001f));
        }
        Assert.That(sawWin && sawLoss && completed >= 3, Is.True, "Expected both outcomes and repeated cycles.");
        Assert.That(measuredRest, Is.True);
        Assert.That(started, Is.EqualTo(completed));

        deadline = Time.realtimeSinceStartup + 20;
        while (!machine.IsSpinning && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(machine.IsSpinning, Is.True);
        npc.enabled = false;
        machine.enabled = false;
        yield return null;
        machine.enabled = true;
        Assert.That(machine.TrySpin(SlotSpinMode.ForceWin), Is.True);
        npc.enabled = true;
        while (machine.IsSpinning) {
            yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Rest"), Is.True, "External spin must not be claimed by the NPC.");
        }
        Assert.That(npc.IsWaitingForResult, Is.False);
        deadline = Time.realtimeSinceStartup + 12;
        while (!npc.IsWaitingForResult && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(npc.IsWaitingForResult, Is.True, "NPC must resume after cancellation/re-enable.");
        machine.enabled = false;
        yield return null;
        Assert.That(npc.IsWaitingForResult, Is.False);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

}
