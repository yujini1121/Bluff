using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PrefabCaptureWindow : EditorWindow
{
    // =========================================================
    // Serialized Window State
    // Undo / Redo가 동작하려면 값들이 Serialized 되어 있어야 함
    // =========================================================

    [SerializeField]
    private GameObject targetPrefab;

    [SerializeField]
    private Vector3 positionOffset = Vector3.zero;

    [SerializeField]
    private Vector3 modelRotation = new Vector3(20f, -25f, 0f);

    [SerializeField]
    private float fieldOfView = 28f;

    [SerializeField]
    private float framing = 1.15f;

    [SerializeField]
    private float keyLightIntensity = 1.4f;

    [SerializeField]
    private float fillLightIntensity = 0.6f;

    [SerializeField]
    private float ambientIntensity = 0.3f;

    [SerializeField]
    private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

    [SerializeField]
    private int outputResolution = 1024;


    // =========================================================
    // Runtime Preview State
    // =========================================================

    private PreviewRenderUtility previewUtility;
    private GameObject previewInstance;
    private GameObject loadedPrefab;

    private Quaternion baseRotation;
    private Vector3 baseScale;


    // =========================================================
    // Prefab별 저장용 데이터
    // =========================================================

    [Serializable]
    private class CaptureSettings
    {
        public Vector3 positionOffset;
        public Vector3 modelRotation;

        public float fieldOfView;
        public float framing;

        public float keyLightIntensity;
        public float fillLightIntensity;
        public float ambientIntensity;

        public Color backgroundColor;

        public int outputResolution;
    }


    // =========================================================
    // Open
    // =========================================================

    [MenuItem("Tools/Bluff/Prefab Capture")]
    private static void Open()
    {
        PrefabCaptureWindow window =
            GetWindow<PrefabCaptureWindow>("Prefab Capture");

        window.minSize = new Vector2(440f, 700f);
    }


    // =========================================================
    // Unity Events
    // =========================================================

    private void OnEnable()
    {
        Undo.undoRedoPerformed += HandleUndoRedo;

        if (targetPrefab != null)
        {
            CreatePreview();
        }
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;

        SaveCurrentSettings();
        CleanupPreview();
    }

    private void HandleUndoRedo()
    {
        // Prefab 자체가 Undo로 변경됐을 수도 있음
        if (loadedPrefab != targetPrefab)
        {
            CreatePreview();
        }

        // Undo 결과를 현재 Prefab 설정에도 반영
        SaveCurrentSettings();

        Repaint();
    }


    // =========================================================
    // GUI
    // =========================================================

    private void OnGUI()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "Bluff Prefab Capture",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(5);

        DrawPrefabSelector();

        EditorGUILayout.Space(10);

        DrawViewSettings();

        EditorGUILayout.Space(10);

        DrawLightingSettings();

        EditorGUILayout.Space(10);

        DrawOutputSettings();

        EditorGUILayout.Space(12);

        DrawPreview();

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(targetPrefab == null))
        {
            if (GUILayout.Button("Export PNG", GUILayout.Height(38)))
            {
                ExportPNG();
            }
        }
    }


    // =========================================================
    // Prefab Selector
    // =========================================================

    private void DrawPrefabSelector()
    {
        GameObject newPrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Prefab",
                targetPrefab,
                typeof(GameObject),
                false
            );

        if (newPrefab == targetPrefab)
            return;

        // 기존 Prefab 설정부터 저장
        SaveCurrentSettings();

        Undo.RecordObject(
            this,
            "Change Capture Prefab"
        );

        targetPrefab = newPrefab;

        // 새 Prefab의 저장 설정 불러오기
        if (targetPrefab != null)
        {
            LoadSettings(targetPrefab);
        }
        else
        {
            ResetValuesWithoutUndo();
        }

        EditorUtility.SetDirty(this);

        CreatePreview();

        Repaint();
    }


    // =========================================================
    // View Settings
    // =========================================================

    private void DrawViewSettings()
    {
        EditorGUILayout.LabelField(
            "View",
            EditorStyles.boldLabel
        );

        Vector3 newPosition =
            EditorGUILayout.Vector3Field(
                "Position",
                positionOffset
            );

        Vector3 newRotation =
            EditorGUILayout.Vector3Field(
                "Rotation",
                modelRotation
            );

        float newFov =
            EditorGUILayout.Slider(
                "FOV",
                fieldOfView,
                10f,
                60f
            );

        float newFraming =
            EditorGUILayout.Slider(
                "Framing",
                framing,
                0.7f,
                2.5f
            );

        bool changed =
            newPosition != positionOffset ||
            newRotation != modelRotation ||
            !Mathf.Approximately(newFov, fieldOfView) ||
            !Mathf.Approximately(newFraming, framing);

        if (changed)
        {
            Undo.RecordObject(
                this,
                "Change Prefab Capture View"
            );

            positionOffset = newPosition;
            modelRotation = newRotation;
            fieldOfView = newFov;
            framing = newFraming;

            OnSettingsChanged();
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Reset View"))
        {
            Undo.RecordObject(
                this,
                "Reset Prefab Capture View"
            );

            positionOffset = Vector3.zero;
            modelRotation = new Vector3(20f, -25f, 0f);
            fieldOfView = 28f;
            framing = 1.15f;

            OnSettingsChanged();
        }
    }


    // =========================================================
    // Lighting
    // =========================================================

    private void DrawLightingSettings()
    {
        EditorGUILayout.LabelField(
            "Lighting",
            EditorStyles.boldLabel
        );

        float newKey =
            EditorGUILayout.Slider(
                "Key Light",
                keyLightIntensity,
                0f,
                4f
            );

        float newFill =
            EditorGUILayout.Slider(
                "Fill Light",
                fillLightIntensity,
                0f,
                4f
            );

        float newAmbient =
            EditorGUILayout.Slider(
                "Ambient",
                ambientIntensity,
                0f,
                2f
            );

        Color newBackground =
            EditorGUILayout.ColorField(
                "Background",
                backgroundColor
            );

        bool changed =
            !Mathf.Approximately(
                newKey,
                keyLightIntensity
            ) ||
            !Mathf.Approximately(
                newFill,
                fillLightIntensity
            ) ||
            !Mathf.Approximately(
                newAmbient,
                ambientIntensity
            ) ||
            newBackground != backgroundColor;

        if (changed)
        {
            Undo.RecordObject(
                this,
                "Change Prefab Capture Lighting"
            );

            keyLightIntensity = newKey;
            fillLightIntensity = newFill;
            ambientIntensity = newAmbient;
            backgroundColor = newBackground;

            OnSettingsChanged();
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Reset Lighting"))
        {
            Undo.RecordObject(
                this,
                "Reset Prefab Capture Lighting"
            );

            keyLightIntensity = 1.4f;
            fillLightIntensity = 0.6f;
            ambientIntensity = 0.3f;

            backgroundColor =
                new Color(0f, 0f, 0f, 0f);

            OnSettingsChanged();
        }
    }


    // =========================================================
    // Output
    // =========================================================

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField(
            "Output",
            EditorStyles.boldLabel
        );

        string[] names =
        {
            "512 x 512",
            "1024 x 1024",
            "2048 x 2048"
        };

        int[] values =
        {
            512,
            1024,
            2048
        };

        int newResolution =
            EditorGUILayout.IntPopup(
                "Resolution",
                outputResolution,
                names,
                values
            );

        if (newResolution != outputResolution)
        {
            Undo.RecordObject(
                this,
                "Change Capture Resolution"
            );

            outputResolution = newResolution;

            OnSettingsChanged();
        }
    }


    // =========================================================
    // Preview
    // =========================================================

    private void DrawPreview()
    {
        float size =
            Mathf.Min(
                position.width - 20f,
                440f
            );

        Rect rect =
            GUILayoutUtility.GetRect(
                size,
                size,
                GUILayout.ExpandWidth(false)
            );

        DrawCheckerboard(rect);

        if (targetPrefab == null)
        {
            GUI.Label(
                rect,
                "Prefab을 넣어주세요.",
                GetCenteredStyle()
            );

            return;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        Texture preview =
            RenderPreview(
                Mathf.Clamp(
                    (int)size,
                    256,
                    512
                )
            );

        if (preview != null)
        {
            GUI.DrawTexture(
                rect,
                preview,
                ScaleMode.ScaleToFit,
                true
            );
        }
    }


    // =========================================================
    // Create Preview
    // =========================================================

    private void CreatePreview()
    {
        CleanupPreview();

        if (targetPrefab == null)
            return;

        previewUtility =
            new PreviewRenderUtility();

        previewInstance =
            previewUtility.InstantiatePrefabInScene(
                targetPrefab
            );

        if (previewInstance == null)
            return;

        loadedPrefab = targetPrefab;

        baseRotation =
            previewInstance.transform.rotation;

        baseScale =
            previewInstance.transform.localScale;


        // 게임 로직은 Preview에서 필요 없음
        MonoBehaviour[] behaviours =
            previewInstance
                .GetComponentsInChildren<MonoBehaviour>(
                    true
                );

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        SetupCameraAndLights();
    }


    // =========================================================
    // Camera / Lights
    // =========================================================

    private void SetupCameraAndLights()
    {
        if (previewUtility == null)
            return;

        Camera camera =
            previewUtility.camera;

        camera.clearFlags =
            CameraClearFlags.SolidColor;

        camera.backgroundColor =
            backgroundColor;

        camera.orthographic = false;
        camera.allowHDR = false;

        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;


        Light[] lights =
            previewUtility.lights;

        if (lights == null ||
            lights.Length < 2)
        {
            return;
        }


        // -------------------------
        // Key Light
        // -------------------------

        lights[0].type =
            LightType.Directional;

        lights[0].color =
            Color.white;

        lights[0].intensity =
            keyLightIntensity;

        lights[0].shadows =
            LightShadows.None;

        lights[0].transform.rotation =
            Quaternion.Euler(
                35f,
                -35f,
                0f
            );


        // -------------------------
        // Fill / Rim
        // -------------------------

        lights[1].type =
            LightType.Directional;

        lights[1].color =
            Color.white;

        lights[1].intensity =
            fillLightIntensity;

        lights[1].shadows =
            LightShadows.None;

        lights[1].transform.rotation =
            Quaternion.Euler(
                330f,
                145f,
                0f
            );


        previewUtility.ambientColor =
            Color.white * ambientIntensity;
    }


    // =========================================================
    // Render
    // =========================================================

    private Texture RenderPreview(int size)
    {
        EnsurePreview();

        if (previewUtility == null ||
            previewInstance == null)
        {
            return null;
        }

        if (!SetupModelAndCamera())
            return null;

        SetupCameraAndLights();

        previewUtility.BeginPreview(
            new Rect(
                0f,
                0f,
                size,
                size
            ),
            GUIStyle.none
        );

        previewUtility.camera.pixelRect =
            new Rect(
                0f,
                0f,
                size,
                size
            );

        // true = URP / SRP 허용
        previewUtility.Render(true);

        return previewUtility.EndPreview();
    }


    // =========================================================
    // Model Position / Camera Framing
    // =========================================================

    private bool SetupModelAndCamera()
    {
        if (previewInstance == null)
            return false;

        previewInstance.transform.position =
            Vector3.zero;

        previewInstance.transform.rotation =
            Quaternion.Euler(modelRotation)
            * baseRotation;

        previewInstance.transform.localScale =
            baseScale;


        // 회전 적용 후 Bounds 계산
        if (!TryGetBounds(
                previewInstance,
                out Bounds bounds))
        {
            return false;
        }


        // Renderer의 실제 중심을 원점으로 정렬
        previewInstance.transform.position -=
            bounds.center;


        // 사용자가 지정한 Offset 추가
        previewInstance.transform.position +=
            positionOffset;


        // 이동 후 다시 Bounds 계산
        if (!TryGetBounds(
                previewInstance,
                out bounds))
        {
            return false;
        }


        Camera camera =
            previewUtility.camera;

        camera.fieldOfView =
            fieldOfView;


        // 정사각형 프레임이므로
        // X/Y 중 큰 쪽에 맞춰 카메라 거리 계산
        float halfSize =
            Mathf.Max(
                bounds.extents.x,
                bounds.extents.y
            );

        halfSize *= framing;

        halfSize =
            Mathf.Max(
                halfSize,
                0.001f
            );


        float distance =
            halfSize /
            Mathf.Tan(
                fieldOfView
                * 0.5f
                * Mathf.Deg2Rad
            );


        // 모델 깊이 확보
        distance +=
            bounds.extents.z;

        distance =
            Mathf.Max(
                distance,
                0.1f
            );


        camera.transform.position =
            new Vector3(
                0f,
                0f,
                -distance
            );

        camera.transform.rotation =
            Quaternion.identity;


        camera.nearClipPlane =
            0.01f;

        camera.farClipPlane =
            distance
            + bounds.size.magnitude * 4f
            + 10f;


        return true;
    }


    // =========================================================
    // Bounds
    // =========================================================

    private static bool TryGetBounds(
        GameObject root,
        out Bounds bounds)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        bool found = false;

        bounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled)
            {
                continue;
            }

            if (!found)
            {
                bounds =
                    renderer.bounds;

                found = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        return found;
    }


    // =========================================================
    // Export
    // =========================================================

    private void ExportPNG()
    {
        EnsurePreview();

        Texture source =
            RenderPreview(
                outputResolution
            );

        if (source == null)
        {
            Debug.LogError(
                "Prefab Preview를 렌더링하지 못했습니다."
            );

            return;
        }


        Texture2D result =
            CopyTexture(
                source,
                outputResolution,
                outputResolution
            );


        string defaultName =
            targetPrefab != null
                ? targetPrefab.name + "_UI"
                : "Prefab_UI";


        string path =
            EditorUtility.SaveFilePanel(
                "Export Prefab PNG",
                "",
                defaultName,
                "png"
            );


        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(result);
            return;
        }


        File.WriteAllBytes(
            path,
            result.EncodeToPNG()
        );

        DestroyImmediate(result);

        AssetDatabase.Refresh();

        Debug.Log(
            $"PNG Export 완료: {path}"
        );
    }


    private static Texture2D CopyTexture(
        Texture source,
        int width,
        int height)
    {
        RenderTexture temporary =
            RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32
            );

        Graphics.Blit(
            source,
            temporary
        );

        RenderTexture previous =
            RenderTexture.active;

        RenderTexture.active =
            temporary;


        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false
            );


        texture.ReadPixels(
            new Rect(
                0,
                0,
                width,
                height
            ),
            0,
            0
        );

        texture.Apply();


        RenderTexture.active =
            previous;

        RenderTexture.ReleaseTemporary(
            temporary
        );


        return texture;
    }


    // =========================================================
    // Prefab별 설정 저장
    // =========================================================

    private void SaveCurrentSettings()
    {
        if (targetPrefab == null)
            return;

        string key =
            GetSettingsKey(
                targetPrefab
            );

        if (string.IsNullOrEmpty(key))
            return;


        CaptureSettings settings =
            new CaptureSettings
            {
                positionOffset =
                    positionOffset,

                modelRotation =
                    modelRotation,

                fieldOfView =
                    fieldOfView,

                framing =
                    framing,

                keyLightIntensity =
                    keyLightIntensity,

                fillLightIntensity =
                    fillLightIntensity,

                ambientIntensity =
                    ambientIntensity,

                backgroundColor =
                    backgroundColor,

                outputResolution =
                    outputResolution
            };


        string json =
            JsonUtility.ToJson(
                settings
            );

        EditorPrefs.SetString(
            key,
            json
        );
    }


    private void LoadSettings(
        GameObject prefab)
    {
        string key =
            GetSettingsKey(
                prefab
            );

        if (string.IsNullOrEmpty(key) ||
            !EditorPrefs.HasKey(key))
        {
            ResetValuesWithoutUndo();
            return;
        }


        string json =
            EditorPrefs.GetString(
                key
            );

        CaptureSettings settings =
            JsonUtility.FromJson<CaptureSettings>(
                json
            );

        if (settings == null)
        {
            ResetValuesWithoutUndo();
            return;
        }


        positionOffset =
            settings.positionOffset;

        modelRotation =
            settings.modelRotation;

        fieldOfView =
            settings.fieldOfView;

        framing =
            settings.framing;

        keyLightIntensity =
            settings.keyLightIntensity;

        fillLightIntensity =
            settings.fillLightIntensity;

        ambientIntensity =
            settings.ambientIntensity;

        backgroundColor =
            settings.backgroundColor;

        outputResolution =
            settings.outputResolution;


        // 예전/깨진 설정 방지
        if (fieldOfView <= 0f)
            fieldOfView = 28f;

        if (framing <= 0f)
            framing = 1.15f;

        if (outputResolution != 512 &&
            outputResolution != 1024 &&
            outputResolution != 2048)
        {
            outputResolution = 1024;
        }
    }


    private static string GetSettingsKey(
        GameObject prefab)
    {
        if (prefab == null)
            return null;


        string path =
            AssetDatabase.GetAssetPath(
                prefab
            );

        if (string.IsNullOrEmpty(path))
            return null;


        string guid =
            AssetDatabase.AssetPathToGUID(
                path
            );

        if (string.IsNullOrEmpty(guid))
            return null;


        return
            $"Bluff.PrefabCapture.{guid}";
    }


    // =========================================================
    // Helpers
    // =========================================================

    private void OnSettingsChanged()
    {
        EditorUtility.SetDirty(this);

        SaveCurrentSettings();

        Repaint();
    }


    private void ResetValuesWithoutUndo()
    {
        positionOffset =
            Vector3.zero;

        modelRotation =
            new Vector3(
                20f,
                -25f,
                0f
            );

        fieldOfView =
            28f;

        framing =
            1.15f;


        keyLightIntensity =
            1.4f;

        fillLightIntensity =
            0.6f;

        ambientIntensity =
            0.3f;


        backgroundColor =
            new Color(
                0f,
                0f,
                0f,
                0f
            );

        outputResolution =
            1024;
    }


    private void EnsurePreview()
    {
        if (loadedPrefab != targetPrefab ||
            previewUtility == null ||
            previewInstance == null)
        {
            CreatePreview();
        }
    }


    private void CleanupPreview()
    {
        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }

        previewInstance = null;
        loadedPrefab = null;
    }


    // =========================================================
    // Checkerboard Background
    // =========================================================

    private static void DrawCheckerboard(
        Rect rect)
    {
        const float cellSize =
            16f;

        Color colorA =
            new Color(
                0.22f,
                0.22f,
                0.22f
            );

        Color colorB =
            new Color(
                0.32f,
                0.32f,
                0.32f
            );


        int columns =
            Mathf.CeilToInt(
                rect.width / cellSize
            );

        int rows =
            Mathf.CeilToInt(
                rect.height / cellSize
            );


        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Rect cellRect =
                    new Rect(
                        rect.x
                        + x * cellSize,

                        rect.y
                        + y * cellSize,

                        cellSize,
                        cellSize
                    );


                EditorGUI.DrawRect(
                    cellRect,
                    ((x + y) % 2 == 0)
                        ? colorA
                        : colorB
                );
            }
        }
    }


    private static GUIStyle GetCenteredStyle()
    {
        return new GUIStyle(
            EditorStyles.centeredGreyMiniLabel
        )
        {
            alignment =
                TextAnchor.MiddleCenter
        };
    }
}