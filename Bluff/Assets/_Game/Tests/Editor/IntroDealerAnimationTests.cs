using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

public sealed class IntroDealerAnimationTests
{
    [Test]
    public void SceneUsesAnimatedPrefabAndDedicatedLoopWithoutChangingPackClip()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity");
        var model = GameObject.Find("PF_Dealer");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model),
            Is.EqualTo("Assets/_Game/Art/Characters/Dealer/Models/PF_Dealer_Animated.prefab"));
        var animator = model.GetComponent<Animator>();
        Assert.That(animator.enabled && animator.isHuman && animator.avatar.isValid, Is.True);
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(model.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(8));
        Assert.That(model.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
        Assert.That(model.GetComponent<DealerAnimationController>(), Is.Null);
        Assert.That(model.transform.Find("Dealer Look Target"), Is.Not.Null);
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(IntroDealerSetupEditor.ControllerPath));
        Assert.That(controller.layers[0].stateMachine.states.Length, Is.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.defaultState.name, Is.EqualTo("Idle"));
        var idle = (AnimationClip)controller.layers[0].stateMachine.defaultState.motion;
        Assert.That(idle.isLooping && idle.humanMotion, Is.True);
        Assert.That(idle.events.Length, Is.Zero);
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Casino Animation/Assets/Animation/Spin Wheel/Dealer/AS_Dealer_Wheel_Idle.anim");
        Assert.That(source.isLooping, Is.False, "팩 원본의 반복 설정은 유지한다");
        IntroCutsceneSetupEditor.ValidateOpenScene();
    }

    [UnityTest]
    public IEnumerator IdleLoopsWithoutDrift_AndRemainsNaturalThroughApproachAndDialogue()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity");
        yield return new EnterPlayMode();
        yield return null;
        yield return null;
        var model = GameObject.Find("PF_Dealer");
        var animator = model.GetComponent<Animator>();
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 rootPosition = model.transform.position;
        Quaternion rootRotation = model.transform.rotation;
        Vector3 initialHead = head.position;
        Vector3 previousHead = head.position;
        float movement = 0f;
        float deadline = Time.realtimeSinceStartup + 4.1f;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            movement = Mathf.Max(movement, Vector3.Distance(initialHead, head.position));
            Assert.That(Vector3.Distance(previousHead, head.position), Is.LessThan(0.015f), "반복 경계에서 머리가 튀지 않아야 한다");
            previousHead = head.position;
        }
        Assert.That(movement, Is.GreaterThan(0.0005f), "정지 포즈가 아니라 실제 뼈 애니메이션이 재생되어야 한다");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(2f));
        Assert.That(Vector3.Distance(model.transform.position, rootPosition), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(model.transform.rotation, rootRotation), Is.LessThan(0.001f));
        var flow = Object.FindObjectOfType<IntroFlowController>();
        Assert.That(flow.BeginBarSequence(), Is.True);
        var director = (PlayableDirector)new SerializedObject(flow).FindProperty("barDirector").objectReferenceValue;
        Directory.CreateDirectory("Logs/IntroDealer/Final");
        foreach (float time in new[] { 4.85f, 8.2f, 11.4f })
        {
            deadline = Time.realtimeSinceStartup + 15f;
            while (flow.State == IntroFlowController.IntroState.BarCutscene && director.time < time)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            IntroDealerSetupEditor.Capture(Camera.main, $"Logs/IntroDealer/Final/Idle-{time:F2}.png");
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(Vector3.Distance(model.transform.position, rootPosition), Is.LessThan(0.0001f));
        Assert.That(flow.TryInteract(), Is.True);
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        IntroDealerSetupEditor.Capture(Camera.main, "Logs/IntroDealer/Final/Dialogue.png");
        yield return new ExitPlayMode();
    }
}
