using UnityEngine;
using UnityEngine.Playables;

public class IntroDoorTrigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector doorRevealDirector;
    [SerializeField] private Transform insidePoint;

    private bool doorOpened = false;
    private bool cutsceneStarted = false;

    private void OnTriggerEnter(Collider other)
    {
        if (doorOpened)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (doorRevealDirector != null)
        {
            doorRevealDirector.Play();
        }

        doorOpened = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (cutsceneStarted)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (insidePoint == null)
        {
            return;
        }

        Vector3 exitDirection = other.transform.position - transform.position;
        Vector3 insideDirection = insidePoint.position - transform.position;

        if (Vector3.Dot(exitDirection, insideDirection) <= 0f)
        {
            return;
        }

        PlayerMove playerMove = other.GetComponent<PlayerMove>();

        if (playerMove != null)
        {
            playerMove.SetMoveInputEnabled(false);
        }

        cutsceneStarted = true;
    }
}