using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CutsceneOverlay : MonoBehaviour
{
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Image shade;
    [SerializeField] private Image flash;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private TMP_Text unlockCaption;
    public bool IsConfigured => uiRoot != null && shade != null && flash != null &&
        caption != null && unlockCaption != null;

    private void Awake() => Hide();

    public void Show()
    {
        uiRoot.SetActive(true);
    }

    public void Hide()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
    }
}
