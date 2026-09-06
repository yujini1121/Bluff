using System.Collections;
using UnityEngine;

public class SoundSystem : MonoBehaviour
{
    public static SoundSystem Instance { get; private set; }

    [SerializeField] private AudioSource bgm;
    [SerializeField] private AudioSource chipStackSFX;
    [SerializeField] private AudioSource cardSFX;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void PlayBGM()
    {
        bgm.Play();
    }

    public void StopBGM()
    {
        bgm.Stop();
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
}
