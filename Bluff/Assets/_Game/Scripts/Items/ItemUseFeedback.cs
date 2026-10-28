using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemUseFeedback : MonoBehaviour
{
    private const float Duration = 0.4f;

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
            new Vector3(4f, 18f, 4f), Duration, 18, 0.9f));
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
