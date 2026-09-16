using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SoundSystem : MonoBehaviour
{
    public static SoundSystem Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider MasterSlider;
    [SerializeField] private Slider BGMVolumeSlider;
    [SerializeField] private Slider SFXVolumeSlider;

    [SerializeField] private AudioSource[] bgmList;
    [SerializeField] private AudioSource chipStackSFX;
    [SerializeField] private AudioSource cardSFX;

    private int currentBGMIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        StartCoroutine(RepeatPlaybackBGM(currentBGMIndex));
    }

    void Start()
    {
        float savedMasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedBGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        MasterSlider.value = savedMasterVolume;
        BGMVolumeSlider.value = savedBGMVolume;
        SFXVolumeSlider.value = savedSFXVolume;

        SetMasterVolume(MasterSlider.value);
        SetBGMVolume(BGMVolumeSlider.value);
        SetSFXVolume(SFXVolumeSlider.value);
    }

    public IEnumerator RepeatPlaybackBGM(int index)
    {
        if (index < 0 || index >= bgmList.Length)
        {
            Debug.LogWarning("Invalid BGM index.");
            yield break;
        }
        bgmList[index].Play();

        yield return new WaitForSeconds(bgmList[index].clip.length);

        currentBGMIndex++;
        if (currentBGMIndex >= bgmList.Length)
        {
            currentBGMIndex = 0;
        }
        StartCoroutine(RepeatPlaybackBGM(currentBGMIndex));
    }

    public void StopBGM()
    {
        StopCoroutine(RepeatPlaybackBGM(currentBGMIndex));
    }

    public void PlayChipStackSFX()
    {
        AudioSource instantiateSFX = Instantiate(chipStackSFX, this.gameObject.transform);
        instantiateSFX.Play();
        Destroy(instantiateSFX.gameObject, instantiateSFX.clip.length);
    }

    public void PlayCardSFX()
    {
        AudioSource instantiatedSFX = Instantiate(cardSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void SetMasterVolume(float volume)
    {
        if (volume < 0.0001f)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
            return;
        }

        volume = Mathf.Max(volume, 0.0001f);
        float db = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("MasterVolume", db);

        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    public void SetBGMVolume(float volume)
    {
        if (volume < 0.0001f)
        {
            audioMixer.SetFloat("BGMVolume", -80f);
            return;
        }
        volume = Mathf.Max(volume, 0.0001f);
        float db = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("BGMVolume", db);

        PlayerPrefs.SetFloat("BGMVolume", volume);
    }

    public void SetSFXVolume(float volume)
    {
        if (volume < 0.0001f)
        {
            audioMixer.SetFloat("SFXVolume", -80f);
            return;
        }
        volume = Mathf.Max(volume, 0.0001f);
        float db = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("SFXVolume", db);

        PlayerPrefs.SetFloat("SFXVolume", volume);
    }
}