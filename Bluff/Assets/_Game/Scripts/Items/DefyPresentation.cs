using System;
using DG.Tweening;
using UnityEngine;

public sealed class DefyPresentation : MonoBehaviour
{
    private Sequence sequence;
    private Action onImpact;
    private Action onFinished;
    private bool chipsSynced;

    public bool TryPlay(Vector3 target, Action onImpact, Action onFinished)
    {
        if (sequence != null || !isActiveAndEnabled)
        {
            return false;
        }

        this.onImpact = onImpact;
        this.onFinished = onFinished;
        transform.SetParent(null, true);

        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Vector3 impactPosition = target + Vector3.up * 0.055f;

        sequence = DOTween.Sequence().SetAutoKill(true);

        sequence.Append(transform.DOMoveY(transform.position.y + 0.025f, 0.15f)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(startScale * 1.07f, 0.15f)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DORotateQuaternion(
            startRotation * Quaternion.Euler(0f, 8f, 0f), 0.15f));

        sequence.Append(transform.DOMove(impactPosition + Vector3.up * 0.03f, 0.24f)
            .SetEase(Ease.OutCubic));
        sequence.Append(transform.DOMove(impactPosition, 0.04f)
            .SetEase(Ease.InCubic));

        sequence.AppendCallback(Impact);
        sequence.Append(transform.DOScale(startScale * 1.13f, 0.045f));
        sequence.AppendInterval(0.04f);

        sequence.Append(transform.DOScale(Vector3.zero, 0.15f)
            .SetEase(Ease.InCubic));
        sequence.Join(transform.DORotateQuaternion(
            startRotation * Quaternion.Euler(0f, 45f, 0f), 0.15f));
        sequence.OnComplete(Finish);
        return true;
    }

    public void Cancel()
    {
        if (sequence == null) return;

        sequence.Kill(false);
        Finish();
    }

    private void Impact()
    {
        if (chipsSynced) return;

        chipsSynced = true;
        try
        {
            onImpact?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void Finish()
    {
        if (sequence == null) return;

        sequence = null;
        Impact();

        try
        {
            onFinished?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (this == null) return;
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDisable()
    {
        Cancel();
    }
}
