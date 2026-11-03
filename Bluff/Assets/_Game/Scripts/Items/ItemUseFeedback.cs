using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemUseFeedback : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.4f;
    [SerializeField] private Vector3 rotationStrength = new Vector3(4f, 18f, 4f);
    [SerializeField, Min(1)] private int vibrato = 18;
    [SerializeField, Range(0f, 1f)] private float elasticity = 0.9f;

    private Sequence sequence;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    public bool PlayRejected()
    {
        if (!isActiveAndEnabled || sequence != null)
        {
            return false;
        }

        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        sequence = DOTween.Sequence().SetAutoKill(true);
        sequence.Append(transform.DOPunchRotation(
            rotationStrength, duration, vibrato, elasticity));
        sequence.SetEase(Ease.Linear).OnKill(RestorePose);
        return true;
    }

    public void Cancel()
    {
        sequence?.Kill(false);
    }

    private void RestorePose()
    {
        sequence = null;
        if (this != null)
        {
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
    }
}
