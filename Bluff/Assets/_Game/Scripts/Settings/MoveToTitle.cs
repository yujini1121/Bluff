using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToTitle : MonoBehaviour
{
    // Start is called before the first frame update
    public void ToTitle()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}
