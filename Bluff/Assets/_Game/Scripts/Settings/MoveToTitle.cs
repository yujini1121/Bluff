using UnityEngine;

public class MoveToTitle : MonoBehaviour
{
    SoundSystem soundSystem;

    void Awake()
    {
        soundSystem = FindAnyObjectByType<SoundSystem>().GetComponent<SoundSystem>();
    }

    public void ToTitle()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
        soundSystem.StopBGM();
    }
}
