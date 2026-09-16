using UnityEngine;

public class SetActiveButton : MonoBehaviour
{
    [SerializeField] private GameObject target;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void On()
    {
        target.SetActive(true);
    }

    public void Off()
    {
        target.SetActive(false);
    }
}
