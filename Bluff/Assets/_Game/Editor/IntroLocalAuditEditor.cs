using System.IO;
using System.Linq;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class IntroLocalAuditEditor
{
    public static void AuditBatch()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Intro.unity");
        AuditOpenScene();
    }
    [MenuItem("Tools/Intro/Audit Local Intro")]
    public static void AuditOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var report = new StringBuilder();
        foreach (var t in all.Where(t => t.name.Contains("Dealer") || t.name.Contains("dealer") ||
                     t.GetComponent<Camera>() || t.GetComponent<CinemachineVirtualCamera>() ||
                     t.GetComponent<PlayableDirector>() || t.GetComponent<IntroDoorTrigger>() ||
                     t.GetComponent<PlayerMove>() || t.name.Contains("Inside")))
        {
            report.AppendLine($"{PathOf(t)} active={t.gameObject.activeInHierarchy} pos={t.position:F3} rot={t.eulerAngles:F2} scale={t.lossyScale:F3} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)}");
            foreach (var a in t.GetComponents<Animator>()) report.AppendLine($" Animator avatar={a.avatar} human={a.isHuman} controller={a.runtimeAnimatorController}");
            foreach (var c in t.GetComponents<Collider>()) report.AppendLine($" Collider center={c.bounds.center:F3} size={c.bounds.size:F3}");
            foreach (var d in t.GetComponents<PlayableDirector>())
            {
                report.AppendLine($" Director {AssetDatabase.GetAssetPath(d.playableAsset)} duration={d.duration} wrap={d.extrapolationMode}");
                if (d.playableAsset != null) foreach (var o in d.playableAsset.outputs) report.AppendLine($"  {o.streamName}: {d.GetGenericBinding(o.sourceObject)}");
            }
            if (t.name == "PF_Dealer") foreach (var r in t.GetComponentsInChildren<Renderer>()) report.AppendLine($" Renderer {r.name} {r.GetType().Name} bounds={r.bounds}");
        }
        report.AppendLine("Dirty=" + scene.isDirty);
        foreach (var r in all.SelectMany(t => t.GetComponents<Renderer>()).Where(r => r.gameObject.activeInHierarchy &&
            (r.name.ToLower().Contains("chair") || r.name.ToLower().Contains("pub") || r.name.ToLower().Contains("wall"))))
            report.AppendLine($"Geometry {PathOf(r.transform)} bounds={r.bounds} pos={r.transform.position:F3}");
        Directory.CreateDirectory("Logs/IntroRevisionBackup");
        File.WriteAllText("Logs/IntroRevisionBackup/local-audit.txt", report.ToString());
        Debug.Log("Intro local audit complete");
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
