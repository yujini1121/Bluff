using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

public static class IntroBarRevisionEditor
{
    [MenuItem("Tools/Intro/Move Next To Former Skip Position")]
    public static void MoveNextToFormerSkipPosition()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != IntroCutsceneSetupEditor.IntroPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Intro in Edit Mode.");
        var next = (RectTransform)Find(scene, "Next Button");
        Undo.RecordObject(next, "Move Intro Next button");
        // Exact RectTransform values read from the local pre-removal Skip Button backup.
        next.anchorMin = next.anchorMax = new Vector2(0.5f, 0.5f);
        next.pivot = new Vector2(0.5f, 0.5f);
        next.anchoredPosition = new Vector2(815f, 70f);
        var outline = Find(scene, "Bar ESC Keycap").GetComponentInChildren<IntroKeycapOutline>(true);
        var serialized = new SerializedObject(outline);
        serialized.FindProperty("thickness").floatValue = 3.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Intro/Apply Bar Composition and Keycaps")]
    public static void ApplyOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != IntroCutsceneSetupEditor.IntroPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Intro in Edit Mode.");
        Directory.CreateDirectory("Logs/IntroRevisionBackup");
        var flow = All<IntroFlowController>(scene).Single();
        var rig = All<IntroBarSequenceCamera>(scene).Single();
        var director = rig.GetComponent<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var track = timeline.GetOutputTracks().OfType<AnimationTrack>().Single();
        var clip = track.GetClips().Single();
        var motion = ((AnimationPlayableAsset)clip.asset).clip;
        var path = ((Animator)director.GetGenericBinding(track)).transform;
        var target = Find(scene, "Dealer Look Target");
        // Preserve the existing authored camera height, not a new guessed eye height.
        var oldHeight = AnimationUtility.GetEditorCurve(motion,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"));
        float y = oldHeight != null ? path.parent.TransformPoint(new Vector3(0, oldHeight.Evaluate(0), 0)).y : path.position.y;
        Vector3 entry = new Vector3(1.238f, y, 15.83f);
        Vector3 end = new Vector3(9.65f, y, 14.8f);
        // Follow the public side of the counter. The model's authored face is opposite its Transform.forward.
        Vector3 clearView = new Vector3(2.8f, y, 16.3f);
        Vector3[] positions = {
            entry, entry, entry, clearView, clearView, clearView,
            new Vector3(3.4f, y, 16.05f), new Vector3(6.3f, y, 16.05f),
            new Vector3(8.75f, y, 15.65f), end, end
        };
        float[] times = { 0f, 1.2f, 2.9f, 4.7f, 5.9f, 6.6f, 8.2f, 9.8f, 11.3f, 12.8f, 13.4f };
        // Baseline 78 degrees: left -30, right +25. Pitch remains nearly level.
        Vector3[] rotations = {
            new Vector3(0,78,0), new Vector3(0,78,0), new Vector3(-1,48,0),
            new Vector3(0,103,0), Look(clearView,target), Look(clearView,target),
            Look(positions[6],target), Look(positions[7],target), Look(positions[8],target), Look(end,target), Look(end,target)
        };
        for (int i=1;i<rotations.Length;i++)
            rotations[i] = rotations[i-1] + new Vector3(Mathf.DeltaAngle(rotations[i-1].x,rotations[i].x),
                Mathf.DeltaAngle(rotations[i-1].y,rotations[i].y),0);
        Undo.RecordObject(motion, "Revise Intro camera keys");
        motion.ClearCurves();
        WriteCurves(motion,"m_LocalPosition",times,positions.Select(p=>path.parent.InverseTransformPoint(p)).ToArray());
        WriteCurves(motion,"localEulerAnglesRaw",times,rotations);
        timeline.fixedDuration = 13.4;
        clip.duration = 13.4;
        clip.displayName = "Entry 0-1.2 | Left 2.9 | Right 4.7 | Dealer 5.9 | Approach 6.6-12.8";
        path.SetPositionAndRotation(entry,Quaternion.Euler(rotations[0]));
        rig.VirtualCamera.transform.SetPositionAndRotation(entry,path.rotation);
        var arrival = Find(scene,"Dealer Interaction View - Reference");
        arrival.SetPositionAndRotation(end,Quaternion.Euler(rotations.Last()));
        var start = All<Transform>(scene).FirstOrDefault(t=>t.name=="Bar Entry - Reference");
        if(start==null) { start=new GameObject("Bar Entry - Reference").transform; start.SetParent(path.parent,false); }
        start.SetPositionAndRotation(entry,path.rotation);

        // Use a neutral TMP material rather than the purple material inherited by the old prompt.
        var source = All<TMP_Text>(scene).First(t=>t.name=="Next Text");
        var oldPrompt = Find(scene,"Dealer E Prompt");
        Transform canvas = source.canvas.transform;
        UnityEngine.Object.DestroyImmediate(oldPrompt.gameObject);
        var e = CreateKeycap(canvas,source,"Dealer E Prompt","E",new Vector2(58,58),
            new Vector2(0.5f,0.18f),Vector2.zero);
        var previousEsc = All<Transform>(scene).FirstOrDefault(t=>t.name=="Bar ESC Keycap");
        if(previousEsc!=null) UnityEngine.Object.DestroyImmediate(previousEsc.gameObject);
        var esc = CreateKeycap(canvas,source,"Bar ESC Keycap","ESC",new Vector2(86,54),
            new Vector2(1,0),new Vector2(-90,65));
        var oldSkip = All<Transform>(scene).FirstOrDefault(t=>t.name=="Skip Button");
        if(oldSkip!=null) UnityEngine.Object.DestroyImmediate(oldSkip.gameObject);
        var hold = rig.GetComponent<IntroHoldToSkip>();
        if(hold==null) hold=rig.gameObject.AddComponent<IntroHoldToSkip>();
        Set(hold,"flow",flow);
        Set(hold,"keycap",esc);
        Set(hold,"outline",esc.GetComponentInChildren<IntroKeycapOutline>(true));
        Set(flow,"interactionPrompt",e);
        Set(flow,"holdToSkip",hold);
        Set(flow,"doorDirector",All<PlayableDirector>(scene).Single(d=>d!=director));
        e.SetActive(false);
        esc.SetActive(false);

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(motion);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        IntroCutsceneSetupEditor.ValidateOpenScene();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/IntroRevisionBackup/applied.txt",
            $"Entry={entry:F4}\nEnd={end:F4}\nRotation={Quaternion.Euler(rotations.Last()).eulerAngles:F4}\nDuration={timeline.duration}\n");
        Debug.Log("Intro Bar composition / E and ESC keycaps saved.");
    }

    private static GameObject CreateKeycap(Transform parent,TMP_Text source,string name,string label,Vector2 size,Vector2 anchor,Vector2 offset)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Image));
        root.transform.SetParent(parent,false);
        var rect=(RectTransform)root.transform;
        rect.anchorMin=rect.anchorMax=anchor; rect.pivot=new Vector2(0.5f,0.5f);
        rect.sizeDelta=size; rect.anchoredPosition=offset;
        var background=root.GetComponent<Image>();
        background.color=new Color(0.025f,0.035f,0.065f,0.82f); background.raycastTarget=false;
        var border=new GameObject("Outline",typeof(RectTransform),typeof(IntroKeycapOutline));
        border.transform.SetParent(root.transform,false);
        Stretch((RectTransform)border.transform);
        border.GetComponent<IntroKeycapOutline>().raycastTarget=false;
        if (label == "ESC")
        {
            var outlineSettings = new SerializedObject(border.GetComponent<IntroKeycapOutline>());
            outlineSettings.FindProperty("thickness").floatValue = 3.5f;
            outlineSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        var textObject=new GameObject("Key",typeof(RectTransform),typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform,false);
        Stretch((RectTransform)textObject.transform);
        var text=textObject.GetComponent<TextMeshProUGUI>();
        const string materialPath = "Assets/_Game/Scenes/IntroSkeleton/Intro_Keycap.mat";
        var keyMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (keyMaterial == null)
        {
            keyMaterial = new Material(source.font.material) { name = "Intro_Keycap" };
            AssetDatabase.CreateAsset(keyMaterial, materialPath);
        }
        keyMaterial.DisableKeyword("GLOW_ON");
        keyMaterial.DisableKeyword("UNDERLAY_ON");
        if (keyMaterial.HasProperty("_FaceColor")) keyMaterial.SetColor("_FaceColor", Color.white);
        if (keyMaterial.HasProperty("_OutlineWidth")) keyMaterial.SetFloat("_OutlineWidth", 0);
        if (keyMaterial.HasProperty("_GlowPower")) keyMaterial.SetFloat("_GlowPower", 0);
        EditorUtility.SetDirty(keyMaterial);
        text.font=source.font; text.fontSharedMaterial=keyMaterial;
        text.text=label; text.fontSize=label=="E"?28:22; text.color=Color.white;
        text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        text.fontStyle=FontStyles.Normal;
        return root;
    }
    private static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    private static Vector3 Look(Vector3 p,Transform target)=>Quaternion.LookRotation(target.position-p).eulerAngles;
    private static void WriteCurves(AnimationClip clip,string property,float[] times,Vector3[] values)
    {
        for(int axis=0;axis<3;axis++)
        {
            var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i][axis])).ToArray());
            for(int i=0;i<curve.length;i++) {
                AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),property+"."+"xyz"[axis]),curve);
        }
    }
    private static T[] All<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    private static Transform Find(Scene scene,string name)=>All<Transform>(scene).Single(t=>t.name==name);
    private static void Set(UnityEngine.Object o,string property,UnityEngine.Object value) { var s=new SerializedObject(o);s.FindProperty(property).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo(); }
}
