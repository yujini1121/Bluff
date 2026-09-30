using System;
using DG.Tweening;
using UnityEngine;

public sealed class RefreshCardPresentation : MonoBehaviour
{
    private Sequence sequence;
    private Action onActivated;
    private Action onFinished;
    private bool finished;

    public bool TryPlay(Action onActivated, Action onFinished)
    {
        if (sequence != null || finished || !isActiveAndEnabled)
        {
            return false;
        }

        this.onActivated = onActivated;
        this.onFinished = onFinished;
        transform.SetParent(null, true);

        float startY = transform.position.y;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;

        sequence = DOTween.Sequence().SetAutoKill(true);
        sequence.Append(transform.DOMoveY(startY + 0.025f, 0.11f)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(startScale * 1.07f, 0.11f)
            .SetEase(Ease.OutCubic));
        sequence.Append(transform.DOLocalRotateQuaternion(
            startRotation * Quaternion.Euler(0f, 18f, 0f), 0.09f)
            .SetEase(Ease.OutCubic));
        sequence.Append(transform.DOLocalRotateQuaternion(startRotation, 0.04f));
        sequence.AppendInterval(0.02f);
        sequence.OnComplete(ActivationFinished);
        return true;
    }

    public void Consume()
    {
        if (finished || sequence != null) return;

        sequence = DOTween.Sequence().SetAutoKill(true);
        sequence.Append(transform.DOScale(Vector3.zero, 0.1f)
            .SetEase(Ease.InCubic));
        sequence.OnComplete(Finish);
    }

    public void Cancel()
    {
        if (finished) return;

        sequence?.Kill(false);
        Finish();
    }

    private void ActivationFinished()
    {
        sequence = null;
        try
        {
            onActivated?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Cancel();
        }
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
