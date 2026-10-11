using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntroDealerSetupEditor
{
    private const string RigPath = "Assets/_Game/Art/Characters/Dealer/Models/PF_Dealer_Animated.prefab";
    private const string WheelFolder = "Assets/Casino Animation/Assets/Animation/Spin Wheel/Dealer/";
    public const string IdlePath = "Assets/_Game/Scenes/Intro/Intro_DealerIdle.anim";
    public const string ControllerPath = "Assets/_Game/Scenes/Intro/Intro_DealerPresentation.controller";

    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity");
        ApplyOpenScene();
    }

    [MenuItem("Tools/Intro/Apply Dealer Idle Animation")]
    public static void ApplyOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != IntroCutsceneSetupEditor.IntroPath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Intro 씬을 편집 모드에서 열어 주세요.");
        var original = scene.GetRootGameObjects().Single(r => r.name == "PF_Dealer");
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original) == RigPath)
        {
            Debug.Log("인트로 딜러 애니메이션이 이미 연결되어 있습니다. 기존 설정을 유지합니다.");
            return;
        }
        var stagedModel = original.transform.Find("Intro Dealer Animated");
        if (stagedModel != null)
        {
            ReplaceTemporaryModel(original, stagedModel.gameObject);
            IntroCutsceneSetupEditor.ValidateOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }
        var renderers = original.GetComponentsInChildren<MeshRenderer>();
        var originalBounds = renderers[0].bounds;
        foreach (var renderer in renderers) originalBounds.Encapsulate(renderer.bounds);

        // 팩 원본의 반복 설정을 바꾸지 않고 인트로 전용 사본만 반복 재생한다.
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath) == null &&
            !AssetDatabase.CopyAsset(WheelFolder + "AS_Dealer_Wheel_Idle.anim", IdlePath))
            throw new System.InvalidOperationException("대기 애니메이션 사본을 만들지 못했습니다.");
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(idleClip);
        clipSettings.loopTime = true;
        clipSettings.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(idleClip, clipSettings);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.defaultState;
        foreach (var state in machine.states)
            if (state.state != idle) machine.RemoveState(state.state);
        idle.name = "Idle";
        idle.motion = idleClip;
        idle.speed = 1f;
        idle.writeDefaultValues = true;
        machine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(idle);
        EditorUtility.SetDirty(machine);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath), original.transform);
        Undo.RegisterCreatedObjectUndo(model, "인트로 딜러 애니메이션 연결");
        model.name = "Intro Dealer Animated";
        model.transform.SetPositionAndRotation(original.transform.position, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // 칩 이동·베팅 이벤트는 인트로에 필요하지 않으므로 인스턴스에서만 제외한다.
        Undo.DestroyObjectImmediate(model.GetComponent<DealerAnimationController>());
        animator.Rebind();
        animator.Play("Idle", 0, 0f);
        animator.Update(0f);
        var animatedBounds = SkinBounds(model);
        float scale = originalBounds.size.y / animatedBounds.size.y;
        model.transform.localScale = Vector3.one * scale;
        animator.Update(0f);
        animatedBounds = SkinBounds(model);
        model.transform.position += Vector3.up * (originalBounds.min.y - animatedBounds.min.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        ReplaceTemporaryModel(original, model);
        EditorUtility.SetDirty(idleClip);
        AssetDatabase.SaveAssets();
        IntroCutsceneSetupEditor.ValidateOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs/IntroDealer");
        File.WriteAllText("Logs/IntroDealer/applied.txt", $"Rig={RigPath}\nIdle={IdlePath}\nScale={scale}\nPosition={model.transform.position:F4}\nOriginalBounds={originalBounds}\nAnimatedBounds={SkinBounds(model)}\n");
    }

    private static void ReplaceTemporaryModel(GameObject original, GameObject model)
    {
        var target = original.transform.Find("Dealer Look Target");
        Undo.SetTransformParent(model.transform, null, "Animated 딜러로 교체");
        Undo.SetTransformParent(target, model.transform, "딜러 시선 타깃 인계");
        model.name = original.name;
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        Undo.DestroyObjectImmediate(original);
    }

    private static Bounds SkinBounds(GameObject model)
    {
        var bounds = new Bounds();
        bool first = true;
        var mesh = new Mesh();
        try
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 world = renderer.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                    else bounds.Encapsulate(world);
                }
            }
        }
        finally { Object.DestroyImmediate(mesh); }
        return bounds;
    }

    public static void Capture(Camera camera, string path)
    {
        var texture = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        var oldTarget = camera.targetTexture;
        float oldAspect = camera.aspect;
        try
        {
            camera.aspect = 1280f / 720f;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            camera.aspect = oldAspect;
            RenderTexture.active = oldActive;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(pixels);
        }
    }

    public static void AuditBatch()
    {
        Directory.CreateDirectory("Logs/IntroDealer");
        var report = new StringBuilder();
        foreach (var path in new[] { "Assets/_Game/Scenes/Dev_Yujin.unity", "Assets/_Game/Scenes/Intro.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            report.AppendLine(path);
            foreach (var animator in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Animator>(true)))
            {
                report.AppendLine($"Animator {animator.name} active={animator.gameObject.activeInHierarchy} human={animator.isHuman} avatar={AssetDatabase.GetAssetPath(animator.avatar)} controller={AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animator.gameObject)} pos={animator.transform.position:F3} rot={animator.transform.eulerAngles:F2} scale={animator.transform.lossyScale:F3} rootMotion={animator.applyRootMotion}");
                if (!animator.isHuman) continue;
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                    report.AppendLine($" Mesh {renderer.name}: {renderer.sharedMesh.name} bones={renderer.bones.Length} bounds={renderer.bounds} materials={string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))}");
                foreach (var component in animator.GetComponents<MonoBehaviour>()) report.AppendLine(" Script " + component.GetType().Name);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Casino Animation/Assets/Animation" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(path.Contains("Idle") || path.Contains("ComePlay") || path.Contains("ThankYou"))) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            report.AppendLine($"Clip {path} human={clip.humanMotion} duration={clip.length:F2} loop={clip.isLooping} events={clip.events.Length}");
        }
        File.WriteAllText("Logs/IntroDealer/audit.txt", report.ToString());
    }
}

