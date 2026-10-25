using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroSlotStation : MonoBehaviour
{
    [SerializeField] private SlotMachineController machine;
    [SerializeField] private Transform interactionTarget;
    [SerializeField] private Transform cameraPose;
    [SerializeField] private Collider spinControl;
    [SerializeField, Min(.1f)] private float interactionDistance = 2.8f;
    [SerializeField, Range(1, 90)] private float interactionAngle = 35;
    [SerializeField, Range(25, 80)] private float fieldOfView = 48;
    private IntroSlotSession owner;
    private bool npcReserved;
    public SlotMachineController Machine => machine;
    public Transform CameraPose => cameraPose;
    public Transform InteractionTarget => interactionTarget;
    public float FieldOfView => fieldOfView;
    public bool IsAvailable => isActiveAndEnabled && machine && machine.isActiveAndEnabled &&
        machine.IsConfigured && !machine.IsSpinning && !owner && !npcReserved && cameraPose && interactionTarget;

    private void Awake()
    {
        foreach (var npc in FindObjectsOfType<SlotMachineNpc>(true))
            if (npc.Machine == machine) npcReserved = true;
    }

    public bool CanApproach(Transform view)
    {
        if (!IsAvailable || !view) return false;
        Vector3 offset = interactionTarget.position - view.position;
        if (offset.magnitude > interactionDistance || Vector3.Angle(view.forward, offset) > interactionAngle) return false;
        if (Physics.Raycast(view.position, offset.normalized, out var hit, offset.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.transform == machine.transform || hit.transform.IsChildOf(machine.transform);
        return true;
    }

    public bool IsSpinControlHit(Ray pointer) => spinControl && spinControl.enabled &&
        spinControl.gameObject.activeInHierarchy && spinControl.Raycast(pointer, out _, 20f);

    public bool TryClaim(IntroSlotSession session)
    {
        if (!session || !IsAvailable) return false;
        owner = session;
        return true;
    }

    public void Release(IntroSlotSession session) { if (owner == session) owner = null; }
}

