using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(BoxCollider))]
public sealed class IntroDoorTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector doorRevealDirector;
    [SerializeField] private Transform insidePoint;
    [SerializeField] private IntroFlowController introFlowController;
    private readonly HashSet<Collider> occupants = new HashSet<Collider>();
    private bool doorOpened;
    private bool cutsceneStarted;

    private void OnTriggerEnter(Collider other)
    {
        PlayerMove player = other.GetComponentInParent<PlayerMove>();
        if (player == null || !player.CompareTag("Player")) return;
        occupants.Add(other);
        if (doorOpened || doorRevealDirector == null) return;
        doorOpened = true;
        doorRevealDirector.playOnAwake = false;
        doorRevealDirector.extrapolationMode = DirectorWrapMode.Hold;
        doorRevealDirector.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!occupants.Remove(other) || occupants.Count != 0 || !doorOpened || cutsceneStarted) return;
        PlayerMove player = other.GetComponentInParent<PlayerMove>();
        if (player == null || insidePoint == null || introFlowController == null) return;
        if (!IsIndoorExit(player.transform.position)) return;
        cutsceneStarted = introFlowController.BeginBarSequence();
    }

    public bool IsIndoorExit(Vector3 position)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 localInside = transform.InverseTransformPoint(insidePoint.position) - box.center;
        Vector3 localExit = transform.InverseTransformPoint(position) - box.center;
        localInside.y = localExit.y = 0f;
        if (localInside.sqrMagnitude < 0.001f) return false;
        if (Mathf.Abs(localInside.x) >= Mathf.Abs(localInside.z))
            return localExit.x * Mathf.Sign(localInside.x) >= box.size.x * 0.5f;
        return localExit.z * Mathf.Sign(localInside.z) >= box.size.z * 0.5f;
    }
}
