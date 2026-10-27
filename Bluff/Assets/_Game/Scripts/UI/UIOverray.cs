using System.Collections.Generic;
using UnityEngine;

public class UIOverray : MonoBehaviour
{
    private List<GameObject> uiPages;

    void Awake()
    {
        uiPages = new List<GameObject>();
    }

    public int UIPageCount
    {
        get { return uiPages.Count; }
    }

    public void AddUI(GameObject ui)
    {
        uiPages.Add(ui);
    }

    public void CloseUI()
    {
        // Close the last opened UI page
        if (uiPages.Count > 0)
        {
            uiPages[UIPageCount - 1].SetActive(false);
            uiPages.RemoveAt(uiPages.Count - 1);
        }
    }

    public void CloseUI(GameObject ui)
    {
        // Close UI page anywhere
        if (uiPages.Contains(ui))
        {
            ui.SetActive(false);
            uiPages.Remove(ui);
        }
    }
}

