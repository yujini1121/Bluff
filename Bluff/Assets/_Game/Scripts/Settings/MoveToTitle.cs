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
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
        soundSystem.StopBGM();
    }
}
