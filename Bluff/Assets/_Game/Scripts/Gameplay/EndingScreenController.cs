using System;
using System.Linq;
using Michsky.UI.Shift;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class EndingScreenController : MonoBehaviour
{
    [Header("Authored UI references")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text victoryCaption;
    [SerializeField] private TMP_Text defeatCaption;
    [SerializeField] private TMP_Text neutralCaption;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private MainButton continueButtonLabel;
    [SerializeField] private string continueLabel;
    [SerializeField] private string restartLabel;
    [SerializeField] private string titleSceneName;

    [Header("Scene gameplay references")]
    [SerializeField] private CanvasGroup[] hud = Array.Empty<CanvasGroup>();
    [SerializeField] private Behaviour[] inputs = Array.Empty<Behaviour>();

    private TMP_Text caption;
    private HudState[] hudStates;
    private bool[] inputStates;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private float timeScale;
    private GameObject previousSelection;
    private Action continueAction;
    private bool navigating;
    private bool invokingAction;
    private bool navigationReady;
    private Vector2 lastPointerPosition;
    public bool IsActive { get; private set; }
    public bool IsVisible => canvas != null && canvas.enabled;
    public bool HasReferences => canvas != null && victoryCaption != null && defeatCaption != null &&
        neutralCaption != null && titleButton != null && continueButton != null && continueButtonLabel != null &&
        !string.IsNullOrEmpty(continueLabel) && !string.IsNullOrEmpty(restartLabel) &&
        !string.IsNullOrEmpty(titleSceneName);

    private struct HudState { public float Alpha; public bool Interactable, Raycasts; }

    private void Awake()
    {
        if (canvas != null) canvas.enabled = false;
        if (titleButton != null) titleButton.interactable = false;
        if (continueButton != null) continueButton.interactable = false;
    }

    public void OnContinueClicked()
    {
        if (navigating || invokingAction || !IsVisible || !continueButton.interactable || continueAction == null) return;
        invokingAction = true;
        try { continueAction.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally { invokingAction = false; }
    }

    public void Begin()
    {
        if (IsActive || !HasReferences) return;
        cursorLock = Cursor.lockState; cursorVisible = Cursor.visible; timeScale = Time.timeScale;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        hudStates = hud.Select(g => g != null ? new HudState { Alpha = g.alpha, Interactable = g.interactable,
            Raycasts = g.blocksRaycasts } : default).ToArray();
        inputStates = inputs.Select(b => b != null && b.enabled).ToArray();
        IsActive = true;
        Time.timeScale = 1;
        EnforceLock();
    }

    public void Show(GameWinner winner, bool canContinue, Action onContinue, Action onRestart = null)
    {
        if (!HasReferences) return;
        Begin();
        caption = winner == GameWinner.Player ? victoryCaption : winner == GameWinner.Dealer ? defeatCaption : neutralCaption;
        victoryCaption.gameObject.SetActive(caption == victoryCaption);
        defeatCaption.gameObject.SetActive(caption == defeatCaption);
        neutralCaption.gameObject.SetActive(caption == neutralCaption);
        continueAction = canContinue ? onContinue : onRestart;
        string label = canContinue ? continueLabel : restartLabel;
        continueButtonLabel.buttonText = label;
        continueButtonLabel.normalText.text = label;
        continueButtonLabel.highlightedText.text = label;
        continueButtonLabel.pressedText.text = label;
        continueButton.interactable = continueAction != null;
        continueButton.gameObject.SetActive(true);
        titleButton.interactable = Application.CanStreamedLevelBeLoaded(titleSceneName);
        canvas.enabled = true;
        EnforceLock();
        navigationReady = false;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            var module = EventSystem.current.currentInputModule as StandaloneInputModule;
            if (module != null) lastPointerPosition = module.input.mousePosition;
        }
    }

    private void LateUpdate()
    {
        if (!IsActive) return;
        EnforceLock();
        UpdateNavigationSelection();
    }

    private void UpdateNavigationSelection()
    {
        var events = EventSystem.current;
        if (!IsVisible || navigating || events == null || !events.isFocused || !events.sendNavigationEvents ||
            !(events.currentInputModule is StandaloneInputModule module)) return;

        Vector2 pointer = module.input.mousePosition;
        if (pointer != lastPointerPosition)
        {
            events.SetSelectedGameObject(null);
            navigationReady = false;
        }
        lastPointerPosition = pointer;
        bool moving = !Mathf.Approximately(module.input.GetAxisRaw(module.horizontalAxis), 0f) ||
                      !Mathf.Approximately(module.input.GetAxisRaw(module.verticalAxis), 0f);
        if (!moving) { navigationReady = true; return; }
        if (!navigationReady || events.currentSelectedGameObject != null) return;
        var first = titleButton.IsInteractable() ? titleButton : continueButton;
        if (first.IsInteractable()) events.SetSelectedGameObject(first.gameObject);
    }

    private void EnforceLock()
    {
        foreach (var group in hud)
            if (group != null) { group.alpha = 0; group.interactable = group.blocksRaycasts = false; }
        foreach (var input in inputs) if (input != null) input.enabled = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    public void Release()
    {
        if (!IsActive) return;
        IsActive = false;
        if (canvas != null) canvas.enabled = false;
        titleButton.interactable = continueButton.interactable = false;
        for (int i = 0; i < hud.Length; i++)
        {
            if (hud[i] == null) continue;
            hud[i].alpha = hudStates[i].Alpha;
            hud[i].interactable = hudStates[i].Interactable;
            hud[i].blocksRaycasts = hudStates[i].Raycasts;
        }
        for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].enabled = inputStates[i];
        Time.timeScale = timeScale;
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        continueAction = null;
    }

    public void GoToTitle()
    {
        if (!IsVisible || navigating || !Application.CanStreamedLevelBeLoaded(titleSceneName)) return;
        navigating = true;
        Release();
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SceneManager.LoadScene(titleSceneName);
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
