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
    [SerializeField] private AudioSource titleBGM;
    [SerializeField] private AudioSource chipStackSFX;
    [SerializeField] private AudioSource cardSFX;
    [SerializeField] private AudioSource itemChipsPocketSFX;
    [SerializeField] private AudioSource itemDefySFX;
    [SerializeField] private AudioSource itemPrizmChipSFX;
    [SerializeField] private AudioSource itemRefreshCardSFX;
    [SerializeField] private AudioSource clickSFX;
    [SerializeField] private AudioSource itemUseFailedSFX;

    private Coroutine bgmPlayback;

    public int currentBGMIndex { get; private set; } = 0;
    public bool isBGMPlaying => bgmPlayback != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // The persistent sound owner must follow the UI of the newly loaded scene.
            Instance.MasterSlider = MasterSlider;
            Instance.BGMVolumeSlider = BGMVolumeSlider;
            Instance.SFXVolumeSlider = SFXVolumeSlider;
            Instance.RestoreSliderValues();
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    void Start() => RestoreSliderValues();

    private void RestoreSliderValues()
    {
        float savedMasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedBGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

        if (MasterSlider != null) MasterSlider.value = savedMasterVolume;
        if (BGMVolumeSlider != null) BGMVolumeSlider.value = savedBGMVolume;
        if (SFXVolumeSlider != null) SFXVolumeSlider.value = savedSFXVolume;

        if (MasterSlider != null) SetMasterVolume(MasterSlider.value);
        if (BGMVolumeSlider != null) SetBGMVolume(BGMVolumeSlider.value);
        if (SFXVolumeSlider != null) SetSFXVolume(SFXVolumeSlider.value);
    }

    public void PlayBGM()
    {
        if (bgmPlayback == null && bgmList != null && bgmList.Length > 0)
        {
            bgmPlayback = StartCoroutine(RepeatPlaybackBGM());
        }
    }

    private IEnumerator RepeatPlaybackBGM()
    {
        while (true)
        {
            AudioSource track = bgmList[currentBGMIndex];
            track.Play();
            yield return new WaitForSeconds(track.clip.length);
            track.Stop();
            currentBGMIndex = (currentBGMIndex + 1) % bgmList.Length;
        }
    }

    public void StopBGM()
    {
        if (bgmPlayback == null) return;

        StopCoroutine(bgmPlayback);
        bgmPlayback = null;
        bgmList[currentBGMIndex].Stop();
    }

    public IEnumerator PlayTitleBGM()
    {
        titleBGM.Play();
        yield return new WaitForSeconds(titleBGM.clip.length);
        StartCoroutine(PlayTitleBGM());
    }

    public void StopTitleBGM()
    {
        titleBGM.Stop();
        StopCoroutine(PlayTitleBGM());
    }

    // SFX Play
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

    public void PlayItemChipsPocketSFX()
    {
        AudioSource instantiatedSFX = Instantiate(itemChipsPocketSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void PlayItemDefySFX()
    {
        AudioSource instantiatedSFX = Instantiate(itemDefySFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void PlayItemPrizmChipSFX()
    {
        AudioSource instantiatedSFX = Instantiate(itemPrizmChipSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void PlayItemRefreshCardSFX()
    {
        AudioSource instantiatedSFX = Instantiate(itemRefreshCardSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void PlayClickSFX()
    {
        AudioSource instantiatedSFX = Instantiate(clickSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    public void PlayItemUseFailedSFX()
    {
        if (itemUseFailedSFX == null || itemUseFailedSFX.clip == null)
        {
            return;
        }

        AudioSource instantiatedSFX = Instantiate(itemUseFailedSFX, this.gameObject.transform);
        instantiatedSFX.Play();
        Destroy(instantiatedSFX.gameObject, instantiatedSFX.clip.length);
    }

    // Set Volume
    public void SetMasterVolume(float volume)
    {
        if (volume < 0.0001f)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
            return;
        }

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

        float db = Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("SFXVolume", db);

        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    void Update()
    {
        if (MasterSlider != null) SetMasterVolume(MasterSlider.value);
        if (BGMVolumeSlider != null) SetBGMVolume(BGMVolumeSlider.value);
        if (SFXVolumeSlider != null) SetSFXVolume(SFXVolumeSlider.value);
    }
}
