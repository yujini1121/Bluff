using System;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Author real scene objects/assets through Unity; never regenerate over artist adjustments.
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
        Scene scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == ScenePath,
            "Open Dev_Yujin in Edit Mode first.");
        if (All<VictoryCutsceneController>(scene).Any())
        {
            var existing = All<VictoryCutsceneController>(scene).Single();
            var references = new SerializedObject(existing).FindProperty("gameplayHud");
            if (references.arraySize == 0 || Enumerable.Range(0, references.arraySize)
                .Any(i => references.GetArrayElementAtIndex(i).objectReferenceValue == null))
            {
                SetArray(existing, "gameplayHud", All<Canvas>(scene)
                    .Where(c => c.rootCanvas == c && !c.transform.IsChildOf(existing.transform))
                    .Select(HudGroup).ToArray());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            ValidateOpenScene();
            return;
        }
        Require(AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) == null,
            "Victory Timeline exists without a scene controller; restore scene bindings before continuing.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Scenes", "Victory");

        GameplayController gameplay = All<GameplayController>(scene).Single();
        Camera main = All<Camera>(scene).Single(c => c.CompareTag("MainCamera"));
        SwitchCamera switcher = All<SwitchCamera>(scene).Single();
        CinemachineVirtualCamera player = Ref<CinemachineVirtualCamera>(switcher, "playerCam");
        DealerAnimationController dealer = All<DealerAnimationController>(scene).Single();
        Animator dealerAnimator = Ref<Animator>(dealer, "animator");
        Transform head = dealerAnimator != null && dealerAnimator.isHuman
            ? dealerAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
        Transform hand = Ref<Transform>(dealer, "chipSocket");
        Transform table = All<Transform>(scene).First(t => t.name == "Dealer Showdown Card Point");
        Vector3 face = head != null ? head.position : dealer.transform.position + Vector3.up * 1.4f;
        Vector3 towardPlayer = Vector3.ProjectOnPlane(player.transform.position - face, Vector3.up).normalized;
        if (towardPlayer.sqrMagnitude < 0.5f) towardPlayer = Vector3.back;
        float tableY = table.position.y;
        Vector3 reveal = hand != null ? hand.position : face - Vector3.up * 0.5f;
        reveal += towardPlayer * 0.12f;
        reveal.y = Mathf.Max(reveal.y, tableY + 0.18f);
        Vector3 receive = Vector3.Lerp(reveal, player.transform.position, 0.64f);
        receive.y = tableY + 0.22f;
        Vector3 dealerView = face + towardPlayer * 1.4f + Vector3.up * 0.08f;
        Vector3 handsView = reveal + towardPlayer * 0.85f + Vector3.up * 0.45f;
        Vector3 receiveView = receive + towardPlayer * 0.72f + Vector3.up * 0.4f;

        var huds = All<Canvas>(scene).Where(c => c.rootCanvas == c).Select(HudGroup).ToArray();
        Behaviour[] inputs = All<MonoBehaviour>(scene).Where(c => c is SwitchCamera || c is Pause || c is PlayerMove)
            .Cast<Behaviour>().ToArray();
        var root = new GameObject("Victory Cutscene");
        var flow = root.AddComponent<VictoryCutsceneController>();
        var rig = root.AddComponent<CutsceneCameraRig>();
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.extrapolationMode = DirectorWrapMode.Hold;
        Transform path = Child(root.transform, "Timeline Camera Path");
        var pathAnimator = path.gameObject.AddComponent<Animator>();
        path.SetPositionAndRotation(dealerView, Look(dealerView, face));
        var vcam = Child(root.transform, "Victory Camera").gameObject.AddComponent<CinemachineVirtualCamera>();
        vcam.m_Lens = player.m_Lens;
        vcam.Priority = -100;
        vcam.enabled = false;
        Set(rig, "outputCamera", main);
        Set(rig, "virtualCamera", vcam);
        Set(rig, "authoredPath", path);

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 14d;
        AssetDatabase.CreateAsset(timeline, TimelinePath);
        AnimationTrack cameraTrack = timeline.CreateTrack<AnimationTrack>(null, "Dealer / Hands / Receive / Memory Dealer");
        cameraTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
        var motion = new AnimationClip { name = "Victory camera poses", frameRate = 60f };
        AssetDatabase.AddObjectToAsset(motion, timeline);
        float[] times = { 0, 4, 5, 6, 8, 9.5f, 9.6f, 10.4f, 14f };
        Vector3[] positions = { dealerView, dealerView, handsView, handsView, receiveView, receiveView,
            dealerView, dealerView, dealerView };
        Vector3[] targets = { face, face, reveal, reveal, receive, receive, face, face, face };
        Vector3[] rotations = positions.Select((p, i) => Look(p, targets[i]).eulerAngles).ToArray();
        for (int i = 1; i < rotations.Length; i++)
            rotations[i] = rotations[i - 1] + new Vector3(Mathf.DeltaAngle(rotations[i - 1].x, rotations[i].x),
                Mathf.DeltaAngle(rotations[i - 1].y, rotations[i].y), Mathf.DeltaAngle(rotations[i - 1].z, rotations[i].z));
        Curves(motion, "m_LocalPosition", times, positions);
        Curves(motion, "localEulerAnglesRaw", times, rotations);
        TimelineClip clip = cameraTrack.CreateClip<AnimationPlayableAsset>();
        clip.displayName = "0–4 entry | WAIT NEXT | 5 reveal | 6–8 handoff | 9 memory | 11 last words";
        clip.duration = 14;
        var animation = (AnimationPlayableAsset)clip.asset;
        animation.clip = motion;
        animation.removeStartOffset = false;
        animation.applyFootIK = false;
        animation.loop = AnimationPlayableAsset.LoopMode.Off;
        director.playableAsset = timeline;
        director.SetGenericBinding(cameraTrack, pathAnimator);

        Transform revealPoint = Child(root.transform, "Chip Reveal Point - replace with authored hand pose");
        Transform receivePoint = Child(root.transform, "Chip Receive Point");
        revealPoint.position = reveal;
        receivePoint.position = receive;
        revealPoint.rotation = receivePoint.rotation = Quaternion.LookRotation(towardPlayer);
        Transform chip = Child(root.transform, "Memory Chip - Placeholder");
        chip.SetPositionAndRotation(reveal, revealPoint.rotation);
        var bodyMaterial = Material("MemoryChip_Body", new Color(0.018f, 0.028f, 0.035f), false);
        var glowMaterial = Material("MemoryChip_Core", new Color(0.08f, 0.75f, 0.9f), true);
        var contactMaterial = Material("MemoryChip_Contacts", new Color(0.85f, 0.63f, 0.22f), false);
        Cube(chip, "Replaceable Body", Vector3.zero, new Vector3(0.16f, 0.022f, 0.11f), bodyMaterial);
        var glow = Cube(chip, "Activation Core", new Vector3(0, 0.013f, 0), new Vector3(0.11f, 0.004f, 0.06f), glowMaterial);
        for (int i = 0; i < 4; i++) Cube(chip, "Contact " + (i + 1), new Vector3((i - 1.5f) * 0.033f, 0.013f, 0.046f),
            new Vector3(0.018f, 0.004f, 0.018f), contactMaterial);
        chip.gameObject.SetActive(false);

        Transform ui = Child(root.transform, "Victory Screen", true);
        Canvas canvas = ui.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = ui.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        ui.gameObject.AddComponent<GraphicRaycaster>();
        var overlay = root.AddComponent<CutsceneOverlay>();
        Image shade = ScreenImage(ui, "Fade / Input Shield", Color.clear);
        Image flash = ScreenImage(ui, "Memory Flash - future fullscreen effect", Color.clear);
        flash.raycastTarget = false;

        // Copy the existing tuned panel + Dialogue settings without editing/saving Intro.
        Scene intro = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity", OpenSceneMode.Additive);
        DialogueController source = All<DialogueController>(intro).Single();
        GameObject sourcePanel = Ref<GameObject>(source, "dialoguePanel");
        TMP_Text sourceText = Ref<TMP_Text>(source, "dialogueText");
        GameObject panel = Object.Instantiate(sourcePanel, ui, false);
        panel.name = "Victory Dialogue Panel";
        string textPath = AnimationUtility.CalculateTransformPath(sourceText.transform, sourcePanel.transform);
        TMP_Text text = (textPath.Length == 0 ? panel.transform : panel.transform.Find(textPath)).GetComponent<TMP_Text>();
        DialogueController dialogue = Child(root.transform, "Victory Dialogue").gameObject.AddComponent<DialogueController>();
        EditorUtility.CopySerialized(source, dialogue);
        Set(dialogue, "dialoguePanel", panel);
        Set(dialogue, "dialogueText", text);
        var next = panel.GetComponentsInChildren<Button>(true).Single();
        for (int i = next.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(next.onClick, i);
        UnityEventTools.AddPersistentListener(next.onClick, dialogue.Next);
        TMP_FontAsset font = sourceText.font;
        Material fontMaterial = sourceText.fontSharedMaterial;
        EditorSceneManager.CloseScene(intro, true);
        SceneManager.SetActiveScene(scene);
        panel.SetActive(false);
        dialogue.gameObject.SetActive(false);
        TMP_Text caption = Text(ui, "Final Message", font, fontMaterial, 56f);
        caption.text = "YOU CHOSE TO FORGET.";
        RectTransform captionRect = caption.rectTransform;
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0.5f);
        captionRect.sizeDelta = new Vector2(1700, 150);
        TMP_Text unlockCaption = Object.Instantiate(caption, ui, false);
        unlockCaption.name = "Unlock Caption";
        unlockCaption.text = "UNLOCK";
        Set(overlay, "uiRoot", ui.gameObject);
        Set(overlay, "shade", shade);
        Set(overlay, "flash", flash);
        Set(overlay, "caption", caption);
        Set(overlay, "unlockCaption", unlockCaption);
        ui.gameObject.SetActive(false);

        Set(flow, "director", director);
        Set(flow, "cameraRig", rig);
        Set(flow, "dialogue", dialogue);
        Set(flow, "dialoguePanel", panel);
        Set(flow, "overlay", overlay);
        Set(flow, "memoryChip", chip);
        Set(flow, "chipRevealPoint", revealPoint);
        Set(flow, "chipReceivePoint", receivePoint);
        Set(flow, "chipGlow", glow);
        SetArray(flow, "gameplayHud", huds);
        SetArray(flow, "suspendedInputs", inputs);
        Set(gameplay, "victoryCutscene", flow);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        ValidateOpenScene();
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs/Victory");
        File.WriteAllText("Logs/Victory/Setup.txt", $"Scene and Timeline bound.\nFace: {face}\nTable Y: {tableY}\nReveal: {reveal}\nReceive: {receive}\nDealer view: {dealerView}\nHUD canvases: {huds.Length}\nInputs: {string.Join(", ", inputs.Select(i => i.GetType().Name))}\n");
        Debug.Log("Victory cutscene authored and validated in Dev_Yujin.");
    }

    [MenuItem("Tools/Victory/Validate Open Gameplay Scene")]
    public static void ValidateOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        var flow = All<VictoryCutsceneController>(scene).Single();
        Require(flow.IsConfigured, "Victory references are incomplete.");
        var director = flow.GetComponent<PlayableDirector>();
        Require(!director.playOnAwake, "Victory must not autoplay.");
        Require(director.playableAsset.outputs.Any(), "Timeline must have a playable track.");
        foreach (var output in director.playableAsset.outputs)
            Require(director.GetGenericBinding(output.sourceObject) != null, "Unbound Timeline track: " + output.streamName);
        Require(Ref<VictoryCutsceneController>(All<GameplayController>(scene).Single(), "victoryCutscene") == flow,
            "Gameplay trigger is not linked.");
    }

    private static CanvasGroup HudGroup(Canvas canvas)
    {
        var group = canvas.GetComponent<CanvasGroup>();
        return group != null ? group : canvas.gameObject.AddComponent<CanvasGroup>();
    }

    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static T Ref<T>(Object target, string field) where T : Object =>
        (T)new SerializedObject(target).FindProperty(field).objectReferenceValue;
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(Object target, string field, Object[] values)
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Transform Child(Transform parent, string name, bool rect = false)
    {
        var child = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }
    private static Quaternion Look(Vector3 from, Vector3 to) => Quaternion.LookRotation(to - from);
    private static Material Material(string name, Color color, bool emission)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 0.25f); }
        AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
        return material;
    }
    private static Renderer Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube.GetComponent<Renderer>();
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Image ScreenImage(Transform parent, string name, Color color)
    {
        var image = Child(parent, name, true).gameObject.AddComponent<Image>();
        Stretch(image.rectTransform);
        image.color = color;
        return image;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, Material material, float size)
    {
        var text = Child(parent, name, true).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSharedMaterial = material; text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.color = new Color(0.86f, 0.88f, 0.84f);
        return text;
    }
    private static void Curves(AnimationClip clip, string property, float[] times, Vector3[] values)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, values[i][axis], 0, 0)).ToArray());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property + "." + "xyz"[axis]), curve);
        }
    }
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
