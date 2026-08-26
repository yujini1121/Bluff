using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

public class DealerAnimationController : MonoBehaviour
{
    private const float BetChipMoveDuration = 0.25f;
    private const float BetAnimationTimeout = 4f;
    private const float FoldAnimationTimeout = 4f;
    private const float CheckAnimationTimeout = 4f;
    private const string CallTrigger = "Call";
    private const string AllInTrigger = "All-In";
    private const string CollectTrigger = "Collect";
    private const string FoldTrigger = "Fold";
    private const string CheckTrigger = "Check";
    private const string ThinkTrigger = "Think";
    private const string ThinkLayerName = "UpperBody Layer";
    private const string ThinkIdleState = "UpperBody Layer.Empty";
    private const float ThinkExitDuration = 0.1f;

    [SerializeField] private Animator animator;
    [SerializeField] private Transform chipSocket;
    [SerializeField] private Vector3 allInChipOffset;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;
    [SerializeField] private Transform rightHandIKTarget;
    [SerializeField] private Transform callGrabIKPoint;
    [SerializeField] private Transform callPlaceIKPoint;
    [SerializeField, Min(0f)] private float handIkBlendDuration = 0.1f;
    [SerializeField] private Transform potPoint;
    [SerializeField] private Transform dealerChipPoint;
    [FormerlySerializedAs("chip")]
    [FormerlySerializedAs("collectChip")]
    [SerializeField] private Transform testChip;

    private Transform[] activeBetChips;
    private Transform[] originalChipParents;
    private Vector3[] originalChipLocalPositions;
    private Quaternion[] originalChipLocalRotations;
    private Vector3[] originalChipLocalScales;
    private Vector3[] chipOffsetsAtGrab;
    private Quaternion[] chipWorldRotationsAtGrab;
    private float[] chipWorldYAtGrab;
    private float allInLockedFollowX;
    private bool keepChipWorldY;
    private bool isCallMove;
    private bool isCollectMove;
    private bool isFollowingChipSocket;
    private Vector3[] betPotTargetPositions;
    private Action<GameObject[]> betMoveCompleted;
    private Action<GameObject[]> betMoveFailed;
    private Coroutine betTimeoutCoroutine;
    private Coroutine foldTimeoutCoroutine;
    private Coroutine checkTimeoutCoroutine;
    private readonly List<Tween> betMoveTweens = new List<Tween>();
    private Tween handIkWeightTween;
    private int completedBetMoveCount;
    private Action foldAnimationCompleted;
    private Action foldAnimationFailed;
    private Action checkAnimationCompleted;
    private Action checkAnimationFailed;
    private bool isApplicationQuitting;

