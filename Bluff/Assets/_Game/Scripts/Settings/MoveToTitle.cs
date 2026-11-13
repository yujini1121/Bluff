using UnityEngine;

public class MoveToTitle : MonoBehaviour
{
    public void ToTitle()
    {
        Time.timeScale = 1f;
        SoundSystem.Instance.StopBGM();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}
