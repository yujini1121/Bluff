using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class DealerAnimationController : MonoBehaviour
{
    private const float BetChipMoveDuration = 0.25f;
    private const float BetAnimationTimeout = 4f;
    private const string CallTrigger = "Call";
    private const string AllInTrigger = "All-In";

    [SerializeField] private Animator animator;
    [SerializeField] private Transform chipSocket;
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
    private bool keepChipWorldY;
    private bool isFollowingChipSocket;
    private Vector3[] betPotTargetPositions;
    private Action<GameObject[]> betMoveCompleted;
    private Action<GameObject[]> betMoveFailed;
    private Coroutine betTimeoutCoroutine;
    private readonly List<Tween> betMoveTweens = new List<Tween>();
    private int completedBetMoveCount;

    public bool TryPlayCallChips(
        GameObject[] chips,
        Vector3[] potTargetPositions,
        Action<GameObject[]> onMoveCompleted,
        Action<GameObject[]> onMoveFailed)
    {
        return TryPlayBetChips(
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
        return TryPlayBetChips(
            AllInTrigger,
            chips,
            potTargetPositions,
            onMoveCompleted,
            onMoveFailed);
    }

    private bool TryPlayBetChips(
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

        if (activeBetChips != null)
        {
            if (isFollowingChipSocket)
            {
                return;
            }

            if (!HasValidActiveBetChips())
            {
                FailBetChipMove();
                return;
            }

            chipOffsetsAtGrab =
                new Vector3[activeBetChips.Length];
            chipWorldRotationsAtGrab =
                new Quaternion[activeBetChips.Length];
            chipWorldYAtGrab = new float[activeBetChips.Length];
            Vector3 offsetOrigin = keepChipWorldY
                ? chipSocket.position
                : GetBetChipCenter();

            for (int index = 0; index < activeBetChips.Length; index++)
            {
                Transform chip = activeBetChips[index];
                chipOffsetsAtGrab[index] =
                    chip.position - offsetOrigin;
                chipWorldRotationsAtGrab[index] = chip.rotation;
                chipWorldYAtGrab[index] = chip.position.y;
            }

            isFollowingChipSocket = true;
            UpdateFollowedBetChips();
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
        if (activeBetChips != null)
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
        if (testChip == null || dealerChipPoint == null)
        {
            return;
        }

        testChip.SetParent(null);
        testChip
            .DOMove(dealerChipPoint.position, BetChipMoveDuration)
            .SetEase(Ease.OutQuad);
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

    private void FailBetChipMove()
    {
        if (activeBetChips == null && betMoveFailed == null)
        {
            return;
        }

        GameObject[] failedChips = GetActiveBetChipObjects();
        Action<GameObject[]> failedCallback = betMoveFailed;
        isFollowingChipSocket = false;
        KillBetMoveTweens();
        RestoreBetChips();
        ClearBetChipState(false);
        failedCallback?.Invoke(failedChips);
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

            chip.SetParent(originalChipParents[index], false);
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
        keepChipWorldY = false;
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

    private void OnDisable()
    {
        FailBetChipMove();
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
        for (int index = 0; index < activeBetChips.Length; index++)
        {
            Transform chip = activeBetChips[index];
            Vector3 followedPosition =
                chipSocket.position + chipOffsetsAtGrab[index];

            if (keepChipWorldY)
            {
                followedPosition.y = chipWorldYAtGrab[index];
            }

            chip.position = followedPosition;
            chip.rotation = chipWorldRotationsAtGrab[index];
        }
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayThink();
        }
    }
}
