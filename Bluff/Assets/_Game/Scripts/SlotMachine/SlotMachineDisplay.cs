using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SlotMachineDisplay : MonoBehaviour
{
    [SerializeField] private Image logoImage, resultImage, decorationImage;
    [SerializeField] private TMP_Text logoFallback, resultText, decorationFallback;
    [SerializeField] private CanvasGroup signGroup, decorationGroup;
    [SerializeField] private RectTransform ledSweep;
    private Sequence effects;
    private SlotMachineSprites sprites;
    private Vector2 sweepOrigin;
    public bool HasReferences => logoImage && resultImage && decorationImage && logoFallback && resultText &&
        decorationFallback && signGroup && decorationGroup && ledSweep;

    public void Configure(Image logo, TMP_Text logoText, CanvasGroup sign, Image result, TMP_Text status,
        Image decoration, TMP_Text decorationText, CanvasGroup auxiliary, RectTransform sweep)
    {
        logoImage = logo; logoFallback = logoText; signGroup = sign;
        resultImage = result; resultText = status;
        decorationImage = decoration; decorationFallback = decorationText;
        decorationGroup = auxiliary; ledSweep = sweep;
    }

    public void Initialize(SlotMachineSprites bank)
    {
        sprites = bank; sweepOrigin = ledSweep.anchoredPosition;
        logoImage.sprite = bank.logo; logoImage.enabled = bank.logo;
        logoFallback.enabled = !bank.logo;
        decorationImage.sprite = bank.decoration; decorationImage.enabled = bank.decoration;
        decorationFallback.enabled = !bank.decoration;
        ShowIdle();
    }

    public void ShowIdle() { StopEffects(); Status(sprites.idle, "READY", new Color(0.7f, 0.87f, 1)); }

    public void ShowSpinning(bool unscaled)
    {
        StopEffects(); Status(null, "SPINNING", new Color(1, 0.45f, 0.86f));
        effects = DOTween.Sequence().SetUpdate(unscaled);
        effects.Append(signGroup.DOFade(0.6f, 0.2f));
        effects.Join(decorationGroup.DOFade(0.55f, 0.2f));
        effects.AppendInterval(0.2f);
        effects.Append(signGroup.DOFade(1, 0.2f));
        effects.Join(decorationGroup.DOFade(1, 0.2f));
        effects.AppendInterval(0.4f);
        effects.SetLoops(-1, LoopType.Yoyo);
    }

    public void ShowResult(SlotSpinResult result, bool unscaled)
    {
        StopEffects();
        bool jackpot = result.Outcome == SlotOutcome.Jackpot;
        Status(result.IsWin ? sprites.win : sprites.lose, jackpot ? "JACKPOT" : result.IsWin ? "WIN" : "TRY AGAIN",
            result.IsWin ? new Color(1, 0.86f, 0.4f) : new Color(0.77f, 0.72f, 0.9f));
        // A jackpot remains readable even when only win.png (and no jackpot artwork) is supplied.
        if (jackpot) { resultImage.enabled = false; resultText.enabled = true; }
        effects = DOTween.Sequence().SetUpdate(unscaled);
        effects.Append(signGroup.DOFade(result.IsWin ? 0.45f : 0.85f, 0.15f));
        effects.Join(decorationGroup.DOFade(result.IsWin ? 0.45f : 0.85f, 0.15f));
        effects.Append(signGroup.DOFade(1, 0.15f));
        effects.Join(decorationGroup.DOFade(1, 0.15f));
        effects.SetLoops(result.IsWin ? 4 : 1);
    }

    private void Status(Sprite sprite, string label, Color color)
    {
        resultImage.sprite = sprite; resultImage.enabled = sprite;
        resultText.text = label; resultText.color = color; resultText.enabled = !sprite;
    }

    public void StopEffects()
    {
        effects?.Kill(false); effects = null;
        if (signGroup) signGroup.alpha = 1;
        if (decorationGroup) decorationGroup.alpha = 1;
        if (ledSweep) ledSweep.anchoredPosition = sweepOrigin;
    }
    private void OnDisable() => StopEffects();
    private void OnDestroy() => StopEffects();
}
