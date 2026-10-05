using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public sealed class IntroFadeController : MonoBehaviour
{
    [SerializeField] private CanvasGroup overlay;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.8f;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.8f;
    public bool IsTransitioning { get; private set; }
    private float previousTimeScale;
    private bool ownsTimeScale;

    private void Awake()
    {
        if (overlay == null) overlay = GetComponent<CanvasGroup>();
        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
    }

    public void TransitionToGameplay()
    {
        if (IsTransitioning) return;
        if (!Application.CanStreamedLevelBeLoaded(IntroFlowController.GameplaySceneName))
        {
            Debug.LogError("Intro: enabled Gameplay Scene is missing from Build Settings.", this);
            return;
        }
        IsTransitioning = true;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Transition());
    }

    private IEnumerator Transition()
    {
        overlay.blocksRaycasts = true;
        yield return FadeTo(1f, fadeOutSeconds);
        previousTimeScale = Time.timeScale;
        ownsTimeScale = true;
        Time.timeScale = 0f;
        AsyncOperation load = SceneManager.LoadSceneAsync(IntroFlowController.GameplaySceneName);
        while (!load.isDone) yield return null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return null;
        yield return null;
        yield return FadeTo(0f, fadeInSeconds);
        RestoreTimeScale();
        overlay.blocksRaycasts = false;
        Destroy(gameObject);
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        float start = overlay.alpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.alpha = target;
    }

    private void OnDestroy() => RestoreTimeScale();
    private void RestoreTimeScale()
    {
        if (!ownsTimeScale) return;
        Time.timeScale = previousTimeScale;
        ownsTimeScale = false;
    }
}
