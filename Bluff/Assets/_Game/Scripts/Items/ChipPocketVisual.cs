using DG.Tweening;
using UnityEngine;

public sealed class ChipPocketVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform itemRoot;
    [SerializeField] private Transform chip01;
    [SerializeField] private Transform chip02;
    [SerializeField] private Transform chip03;
    [SerializeField] private Transform chipTarget;

    [Header("Lift")]
    [SerializeField, Min(0f)] private float liftHeight = 0.08f;
    [SerializeField, Min(0.01f)] private float liftDuration = 0.18f;
    [SerializeField, Min(1f)] private float liftScale = 1.08f;

    [Header("Wiggle")]
    [SerializeField, Min(0f)] private float wiggleAngle = 5f;
    [SerializeField, Min(0.01f)] private float wiggleDuration = 0.12f;

    [Header("Visual Chips")]
    [SerializeField, Min(0f)] private float chipJumpPower = 0.1f;
    [SerializeField, Min(0.01f)] private float chipMoveDuration = 0.34f;
    [SerializeField, Min(0f)] private float chipStagger = 0.06f;
    [Tooltip("World-space spacing along Chip Target's right axis.")]
    [SerializeField, Min(0f)] private float chipTargetSpread = 0.025f;

    [Header("Consume")]
    [SerializeField, Min(0.01f)] private float consumeDuration = 0.16f;

    private Sequence previewSequence;
    private TransformState rootState;
    private TransformState[] chipStates;
    private bool hasSnapshot;

    [ContextMenu("Play Preview (Play Mode)")]
    public void PlayPreview()
    {
        ResetPreview();
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            Debug.LogWarning("ChipPocket preview requires an enabled component in Play Mode.", this);
            return;
        }

        if (!ValidateReferences())
        {
            return;
        }

        rootState = new TransformState(itemRoot);
        chipStates = new[]
        {
            new TransformState(chip01),
            new TransformState(chip02),
            new TransformState(chip03)
        };
        hasSnapshot = true;

        Vector3 startPosition = itemRoot.position;
        Quaternion startRotation = itemRoot.localRotation;
        Vector3 startScale = itemRoot.localScale;
        previewSequence = DOTween.Sequence();

        previewSequence.Append(itemRoot
            .DOMove(startPosition + Vector3.up * liftHeight, liftDuration)
            .SetEase(Ease.OutQuad));
        previewSequence.Join(itemRoot
            .DOScale(startScale * liftScale, liftDuration)
            .SetEase(Ease.OutQuad));
        previewSequence.Append(itemRoot
            .DOLocalRotateQuaternion(startRotation * Quaternion.Euler(0f, 0f, wiggleAngle), wiggleDuration * 0.25f)
            .SetEase(Ease.OutSine));
        previewSequence.Append(itemRoot
            .DOLocalRotateQuaternion(startRotation * Quaternion.Euler(0f, 0f, -wiggleAngle), wiggleDuration * 0.5f)
            .SetEase(Ease.InOutSine));
        previewSequence.Append(itemRoot
            .DOLocalRotateQuaternion(startRotation, wiggleDuration * 0.25f)
            .SetEase(Ease.OutSine));

        Sequence flights = DOTween.Sequence();
        flights.AppendInterval(chipMoveDuration + chipStagger * (chipStates.Length - 1));
        for (int index = 0; index < chipStates.Length; index++)
        {
            Transform chip = chipStates[index].Transform;
            if (chip != null)
            {
                flights.Insert(0f, CreateChipFlight(chip, index));
            }
        }
        previewSequence.Append(flights);
        previewSequence.Append(itemRoot.DOScale(Vector3.zero, consumeDuration).SetEase(Ease.InQuad));
        previewSequence.OnKill(() => previewSequence = null);
    }

    private Sequence CreateChipFlight(Transform chip, int index)
    {
        Vector3 start = default;
        Vector3 destination = default;
        Sequence flight = DOTween.Sequence();
        flight.AppendInterval(index * chipStagger);
        flight.AppendCallback(() =>
        {
            if (chip == null || chipTarget == null)
            {
                ResetPreview();
                return;
            }

            chip.SetParent(null, true);
            start = chip.position;
            destination = chipTarget.position + chipTarget.right * ((index - 1) * chipTargetSpread);
        });
        flight.Append(DOTween.To(() => 0f, progress =>
        {
            if (chip != null)
            {
                chip.position = Vector3.LerpUnclamped(start, destination, progress)
                    + Vector3.up * (4f * chipJumpPower * progress * (1f - progress));
            }
        }, 1f, chipMoveDuration).SetEase(Ease.Linear));
        flight.AppendCallback(() =>
        {
            if (chip != null)
            {
                chip.localScale = Vector3.zero;
            }
        });
        return flight;
    }

    [ContextMenu("Reset Preview")]
    public void ResetPreview()
    {
        Sequence activeSequence = previewSequence;
        previewSequence = null;
        activeSequence?.Kill(false);
        if (!hasSnapshot)
        {
            return;
        }

        rootState.Restore();
        foreach (TransformState state in chipStates)
        {
            state.Restore();
        }
        chipStates = null;
        hasSnapshot = false;
    }

    private bool ValidateReferences()
    {
        if (itemRoot == null || chipTarget == null || chipTarget.IsChildOf(itemRoot))
        {
            Debug.LogWarning("Assign Item Root and a Chip Target outside the pocket hierarchy.", this);
            return false;
        }

        Transform[] chips = { chip01, chip02, chip03 };
        bool missingChip = false;
        for (int index = 0; index < chips.Length; index++)
        {
            Transform chip = chips[index];
            if (chip == null)
            {
                missingChip = true;
                continue;
            }
            if (chip == itemRoot || !chip.IsChildOf(itemRoot) || transform.IsChildOf(chip))
            {
                Debug.LogWarning("Visual chips must be children of Item Root. Place ChipPocketVisual on the root or a separate preview object.", this);
                return false;
            }
            for (int other = 0; other < index; other++)
            {
                if (chips[other] != null && (chip.IsChildOf(chips[other]) || chips[other].IsChildOf(chip)))
                {
                    Debug.LogWarning("Assign three distinct visual chips without nesting one inside another.", this);
                    return false;
                }
            }
        }
        if (missingChip)
        {
            Debug.LogWarning("Unassigned visual chips will be skipped. Connect all three to preview the full ChipPocket effect.", this);
        }
        return true;
    }

    private void OnDisable()
    {
        ResetPreview();
    }

    private void OnDestroy()
    {
        ResetPreview();
    }

    private readonly struct TransformState
    {
        public readonly Transform Transform;
        private readonly Transform parent;
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;
        private readonly int siblingIndex;

        public TransformState(Transform target)
        {
            Transform = target;
            parent = target != null ? target.parent : null;
            localPosition = target != null ? target.localPosition : Vector3.zero;
            localRotation = target != null ? target.localRotation : Quaternion.identity;
            localScale = target != null ? target.localScale : Vector3.one;
            siblingIndex = target != null ? target.GetSiblingIndex() : 0;
        }

        public void Restore()
        {
            if (Transform == null)
            {
                return;
            }
            Transform.SetParent(parent, false);
            Transform.localPosition = localPosition;
            Transform.localRotation = localRotation;
            Transform.localScale = localScale;
            Transform.SetSiblingIndex(siblingIndex);
        }
    }
}
