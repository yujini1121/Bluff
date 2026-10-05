using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class IntroSceneSkeletonTests
{
    private bool gameplayLoaded;
    private float alphaAtLoad;
    private CanvasGroup transitionGroup;

    private void OnGameplayLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != IntroFlowController.GameplaySceneName) return;
        gameplayLoaded = true;
        alphaAtLoad = transitionGroup.alpha;
    }
    [UnityTest]
    public IEnumerator ActualScene_Door_Backtrack_Bar_E_Dialogue_Fade_ActualGameplayLoad()
    {
        EditorSceneManager.OpenScene(IntroCutsceneSetupEditor.IntroPath);
        IntroCutsceneSetupEditor.ValidateOpenScene();
        UnityEditor.SessionState.SetBool("Intro.FinalCapture", SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null);
        yield return new EnterPlayMode();
        SceneManager.LoadScene("Intro");
        yield return null;
        yield return null;
        yield return Seconds(0.2f);
        var flow = UnityEngine.Object.FindObjectOfType<IntroFlowController>();
        var player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        var trigger = UnityEngine.Object.FindObjectOfType<IntroDoorTrigger>();
        var dialogue = UnityEngine.Object.FindObjectOfType<DialogueController>();
        Assert.That(flow, Is.Not.Null, "Loaded Intro flow");
        Assert.That(player, Is.Not.Null, "Loaded Intro player");
        Assert.That(trigger, Is.Not.Null, "Loaded Intro trigger");
        Assert.That(dialogue, Is.Not.Null, "Loaded Intro dialogue");
        var body = player.GetComponent<Rigidbody>();
        var door = Read<PlayableDirector>(trigger, "doorRevealDirector");
        var bar = Read<PlayableDirector>(flow, "barDirector");
        var prompt = Read<GameObject>(flow, "interactionPrompt");
        var fade = UnityEngine.Object.FindObjectOfType<IntroFadeController>();
        Assert.That(fade, Is.Not.Null, "Loaded Intro fade");
        var group = fade.GetComponent<CanvasGroup>();
        Assert.That(GameObject.Find("PF_Dealer"), Is.Not.Null, "Locally added Dealer is active");
        var model = GameObject.Find("PF_Dealer").transform;
        Vector3 originalDealerPosition = model.position;
        Assert.That(player.IsMoveInputEnabled, Is.True);
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.Corridor));
        Assert.That(prompt.activeSelf, Is.False);
        Assert.That(Camera.allCamerasCount, Is.EqualTo(1));
        Assert.That(UnityEngine.Object.FindObjectsOfType<AudioListener>().Length, Is.EqualTo(1));
        Capture("01_Corridor");
        body.useGravity = false;
        Vector3 center = trigger.GetComponent<BoxCollider>().bounds.center;
        center.y = body.position.y;
        body.position = center;
        yield return Seconds(0.15f);
        Assert.That(door.time, Is.GreaterThan(0));
        Assert.That(player.IsMoveInputEnabled, Is.True, "Door opening must not lock movement or look");
        body.position = center + Vector3.left * 4f;
        yield return Seconds(1.2f);
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.Corridor));
        Assert.That(player.IsMoveInputEnabled, Is.True);
        Assert.That(door.time, Is.GreaterThanOrEqualTo(door.duration - 0.02d));
        var doorBindingList = new System.Collections.Generic.List<Animator>();
        foreach (var output in door.playableAsset.outputs)
            doorBindingList.Add((Animator)door.GetGenericBinding(output.sourceObject));
        var doorBindings = doorBindingList.ToArray();
        Vector3[] openPoses = doorBindings.Select(a => a.transform.position).ToArray();
        body.position = center;
        yield return Seconds(0.15f);
        Assert.That(door.time, Is.GreaterThan(0.9d), "Re-entry must not restart opening");
        body.rotation = Quaternion.Euler(0, 90, 0);
        body.position = center + Vector3.right * 4f;
        yield return Seconds(0.15f);
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.BarCutscene));
        Assert.That(player.IsMoveInputEnabled, Is.False);
        Assert.That(player.transform.position.x, Is.EqualTo(1.238f).Within(0.01f));
        Assert.That(player.transform.position.z, Is.EqualTo(15.83f).Within(0.01f));
        var hold = Read<IntroHoldToSkip>(flow, "holdToSkip");
        Assert.That(hold.IsAvailable, Is.True);
        yield return Seconds(2.7f);
        Quaternion leftLook = Camera.main.transform.rotation;
        Capture("02_LookLeft");
        yield return Seconds(1.8f);
        Assert.That(Quaternion.Angle(leftLook, Camera.main.transform.rotation), Is.GreaterThan(30f), "The look-around must remain visible");
        Capture("03_LookRight");
        float deadline = Time.realtimeSinceStartup + 15f;
        while (flow.State == IntroFlowController.IntroState.BarCutscene)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            yield return null;
        }
        yield return Seconds(0.5f);
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.DealerInteraction));
        Assert.That(player.IsMoveInputEnabled, Is.True);
        Assert.That(dialogue.IsRunning, Is.False, "Must wait for E");
        Assert.That(prompt.activeSelf, Is.True);
        Assert.That(hold.IsAvailable, Is.True);
        Assert.That(Camera.main.transform.position.x, Is.EqualTo(9.65f).Within(0.02f));
        Assert.That(Camera.main.transform.position.z, Is.EqualTo(14.8f).Within(0.02f));
        Capture("04_Dealer_E_Wait");
        Assert.That(Vector3.Distance(model.position, originalDealerPosition), Is.LessThan(0.001f));
        Transform target = Read<Transform>(flow, "dealerLookTarget");
        Vector3 screen = Camera.main.WorldToViewportPoint(target.position);
        Assert.That(screen.z, Is.GreaterThan(0f));
        Assert.That(screen.x, Is.InRange(0.35f, 0.65f));
        Assert.That(screen.y, Is.InRange(0.35f, 0.65f));
        for (int i = 0; i < doorBindings.Length; i++)
            Assert.That(Vector3.Distance(doorBindings[i].transform.position, openPoses[i]), Is.LessThan(0.001f));
        yield return Seconds(0.5f);
        Assert.That(dialogue.IsRunning, Is.False, "Waiting does not auto-start dialogue");
        Assert.That(flow.TryInteract(), Is.True, "Exercise the exact handler used by E");
        Assert.That(prompt.activeSelf, Is.False);
        Assert.That(player.IsMoveInputEnabled, Is.False);
        Assert.That(dialogue.CurrentLineIndex, Is.Zero);
        Capture("05_Dialogue");
        yield return Seconds(0.1f);
        while (dialogue.IsRunning) dialogue.Next();
        Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.Transition));
        yield return Seconds(0.3f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Intro"));
        Assert.That(group.alpha, Is.InRange(0.05f, 0.99f), "Fade Out precedes loading");
        gameplayLoaded = false;
        alphaAtLoad = -1;
        transitionGroup = group;
        SceneManager.sceneLoaded += OnGameplayLoaded;
        deadline = Time.realtimeSinceStartup + 30f;
        while (!gameplayLoaded)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Actual Gameplay load timed out");
            yield return null;
        }
        SceneManager.sceneLoaded -= OnGameplayLoaded;
        Assert.That(alphaAtLoad, Is.EqualTo(1f).Within(0.0001f), "Scene activates behind opaque black");
        Capture("06_Gameplay_Black");
        yield return Seconds(0.4f);
        Assert.That(group.alpha, Is.InRange(0.01f, 0.99f), "Fade In runs after Gameplay load");
        yield return Seconds(0.8f);
        Assert.That(fade == null, Is.True, "Persistent overlay must clean itself up");
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(IntroFlowController.GameplaySceneName));
        Capture("07_Gameplay");
        yield return Seconds(0.2f);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ActualScene_HoldSkip_FromEveryPhase_LoadsGameplay()
    {
        EditorSceneManager.OpenScene(IntroCutsceneSetupEditor.IntroPath);
        yield return new EnterPlayMode();
        for (int phase = 0; phase < 4; phase++)
        {
            SceneManager.LoadScene("Intro");
            yield return Seconds(0.3f);
            var flow = UnityEngine.Object.FindObjectOfType<IntroFlowController>();
            var player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
            var hold = Read<IntroHoldToSkip>(flow, "holdToSkip");
            var keycap = Read<GameObject>(hold, "keycap");
            var bar = Read<PlayableDirector>(flow, "barDirector");
            var fade = UnityEngine.Object.FindObjectOfType<IntroFadeController>();
            if (phase >= 1) Assert.That(flow.BeginBarSequence(), Is.True);
            if (phase >= 2)
            {
                bar.time = bar.duration;
                yield return null;
                yield return null;
            }
            if (phase == 3) Assert.That(flow.TryInteract(), Is.True);
            Assert.That((int)flow.State, Is.EqualTo(phase));
            Assert.That(hold.IsAvailable, Is.True);
            Assert.That(keycap.activeSelf, Is.False, "Never show ESC before a press in any phase");
            hold.enabled = false;
            hold.SetAvailable(true);
            hold.Tick(true, 0.65f);
            Assert.That(keycap.activeSelf, Is.True);
            UnityEditor.SessionState.SetBool("Intro.FinalCapture", true);
            Capture("Skip_" + phase + "_Hold");
            hold.Tick(false, 0.2f);
            Assert.That(hold.Progress, Is.Zero);
            Assert.That(keycap.activeSelf, Is.False);
            hold.Tick(true, 0.65f);
            Assert.That((int)flow.State, Is.EqualTo(phase), "Released holds must not accumulate");
            hold.Tick(true, 0.6f);
            Assert.That(flow.State, Is.EqualTo(IntroFlowController.IntroState.Transition));
            flow.SkipIntro(); // A repeated request must not start a second transition.
            Assert.That(hold.IsAvailable, Is.False);
            Assert.That(keycap.activeSelf, Is.False);
            Assert.That(player.IsMoveInputEnabled, Is.False);
            Assert.That(bar.state, Is.Not.EqualTo(PlayState.Playing));
            Assert.That(Read<GameObject>(flow, "interactionPrompt").activeSelf, Is.False);
            Assert.That(fade.IsTransitioning, Is.True);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (fade != null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(IntroFlowController.GameplaySceneName));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        yield return new ExitPlayMode();
    }
    private static T Read<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object
    {
        Assert.That(target != null, Is.True, "Read target " + field);
        var property = new UnityEditor.SerializedObject(target).FindProperty(field);
        Assert.That(property, Is.Not.Null, "Serialized field " + target.GetType().FullName + "." + field);
        Assert.That(property.objectReferenceValue != null, Is.True, "Assigned field " + field);
        return (T)property.objectReferenceValue;
    }
    private static void Capture(string name)
    {
        if (!UnityEditor.SessionState.GetBool("Intro.FinalCapture", false)) return;
        System.IO.Directory.CreateDirectory("Logs/IntroRebuildReview");
        string destination = System.IO.Path.GetFullPath("Logs/IntroRebuildReview/" + name + ".png");
        // Capture at a stable resolution, independent of the Editor Game View panel size.
        Camera camera = Camera.main;
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>()
            .Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        try
        {
            foreach (Canvas canvas in canvases)
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
            System.IO.File.WriteAllBytes(destination, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            foreach (Canvas canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
    private static IEnumerator Seconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
