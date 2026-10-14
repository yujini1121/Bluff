using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Converts the original imported model identity to a normal, script-free split prefab.</summary>
public static class SlotMachinePrefabMigration
{
    const string Original = "Assets/_Game/Art/Environment/SlotMachine/MD_SlotMachine/slot machine 1.obj";
    const string Destination = "Assets/_Game/Art/Environment/SlotMachine/MD_SlotMachine/slot machine 1.prefab";
    const string Source = "Assets/_Game/Art/Environment/EditedSlotMachine.prefab";
    const string ScenePath = "Assets/_Game/Scenes/Dev_Yujin.unity";
    const string Guid = "9d3537b122b8b9b4c8e2a9bf22a71d74";
    const string Output = "Logs/SlotMachinePrefabMigration";
    static readonly string[] Names = { "Body", "Screen_01", "Screen_02", "Screen_03", "Screen_04" };

    [Serializable] public class Placement
    {
        public string name;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Bounds bounds;
    }
    [Serializable] public class Result
    {
        public string prefab, guid, backup;
        public Vector3 commonTranslation;
        public float commonScale, maxWorldBoundsError;
        public bool savedSceneReloaded, fivePartsPerInstance, zeroRuntimeScripts, noSceneWrappers;
        public Placement[] placements;
    }

