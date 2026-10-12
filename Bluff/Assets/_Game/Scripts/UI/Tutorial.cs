using UnityEngine;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    [SerializeField] private GameObject[] tutorialSteps;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    private int currentIndex = 0;

    void Start()
    {
        currentIndex = 0;
    }

    public void PrevStep()
    {
        CloseStep(currentIndex--);
        OpenStep(currentIndex);
    }

    public void NextStep()
    {
        CloseStep(currentIndex++);
        OpenStep(currentIndex);
    }

    void OpenStep(int index)
    {
        tutorialSteps[index].SetActive(true);
    }

    void CloseStep(int index)
    {
        tutorialSteps[index].SetActive(false);
    }

    void Update()
    {
        prevButton.interactable = currentIndex > 0;
        nextButton.interactable = currentIndex < tutorialSteps.Length - 1;
    }
}
