using System;
using DG.Tweening;
using UnityEngine;

public sealed class RadialBettingAnimator : MonoBehaviour
{
    [SerializeField] private CanvasGroup actions;
    [SerializeField] private CanvasGroup raise;
    [SerializeField] private RectTransform rotor;
    [SerializeField, Min(.1f)] private float transitionDuration = .23f;
    [SerializeField] private float raiseRotation = -5f;
    [SerializeField] private bool pulseRotor = true;
    private Sequence transition;
    public bool IsTransitioning => transition != null && transition.IsActive();

    public void Show(bool showRaise, Action completed)
    {
        Kill();
        SetInput(false, false);
        var outgoing = showRaise ? actions : raise;
        var incoming = showRaise ? raise : actions;
        transition = DOTween.Sequence().SetUpdate(true);
        float fadeOut = transitionDuration * .39f;
        float fadeIn = transitionDuration * .61f;
        transition.Append(outgoing.DOFade(0, fadeOut));
        if (pulseRotor) transition.Join(rotor.DOScale(.975f, fadeOut).SetEase(Ease.OutQuad));
        transition.Append(incoming.DOFade(1, fadeIn));
        if (pulseRotor) transition.Join(rotor.DOScale(1, fadeIn).SetEase(Ease.OutQuad));
        transition.Insert(0, rotor.DOLocalRotate(new Vector3(0, 0, showRaise ? raiseRotation : 0), transitionDuration)
            .SetEase(Ease.InOutSine));
        transition.OnComplete(() => { transition = null; completed?.Invoke(); });
    }

    public void SetInput(bool actionInput, bool raiseInput)
    {
        actions.interactable = actions.blocksRaycasts = actionInput;
        raise.interactable = raise.blocksRaycasts = raiseInput;
    }
    public void ResetState()
    {
        Kill();
        if (actions == null || raise == null || rotor == null) return;
        actions.alpha = 1;
        raise.alpha = 0;
        rotor.localRotation = Quaternion.identity;
        rotor.localScale = Vector3.one;
        SetInput(false, false);
    }
    private void Kill()
    {
        transition?.Kill(false);
        transition = null;
    }
    private void OnDisable() => ResetState();
    private void OnDestroy() => Kill();
}
