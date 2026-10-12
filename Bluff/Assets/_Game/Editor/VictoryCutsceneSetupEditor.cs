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
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class VictoryCutsceneSetupEditor
{
    public const string ScenePath = "Assets/_Game/Scenes/Dev_Yujin.unity";
    public const string Folder = "Assets/_Game/Scenes/Victory";
    public const string TimelinePath = Folder + "/Victory.playable";

    public static void SetupBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SetupOpenScene();
    }

    [MenuItem("Tools/Victory/Configure Open Gameplay Scene")]
    public static void SetupOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == ScenePath,
            "Open Dev_Yujin in Edit Mode first.");
        var flow = All<VictoryCutsceneController>(scene).Single();
        var director = Ref<PlayableDirector>(flow, "director");
        var timeline = director.playableAsset as TimelineAsset;
        Require(timeline != null, "Restore the existing Victory Timeline and scene references first.");
        if (!timeline.GetOutputTracks().OfType<CinemachineTrack>().Any()) Migrate(flow, director, timeline);
        // Repair missing bindings only; never overwrite an existing binding or authored pose/key.
        BindMissing(flow, director, timeline);
        ValidateOpenScene();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Victory/Reload Saved Victory Scene")]
    public static void ReloadSavedScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(TimelinePath))
            Resources.UnloadAsset(asset);
        AssetDatabase.ImportAsset(TimelinePath, ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/Victory/Migrate Legacy Timeline Once (with backup)")]
    public static void MigrateOpenScene() => SetupOpenScene();

    private static void Migrate(VictoryCutsceneController flow, PlayableDirector director, TimelineAsset timeline)
    {
        // Fail before changing objects or assets when the legacy setup is incomplete.
        var rig = Ref<CutsceneCameraRig>(flow, "cameraRig");
        var output = Ref<Camera>(rig, "outputCamera");
        var entry = Ref<CinemachineVirtualCamera>(rig, "virtualCamera");
        Transform root = flow.transform;
        var path = root.Find("Timeline Camera Path");
        var legacy = timeline.GetOutputTracks().OfType<AnimationTrack>().Single();
        var source = ((AnimationPlayableAsset)legacy.GetClips().Single().asset).clip;
        var chip = Ref<Transform>(flow, "memoryChip");
        var reveal = root.Cast<Transform>().SingleOrDefault(t => t.name.StartsWith("Chip Reveal Point"));
        var receive = root.Find("Chip Receive Point");
        var overlay = Ref<CutsceneOverlay>(flow, "overlay");
        Require(output != null && output.GetComponent<CinemachineBrain>() != null && entry != null &&
            path != null && path.GetComponent<Animator>() != null && source != null && chip != null &&
            reveal != null && receive != null && overlay != null && overlay.IsConfigured &&
            Ref<Renderer>(flow,"chipGlow") != null, "Legacy Victory references are incomplete; no migration performed.");
        foreach (string name in new[] { "Chip_Reveal.anim", "Chip_Transfer.anim", "Chip_Activate.anim",
            "Memory_Flash.anim", "Fade.anim", "UNLOCK.anim", "YOU_CHOSE_TO_FORGET..anim",
            "Dialogue_Start.signal", "Memory_Playback.signal", "Return_Camera.signal", "Finish.signal" })
            Require(!File.Exists(Folder + "/" + name), "Migration would overwrite an authored asset: " + name);
        // Disk copies include unsaved authored changes and keep the original Timeline GUID intact.
        EditorSceneManager.SaveScene(flow.gameObject.scene);
        AssetDatabase.SaveAssets();
        string backup = "Logs/VictoryTimeline/Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/Dev_Yujin.unity");
        File.Copy(TimelinePath, backup + "/Victory.playable");
        File.Copy(TimelinePath + ".meta", backup + "/Victory.playable.meta");

        var originalPosition = path.localPosition;
        var originalRotation = path.localRotation;
        var cameras = new CinemachineVirtualCamera[5];
        cameras[0] = entry;
        entry.name = "Entry";
        entry.Priority = -1000;
        entry.enabled = true;
        entry.ForceCameraPosition(output.transform.position, output.transform.rotation);
        string[] names = { "Dealer", "Chip Close-up", "Handoff", "Memory Dealer" };
        float[] samples = { 4, 6, 8, 9.6f };
        for (int i = 0; i < names.Length; i++)
        {
            source.SampleAnimation(path.gameObject, samples[i]);
            var camera = Child(root, names[i]).gameObject.AddComponent<CinemachineVirtualCamera>();
            camera.m_Lens = entry.m_Lens;
            camera.Priority = -1000;
            camera.ForceCameraPosition(path.position, path.rotation);
            cameras[i + 1] = camera;
        }
        path.localPosition = originalPosition;
        path.localRotation = originalRotation;
        legacy.muted = true;
        legacy.name = "Legacy Camera Path (muted migration reference)";
        path.GetComponent<Animator>().enabled = false;
        SetArray(rig, "shotCameras", cameras);
        var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "CAMERAS - edit shots / overlap to blend");
        director.SetGenericBinding(cameraTrack, output.GetComponent<CinemachineBrain>());
        double[] starts = { 0, 1, 4, 6, 9.5 };
        double[] ends = { 4, 5, 8, 9.6, 14 };
        for (int i = 0; i < cameras.Length; i++)
        {
            var shotClip = cameraTrack.CreateClip<CinemachineShot>();
            shotClip.displayName = cameras[i].name;
            shotClip.start = starts[i];
            shotClip.duration = ends[i] - starts[i];
            var shot = (CinemachineShot)shotClip.asset;
            shot.DisplayName = cameras[i].name;
            shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            director.SetReferenceValue(shot.VirtualCamera.exposedName, cameras[i]);
            if (i > 0) shotClip.blendInDuration = ends[i - 1] - starts[i];
            if (i < 4) shotClip.blendOutDuration = ends[i] - starts[i + 1];
        }

        Transform motionRoot = Child(root, "Memory Chip Animation");
        chip.SetParent(motionRoot, true);
        var chipAnimator = motionRoot.gameObject.AddComponent<Animator>();
        var glow = chip.gameObject.AddComponent<MemoryChipGlow>();
        Set(glow, "target", Ref<Renderer>(flow, "chipGlow"));
        string chipPath = AnimationUtility.CalculateTransformPath(chip, motionRoot);
        var chipGroup = timeline.CreateTrack<GroupTrack>(null, "MEMORY CHIP");
        var movement = timeline.CreateTrack<AnimationTrack>(chipGroup, "Chip Transform + Emission (Animation window)");
        movement.trackOffset = TrackOffset.ApplyTransformOffsets;
        director.SetGenericBinding(movement, chipAnimator);
        Vector3 from = motionRoot.InverseTransformPoint(reveal.position);
        Vector3 to = motionRoot.InverseTransformPoint(receive.position);
        Vector3 rotationFrom = (Quaternion.Inverse(motionRoot.rotation) * reveal.rotation).eulerAngles;
        Vector3 rotationTo = rotationFrom + new Vector3(Mathf.DeltaAngle(rotationFrom.x, receive.eulerAngles.x),
            Mathf.DeltaAngle(rotationFrom.y, receive.eulerAngles.y), Mathf.DeltaAngle(rotationFrom.z, receive.eulerAngles.z));
        var revealMotion = NewAnimation("Chip_Reveal");
        VectorCurves(revealMotion, chipPath, "m_LocalPosition", new[] { 0f, 0.7f, 1f },
            new[] { from - Vector3.up * 0.12f, from, from });
        VectorCurves(revealMotion, chipPath, "localEulerAnglesRaw", new[] { 0f, 1f }, new[] { rotationFrom, rotationFrom });
        FloatCurve(revealMotion, chipPath, typeof(MemoryChipGlow), "intensity", new[] { 0f, 1f }, new[] { 0.25f, 0.25f });
        AddAnimation(movement, revealMotion, 5, 1, "Reveal");
        var transfer = NewAnimation("Chip_Transfer");
        VectorCurves(transfer, chipPath, "m_LocalPosition", new[] { 0f, 0.5f, 1f, 1.5f, 2f },
            new[] { from, Vector3.Lerp(from,to,0.15625f) + Vector3.up * 0.05657f,
                Vector3.Lerp(from,to,0.5f) + Vector3.up * 0.08f,
                Vector3.Lerp(from,to,0.84375f) + Vector3.up * 0.05657f, to });
        VectorCurves(transfer, chipPath, "localEulerAnglesRaw", new[] { 0f, 2f }, new[] { rotationFrom, rotationTo });
        FloatCurve(transfer, chipPath, typeof(MemoryChipGlow), "intensity", new[] { 0f, 2f }, new[] { 0.25f, 0.25f });
        AddAnimation(movement, transfer, 6, 2, "Transfer - edit position / rotation curves");
        var activate = NewAnimation("Chip_Activate");
        VectorCurves(activate, chipPath, "m_LocalPosition", new[] { 0f, 1.6f }, new[] { to, to });
        VectorCurves(activate, chipPath, "localEulerAnglesRaw", new[] { 0f, 1.6f }, new[] { rotationTo, rotationTo });
        FloatCurve(activate, chipPath, typeof(MemoryChipGlow), "intensity", new[] { 0f, 0.6f, 1.6f }, new[] { 0.25f, 4f, 4f });
        AddAnimation(movement, activate, 8, 1.6, "Activate - key emission intensity");
        var visibility = timeline.CreateTrack<ActivationTrack>(chipGroup, "Chip visibility");
        visibility.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
        var visibleClip = visibility.CreateDefaultClip();
        visibleClip.start = 5; visibleClip.duration = 4.6; visibleClip.displayName = "Visible";
        director.SetGenericBinding(visibility, chip.gameObject);

        var screen = Ref<GameObject>(overlay, "uiRoot");
        var screenAnimator = screen.AddComponent<Animator>();
        // Canvas stays available while scrubbing; CanvasGroup keys control all presentation.
        screen.SetActive(true);
        var shade = Ref<Image>(overlay, "shade");
        var flash = Ref<Image>(overlay, "flash");
        var caption = Ref<TMP_Text>(overlay, "caption");
        var unlock = Ref<TMP_Text>(overlay, "unlockCaption");
        shade.color = Color.black;
        flash.color = new Color(0.78f, 0.94f, 1f, 1f);
        caption.alpha = unlock.alpha = 1;
        var effects = timeline.CreateTrack<GroupTrack>(null, "VISUAL EFFECTS / CAPTIONS");
        // Each child gets its own Animator so independent tracks never compete for a binding.
        Object.DestroyImmediate(screenAnimator);
        Effect(timeline, director, effects, flash.gameObject, "Memory Flash", 9, 0.6,
            new[] { 0f, 0.149f, 0.15f, 0.379f, 0.38f, 0.469f, 0.47f, 0.6f },
            new[] { 0.8f, 0.8f, 0f, 0f, 0.8f, 0.8f, 0f, 0f }, true);
        Effect(timeline, director, effects, shade.gameObject, "Fade / return under black", 10.4, 4.6,
            new[] { 0f, 0.6f, 3.95f, 4.6f }, new[] { 0f, 1f, 1f, 0f });
        Effect(timeline, director, effects, unlock.gameObject, "UNLOCK", 8, 1,
            new[] { 0f, 0.983333f, 1f }, new[] { 1f, 1f, 0f }, true);
        Effect(timeline, director, effects, caption.gameObject, "YOU CHOSE TO FORGET. / fade out", 11, 3,
            new[] { 0f, 1.6f, 2.7f, 3f }, new[] { 1f, 1f, 0f, 0f });

        var receiver = flow.gameObject.AddComponent<SignalReceiver>();
        var events = timeline.CreateTrack<SignalTrack>(null, "DIALOGUE / EVENTS - runtime gates");
        director.SetGenericBinding(events, receiver);
        Signal(events, receiver, "Dialogue_Start", 4, flow.BeginDialogue);
        Signal(events, receiver, "Memory_Playback", 9, flow.BeginMemoryPlayback);
        Signal(events, receiver, "Return_Camera", 14, flow.ReturnToGameplayCamera);
        Signal(events, receiver, "Finish", 15, flow.CompleteTimeline);
        var audio = timeline.CreateTrack<AudioTrack>(null, "AUDIO - optional future SFX");
        var audioSource = flow.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        director.SetGenericBinding(audio, audioSource);
        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.extrapolationMode = DirectorWrapMode.None;
        EditorUtility.SetDirty(timeline);
        Debug.Log("Victory Timeline migrated. Backup: " + backup);
    }

    private static void BindMissing(VictoryCutsceneController flow, PlayableDirector director, TimelineAsset timeline)
    {
        var root = flow.transform;
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track.mutedInHierarchy || director.GetGenericBinding(track) != null) continue;
            Object binding = null;
            if (track is CinemachineTrack) binding = Ref<Camera>(Ref<CutsceneCameraRig>(flow,"cameraRig"),"outputCamera").GetComponent<CinemachineBrain>();
            if (track is SignalTrack) binding = flow.GetComponent<SignalReceiver>();
            if (track is AudioTrack) binding = flow.GetComponent<AudioSource>();
            if (track is ActivationTrack) binding = Ref<Transform>(flow,"memoryChip").gameObject;
            if (track is AnimationTrack)
            {
                if (track.name.StartsWith("Chip Transform")) binding = root.Find("Memory Chip Animation")?.GetComponent<Animator>();
                else
                {
                    string[] fields = { "shade", "flash", "caption", "unlockCaption" };
                    var overlay = Ref<CutsceneOverlay>(flow,"overlay");
                    foreach (string field in fields)
                    {
                        var component = Ref<Component>(overlay,field);
                        if (component != null && track.name == component.name) binding = component.GetComponent<Animator>();
                    }
                }
            }
            if (binding != null) director.SetGenericBinding(track,binding);
        }
        foreach (var shot in timeline.GetOutputTracks().OfType<CinemachineTrack>().SelectMany(t => t.GetClips()).Select(c => (CinemachineShot)c.asset))
            if (shot.VirtualCamera.Resolve(director) == null)
            {
                var camera = root.Find(shot.DisplayName)?.GetComponent<CinemachineVirtualCamera>();
                if (camera != null) director.SetReferenceValue(shot.VirtualCamera.exposedName,camera);
            }
    }

    [MenuItem("Tools/Victory/Validate Open Gameplay Scene")]
    public static void ValidateOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        var flow = All<VictoryCutsceneController>(scene).Single();
        Require(flow.IsConfigured, "Victory references are incomplete.");
        var director = Ref<PlayableDirector>(flow,"director");
        var timeline = director.playableAsset as TimelineAsset;
        Require(timeline != null && !director.playOnAwake, "Victory Timeline must exist and must not autoplay.");
        Require(director.timeUpdateMode == DirectorUpdateMode.UnscaledGameTime, "Use native unscaled Timeline playback.");
        var cameraTrack = timeline.GetOutputTracks().OfType<CinemachineTrack>().Single();
        var cameras = cameraTrack.GetClips().Select(c => ((CinemachineShot)c.asset).VirtualCamera.Resolve(director)).ToArray();
        Require(cameras.Length >= 5 && cameras.All(c => c != null) && cameras.Distinct().Count() >= 5,
            "Five independently bound camera Shots are required.");
        var firstShot = cameraTrack.GetClips().OrderBy(c=>c.start).First();
        Require(firstShot.start == 0 && ((CinemachineShot)firstShot.asset).VirtualCamera.Resolve(director) ==
            Ref<CinemachineVirtualCamera>(Ref<CutsceneCameraRig>(flow,"cameraRig"),"virtualCamera"), "Entry Shot is required at time zero.");
        foreach (var track in timeline.GetOutputTracks().Where(t => !t.mutedInHierarchy))
            Require(director.GetGenericBinding(track) != null, "Unbound Timeline track: " + track.name);
        var chip = Ref<Transform>(flow,"memoryChip");
        Require(chip.GetComponent<Item>() == null && chip.GetComponent<MemoryChipGlow>() != null, "Dedicated memory-chip visual/glow required.");
        Require(timeline.GetOutputTracks().OfType<AnimationTrack>().Any(t =>
            director.GetGenericBinding(t) == chip.parent.GetComponent<Animator>() && t.GetClips().Count() >= 3), "Chip animation binding missing.");
        var receiver = flow.GetComponent<SignalReceiver>();
        Require(receiver != null, "SignalReceiver missing.");
        foreach (var marker in timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>())
        {
            var reaction = receiver.GetReaction(marker.asset);
            Require(reaction != null && reaction.GetPersistentEventCount() > 0, "Unconnected Signal: " + marker.name);
        }
        foreach (string signal in new[] { "Dialogue_Start", "Memory_Playback", "Return_Camera", "Finish" })
            Require(timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>().Any(m=>m.asset.name == signal), "Missing Signal: " + signal);
        Require(Ref<VictoryCutsceneController>(All<GameplayController>(scene).Single(),"victoryCutscene") == flow, "Gameplay trigger is not linked.");
        Debug.Log("Victory Timeline references, Shots, chip animation and Signals validated.");
    }

    private static void Signal(SignalTrack track, SignalReceiver receiver, string name, double time, UnityAction callback)
    {
        var asset = ScriptableObject.CreateInstance<SignalAsset>();
        asset.name = name;
        AssetDatabase.CreateAsset(asset, Folder + "/" + name + ".signal");
        var marker = track.CreateMarker<SignalEmitter>(time);
        marker.name = name; marker.asset = asset; marker.emitOnce = true; marker.retroactive = false;
        var reaction = new UnityEvent();
        UnityEventTools.AddPersistentListener(reaction,callback);
        receiver.AddReaction(asset,reaction);
    }

    private static void Effect(TimelineAsset timeline, PlayableDirector director, GroupTrack parent, GameObject target,
        string clipName, double start, double duration, float[] times, float[] values, bool stepped = false)
    {
        var group = target.AddComponent<CanvasGroup>();
        group.alpha = 0; group.interactable = false;
        group.blocksRaycasts = target.GetComponent<Image>()?.raycastTarget ?? false;
        var animator = target.AddComponent<Animator>();
        var track = timeline.CreateTrack<AnimationTrack>(parent,target.name);
        director.SetGenericBinding(track,animator);
        var motion = NewAnimation(clipName.Split('/')[0].Trim().Replace(' ','_'));
        if (values[0] != 0)
        {
            times = new[] { 0f, 0.0001f }.Concat(times.Skip(1)).ToArray();
            values = new[] { 0f, values[0] }.Concat(values.Skip(1)).ToArray();
        }
        FloatCurve(motion,"",typeof(CanvasGroup),"m_Alpha",times,values,stepped);
        AddAnimation(track,motion,start,duration,clipName,true);
    }

    private static AnimationClip NewAnimation(string name)
    {
        var clip = new AnimationClip { name = name, frameRate = 60 };
        AssetDatabase.CreateAsset(clip,Folder + "/" + name + ".anim");
        return clip;
    }
    private static void AddAnimation(AnimationTrack track, AnimationClip motion, double start, double duration, string name, bool hold = false)
    {
        var clip = track.CreateClip<AnimationPlayableAsset>();
        clip.start = start; clip.duration = duration; clip.displayName = name;
        var asset = (AnimationPlayableAsset)clip.asset;
        asset.clip = motion; asset.removeStartOffset = false; asset.applyFootIK = false;
        asset.loop = AnimationPlayableAsset.LoopMode.Off;
        var serialized = new SerializedObject(track);
        var clips = serialized.FindProperty("m_Clips");
        var item = clips.GetArrayElementAtIndex(clips.arraySize - 1);
        item.FindPropertyRelative("m_PreExtrapolationMode").enumValueIndex = hold ? 1 : 0;
        item.FindPropertyRelative("m_PostExtrapolationMode").enumValueIndex = hold ? 1 : 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void VectorCurves(AnimationClip clip,string path,string property,float[] times,Vector3[] values)
    {
        for (int axis=0;axis<3;axis++) FloatCurve(clip,path,typeof(Transform),property+"."+"xyz"[axis],times,values.Select(v=>v[axis]).ToArray());
    }
    private static void FloatCurve(AnimationClip clip,string path,Type type,string property,float[] times,float[] values,bool stepped=false)
    {
        var curve = new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i],0,0)).ToArray());
        if (stepped) for(int i=0;i<times.Length;i++) AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,type,property),curve);
    }
    private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static T Ref<T>(Object target,string field) where T:Object => (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    private static void Set(Object target,string field,Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(Object target,string field,Object[] values)
    {
        var serialized = new SerializedObject(target); var array=serialized.FindProperty(field);
        array.arraySize=values.Length;
        for(int i=0;i<values.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Transform Child(Transform parent,string name)
    {
        var child=new GameObject(name); child.transform.SetParent(parent,false); return child.transform;
    }
    private static void Require(bool valid,string message) { if(!valid) throw new InvalidOperationException(message); }
}
