using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleLogoSequence : MonoBehaviour
{
    [Header("Canvas Groups")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private CanvasGroup logo1CanvasGroup;
    [SerializeField] private CanvasGroup logo2CanvasGroup;
    [SerializeField] private CanvasGroup logo3CanvasGroup;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0f)] private float logoFadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float logoHoldDuration = 1f;
    [SerializeField, Min(0f)] private float logoFadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float logoInterval = 0.15f;
    [SerializeField, Min(0f)] private float finalFadeOutDuration = 0.5f;

    private Sequence sequence;

    private void Awake()
    {
        if (overlayCanvasGroup == null || logo1CanvasGroup == null ||
            logo2CanvasGroup == null || logo3CanvasGroup == null)
        {
            Debug.LogError("CanvasGroup 확인", this);
            gameObject.SetActive(false);
            return;
        }

        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = true;
        PrepareLogo(logo1CanvasGroup);
        PrepareLogo(logo2CanvasGroup);
        PrepareLogo(logo3CanvasGroup);
    }

    private void Start()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        sequence = DOTween.Sequence().SetUpdate(true);
        AppendLogo(logo1CanvasGroup);
        sequence.AppendInterval(logoInterval);
        AppendLogo(logo2CanvasGroup);
        sequence.AppendInterval(logoInterval);
        AppendLogo(logo3CanvasGroup);
        sequence.Append(overlayCanvasGroup.DOFade(0f, finalFadeOutDuration)
            .SetEase(Ease.Linear));
        sequence.OnComplete(CompleteSequence);
    }

    private static void PrepareLogo(CanvasGroup logoCanvasGroup)
    {
        logoCanvasGroup.alpha = 0f;
        Image image = logoCanvasGroup.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = image.sprite != null;
        }
    }

    private void AppendLogo(CanvasGroup logoCanvasGroup)
    {
        sequence.Append(logoCanvasGroup.DOFade(1f, logoFadeInDuration)
            .SetEase(Ease.Linear));
        sequence.AppendInterval(logoHoldDuration);
        sequence.Append(logoCanvasGroup.DOFade(0f, logoFadeOutDuration)
            .SetEase(Ease.Linear));
    }

    private void CompleteSequence()
    {
        sequence = null;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        KillSequence();
    }

    private void OnDestroy()
    {
        KillSequence();
    }

    private void KillSequence()
    {
        Sequence activeSequence = sequence;
        sequence = null;
        if (activeSequence != null && activeSequence.IsActive())
        {
            activeSequence.Kill(false);
        }
    }
}