    public bool TryPlayCallChips(
        GameObject[] chips,
        Vector3[] potTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        return TryPlayChipMove(
            CallTrigger,
            chips,
            potTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool TryPlayAllInChips(
        GameObject[] chips,
        Vector3[] potTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        return TryPlayChipMove(
            AllInTrigger,
            chips,
            potTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool TryPlayCollectChips(
        GameObject[] chips,
        Vector3[] dealerTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        return TryPlayChipMove(
            CollectTrigger,
            chips,
            dealerTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    public bool TryPlayFold(
        Action onCompleted,
        Action onFailed)
    {
        if (onCompleted == null ||
            onFailed == null ||
            activeBetChips != null ||
            foldAnimationCompleted != null ||
            foldAnimationFailed != null ||
            !CanSetTrigger(FoldTrigger))
        {
            return false;
        }

        foldAnimationCompleted = onCompleted;
        foldAnimationFailed = onFailed;
        animator.SetTrigger(FoldTrigger);
        foldTimeoutCoroutine =
            StartCoroutine(FoldAnimationTimeoutRoutine());
        return true;
    }

    public bool TryPlayCheck(
        Action onCompleted,
        Action onFailed)
    {
        if (onCompleted == null ||
            onFailed == null ||
            activeBetChips != null ||
            checkAnimationCompleted != null ||
            checkAnimationFailed != null ||
            !CanSetTrigger(CheckTrigger))
        {
            return false;
        }

        checkAnimationCompleted = onCompleted;
        checkAnimationFailed = onFailed;
        animator.SetTrigger(CheckTrigger);
        checkTimeoutCoroutine =
            StartCoroutine(CheckAnimationTimeoutRoutine());
        return true;
    }

    private bool TryPlayChipMove(
        string triggerName,
        GameObject[] chips,
        Vector3[] potTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        if (chips == null ||
            chips.Length == 0 ||
            potTargetPositions == null ||
            chips.Length != potTargetPositions.Length ||
            activeBetChips != null ||
            chipSocket == null ||
            !CanSetTrigger(triggerName))
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

        activeBetChips = new Transform[chips.Length];
        originalChipParents = new Transform[chips.Length];
        originalChipLocalPositions = new Vector3[chips.Length];
        originalChipLocalRotations = new Quaternion[chips.Length];
        originalChipLocalScales = new Vector3[chips.Length];
        betPotTargetPositions = new Vector3[potTargetPositions.Length];

        for (int index = 0; index < chips.Length; index++)
        {
            Transform chip = chips[index].transform;
            activeBetChips[index] = chip;
            originalChipParents[index] = chip.parent;
            originalChipLocalPositions[index] = chip.localPosition;
            originalChipLocalRotations[index] = chip.localRotation;
            originalChipLocalScales[index] = chip.localScale;
            betPotTargetPositions[index] = potTargetPositions[index];
        }

        betMoveCompleted = onMoveCompleted;
        betMoveFailed = onMoveFailed;
        keepChipWorldY = triggerName == AllInTrigger;
        isCallMove = triggerName == CallTrigger;
        isCollectMove = triggerName == CollectTrigger;

        if (isCallMove)
        {
            ResetCallHandIK();
        }

        animator.SetTrigger(triggerName);
        betTimeoutCoroutine = StartCoroutine(BetAnimationTimeoutRoutine());
        return true;
    }

    public void PlayCall()
    {
        TrySetTrigger(CallTrigger);
    }

    public void PlayAllIn()
    {
        TrySetTrigger(AllInTrigger);
    }

    public void PlayCollect()
    {
        TrySetTrigger(CollectTrigger);
    }

    public void PlayThink()
    {
        TryPlayThink();
    }

    public bool TryPlayThink()
    {
        return TrySetTrigger(ThinkTrigger);
    }

    public void StopThink()
    {
        if (!isActiveAndEnabled ||
            animator == null ||
            !animator.enabled ||
            !animator.gameObject.activeInHierarchy ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.ResetTrigger(ThinkTrigger);
        int thinkLayerIndex = animator.GetLayerIndex(ThinkLayerName);
        int idleStateHash = Animator.StringToHash(ThinkIdleState);

        if (thinkLayerIndex < 0 ||
            !animator.HasState(thinkLayerIndex, idleStateHash))
        {
            return;
        }

        animator.CrossFade(
            idleStateHash,
            ThinkExitDuration,
            thinkLayerIndex);
    }

    public void BeginCallGrabIK()
    {
        BeginCallHandIK(callGrabIKPoint);
    }

    public void BeginCallPlaceIK()
    {
        BeginCallHandIK(callPlaceIKPoint);
    }

    public void GrabChip()
    {
        if (activeBetChips != null)
        {
            if (isCollectMove)
            {
                return;
            }

            if (isFollowingChipSocket)
            {
                return;
            }

            if (!HasValidActiveBetChips())
            {
                FailBetChipMove();
                return;
            }

            if (chipSocket == null)
            {
                FailBetChipMove();
                return;
            }

            chipOffsetsAtGrab =
                new Vector3[activeBetChips.Length];
            chipWorldRotationsAtGrab =
                new Quaternion[activeBetChips.Length];
            chipWorldYAtGrab = new float[activeBetChips.Length];
            Vector3 offsetOrigin = GetBetChipCenter();

            for (int index = 0; index < activeBetChips.Length; index++)
            {
                Transform chip = activeBetChips[index];
                chipOffsetsAtGrab[index] =
                    chip.position - offsetOrigin;
                chipWorldRotationsAtGrab[index] = chip.rotation;
                chipWorldYAtGrab[index] = chip.position.y;
            }

            if (keepChipWorldY)
            {
                allInLockedFollowX =
                    GetAllInFollowPosition().x;
            }

            isFollowingChipSocket = true;
            UpdateFollowedBetChips();

            if (isCallMove)
            {
                BlendCallHandIK(0f);
            }

            return;
        }

        if (testChip != null && chipSocket != null)
        {
            testChip.SetParent(chipSocket);
            testChip.localPosition = Vector3.zero;
            testChip.localRotation = Quaternion.identity;
        }
    }

    public void ReleaseChip()
    {
        if (activeBetChips != null)
        {
            if (isCollectMove)
            {
                return;
            }

            StartChipMove();

            if (isCallMove)
            {
                BlendCallHandIK(0f);
            }

            return;
        }

        if (testChip == null || potPoint == null)
        {
            return;
        }

        testChip.SetParent(null);
        testChip
            .DOMove(potPoint.position, BetChipMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    public void ReleaseCollectChip()
    {
        if (activeBetChips != null)
        {
            if (!isCollectMove)
            {
                return;
            }

            StartChipMove();
            return;
        }

        if (testChip == null || dealerChipPoint == null)
        {
            return;
        }

        testChip.SetParent(null);
        testChip
            .DOMove(dealerChipPoint.position, BetChipMoveDuration)
            .SetEase(Ease.OutQuad);
    }

    public void CompleteFoldAnimation()
    {
        if (foldAnimationCompleted == null &&
            foldAnimationFailed == null)
        {
            return;
        }

        Action completedCallback = foldAnimationCompleted;
        ClearFoldAnimationState();
        completedCallback?.Invoke();
    }

    public void CompleteCheckAnimation()
    {
        if (checkAnimationCompleted == null &&
            checkAnimationFailed == null)
        {
            return;
        }

        Action completedCallback = checkAnimationCompleted;
        ClearCheckAnimationState();
        completedCallback?.Invoke();
    }

    private void StartChipMove()
    {
        isFollowingChipSocket = false;

        if (betMoveTweens.Count > 0)
        {
            return;
        }

        if (!HasValidActiveBetChips())
        {
            FailBetChipMove();
            return;
        }

        completedBetMoveCount = 0;

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            Transform movingChip = activeBetChips[index];
            movingChip.SetParent(null, true);
            Tween moveTween = movingChip
                .DOMove(
                    betPotTargetPositions[index],
                    BetChipMoveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(CompleteBetChipMove);
            betMoveTweens.Add(moveTween);
        }
    }

    public void CancelBetChipAnimation()
    {
        FailBetChipMove();
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

    private IEnumerator BetAnimationTimeoutRoutine()
    {
        yield return new WaitForSeconds(BetAnimationTimeout);
        betTimeoutCoroutine = null;
        FailBetChipMove();
    }

    private IEnumerator FoldAnimationTimeoutRoutine()
    {
        yield return new WaitForSeconds(FoldAnimationTimeout);
        foldTimeoutCoroutine = null;
        FailFoldAnimation();
    }

    private IEnumerator CheckAnimationTimeoutRoutine()
    {
        yield return new WaitForSeconds(CheckAnimationTimeout);
        checkTimeoutCoroutine = null;
        FailCheckAnimation();
    }

    private void CompleteBetChipMove()
    {
        if (activeBetChips == null)
        {
            return;
        }

        completedBetMoveCount++;

        if (completedBetMoveCount < activeBetChips.Length)
        {
            return;
        }

        GameObject[] completedChips = GetActiveBetChipObjects();
        Action<GameObject[]> completedCallback = betMoveCompleted;
        ClearBetChipState(false);
        completedCallback?.Invoke(completedChips);
    }

    private void FailBetChipMove(bool notifyFailure = true)
    {
        if (activeBetChips == null && betMoveFailed == null)
        {
            return;
        }

        GameObject[] failedChips = notifyFailure
            ? GetActiveBetChipObjects()
            : null;
        Action<GameObject[]> failedCallback = notifyFailure
            ? betMoveFailed
            : null;
        isFollowingChipSocket = false;
        KillBetMoveTweens();
        RestoreBetChips();
        ClearBetChipState(false);
        failedCallback?.Invoke(failedChips);
    }

    private void FailFoldAnimation(bool notifyFailure = true)
    {
        if (foldAnimationCompleted == null &&
            foldAnimationFailed == null)
        {
            return;
        }

        Action failedCallback = notifyFailure
            ? foldAnimationFailed
            : null;
        ClearFoldAnimationState();
        failedCallback?.Invoke();
    }

    private void FailCheckAnimation(bool notifyFailure = true)
    {
        if (checkAnimationCompleted == null &&
            checkAnimationFailed == null)
        {
            return;
        }

        Action failedCallback = notifyFailure
            ? checkAnimationFailed
            : null;
        ClearCheckAnimationState();
        failedCallback?.Invoke();
    }

    private bool HasValidActiveBetChips()
    {
        if (activeBetChips == null || activeBetChips.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            if (activeBetChips[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreBetChips()
    {
        if (activeBetChips == null)
        {
            return;
        }

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            Transform chip = activeBetChips[index];

            if (chip == null)
            {
                continue;
            }

            Transform originalParent = originalChipParents[index];
            chip.SetParent(
                originalParent != null ? originalParent : null,
                false);
            chip.localPosition = originalChipLocalPositions[index];
            chip.localRotation = originalChipLocalRotations[index];
            chip.localScale = originalChipLocalScales[index];
        }
    }

    private GameObject[] GetActiveBetChipObjects()
    {
        if (activeBetChips == null)
        {
            return null;
        }

        GameObject[] chips = new GameObject[activeBetChips.Length];

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            chips[index] = activeBetChips[index] != null
                ? activeBetChips[index].gameObject
                : null;
        }

        return chips;
    }

    private void ClearBetChipState(bool killTween)
    {
        if (betTimeoutCoroutine != null)
        {
            StopCoroutine(betTimeoutCoroutine);
            betTimeoutCoroutine = null;
        }

        if (killTween)
        {
            KillBetMoveTweens();
        }

        betMoveTweens.Clear();
        completedBetMoveCount = 0;
        activeBetChips = null;
        originalChipParents = null;
        originalChipLocalPositions = null;
        originalChipLocalRotations = null;
        originalChipLocalScales = null;
        chipOffsetsAtGrab = null;
        chipWorldRotationsAtGrab = null;
        chipWorldYAtGrab = null;
        allInLockedFollowX = 0f;
        keepChipWorldY = false;

        if (isCallMove)
        {
            ResetCallHandIK();
        }

        isCallMove = false;
        isCollectMove = false;
        isFollowingChipSocket = false;
        betPotTargetPositions = null;
        betMoveCompleted = null;
        betMoveFailed = null;
    }

    private void KillBetMoveTweens()
    {
        for (int index = 0; index < betMoveTweens.Count; index++)
        {
            betMoveTweens[index]?.Kill(false);
        }

        betMoveTweens.Clear();
    }

    private void ClearFoldAnimationState()
    {
        if (foldTimeoutCoroutine != null)
        {
            StopCoroutine(foldTimeoutCoroutine);
            foldTimeoutCoroutine = null;
        }

        foldAnimationCompleted = null;
        foldAnimationFailed = null;
    }

    private void ClearCheckAnimationState()
    {
        if (checkTimeoutCoroutine != null)
        {
            StopCoroutine(checkTimeoutCoroutine);
            checkTimeoutCoroutine = null;
        }

        checkAnimationCompleted = null;
        checkAnimationFailed = null;
    }

    private void OnDisable()
    {
        bool notifyFailure =
            !isApplicationQuitting &&
            Application.isPlaying &&
            gameObject.scene.IsValid() &&
            gameObject.scene.isLoaded;
        FailBetChipMove(notifyFailure);
        FailFoldAnimation(notifyFailure);
        FailCheckAnimation(notifyFailure);
        ResetCallHandIK();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void LateUpdate()
    {
        if (!isFollowingChipSocket)
        {
            return;
        }

        if (chipSocket == null ||
            !HasValidActiveBetChips() ||
            chipOffsetsAtGrab == null ||
            chipWorldRotationsAtGrab == null ||
            chipWorldYAtGrab == null)
        {
            FailBetChipMove();
            return;
        }

        UpdateFollowedBetChips();
    }

    private void UpdateFollowedBetChips()
    {
        Vector3 allInFollowPosition = keepChipWorldY
            ? GetAllInFollowPosition()
            : chipSocket.position;

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            Transform chip = activeBetChips[index];
            Vector3 followedPosition =
                allInFollowPosition +
                chipOffsetsAtGrab[index];

            if (keepChipWorldY)
            {
                followedPosition.x =
                    allInLockedFollowX +
                    chipOffsetsAtGrab[index].x;
                followedPosition.y = chipWorldYAtGrab[index];
            }

            chip.position = followedPosition;
            chip.rotation = chipWorldRotationsAtGrab[index];
        }
    }

    private Vector3 GetAllInFollowPosition()
    {
        Vector3 localOffset = new Vector3(
            allInChipOffset.x,
            0f,
            allInChipOffset.z);
        return chipSocket.TransformPoint(localOffset);
    }

    private Vector3 GetBetChipCenter()
    {
        Vector3 center = Vector3.zero;

        for (int index = 0; index < activeBetChips.Length; index++)
        {
            center += activeBetChips[index].position;
        }

        return center / activeBetChips.Length;
    }

    private void BeginCallHandIK(Transform targetPoint)
    {
        if (!isCallMove ||
            rightArmIK == null ||
            rightHandIKTarget == null ||
            targetPoint == null)
        {
            return;
        }

        KillCallHandIKTween();
        rightHandIKTarget.SetPositionAndRotation(
            targetPoint.position,
            targetPoint.rotation);
        BlendCallHandIK(1f);
    }

    private void BlendCallHandIK(float targetWeight)
    {
        KillCallHandIKTween();

        if (rightArmIK == null)
        {
            return;
        }

        float duration = Mathf.Max(0f, handIkBlendDuration);

        if (duration <= 0f)
        {
            rightArmIK.weight = targetWeight;
            return;
        }

        handIkWeightTween = DOTween
            .To(
                () => rightArmIK != null
                    ? rightArmIK.weight
                    : 0f,
                value =>
                {
                    if (rightArmIK != null)
                    {
                        rightArmIK.weight = value;
                    }
                },
                targetWeight,
                duration)
            .SetEase(Ease.OutQuad);
    }

    private void ResetCallHandIK()
    {
        KillCallHandIKTween();

        if (rightArmIK != null)
        {
            rightArmIK.weight = 0f;
        }
    }

    private void KillCallHandIKTween()
    {
        if (handIkWeightTween == null)
        {
            return;
        }

        handIkWeightTween.Kill(false);
        handIkWeightTween = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayThink();
        }
    }
}
