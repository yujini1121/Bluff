using System;
using DG.Tweening;
using UnityEngine;

public sealed class PrizmPresentation : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private float activationLiftHeight = 0.025f;
    [SerializeField] private float activationScale = 1.07f;
    [SerializeField] private float activationDuration = 0.225f;

    [Header("Move")]
    [SerializeField] private float moveDuration = 0.36f;

    [Header("Protect")]
    [SerializeField] private float protectSpinDuration = 0.165f;
    [SerializeField] private float protectPunchScale = 0.06f;
    [SerializeField] private float protectHoldDuration = 0.06f;

    [Header("Consume")]
    [SerializeField] private float consumeDuration = 0.225f;

    private Sequence sequence;
    private Action onFinished;
    private Action onProtect;
    private bool finished;

    public bool TryPlay(Vector3 target, Action onFinished, Action onProtect = null)
    {
        if (sequence != null || finished || !isActiveAndEnabled)
        {
            return false;
        }

        this.onFinished = onFinished;
        this.onProtect = onProtect;
        transform.SetParent(null, true);

        Vector3 startScale = transform.localScale;
        Vector3 startRotation = transform.eulerAngles;
        Vector3 protectPosition = target + Vector3.up * 0.055f;

        sequence = DOTween.Sequence().SetAutoKill(true);

        sequence.Append(transform.DOMoveY(
            transform.position.y + activationLiftHeight, activationDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(startScale * activationScale, activationDuration));
        sequence.Join(transform.DORotate(
            startRotation + new Vector3(0f, 8f, 0f), activationDuration));

        sequence.Append(transform.DOMove(protectPosition, moveDuration)
            .SetEase(Ease.OutCubic));

        sequence.AppendCallback(Protect);
        sequence.Append(transform.DORotate(
            startRotation + new Vector3(0f, 278f, 0f), protectSpinDuration,
            RotateMode.FastBeyond360));
        sequence.Join(transform.DOPunchScale(
            startScale * protectPunchScale, protectSpinDuration, 1, 0f));
        sequence.AppendInterval(protectHoldDuration);

        sequence.Append(transform.DOScale(Vector3.zero, consumeDuration)
            .SetEase(Ease.InCubic));
        sequence.Join(transform.DORotate(
            startRotation + new Vector3(0f, 320f, 0f), consumeDuration,
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

    private void Protect()
    {
        onProtect?.Invoke();
    }

    private void OnDisable()
    {
        Cancel();
    }
}
