using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RadialBettingView : MonoBehaviour
{
    [SerializeField] private RadialBettingAnimator animator;
    [SerializeField] private BettingArcSlider slider;
    [SerializeField] private Button callButton, raiseButton, foldButton, confirmButton, backButton, maxButton;
    [SerializeField] private TMP_Text callText, amountText, totalText, rangeText, confirmText, statusText;
    [SerializeField] private TMP_Text[] callTextStates, confirmTextStates;
    [SerializeField] private bool useLegacyAmountColor = true;
    private GameplayController controller;
    private bool allowed, canRaise, raising, submitted;

    public bool IsChoosingRaise => raising;

    public void Bind(GameplayController owner)
    {
        Unbind();
        controller = owner;
        callButton.onClick.AddListener(Call);
        foldButton.onClick.AddListener(Fold);
        raiseButton.onClick.AddListener(OpenRaise);
        confirmButton.onClick.AddListener(Confirm);
        backButton.onClick.AddListener(Back);
        maxButton.onClick.AddListener(Max);
        slider.ValueChanged += SelectAmount;
        ResetState();
    }

    public void Refresh(bool playerTurn, bool acceptInput, bool canCall, bool shortAllIn,
        int selected, int maximum, int callCost)
    {
        gameObject.SetActive(playerTurn);
        allowed = playerTurn && acceptInput && Time.timeScale > 0 && controller != null && controller.isActiveAndEnabled;
        canRaise = allowed && maximum > 0;
        if (!allowed || (raising && !canRaise)) ResetState();
        callButton.interactable = allowed && canCall;
        foldButton.interactable = allowed;
        raiseButton.interactable = canRaise;
        confirmButton.interactable = canRaise;
        maxButton.interactable = canRaise && selected < maximum;
        backButton.interactable = allowed;
        slider.interactable = canRaise;
        SetButtonText(callText, callTextStates,
            (shortAllIn ? "ALL IN" : "CALL") + "\n<size=14>" + callCost + " CHIPS</size>");
        string amount = "+" + (maximum > 0 ? selected : 0);
        amountText.text = "RAISE " + (useLegacyAmountColor ? "<color=#FF80CF>" + amount + "</color>" : amount);
        totalText.text = "TOTAL  " + (maximum > 0 ? (long)callCost + selected : callCost) + "  CHIPS";
        if (rangeText != null) rangeText.text = maximum > 0 ? "MIN 1    /    MAX " + maximum : "RAISE UNAVAILABLE";
        SetButtonText(confirmText, confirmTextStates,
            maximum > 0 && selected == maximum ? "CONFIRM ALL IN" : "CONFIRM RAISE");
        if (statusText != null) statusText.text = !allowed ? "PLEASE WAIT" : maximum <= 0 ? "CALL OR FOLD" : "YOUR MOVE";
        slider.SetRangeAndValue(1, Mathf.Max(1, maximum), selected);
        UpdateInput();
    }

    // Shift's normal / highlighted / pressed groups each own a separate label.
    private static void SetButtonText(TMP_Text primary, TMP_Text[] states, string text)
    {
        if (primary != null) primary.text = text;
        if (states == null) return;
        foreach (var label in states) if (label != null) label.text = text;
    }

    public void OpenRaise()
    {
        if (!CanAct() || !canRaise || raising) return;
        raising = true;
        animator.Show(true, UpdateInput);
    }
    public void Back()
    {
        if (!raising) return;
        raising = false;
        submitted = false;
        animator.Show(false, UpdateInput);
    }
    public void ResetState()
    {
        raising = false;
        submitted = false;
        if (animator != null) animator.ResetState();
    }
    private bool CanAct() => allowed && !submitted && Time.timeScale > 0 &&
        controller != null && controller.isActiveAndEnabled && !animator.IsTransitioning;
    private void UpdateInput() => animator.SetInput(
        CanAct() && !raising, CanAct() && raising && canRaise);
    private void SelectAmount(int amount)
    {
        if (CanAct() && raising && canRaise) controller.OnRaiseAmountSelected(amount);
    }
    private void Max()
    {
        if (CanAct() && raising && canRaise) controller.OnRaiseMaxClicked();
    }
    private void Confirm()
    {
        if (!CanAct() || !raising || !canRaise) return;
        submitted = true;
        UpdateInput();
        controller.OnRaiseClicked();
        // A rejected model action also releases the UI safely on the next refresh.
        submitted = false;
        ResetState();
    }
    private void Call()
    {
        if (!CanAct() || raising || !callButton.interactable) return;
        submitted = true;
        controller.OnCallClicked();
        submitted = false;
    }
    private void Fold()
    {
        if (!CanAct() || raising) return;
        submitted = true;
        controller.OnFoldClicked();
        submitted = false;
    }
    private void OnDisable() { allowed = false; ResetState(); }
    private void OnDestroy() => Unbind();
    private void Unbind()
    {
        if (callButton == null) return;
        callButton.onClick.RemoveListener(Call);
        foldButton.onClick.RemoveListener(Fold);
        raiseButton.onClick.RemoveListener(OpenRaise);
        confirmButton.onClick.RemoveListener(Confirm);
        backButton.onClick.RemoveListener(Back);
        maxButton.onClick.RemoveListener(Max);
        slider.ValueChanged -= SelectAmount;
    }
}
