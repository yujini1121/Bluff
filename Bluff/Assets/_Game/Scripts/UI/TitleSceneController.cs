using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleSceneController : MonoBehaviour
{
    private const string IntroSceneName = "Intro";
    private const string MasterVolume = "MasterVolume";
    private const string BGMVolume = "BGMVolume";
    private const string SFXVolume = "SFXVolume";
    private const string MasterSliderSaveKey = "Master Volume SliderSliderValue";
    private const string BGMSliderSaveKey = "BGM Volume SliderSliderValue";
    private const string SFXSliderSaveKey = "SFX Volume SliderSliderValue";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private GameObject menuButtonGroup;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Button creditsBackButton;

    private GameObject menuSelection;

    private void Start()
    {
        RestoreVolume(masterVolumeSlider, MasterVolume, MasterSliderSaveKey);
        RestoreVolume(bgmVolumeSlider, BGMVolume, BGMSliderSaveKey);
        RestoreVolume(sfxVolumeSlider, SFXVolume, SFXSliderSaveKey);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (creditsPanel != null && creditsPanel.activeSelf)
        {
            CloseCredits();
        }
        else if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
        }
    }

    public void OpenCredits()
    {
        if (creditsPanel == null || creditsPanel.activeSelf || menuButtonGroup == null) return;
        if (settingsPanel != null && settingsPanel.activeSelf) return;

        var events = EventSystem.current;
        menuSelection = events != null ? events.currentSelectedGameObject : null;
        if (events != null) events.SetSelectedGameObject(null);

        menuButtonGroup.SetActive(false);
        creditsPanel.SetActive(true);
        if (events != null && creditsBackButton != null) events.SetSelectedGameObject(creditsBackButton.gameObject);
    }

    public void CloseCredits()
    {
        if (creditsPanel == null || !creditsPanel.activeSelf) return;

        creditsPanel.SetActive(false);
        menuButtonGroup.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuSelection);
        menuSelection = null;
    }

    public void OpenSettings()
    {
        if (settingsPanel.activeSelf) return;

        var events = EventSystem.current;
        if (events != null)
        {
            menuSelection = events.currentSelectedGameObject;
            events.SetSelectedGameObject(null);
        }
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (!settingsPanel.activeSelf) return;

        settingsPanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuSelection);
        menuSelection = null;
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float volume) => SetVolume(MasterVolume, MasterSliderSaveKey, volume);

    public void SetBGMVolume(float volume) => SetVolume(BGMVolume, BGMSliderSaveKey, volume);

    public void SetSFXVolume(float volume) => SetVolume(SFXVolume, SFXSliderSaveKey, volume);

    private void RestoreVolume(Slider slider, string parameter, string sliderSaveKey)
    {
        if (slider == null) return;

        float volume = PlayerPrefs.GetFloat(sliderSaveKey, PlayerPrefs.GetFloat(parameter, 1f));
        slider.SetValueWithoutNotify(volume);
        SetVolume(parameter, sliderSaveKey, slider.value);
    }

    private void SetVolume(string parameter, string sliderSaveKey, float volume)
    {
        if (audioMixer != null) audioMixer.SetFloat(parameter, volume < 0.0001f ? -80f : Mathf.Log10(volume) * 20f);
        // 인게임 및 설정창 볼륨 저장 키 동기화
        PlayerPrefs.SetFloat(parameter, volume);
        PlayerPrefs.SetFloat(sliderSaveKey, volume);
    }

    public void StartRoundLimited()
    {
        StartGame(GameMode.RoundLimited, IntroSceneName);
    }

    public void StartEndless()
    {
        StartGame(GameMode.Endless, IntroFlowController.GameplaySceneName);
    }

    public void ExitGame()
    {
        UnityEngine.Application.Quit();
    }

    private static void StartGame(GameMode gameMode, string sceneName)
    {
        GameModeSelection.Select(gameMode);
        SceneManager.LoadScene(sceneName);
    }
}
