using System.Collections;
using UnityEngine;

public class SoundSystem : MonoBehaviour
{
    public static SoundSystem Instance { get; private set; }

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
}