using System;
using System.Collections;
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

    private Transform activeCallChip;
    private Transform originalChipParent;
    private Vector3 originalChipLocalPosition;
    private Quaternion originalChipLocalRotation;
    private Vector3 originalChipLocalScale;
    private Vector3 callPotTargetPosition;
    private Action<GameObject> callMoveCompleted;
    private Action<GameObject> callMoveFailed;
    private Coroutine callTimeoutCoroutine;
    private Tween callMoveTween;

    public bool TryPlayCallChip(
        GameObject chip,
        Vector3 potTargetPosition,
        Action<GameObject> onMoveCompleted,
        Action<GameObject> onMoveFailed)
    {
        if (chip == null ||
            activeCallChip != null ||
            chipSocket == null ||
            !CanSetTrigger(CallTrigger))
        {
            return false;
        }

        activeCallChip = chip.transform;
        originalChipParent = activeCallChip.parent;
        originalChipLocalPosition = activeCallChip.localPosition;
        originalChipLocalRotation = activeCallChip.localRotation;
        originalChipLocalScale = activeCallChip.localScale;
        callPotTargetPosition = potTargetPosition;
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

        if (activeCallChip != null)
        {
            activeCallChip.SetParent(chipSocket, false);
            activeCallChip.localPosition = Vector3.zero;
            activeCallChip.localRotation = Quaternion.identity;
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
        if (activeCallChip != null)
        {
            if (callMoveTween != null)
            {
                return;
            }

            Transform movingChip = activeCallChip;
            movingChip.SetParent(null, true);
            callMoveTween = movingChip
                .DOMove(callPotTargetPosition, CallChipMoveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => CompleteCallChipMove(movingChip));
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

    private void CompleteCallChipMove(Transform completedChip)
    {
        if (completedChip == null || completedChip != activeCallChip)
        {
            return;
        }

        GameObject completedChipObject = completedChip.gameObject;
        Action<GameObject> completedCallback = callMoveCompleted;
        ClearCallChipState(false);
        completedCallback?.Invoke(completedChipObject);
    }

    private void FailCallChipMove()
    {
        if (activeCallChip == null && callMoveFailed == null)
        {
            return;
        }

        GameObject failedChipObject = activeCallChip != null
            ? activeCallChip.gameObject
            : null;
        Action<GameObject> failedCallback = callMoveFailed;
        RestoreCallChip();
        ClearCallChipState(true);
        failedCallback?.Invoke(failedChipObject);
    }

    private void RestoreCallChip()
    {
        if (activeCallChip == null)
        {
            return;
        }

        activeCallChip.SetParent(originalChipParent, false);
        activeCallChip.localPosition = originalChipLocalPosition;
        activeCallChip.localRotation = originalChipLocalRotation;
        activeCallChip.localScale = originalChipLocalScale;
    }

    private void ClearCallChipState(bool killTween)
    {
        if (callTimeoutCoroutine != null)
        {
            StopCoroutine(callTimeoutCoroutine);
            callTimeoutCoroutine = null;
        }

        if (killTween && callMoveTween != null)
        {
            callMoveTween.Kill(false);
        }

        callMoveTween = null;
        activeCallChip = null;
        originalChipParent = null;
        callMoveCompleted = null;
        callMoveFailed = null;
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
