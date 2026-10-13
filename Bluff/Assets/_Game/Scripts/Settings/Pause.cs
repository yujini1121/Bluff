using UnityEngine;

public class Pause : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    private bool isPaused = false;

    void Start()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
    }

    public void PauseGame()
    {
        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);
        Time.timeScale = isPaused ? 0 : 1;
    }

    public bool IsPaused
    {
        get { return isPaused; }
    }
}
