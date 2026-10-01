using System;
using DG.Tweening;
using UnityEngine;

public sealed class DefyPresentation : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private float activationLiftHeight = 0.025f;
    [SerializeField] private float activationScale = 1.07f;
    [SerializeField] private float activationDuration = 0.225f;

    [Header("Move")]
    [SerializeField] private float moveDuration = 0.36f;
    [SerializeField] private float strikeDuration = 0.06f;

    [Header("Impact")]
    [SerializeField] private float impactScale = 1.13f;
    [SerializeField] private float impactExpandDuration = 0.0675f;
    [SerializeField] private float impactHoldDuration = 0.06f;

    [Header("Consume")]
    [SerializeField] private float consumeDuration = 0.225f;

    private Sequence sequence;
    private Action onImpact;
    private Action onFinished;
    private Action onImpactCue;
    private bool chipsSynced;

    public bool TryPlay(Vector3 target, Action onImpact, Action onFinished,
        Action onImpactCue = null)
    {
        if (sequence != null || !isActiveAndEnabled)
        {
            return false;
        }

        this.onImpact = onImpact;
        this.onFinished = onFinished;
        this.onImpactCue = onImpactCue;
        transform.SetParent(null, true);

        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Vector3 impactPosition = target + Vector3.up * 0.055f;

        sequence = DOTween.Sequence().SetAutoKill(true);

        sequence.Append(transform.DOMoveY(
            transform.position.y + activationLiftHeight, activationDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(startScale * activationScale, activationDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(transform.DORotateQuaternion(
            startRotation * Quaternion.Euler(0f, 8f, 0f), activationDuration));

        sequence.Append(transform.DOMove(impactPosition + Vector3.up * 0.03f, moveDuration)
            .SetEase(Ease.OutCubic));
        sequence.Append(transform.DOMove(impactPosition, strikeDuration)
            .SetEase(Ease.InCubic));

        sequence.AppendCallback(PlayImpactCue);
        sequence.AppendCallback(Impact);
        sequence.Append(transform.DOScale(startScale * impactScale, impactExpandDuration));
        sequence.AppendInterval(impactHoldDuration);

        sequence.Append(transform.DOScale(Vector3.zero, consumeDuration)
            .SetEase(Ease.InCubic));
        sequence.Join(transform.DORotateQuaternion(
            startRotation * Quaternion.Euler(0f, 45f, 0f), consumeDuration));
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

    private void PlayImpactCue()
    {
        onImpactCue?.Invoke();
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
