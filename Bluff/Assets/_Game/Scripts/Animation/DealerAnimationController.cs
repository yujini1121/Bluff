using UnityEngine;

public class DealerAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;

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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            PlayCall();

        if (Input.GetKeyDown(KeyCode.Alpha2))
            PlayAllIn();

        if (Input.GetKeyDown(KeyCode.Alpha3))
            PlayCollect();
    }
}