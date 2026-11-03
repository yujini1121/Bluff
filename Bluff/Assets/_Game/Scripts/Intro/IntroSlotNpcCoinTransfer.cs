using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroSlotNpcCoinTransfer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform sourceCoin;
    [SerializeField] private Transform coinSlot;
    [SerializeField, Min(.1f)] private float travelDuration = .45f;
    [SerializeField, Min(.1f)] private float prepareDuration = .45f;
    [SerializeField, Min(.05f)] private float presentDuration = .22f;
    [SerializeField, Min(.05f)] private float recoverDuration = .2f;
    private Transform movingCoin, hips;
    private Vector3 start, controlA, controlB;
    private Quaternion releaseRotation;
    private float elapsed;
    private bool released;
    public bool IsConfigured => animator && sourceCoin && coinSlot;
    public bool IsTravelling { get; private set; }
    public bool HasArrived { get; private set; }
    private float ReleaseTime => Mathf.Max(.1f, prepareDuration) + Mathf.Max(.05f, presentDuration);
    private float FlightTime => Mathf.Max(.1f, travelDuration);

    public bool BeginTransfer()
    {
        CancelTransfer();
        if (!isActiveAndEnabled || !IsConfigured) return false;
        if (!movingCoin)
        {
            movingCoin = Instantiate(sourceCoin, transform);
            movingCoin.name = "Intro Dealer Travelling Coin";
        }
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Vector3 scale = sourceCoin.lossyScale;
        Vector3 parentScale = transform.lossyScale;
        movingCoin.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        movingCoin.gameObject.SetActive(false);
        IsTravelling = true;
        return true;
    }

    public void ApplyHandAndLookIK()
    {
        if (!IsTravelling || !hips || !coinSlot) return;
        float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.1f, prepareDuration)));
        float offer = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - prepareDuration) / Mathf.Max(.05f, presentDuration)));
        float release = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - ReleaseTime) / FlightTime));
        Vector3 pocket = hips.position + transform.right * .22f + transform.forward * .13f + Vector3.up * .02f;
        Vector3 hand = pocket + transform.forward * (.1f * offer) + Vector3.up * (.035f * offer);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reach * (1 - release));
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, reach * (1 - release) * .7f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, hand);
        animator.SetIKRotation(AvatarIKGoal.RightHand, animator.GetIKRotation(AvatarIKGoal.RightHand));
        float recover = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - ReleaseTime - FlightTime) / Mathf.Max(.05f, recoverDuration)));
        animator.SetLookAtWeight(.55f * reach * recover, .08f, .7f, .5f, .65f);
        animator.SetLookAtPosition(Vector3.Lerp(hand, coinSlot.position, Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - prepareDuration) / (presentDuration + FlightTime)))));
    }

    private void LateUpdate()
    {
        if (!IsTravelling) return;
        if (!movingCoin || !coinSlot || !sourceCoin) { CancelTransfer(); return; }
        elapsed += Time.deltaTime;
        if (elapsed < ReleaseTime)
        {
            movingCoin.SetPositionAndRotation(sourceCoin.position, sourceCoin.rotation);
            movingCoin.gameObject.SetActive(elapsed >= prepareDuration * .85f);
            return;
        }
        if (!released)
        {
            released = true;
            start = sourceCoin.position;
            releaseRotation = sourceCoin.rotation;
            controlA = start + transform.forward * .08f + Vector3.down * .12f;
            controlB = coinSlot.position - transform.forward * .28f + Vector3.down * .025f;
        }
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - ReleaseTime) / FlightTime));
        float inverse = 1 - t;
        movingCoin.position = inverse * inverse * inverse * start + 3 * inverse * inverse * t * controlA +
            3 * inverse * t * t * controlB + t * t * t * coinSlot.position;
        movingCoin.rotation = Quaternion.Slerp(releaseRotation, coinSlot.rotation * Quaternion.Euler(0, 0, 90), t);
        movingCoin.gameObject.SetActive(t < 1);
        if (elapsed < ReleaseTime + FlightTime + Mathf.Max(.05f, recoverDuration)) return;
        IsTravelling = false;
        HasArrived = true;
    }

    public void CancelTransfer()
    {
        IsTravelling = HasArrived = released = false;
        elapsed = 0;
        if (movingCoin) movingCoin.gameObject.SetActive(false);
    }

    private void OnDisable() => CancelTransfer();
    private void OnDestroy() { if (movingCoin) Destroy(movingCoin.gameObject); }
}
