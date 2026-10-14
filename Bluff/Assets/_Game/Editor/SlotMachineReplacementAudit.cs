using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SlotMachineReplacementAudit
{
    [InitializeOnLoadMethod]
    private static void OnReload()
    {
        EditorApplication.delayCall += () => {
            if (!File.Exists(Output + "/audit.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Output + "/audit.request");
            try { Audit(); } catch (Exception e) { File.WriteAllText(Output + "/audit.error.txt", e.ToString()); Debug.LogException(e); }
        };
    }
    public const string ScenePath = "Assets/_Game/Scenes/Dev_Yujin.unity";
    public const string SourcePath = "Assets/_Game/Art/Environment/EditedSlotMachine.prefab";
    public const string Output = "Logs/SlotMachineReplacement";
    [Serializable] public class MeshInfo
    {
        public string path, mesh, asset;
        public Vector3 localPosition, localScale, worldPosition, worldScale;
        public Quaternion localRotation, worldRotation;
        public Bounds meshBounds, rendererBounds;
        public Vector3[] vertices;
    }
    [Serializable] public class ObjectInfo
    {
        public string path, id, prefab;
        public bool active;
        public Vector3 position, scale;
        public Quaternion rotation;
        public MeshInfo[] meshes;
    }
    [Serializable] public class AuditData { public ObjectInfo[] instances; public ObjectInfo source; }

    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static ObjectInfo Describe(GameObject go)
    {
        return new ObjectInfo {
            path = PathOf(go.transform), id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(),
            prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go), active = go.activeInHierarchy,
            position = go.transform.position, rotation = go.transform.rotation, scale = go.transform.lossyScale,
            meshes = go.GetComponentsInChildren<MeshFilter>(true).Select(f => new MeshInfo {
                path = PathOf(f.transform), mesh = f.sharedMesh.name, asset = AssetDatabase.GetAssetPath(f.sharedMesh),
                localPosition = f.transform.localPosition, localRotation = f.transform.localRotation, localScale = f.transform.localScale,
                worldPosition = f.transform.position, worldRotation = f.transform.rotation, worldScale = f.transform.lossyScale,
                meshBounds = f.sharedMesh.bounds, rendererBounds = f.GetComponent<Renderer>() ? f.GetComponent<Renderer>().bounds : new Bounds(),
                vertices = f.sharedMesh.vertices.Select(v => go.transform.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray()
            }).ToArray()
        };
    }
    [MenuItem("Tools/Bluff/Slot Machine/Audit Dev_Yujin (read only)")]
    public static void Audit()
    {
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject source = null;
        try {
            var instances = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.IndexOf("slot", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    t.GetComponentsInChildren<MeshFilter>(true).Length > 0 && t.name != "Slot Machines")
                .Select(t => Describe(t.gameObject)).ToArray();
            source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath));
            source.name = "EditedSlotMachine";
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            source.transform.localScale = Vector3.one;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/audit.json", JsonUtility.ToJson(new AuditData { instances = instances, source = Describe(source) }, true));
            Debug.Log("Slot machine audit written to " + Output + "/audit.json; candidates: " + instances.Length);
        } finally {
            if (source) UnityEngine.Object.DestroyImmediate(source);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
