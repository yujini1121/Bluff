using DG.Tweening;
using UnityEngine;

public class DealerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [SerializeField] private Transform chip;
    [SerializeField] private Transform chipSocket;
    [SerializeField] private Transform potPoint;
    [SerializeField] private Transform dealerChipPoint;

    // Call Animation에서 
    // Grab : 22프레임 (칩을 손에 붙이는 순간)
    // Release : 44프레임 (손에서 완전히 분리한 순간)

    // All-In Animation에서
    // Grab : 20프레임
    // Release : 50프레임

    // Collect Animation에서
    // Grab : 12프레임
    // Release : 29프레임

    public void PlayCall()
    {
        animator.SetTrigger("Call");
    }

    public void PlayAllIn()
    {
        animator.SetTrigger("All-In");
    }

    public void PlayCollect()
    {
        animator.SetTrigger("Collect");
    }

    public void PlayThink()
    {
        animator.SetTrigger("Think");
    }

    public void GrabChip()
    {
        chip.SetParent(chipSocket);

        chip.localPosition = Vector3.zero;
        chip.localRotation = Quaternion.identity;

        Debug.Log("Chip Grab!!!");
    }

    public void ReleaseChip()
    {
        chip.SetParent(null);

        chip
            .DOMove(potPoint.position, 0.25f)
            .SetEase(Ease.OutQuad);

        Debug.Log("Chip Release!!!");
    }

    public void ReleaseCollectChip()
    {
        chip.SetParent(null);

        chip
            .DOMove(dealerChipPoint.position, 0.25f)
            .SetEase(Ease.OutQuad);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayThink();
        }
    }
}