using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class IntroDoorTriggerTests
{
    [Test]
    public void DirectionCheckRejectsCorridorAndSideFaces_WithScaledRotatedTrigger()
    {
        var root = new GameObject("Door test");
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(4, 3, 4);
        root.transform.position = new Vector3(3, 0, 8);
        root.transform.rotation = Quaternion.Euler(0, 70, 0);
        root.transform.localScale = new Vector3(2, 1, 3);
        var door = root.AddComponent<IntroDoorTrigger>();
        var inside = new GameObject("Inside");
        inside.transform.SetParent(root.transform, false);
        inside.transform.localPosition = Vector3.right * 3;
        typeof(IntroDoorTrigger).GetField("insidePoint", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(door, inside.transform);
        Assert.That(door.IsIndoorExit(root.transform.TransformPoint(new Vector3(-3, 0, 0))), Is.False);
        Assert.That(door.IsIndoorExit(root.transform.TransformPoint(new Vector3(1, 0, 3))), Is.False);
        Assert.That(door.IsIndoorExit(root.transform.TransformPoint(new Vector3(3, 0, 0))), Is.True);
        Object.DestroyImmediate(root);
    }
}
