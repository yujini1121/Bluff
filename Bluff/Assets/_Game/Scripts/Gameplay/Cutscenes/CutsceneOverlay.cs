using System.Collections;
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

    public void Show()
    {
        uiRoot.SetActive(true);
        Present(0f, 0f, false, false, 0f);
    }

    public void Present(float darkness, float flashAmount, bool showLastWords, bool showUnlock, float textAlpha)
    {
        shade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(darkness));
        flash.color = new Color(0.78f, 0.94f, 1f, Mathf.Clamp01(flashAmount));
        // Wording, font and layout belong to the scene's TMP UI objects.
        caption.alpha = showLastWords ? Mathf.Clamp01(textAlpha) : 0f;
        unlockCaption.alpha = showUnlock ? 1f : 0f;
    }

    public IEnumerator ClearScreen(float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            Present(1f - Mathf.SmoothStep(0f, 1f, elapsed / seconds), 0f, false, false, 0f);
            yield return null;
        }
    }

    public void Hide()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
    }
}
