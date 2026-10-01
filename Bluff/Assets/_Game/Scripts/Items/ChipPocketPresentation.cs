using System;
using DG.Tweening;
using UnityEngine;

public sealed class ChipPocketPresentation : MonoBehaviour
{
    [Header("Prefab parts")]
    [SerializeField] private Transform rightLid;
    [SerializeField] private Transform chip01;
    [SerializeField] private Transform chip02;
    [SerializeField] private Transform chip03;

    [Header("Activation")]
    [SerializeField, Min(0f)] private float liftHeight = 0.015f;
    [SerializeField, Min(0f)] private float liftDuration = 0.1275f;
    [SerializeField, Min(1f)] private float liftScale = 1.015f;

    [Header("Lid")]
    [SerializeField] private Vector3 lidPopOffset = new Vector3(-0.09f, 0.025f, 0.01f);
    [SerializeField] private Vector3 lidPopEulerAngles = new Vector3(0f, -6f, -7f);
    [SerializeField, Min(0f)] private float lidPopDuration = 0.2025f;
    [SerializeField, Min(0f)] private float afterLidPopDelay = 0.0825f;

    [Header("Chips")]
    [SerializeField] private Vector3 chipLaunchOffset = new Vector3(0f, 0.115f, 0.018f);
    [SerializeField, Min(0f)] private float chipLaunchDuration = 0.135f;
    [SerializeField, Min(0f)] private float chipJumpPower = 0.028f;
    [SerializeField, Min(0f)] private float chipMoveDuration = 0.645f;
    [SerializeField, Min(0f)] private float chipStagger = 0.1125f;

    [Header("Consume")]
    [SerializeField, Min(0f)] private float consumeDuration = 0.165f;

    private Sequence sequence;
    private Action onChipsArrived;
    private Action onFinished;
    private Action onLidOpening;
    private bool chipsSynced;

    public bool TryPlay(Vector3[] targets, Action onChipsArrived, Action onFinished,
        Action onLidOpening = null)
    {
        if (sequence != null || !isActiveAndEnabled ||
            rightLid == null || chip01 == null || chip02 == null || chip03 == null ||
            targets == null || targets.Length != 3)
        {
            return false;
        }

        this.onChipsArrived = onChipsArrived;
        this.onFinished = onFinished;
        this.onLidOpening = onLidOpening;
        transform.SetParent(null, true);

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion poppedLidRotation = rightLid.localRotation *
            Quaternion.Euler(lidPopEulerAngles);

        sequence = DOTween.Sequence().SetAutoKill(true);
        sequence.Append(transform.DOMoveY(
            startPosition.y + liftHeight, liftDuration).SetEase(Ease.OutCubic));
        sequence.Join(transform.DOScale(
            startScale * liftScale, liftDuration).SetEase(Ease.OutCubic));
        sequence.AppendCallback(LidOpening);
        sequence.Append(rightLid.DOLocalMove(
            rightLid.localPosition + lidPopOffset, lidPopDuration).SetEase(Ease.OutQuart));
        sequence.Join(rightLid.DOLocalRotateQuaternion(
            poppedLidRotation, lidPopDuration).SetEase(Ease.OutQuart));
        sequence.AppendInterval(afterLidPopDelay);

        Transform[] chips = { chip01, chip02, chip03 };
        float firstLaunch = sequence.Duration();
        for (int index = 0; index < chips.Length; index++)
        {
            Sequence flight = DOTween.Sequence();
            flight.Append(chips[index].DOMove(
                    transform.TransformDirection(chipLaunchOffset), chipLaunchDuration)
                .SetRelative().SetEase(Ease.OutQuart));
            flight.Append(chips[index].DOJump(
                    targets[index], chipJumpPower, 1, chipMoveDuration)
                .SetEase(Ease.InOutCubic));
            sequence.Insert(firstLaunch + index * chipStagger, flight);
        }

        sequence.AppendCallback(SyncChips);
        sequence.Append(transform.DOScale(
            Vector3.zero, consumeDuration).SetEase(Ease.InCubic));
        sequence.OnComplete(Finish);
        return true;
    }

    public void Cancel()
    {
        if (sequence == null) return;

        sequence.Kill(false);
        Finish();
    }

    private void LidOpening()
    {
        onLidOpening?.Invoke();
    }

    private void SyncChips()
    {
        if (chipsSynced) return;

        if (chip01 != null) chip01.gameObject.SetActive(false);
        if (chip02 != null) chip02.gameObject.SetActive(false);
        if (chip03 != null) chip03.gameObject.SetActive(false);

        chipsSynced = true;
        try
        {
            onChipsArrived?.Invoke();
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
        SyncChips();

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
