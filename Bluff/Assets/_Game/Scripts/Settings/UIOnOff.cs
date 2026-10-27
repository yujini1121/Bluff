using UnityEngine;

public class UiOnOff : MonoBehaviour
{
    private UIOverray uiOverray;
    private Pause pause;
    [SerializeField] private GameObject target;

    void Awake()
    {
        if (uiOverray == null)
        {
            uiOverray = FindObjectOfType<UIOverray>();
        }
        if (pause == null)
        {
            pause = FindObjectOfType<Pause>();
        }
    }

    public void OnOff()
    {
        target.SetActive(!target.activeSelf);

        if(target.activeSelf)
        {
            uiOverray.AddUI(target);
        }
        else
        {
            uiOverray.CloseUI(target);
        }
    }

    public void Pause()
    {
        pause.PauseGame();
    }
}
