using System;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DefeatCutsceneSetupEditor
{
    public const string ScenePath = "Assets/_Game/Scenes/Dev_Yujin.unity";
    public const string Folder = "Assets/_Game/Scenes/Defeat";
    public const string TimelinePath = Folder + "/Defeat.playable";

    public static void SetupBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SetupOpenScene();
    }

    [MenuItem("Tools/Defeat/Configure Open Gameplay Scene (create once)")]
    public static void SetupOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == ScenePath,
            "Open Dev_Yujin in Edit Mode first.");
        if (All<DefeatCutsceneController>(scene).Length > 0)
        {
            // Re-running setup is strictly read-only once authored. Never reset poses,
            // curves, Inspector values, or even intentionally removed bindings.
            ValidateOpenScene();
            Debug.Log("Existing Defeat preserved. No scene or asset values were rewritten.");
            return;
        }
        Require(!Directory.Exists(Folder), "Defeat assets already exist without a scene controller. Restore its scene backup; refusing to overwrite assets.");
        var game = All<GameplayController>(scene).Single();
        var victory = All<VictoryCutsceneController>(scene).Single();
        var victoryRig = Ref<CutsceneCameraRig>(victory, "cameraRig");
        var output = Ref<Camera>(victoryRig, "outputCamera");
        var player = Ref<CinemachineVirtualCamera>(All<SwitchCamera>(scene).Single(), "playerCam");
        var dealer = victory.transform.Find("Dealer").GetComponent<CinemachineVirtualCamera>();
        var sourceDialogue = Ref<DialogueController>(victory, "dialogue");
        var sourcePanel = Ref<GameObject>(victory, "dialoguePanel");
        Require(output != null && player != null && dealer != null && sourceDialogue != null && sourcePanel != null,
            "Gameplay camera / Victory dialogue references are incomplete.");
        Require(Ref<DefeatCutsceneController>(game, "defeatCutscene") == null, "Gameplay already references a different Defeat controller.");

        string backup = "Logs/Defeat/Setup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/Dev_Yujin.disk.unity");
        // saveAsCopy captures unsaved Inspector work without replacing the open scene.
        Require(EditorSceneManager.SaveScene(scene, backup + "/Dev_Yujin.unsaved.unity", true), "Could not back up the open scene.");
        AssetDatabase.CreateFolder("Assets/_Game/Scenes", "Defeat");

        var root = new GameObject("Defeat Cutscene");
        Undo.RegisterCreatedObjectUndo(root, "Create Defeat Cutscene");
        var flow = root.AddComponent<DefeatCutsceneController>();
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.extrapolationMode = DirectorWrapMode.None;
        var rig = root.AddComponent<CutsceneCameraRig>();
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, TimelinePath);
        timeline.editorSettings.frameRate = 60;
        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
        director.playableAsset = timeline;
        Set(flow, "director", director); Set(flow, "cameraRig", rig); Set(flow, "victoryCutscene", victory);
        CopyArray(victory, flow, "gameplayHud"); CopyArray(victory, flow, "suspendedInputs");

        var cameras = new[] {
            Camera(root.transform, "Entry", output.transform, LensSettings.FromCamera(output)),
            Camera(root.transform, "Dealer", dealer.transform, dealer.m_Lens),
            Camera(root.transform, "Player POV", player.transform, player.m_Lens)
        };
        // Dedicated, static shot transforms. Original gameplay/Victory cameras remain untouched.
        Set(rig, "outputCamera", output); Set(rig, "virtualCamera", cameras[0]); SetArray(rig, "shotCameras", cameras);
        var cameraGroup = timeline.CreateTrack<GroupTrack>(null, "CAMERAS");
        var cameraTrack = timeline.CreateTrack<CinemachineTrack>(cameraGroup, "Entry / Dealer / Player POV - overlap to blend");
        director.SetGenericBinding(cameraTrack, output.GetComponent<CinemachineBrain>());
        double[] starts = { 0, 0.6, 2.6 }, ends = { 1.8, 3.6, 11.5 };
        for (int i = 0; i < cameras.Length; i++)
        {
            var clip = cameraTrack.CreateClip<CinemachineShot>();
            clip.displayName = cameras[i].name; clip.start = starts[i]; clip.duration = ends[i] - starts[i];
            var shot = (CinemachineShot)clip.asset;
            shot.DisplayName = cameras[i].name; shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            director.SetReferenceValue(shot.VirtualCamera.exposedName, cameras[i]);
        }

        var screen = new GameObject("Defeat Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        screen.transform.SetParent(root.transform, false);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var sourceCanvas = Ref<GameObject>(Ref<CutsceneOverlay>(victory, "overlay"), "uiRoot").GetComponent<CanvasScaler>();
        if (sourceCanvas != null) EditorUtility.CopySerialized(sourceCanvas, screen.GetComponent<CanvasScaler>());
        Set(flow, "screenRoot", screen);
        // Always block world/UI pointer events, even when the animated shade is transparent.
        var shield = Image(screen.transform, "Input Shield", new Color(0, 0, 0, 0));
        shield.raycastTarget = true;
        var noise = new GameObject("Digital Noise", typeof(RectTransform), typeof(RawImage));
        Stretch(noise.transform, screen.transform);
        noise.GetComponent<RawImage>().texture = CreateNoiseTexture();
        noise.GetComponent<RawImage>().raycastTarget = false;
        var shade = Image(screen.transform, "Blackout", Color.black);
        var captionObject = new GameObject("YOU LOST", typeof(RectTransform), typeof(TextMeshProUGUI));
        Stretch(captionObject.transform, screen.transform);
        var caption = captionObject.GetComponent<TextMeshProUGUI>();
        var sourceCaption = Ref<TMP_Text>(Ref<CutsceneOverlay>(victory, "overlay"), "caption");
        caption.font = sourceCaption.font; caption.fontSharedMaterial = sourceCaption.fontSharedMaterial;
        caption.fontSize = sourceCaption.fontSize; caption.characterSpacing = sourceCaption.characterSpacing;
        caption.alignment = TextAlignmentOptions.Center; caption.text = "YOU LOST.";
        caption.color = new Color(0.82f, 0.86f, 0.89f); caption.raycastTarget = false;

        var dialogue = Child(root.transform, "Defeat Dialogue").gameObject.AddComponent<DialogueController>();
        EditorUtility.CopySerialized(sourceDialogue, dialogue);
        var panel = Object.Instantiate(sourcePanel, screen.transform, false);
        panel.name = "Defeat Dialogue Panel";
        string textPath = AnimationUtility.CalculateTransformPath(Ref<TMP_Text>(sourceDialogue, "dialogueText").transform, sourcePanel.transform);
        Set(dialogue, "dialogueText", panel.transform.Find(textPath).GetComponent<TMP_Text>());
        Set(dialogue, "dialoguePanel", panel);
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, dialogue.Next);
        }
        panel.SetActive(false); dialogue.gameObject.SetActive(false);
        Set(flow, "dialogue", dialogue); Set(flow, "dialoguePanel", panel);

        var fx = Child(root.transform, "Defeat Volume").gameObject.AddComponent<Volume>();
        fx.isGlobal = true; fx.weight = 0;
        fx.priority = All<Volume>(scene).Where(v => v != fx).Select(v => v.priority).DefaultIfEmpty(0).Max() + 10;
        fx.gameObject.layer = output.gameObject.layer;
        var cameraData = output.GetComponent<UniversalAdditionalCameraData>();
        int mask = cameraData != null ? cameraData.volumeLayerMask.value : 1;
        for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) { fx.gameObject.layer = i; break; }
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, Folder + "/DefeatVolume.asset");
        var vignette = profile.Add<Vignette>(); vignette.intensity.Override(0.43f); vignette.smoothness.Override(0.7f);
        var bloom = profile.Add<Bloom>(); bloom.intensity.Override(0.75f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.65f);
        var chromatic = profile.Add<ChromaticAberration>(); chromatic.intensity.Override(0.32f);
        var distortion = profile.Add<LensDistortion>(); distortion.intensity.Override(-0.10f); distortion.scale.Override(1.025f);
        var color = profile.Add<ColorAdjustments>(); color.saturation.Override(-48); color.contrast.Override(-8); color.postExposure.Override(-0.12f);
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile); fx.sharedProfile = profile; Set(flow, "effectsVolume", fx);

        var effects = timeline.CreateTrack<GroupTrack>(null, "SCREEN EFFECTS - recovery 8.15 to 8.65");
        Animate(timeline, director, effects, fx.gameObject, "Vision Distortion and Recovery", typeof(Volume), "weight",
            new[] {0f, 4.2f, 5.2f, 6.6f, 8.05f, 8.15f, 11.5f}, new[] {0f, 0f, 0.2f, 0.57f, 1f, 0f, 0f});
        Alpha(timeline, director, effects, noise, "Intermittent Digital Noise",
            new[] {0f, 5.5f, 5.56f, 5.65f, 6.35f, 6.42f, 6.52f, 7.12f, 7.20f, 7.32f, 7.8f, 8.04f, 8.15f, 11.5f},
            new[] {0f, 0f, 0.035f, 0f, 0f, 0.055f, 0f, 0f, 0.07f, 0f, 0.085f, 0.085f, 0f, 0f}, true);
        var captions = timeline.CreateTrack<GroupTrack>(null, "FADE / CAPTION");
        Alpha(timeline, director, captions, shade.gameObject, "Sudden Blackout",
            new[] {0f, 8.65f, 11.5f}, new[] {0f, 1f, 1f}, true);
        Alpha(timeline, director, captions, captionObject, "YOU LOST - restrained fade",
            new[] {0f, 9.25f, 9.65f, 10.7f, 11.25f, 11.5f}, new[] {0f, 0f, 1f, 1f, 0f, 0f});

        var audioGroup = timeline.CreateTrack<GroupTrack>(null, "AUDIO");
        var muffle = Child(root.transform, "Ambient Muffle").gameObject.AddComponent<DefeatAudioMuffle>();
        Set(flow, "audioMuffle", muffle);
        Animate(timeline, director, audioGroup, muffle.gameObject, "Ambient Low Pass - runtime sources", typeof(DefeatAudioMuffle), "amount",
            new[] {0f, 4.2f, 5.7f, 8.05f, 8.15f, 8.65f, 8.7f, 11.5f}, new[] {0f, 0f, 0.4f, 1f, 0f, 0f, 1f, 1f});
        var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = 0.22f;
        audio.outputAudioMixerGroup = Ref<UnityEngine.Audio.AudioMixerGroup>(sourceDialogue, "dialogueSfxMixerGroup");
        var audioTrack = timeline.CreateTrack<AudioTrack>(audioGroup, "Blackout Click - replace / move here");
        director.SetGenericBinding(audioTrack, audio);
        var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Shift - Complete Sci-Fi UI/Audio/Click.ogg");
        if (audioClip != null)
        {
            var clip = audioTrack.CreateClip<AudioPlayableAsset>(); clip.start = 8.65;
            clip.duration = Math.Min(0.35, audioClip.length); clip.displayName = "Quiet digital click";
            ((AudioPlayableAsset)clip.asset).clip = audioClip;
        }
        var eventGroup = timeline.CreateTrack<GroupTrack>(null, "DIALOGUE / EVENTS");
        var events = timeline.CreateTrack<SignalTrack>(eventGroup, "Dialogue gates / Finish");
        var receiver = root.AddComponent<SignalReceiver>(); director.SetGenericBinding(events, receiver);
        Signal(events, receiver, "Dealer_Declaration", 2.2, flow.BeginDealerDialogue);
        Signal(events, receiver, "Final_Warning", 4.05, flow.BeginFinalWarning);
        Signal(events, receiver, "Finish", 11.5, flow.CompleteTimeline);
        Set(game, "defeatCutscene", flow);
        EditorUtility.SetDirty(timeline);
        ValidateOpenScene();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save configured scene.");
        Debug.Log("Defeat Timeline created and bound. Scene backup: " + backup);
    }

    [MenuItem("Tools/Defeat/Validate Open Gameplay Scene")]
    public static void ValidateOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        var flow = All<DefeatCutsceneController>(scene).Single();
        var director = Ref<PlayableDirector>(flow, "director");
        Require(director != null && director.playableAsset is TimelineAsset, "Defeat director / Timeline missing.");
        foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks().Where(t => !t.mutedInHierarchy))
            Require(director.GetGenericBinding(track) != null, "Missing binding: " + track.name);
        Require(flow.IsConfigured, "Defeat controller / shot references / signal callbacks are incomplete. Duration: " + director.duration.ToString("R"));
        Require(!director.playOnAwake, "Defeat must not autoplay.");
        Require(Ref<DefeatCutsceneController>(All<GameplayController>(scene).Single(), "defeatCutscene") == flow, "Gameplay trigger missing.");
        var timeline = (TimelineAsset)director.playableAsset;
        var cameras = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single().GetClips().OrderBy(c => c.start).ToArray();
        Require(cameras[0].start == 0 && ((CinemachineShot)cameras[0].asset).VirtualCamera.Resolve(director) ==
            Ref<CinemachineVirtualCamera>(Ref<CutsceneCameraRig>(flow, "cameraRig"), "virtualCamera"), "Entry snapshot must be the first Shot at zero.");
        var volume = Ref<Volume>(flow, "effectsVolume");
        Require(AssetDatabase.GetAssetPath(volume.sharedProfile).StartsWith(Folder + "/"), "Defeat must use its own Volume Profile.");
        Require(All<Volume>(scene).All(v => v == volume || v.sharedProfile != volume.sharedProfile), "Defeat Volume Profile must not be shared with gameplay.");
        var markers = timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>().ToArray();
        double first = markers.Single(m => m.asset.name == "Dealer_Declaration").time;
        double second = markers.Single(m => m.asset.name == "Final_Warning").time;
        double end = markers.Single(m => m.asset.name == "Finish").time;
        Require(first > 0 && second > first && end > second && end <= director.duration + 0.001, "Signal order/duration invalid.");
        Debug.Log("Defeat: all active Track bindings, three Shots, dialogue/finish Signals and dedicated Volume validated.");
    }

    [MenuItem("Tools/Defeat/Preview Final Defeat (Play Mode)")]
    public static void PreviewDefeat()
    {
        Require(EditorApplication.isPlaying, "Enter Play Mode first. This is an isolated cutscene preview, not a settlement test.");
        var flow = Object.FindObjectOfType<DefeatCutsceneController>();
        Require(flow != null, "Defeat scene controller missing.");
        // Do not reset an active session or interfere with a running Victory.
        if (flow.IsPlaying) return;
        var victory = Object.FindObjectOfType<VictoryCutsceneController>();
        Require(victory == null || !victory.IsPlaying, "Wait for the active Victory cutscene to finish.");
        var game = Object.FindObjectOfType<GameplayController>();
        var presentation = Object.FindObjectOfType<GameplayPresentationController>();
        Require(presentation == null || !presentation.IsBusy, "Wait for card/chip presentation to finish before previewing.");
        bool wasEnabled = game != null && game.enabled;
        if (wasEnabled) game.enabled = false; // Also cancels any pending dealer coroutine.
        Action restore = null;
        restore = () => { flow.Completed -= restore; if (game != null) game.enabled = wasEnabled; };
        flow.ResetForGame();
        flow.Completed += restore;
        if (!flow.TryPlay(GamePhase.GameOver, GameWinner.Dealer, true)) restore();
    }

    private static CinemachineVirtualCamera Camera(Transform parent, string name, Transform pose, LensSettings lens)
    {
        var camera = Child(parent, name).gameObject.AddComponent<CinemachineVirtualCamera>();
        camera.Priority = -1000; camera.m_Lens = lens;
        camera.ForceCameraPosition(pose.position, pose.rotation);
        return camera;
    }
    private static Image Image(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); Stretch(go.transform, parent);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static void Stretch(Transform child, Transform parent)
    {
        child.SetParent(parent, false);
        var rect = (RectTransform)child; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Texture2D CreateNoiseTexture()
    {
        var texture = new Texture2D(256, 128, TextureFormat.RGBA32, false) { name = "Restrained Digital Noise", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var random = new System.Random(811);
        var pixels = new Color[256 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++)
        {
            float value = (float)random.NextDouble();
            pixels[y * 256 + x] = new Color(value * 0.82f, value * 0.92f, value, y % 9 == 0 ? 0.8f : 0.4f);
        }
        texture.SetPixels(pixels); texture.Apply();
        AssetDatabase.CreateAsset(texture, Folder + "/DigitalNoise.asset"); return texture;
    }
    private static void Alpha(TimelineAsset timeline, PlayableDirector director, GroupTrack group, GameObject target,
        string name, float[] times, float[] values, bool stepped = false)
    {
        var canvasGroup = target.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0; canvasGroup.blocksRaycasts = canvasGroup.interactable = false;
        Animate(timeline, director, group, target, name, typeof(CanvasGroup), "m_Alpha", times, values, stepped);
    }
    private static void Animate(TimelineAsset timeline, PlayableDirector director, GroupTrack group, GameObject target,
        string name, Type type, string property, float[] times, float[] values, bool stepped = false)
    {
        var animator = target.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var track = timeline.CreateTrack<AnimationTrack>(group, name); director.SetGenericBinding(track, animator);
        var animation = new AnimationClip { name = name, frameRate = 60 };
        var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, values[i])).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve("", type, property), curve);
        AssetDatabase.CreateAsset(animation, Folder + "/" + name.Replace(' ', '_') + ".anim");
        var clip = track.CreateClip<AnimationPlayableAsset>(); clip.start = 0; clip.duration = times.Last(); clip.displayName = name;
        var playable = (AnimationPlayableAsset)clip.asset;
        playable.clip = animation; playable.removeStartOffset = false; playable.applyFootIK = false; playable.loop = AnimationPlayableAsset.LoopMode.Off;
    }
    private static void Signal(SignalTrack track, SignalReceiver receiver, string name, double time, UnityAction callback)
    {
        var signal = ScriptableObject.CreateInstance<SignalAsset>(); signal.name = name;
        AssetDatabase.CreateAsset(signal, Folder + "/" + name + ".signal");
        var marker = track.CreateMarker<SignalEmitter>(time);
        marker.name = name; marker.asset = signal; marker.emitOnce = true; marker.retroactive = false;
        var reaction = new UnityEvent(); UnityEventTools.AddPersistentListener(reaction, callback); receiver.AddReaction(signal, reaction);
    }
    private static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static T Ref<T>(Object target, string field) where T : Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    private static void Set(Object target, string field, Object value)
    { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void CopyArray(Object source, Object target, string field)
    {
        var array = new SerializedObject(source).FindProperty(field);
        SetArray(target, field, Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray());
    }
    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target); var array = so.FindProperty(field); array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