    [InitializeOnLoadMethod] static void OnReload()
    {
        EditorApplication.delayCall += () => {
            var request = Output + "/migrate.request";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Migrate(); }
            catch (Exception e) { File.WriteAllText(Output + "/error.txt", e.ToString()); Debug.LogException(e); }
        };
    }

    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static long Id(Object value) { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _, out long id); return id; }
    static float Max(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static Bounds BoundsOf(GameObject go, Matrix4x4 frame)
    {
        var points = go.GetComponentsInChildren<MeshFilter>(true).SelectMany(f => f.sharedMesh.vertices.Select(v =>
            (frame * f.transform.localToWorldMatrix).MultiplyPoint3x4(v))).ToArray();
        Check(points.Length > 0, "Empty model.");
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) bounds.Encapsulate(point);
        return bounds;
    }
    static Transform[] Targets(Scene scene)
    {
        var background = scene.GetRootGameObjects().Single(g => g.name == "Backgrounds");
        var group = background.transform.Find("Slot Machines");
        Check(group, "Missing Slot Machines group.");
        return Enumerable.Range(1, 5).Select(i => {
            var t = group.Find("Slot Machine (" + i + ")");
            Check(t, "Missing target " + i);
            return t;
        }).ToArray();
    }

    [MenuItem("Tools/Bluff/Slot Machine/Convert ORIGINAL prefab (no runtime scripts) %#&m")]
    public static void Migrate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.sceneCount == 1,
            "Exit Play Mode and keep only Dev_Yujin open.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        Check(scene.isLoaded, "Open Dev_Yujin first.");
        if (AssetDatabase.GUIDToAssetPath(Guid) == Destination) { Validate(); return; }
        Check(AssetDatabase.GUIDToAssetPath(Guid) == Original && !File.Exists(Destination), "Unexpected original asset identity.");
        Directory.CreateDirectory(Output);
        string backup = Output + "/Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(Original, backup + "/slot machine 1.obj");
        File.Copy(Original + ".meta", backup + "/slot machine 1.obj.meta");
        File.Copy(ScenePath, backup + "/Dev_Yujin.disk.unity");
        Check(EditorSceneManager.SaveScene(scene, backup + "/Dev_Yujin.live.unity", true), "Scene backup failed.");

        // Remove the previous scene-only implementation, including its runtime reference components.
        var targets = Targets(scene);
        if (targets.Any(t => t.Find("EditedSlotMachine")))
            Check(EditorApplication.ExecuteMenuItem("Tools/Bluff/Slot Machine/Restore Dev_Yujin original models"), "Could not restore previous implementation.");
        targets = Targets(scene);
        var staging = scene.GetRootGameObjects().SingleOrDefault(g => g.name == "EditedSlotMachine");
        if (staging) { staging.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(staging); }
        var placements = targets.Select(t => new Placement {
            name = t.name, position = t.position, rotation = t.rotation, scale = t.lossyScale,
            bounds = BoundsOf(t.gameObject, Matrix4x4.identity)
        }).ToArray();
        Check(EditorSceneManager.SaveScene(scene), "Could not save cleaned original instances.");
        File.Copy(ScenePath, backup + "/Dev_Yujin.cleaned.unity");

        var old = AssetDatabase.LoadAssetAtPath<GameObject>(Original);
        var oldFilter = old.GetComponentsInChildren<MeshFilter>(true).Single();
        var originalIds = new[] { Id(old), Id(old.transform), Id(oldFilter.gameObject), Id(oldFilter.transform), Id(oldFilter), Id(oldFilter.GetComponent<Renderer>()) };
        var oldBounds = BoundsOf(old, old.transform.worldToLocalMatrix);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        Vector3 translation;
        float scale;
        try {
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "EditedSlotMachine";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            Check(model.transform.childCount == 5 && Names.All(n => model.transform.Find(n)), "Unexpected source hierarchy.");
            Check(model.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Source has runtime scripts.");
            var newBounds = BoundsOf(model, Matrix4x4.identity);
            scale = oldBounds.size.y / newBounds.size.y;
            translation = new Vector3(oldBounds.center.x - newBounds.center.x * scale,
                oldBounds.min.y - newBounds.min.y * scale, oldBounds.center.z - newBounds.center.z * scale);
            // Apply the SAME similarity transform to all five parts; preserve rotations and source meshes.
            foreach (Transform child in model.transform) {
                child.localPosition = child.localPosition * scale + translation;
                child.localScale *= scale;
            }
            Check(Max(BoundsOf(model, Matrix4x4.identity).size - oldBounds.size) < 0.0001f, "Unexpected shape mismatch.");
            Check(PrefabUtility.SaveAsPrefabAsset(model, Destination), "Prefab creation failed.");
        } finally { Object.DestroyImmediate(model); }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
        var body = prefab.transform.Find("Body");
        var newIds = new[] { Id(prefab), Id(prefab.transform), Id(body.gameObject), Id(body), Id(body.GetComponent<MeshFilter>()), Id(body.GetComponent<Renderer>()) };
        var map = Enumerable.Range(0, originalIds.Length).ToDictionary(i => newIds[i], i => originalIds[i]);
        Check(map.Count == 6 && originalIds.Distinct().Count() == 6, "Invalid preserved file IDs.");
        var yaml = File.ReadAllText(Destination);
        yaml = Regex.Replace(yaml, @"(?<=fileID: )-?\d+|(?<=&)-?\d+", match => {
            long value = long.Parse(match.Value, CultureInfo.InvariantCulture);
            return map.TryGetValue(value, out var mapped) ? mapped.ToString(CultureInfo.InvariantCulture) : match.Value;
        });
        var meta = Regex.Replace(File.ReadAllText(Destination + ".meta"), @"(?m)^guid: [0-9a-f]+", "guid: " + Guid);
        File.WriteAllText(backup + "/preserved-ids.txt", string.Join("\n", map.Select(p => p.Key + " -> " + p.Value)));

        AssetDatabase.StartAssetEditing();
        try {
            // One asset identity, converted in place from ModelImporter to PrefabImporter.
            // The original OBJ and meta are preserved in the backup outside Assets.
            File.Delete(Original);
            File.Delete(Original + ".meta");
            File.WriteAllText(Destination, yaml);
            File.WriteAllText(Destination + ".meta", meta);
        } finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Check(AssetDatabase.GUIDToAssetPath(Guid) == Destination, "GUID migration failed; use saved backup.");
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var result = ValidateScene(scene, placements);
        result.backup = backup; result.commonScale = scale; result.commonTranslation = translation;
        result.savedSceneReloaded = true;
        Check(EditorSceneManager.SaveScene(scene), "Could not save migrated scene.");
        File.WriteAllText(Output + "/result.json", JsonUtility.ToJson(result, true));
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
        Debug.Log("Original slot machine identity converted to script-free split prefab. Existing five placements verified. " + Destination);
    }

    static Result ValidateScene(Scene scene, Placement[] expected)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
        Check(prefab && AssetDatabase.AssetPathToGUID(Destination) == Guid, "Wrong original prefab identity.");
        Check(prefab.transform.childCount == 5 && Names.All(n => prefab.transform.Find(n)), "Wrong original prefab hierarchy.");
        Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Prefab contains runtime scripts.");
        var targets = Targets(scene);
        float maxError = 0;
        foreach (var target in targets) {
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject) == Destination, "Wrong instance source.");
            Check(target.childCount == 5 && Names.All(n => target.Find(n)), "Scene wrapper or missing part.");
            Check(target.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Runtime component remains.");
            var parts = Names.Select(n => target.Find(n).GetComponent<MeshFilter>()).ToArray();
            Check(parts.All(p => p && p.sharedMesh && p.GetComponent<MeshRenderer>().enabled), "Invalid part or hidden renderer.");
            Check(parts.Select(p => p.sharedMesh).Distinct().Count() == 5, "Parts are not independent meshes.");
            foreach (var part in parts) {
                var p = prefab.transform.Find(part.name);
                Check(part.sharedMesh == p.GetComponent<MeshFilter>().sharedMesh &&
                    Quaternion.Angle(part.transform.localRotation, p.localRotation) < 0.001f, "Mesh or tilt changed.");
            }
            if (expected != null) {
                var before = expected.Single(p => p.name == target.name);
                Check(Vector3.Distance(before.position, target.position) < 0.000001f &&
                    Quaternion.Angle(before.rotation, target.rotation) < 0.001f && Max(before.scale - target.lossyScale) < 0.000001f,
                    "Placement changed.");
                var after = BoundsOf(target.gameObject, Matrix4x4.identity);
                maxError = Mathf.Max(maxError, Max(before.bounds.min - after.min), Max(before.bounds.max - after.max));
            }
        }
        Check(maxError < 0.001f, "World bounds differ by more than 1 mm.");
        return new Result { prefab = Destination, guid = Guid, maxWorldBoundsError = maxError, placements = expected,
            fivePartsPerInstance = true, zeroRuntimeScripts = true, noSceneWrappers = true };
    }

    [MenuItem("Tools/Bluff/Slot Machine/Validate ORIGINAL prefab %#&v")]
    public static void Validate()
    {
        var previous = JsonUtility.FromJson<Result>(File.ReadAllText(Output + "/result.json"));
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var result = ValidateScene(scene, previous.placements);
        File.WriteAllText(Output + "/validation.json", JsonUtility.ToJson(result, true));
        Debug.Log("Original prefab validated: five direct mesh parts, preserved placement, no runtime scripts or scene wrappers.");
    }
}
