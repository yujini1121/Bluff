using System;
using DG.Tweening;
using UnityEngine;

public sealed class PrizmPresentation : MonoBehaviour
{
    private Sequence sequence;
    private Action onFinished;
    private bool finished;

    public bool TryPlay(Vector3 target, Action onFinished)
    {
        if (sequence != null || finished || !isActiveAndEnabled)
        {
            return false;
        }

        this.onFinished = onFinished;
        transform.SetParent(null, true);

        Vector3 startScale = transform.localScale;
        Vector3 startRotation = transform.eulerAngles;
        Vector3 protectPosition = target + Vector3.up * 0.055f;

        sequence = DOTween.Sequence().SetAutoKill(true);

        sequence.Append(transform.DOMoveY(transform.position.y + 0.025f, 0.15f)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(startScale * 1.07f, 0.15f));
        sequence.Join(transform.DORotate(startRotation + new Vector3(0f, 8f, 0f), 0.15f));

        sequence.Append(transform.DOMove(protectPosition, 0.24f)
            .SetEase(Ease.OutCubic));

        sequence.Append(transform.DORotate(
            startRotation + new Vector3(0f, 278f, 0f), 0.11f,
            RotateMode.FastBeyond360));
        sequence.Join(transform.DOPunchScale(startScale * 0.06f, 0.11f, 1, 0f));
        sequence.AppendInterval(0.04f);

        sequence.Append(transform.DOScale(Vector3.zero, 0.15f)
            .SetEase(Ease.InCubic));
        sequence.Join(transform.DORotate(
            startRotation + new Vector3(0f, 320f, 0f), 0.15f,
            RotateMode.FastBeyond360));
        sequence.OnComplete(Finish);
        return true;
    }

    public void Cancel()
    {
        if (finished) return;

        sequence?.Kill(false);
        Finish();
    }

    private void Finish()
    {
        if (finished) return;

        finished = true;
        sequence = null;
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
