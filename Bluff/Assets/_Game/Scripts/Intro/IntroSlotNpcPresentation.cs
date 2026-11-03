using UnityEngine;

public sealed class IntroSlotNpcPresentation : MonoBehaviour
{
    [SerializeField] private IntroFlowController flow;
    [SerializeField] private GameObject actor;

    private void LateUpdate()
    {
        if (!flow || !flow.isActiveAndEnabled || flow.State == IntroFlowController.IntroState.Transition)
        {
            if (actor && actor.activeSelf) actor.SetActive(false);
            return;
        }
        if (flow.State != IntroFlowController.IntroState.BarCutscene && actor && !actor.activeSelf)
            actor.SetActive(true);
    }

    private void OnDisable()
    {
        if (actor) actor.SetActive(false);
    }
}
