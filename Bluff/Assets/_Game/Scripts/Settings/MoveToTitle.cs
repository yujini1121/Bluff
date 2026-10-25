using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToTitle : MonoBehaviour
{
    // Start is called before the first frame update
    public void ToTitle()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}
