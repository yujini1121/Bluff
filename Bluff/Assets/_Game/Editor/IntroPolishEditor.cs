using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class IntroPolishEditor
{
    private static readonly float[] BeatTimes = { 0f, 1.2f, 2.35f, 3.95f, 4.85f, 5.2f, 6.35f, 7.95f, 9.35f, 10.65f, 11.15f };

    public static void CaptureCompositionsBatch()
    {
        var scene = EditorSceneManager.OpenScene(IntroCutsceneSetupEditor.IntroPath);
        var camera = All<Camera>(scene).Single();
        camera.GetComponent<Cinemachine.CinemachineBrain>().enabled = false;
        foreach (var canvas in All<Canvas>(scene)) canvas.enabled = false;
        var target = All<Transform>(scene).Single(t => t.name == "Dealer Look Target");
        Directory.CreateDirectory("Logs/IntroPolish/Composition");
        foreach (float x in new[] { 8.6f, 9.05f, 9.65f, 10.15f })
        {
            camera.transform.position = new Vector3(x, 1.77f, 14.8f);
            camera.transform.LookAt(target);
            camera.fieldOfView = 56f;
            var texture = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = texture;
            camera.aspect = 1280f / 720f;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes($"Logs/IntroPolish/Composition/View-{x:F2}.png", pixels.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene(IntroCutsceneSetupEditor.IntroPath);
        ApplyOpenScene();
    }

    [MenuItem("Tools/Intro/Apply Cinematic Polish")]
    public static void ApplyOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != IntroCutsceneSetupEditor.IntroPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Intro 씬을 편집 모드에서 열어 주세요.");
        var rig = All<IntroBarSequenceCamera>(scene).Single();
        var director = rig.GetComponent<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var clip = timeline.GetOutputTracks().OfType<AnimationTrack>().Single().GetClips().Single();
        var motion = ((AnimationPlayableAsset)clip.asset).clip;
        var bindings = AnimationUtility.GetCurveBindings(motion);
        // 적용 전에 모든 키를 검사하여 사용자 편집을 부분적으로 덮어쓰지 않는다.
        foreach (var binding in bindings)
            if (AnimationUtility.GetEditorCurve(motion, binding).length != BeatTimes.Length)
                throw new InvalidOperationException("기존 11개 키 구성이 변경되었습니다. 수동으로 타이밍을 조정해 주세요.");
        Undo.RecordObjects(new UnityEngine.Object[] { timeline, motion }, "인트로 연출 타이밍 개선");
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(motion, binding);
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].time = BeatTimes[i];
            curve.keys = keys;
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(motion, binding, curve);
        }
        timeline.fixedDuration = clip.duration = BeatTimes.Last();
        clip.displayName = "진입 1.2 | 탐색 3.95 | 발견 4.85 | 접근 5.2–10.65 | 인계 11.15";
        var target = All<Transform>(scene).Single(t => t.name == "Dealer Look Target");
        Set(rig, "dealerLookTarget", target);
        var flow = All<IntroFlowController>(scene).Single();
        var fade = All<IntroFadeController>(scene).Single();
        var sound = All<IntroSoundscape>(scene).SingleOrDefault();
        if (sound == null)
        {
            var root = new GameObject("Intro Soundscape");
            Undo.RegisterCreatedObjectUndo(root, "인트로 사운드 연결");
            sound = root.AddComponent<IntroSoundscape>();
            var music = InstantiateAudio("Assets/_Game/Audio/BGM/CutScene BGM.prefab", root.transform);
            music.loop = true;
            music.volume = 0f;
            var click = InstantiateAudio("Assets/_Game/Audio/SFX/Click SFX.prefab", root.transform);
            click.volume = 0.22f;
            Set(sound, "music", music);
            Set(sound, "interactionSound", click);
        }
        Set(sound, "flow", flow);
        Set(sound, "fade", fade);
        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(motion);
        AssetDatabase.SaveAssets();
        IntroCutsceneSetupEditor.ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("인트로 연출 적용 완료: 11.15초, 연속 진입, 딜러 시선 추적, 사운드 연결.");
    }

    private static AudioSource InstantiateAudio(string path, Transform parent)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        var source = root.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        return source;
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static T[] All<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
}
