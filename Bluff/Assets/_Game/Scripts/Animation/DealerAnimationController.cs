using DG.Tweening;
using UnityEngine;

public class DealerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [SerializeField] private Transform chip;
    [SerializeField] private Transform chipSocket;
    [SerializeField] private Transform potPoint;

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

    // Call Animation에서 
    // GrabChip : 22프레임 (칩을 손에 붙이는 순간)
    // ReleaseChip : 44프레임 (손에서 완전히 분리한 순간)

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


    // 테스트용
    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Alpha1))
    //        PlayCall();

    //    if (Input.GetKeyDown(KeyCode.Alpha2))
    //        PlayAllIn();

    //    if (Input.GetKeyDown(KeyCode.Alpha3))
    //        PlayCollect();
    //}
}