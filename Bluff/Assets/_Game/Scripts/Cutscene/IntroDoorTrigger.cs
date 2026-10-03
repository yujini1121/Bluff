using UnityEngine;
using UnityEngine.Playables;

public class IntroDoorTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector doorRevealDirector;

    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerMove playerMove = other.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            playerMove.SetMoveInputEnabled(false);
        }

        if (doorRevealDirector != null)
        {
            doorRevealDirector.Play();
        }

        isTriggered = true;
    }
}