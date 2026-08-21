using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class DealerAnimationController : MonoBehaviour
{
    private const float CallChipMoveDuration = 0.25f;
    private const float CallAnimationTimeout = 4f;
    private const string CallTrigger = "Call";

    [SerializeField] private Animator animator;
    [SerializeField] private Transform chipSocket;
    [SerializeField] private Transform potPoint;
    [SerializeField] private Transform dealerChipPoint;
    [FormerlySerializedAs("chip")]
    [FormerlySerializedAs("collectChip")]
    [SerializeField] private Transform testChip;

    private Transform[] activeCallChips;
    private Transform[] originalChipParents;
    private Vector3[] originalChipLocalPositions;
    private Quaternion[] originalChipLocalRotations;
    private Vector3[] originalChipLocalScales;
    private Vector3[] callPotTargetPositions;
    private Action<GameObject[]> callMoveCompleted;
    private Action<GameObject[]> callMoveFailed;
    private Coroutine callTimeoutCoroutine;
    private readonly List<Tween> callMoveTweens = new List<Tween>();
    private int completedCallMoveCount;

    public bool TryPlayCallChips(
        GameObject[] chips,
        Vector3[] potTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (chips == null ||
            chips.Length == 0 ||
            potTargetPositions == null ||
            chips.Length != potTargetPositions.Length ||
            activeCallChips != null ||
            chipSocket == null ||
            !CanSetTrigger(CallTrigger))
        {
            return false;
        }

        for (int index = 0; index < chips.Length; index++)
        {
            if (chips[index] == null)
            {
                return false;
            }
        }

        activeCallChips = new Transform[chips.Length];
        originalChipParents = new Transform[chips.Length];
        originalChipLocalPositions = new Vector3[chips.Length];
        originalChipLocalRotations = new Quaternion[chips.Length];
        originalChipLocalScales = new Vector3[chips.Length];
        callPotTargetPositions = new Vector3[potTargetPositions.Length];

        for (int index = 0; index < chips.Length; index++)
        {
            Transform chip = chips[index].transform;
            activeCallChips[index] = chip;
            originalChipParents[index] = chip.parent;
            originalChipLocalPositions[index] = chip.localPosition;
            originalChipLocalRotations[index] = chip.localRotation;
            originalChipLocalScales[index] = chip.localScale;
            callPotTargetPositions[index] = potTargetPositions[index];
        }

        callMoveCompleted = onMoveCompleted;
        callMoveFailed = onMoveFailed;

        animator.SetTrigger(CallTrigger);
        callTimeoutCoroutine = StartCoroutine(CallAnimationTimeoutRoutine());
        return true;
    }

    public void PlayCall()
    {
        TrySetTrigger(CallTrigger);
    }

    public void PlayAllIn()
    {
        TrySetTrigger("All-In");
    }

    public void PlayCollect()
    {
        TrySetTrigger("Collect");
    }

    public void PlayThink()
    {
        TrySetTrigger("Think");
    }

    public void GrabChip()
    {
        if (chipSocket == null)
        {
            return;
        }

        if (activeCallChips != null)
        {
            if (!HasValidActiveCallChips())
            {
                FailCallChipMove();
                return;
            }

            Vector3 firstChipPosition = originalChipLocalPositions[0];

            for (int index = 0; index < activeCallChips.Length; index++)
            {
                Transform chip = activeCallChips[index];
                chip.SetParent(chipSocket, false);
                chip.localPosition =
                    originalChipLocalPositions[index] - firstChipPosition;
                chip.localRotation = Quaternion.identity;
            }

            return;
        }

        if (testChip != null)
        {
            testChip.SetParent(chipSocket);
            testChip.localPosition = Vector3.zero;
            testChip.localRotation = Quaternion.identity;
        }
    }

    public void ReleaseChip()
    {
        if (activeCallChips != null)
        {
            if (callMoveTweens.Count > 0)
            {
                return;
            }

            if (!HasValidActiveCallChips())
            {
                FailCallChipMove();
                return;
            }

            completedCallMoveCount = 0;

            for (int index = 0; index < activeCallChips.Length; index++)
            {
                Transform movingChip = activeCallChips[index];
                movingChip.SetParent(null, true);
                Tween moveTween = movingChip
                    .DOMove(
                        callPotTargetPositions[index],
                        CallChipMoveDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(CompleteCallChipMove);
                callMoveTweens.Add(moveTween);
            }

            return;
        }

        if (testChip == null || potPoint == null)
        {
            return;
        }

        testChip.SetParent(null);
        testChip
            .DOMove(potPoint.position, CallChipMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    public void ReleaseCollectChip()
    {
        if (testChip == null || dealerChipPoint == null)
        {
            return;
        }

        testChip.SetParent(null);
        testChip
            .DOMove(dealerChipPoint.position, CallChipMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    public void CancelCallChipAnimation()
    {
        FailCallChipMove();
    }

    private bool TrySetTrigger(string triggerName)
    {
        if (!CanSetTrigger(triggerName))
        {
            return false;
        }

        animator.SetTrigger(triggerName);
        return true;
    }

    private bool CanSetTrigger(string triggerName)
    {
        if (!isActiveAndEnabled ||
            animator == null ||
            !animator.enabled ||
            !animator.gameObject.activeInHierarchy ||
            animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].type == AnimatorControllerParameterType.Trigger &&
                parameters[index].name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator CallAnimationTimeoutRoutine()
    {
        yield return new WaitForSeconds(CallAnimationTimeout);
        callTimeoutCoroutine = null;
        FailCallChipMove();
    }

    private void CompleteCallChipMove()
    {
        if (activeCallChips == null)
        {
            return;
        }

        completedCallMoveCount++;

        if (completedCallMoveCount < activeCallChips.Length)
        {
            return;
        }

        GameObject[] completedChips = GetActiveCallChipObjects();
        Action<GameObject[]> completedCallback = callMoveCompleted;
        ClearCallChipState(false);
        completedCallback?.Invoke(completedChips);
    }

    private void FailCallChipMove()
    {
        if (activeCallChips == null && callMoveFailed == null)
        {
            return;
        }

        GameObject[] failedChips = GetActiveCallChipObjects();
        Action<GameObject[]> failedCallback = callMoveFailed;
        KillCallMoveTweens();
        RestoreCallChips();
        ClearCallChipState(false);
        failedCallback?.Invoke(failedChips);
    }

    private bool HasValidActiveCallChips()
    {
        if (activeCallChips == null || activeCallChips.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < activeCallChips.Length; index++)
        {
            if (activeCallChips[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreCallChips()
    {
        if (activeCallChips == null)
        {
            return;
        }

        for (int index = 0; index < activeCallChips.Length; index++)
        {
            Transform chip = activeCallChips[index];

            if (chip == null)
            {
                continue;
            }

            chip.SetParent(originalChipParents[index], false);
            chip.localPosition = originalChipLocalPositions[index];
            chip.localRotation = originalChipLocalRotations[index];
            chip.localScale = originalChipLocalScales[index];
        }
    }

    private GameObject[] GetActiveCallChipObjects()
    {
        if (activeCallChips == null)
        {
            return null;
        }

        GameObject[] chips = new GameObject[activeCallChips.Length];

        for (int index = 0; index < activeCallChips.Length; index++)
        {
            chips[index] = activeCallChips[index] != null
                ? activeCallChips[index].gameObject
                : null;
        }

        return chips;
    }

    private void ClearCallChipState(bool killTween)
    {
        if (callTimeoutCoroutine != null)
        {
            StopCoroutine(callTimeoutCoroutine);
            callTimeoutCoroutine = null;
        }

        if (killTween)
        {
            KillCallMoveTweens();
        }

        callMoveTweens.Clear();
        completedCallMoveCount = 0;
        activeCallChips = null;
        originalChipParents = null;
        originalChipLocalPositions = null;
        originalChipLocalRotations = null;
        originalChipLocalScales = null;
        callPotTargetPositions = null;
        callMoveCompleted = null;
        callMoveFailed = null;
    }

    private void KillCallMoveTweens()
    {
        for (int index = 0; index < callMoveTweens.Count; index++)
        {
            callMoveTweens[index]?.Kill(false);
        }

        callMoveTweens.Clear();
    }

    private void OnDisable()
    {
        FailCallChipMove();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayThink();
        }
    }
}
