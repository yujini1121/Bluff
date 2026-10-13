using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
public static class DigitalScreenTearingEditor
{
    [MenuItem("Tools/Defeat/Validate Screen Split (read only)")]
    public static void Validate()
    {
        var effect = UnityEngine.Object.FindObjectsOfType<DigitalScreenTearing>(true).Single();
        var fields = new SerializedObject(effect);
        var director = (PlayableDirector)fields.FindProperty("director").objectReferenceValue;
        var camera = (Camera)fields.FindProperty("targetCamera").objectReferenceValue;
        var track = (AnimationTrack)fields.FindProperty("animationTrack").objectReferenceValue;
        Require(director != null && camera != null && track != null, "Screen Split camera / Timeline references missing.");
        Require(track.timelineAsset == director.playableAsset, "Screen Split track belongs to another Timeline.");
        Require(director.GetGenericBinding(track) == effect.GetComponent<Animator>(), "Screen Split Animation Track binding missing.");
        Require(fields.FindProperty("owner").objectReferenceValue != null, "Defeat lifecycle owner missing.");
        foreach (var property in new[] { "intensity", "crackProgress", "separationProgress" })
            Require(track.GetClips().Any(c => c.asset is AnimationPlayableAsset a && a.clip != null &&
                AnimationUtility.GetCurveBindings(a.clip).Any(b => b.type == typeof(DigitalScreenTearing) && b.propertyName == property)),
                "No Screen Split Timeline curve found: " + property);
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Bluff_URPAsset_Renderer.asset");
        Require(renderer != null && renderer.rendererFeatures.OfType<DigitalScreenTearingFeature>().Count() == 1,
            "Exactly one DigitalScreenTearingFeature should be installed on the gameplay renderer.");
        var feature = renderer.rendererFeatures.OfType<DigitalScreenTearingFeature>().Single();
        Require(new SerializedObject(feature).FindProperty("shader").objectReferenceValue != null, "Screen Split shader reference missing.");
        Debug.Log("Screen Split references and animation binding validated. Render appearance must be checked in Game view.", effect);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
