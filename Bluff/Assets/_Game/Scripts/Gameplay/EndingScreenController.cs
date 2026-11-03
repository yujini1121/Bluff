using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Owns presentation only. The original GameState and ItemSystem remain with GameplayController.
public sealed class EndingScreenController : MonoBehaviour
{
    [Header("Authored UI references")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text victoryCaption;
    [SerializeField] private TMP_Text defeatCaption;
    [SerializeField] private TMP_Text neutralCaption;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button continueButton;
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
    public bool IsActive { get; private set; }
    public bool IsVisible => canvas != null && canvas.enabled;
    public bool HasReferences => canvas != null && victoryCaption != null && defeatCaption != null &&
        neutralCaption != null && titleButton != null && continueButton != null &&
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
        if (!navigating && IsVisible && continueButton.interactable) continueAction?.Invoke();
    }

    // Capture before the cutscene captures its own state, so its cleanup restores our locked state.
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

    public void Show(GameWinner winner, bool canContinue, Action onContinue)
    {
        if (!HasReferences) return;
        Begin();
        caption = winner == GameWinner.Player ? victoryCaption : winner == GameWinner.Dealer ? defeatCaption : neutralCaption;
        victoryCaption.gameObject.SetActive(caption == victoryCaption);
        defeatCaption.gameObject.SetActive(caption == defeatCaption);
        neutralCaption.gameObject.SetActive(caption == neutralCaption);
        continueAction = onContinue;
        continueButton.interactable = canContinue;
        continueButton.gameObject.SetActive(true);
        titleButton.interactable = Application.CanStreamedLevelBeLoaded(titleSceneName);
        canvas.enabled = true;
        EnforceLock();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(titleButton.gameObject);
    }

    private void LateUpdate() { if (IsActive) EnforceLock(); }

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
