using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

public sealed class DialogueController : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private string[] dialogueLines = Array.Empty<string>();
    [SerializeField] private GameObject dialoguePanel;

    [Header("Typing")]
    [SerializeField, Min(0f)] private float characterInterval = 0.04f;
    [SerializeField, Min(0f)] private float commaDelay = 0.12f;
    [SerializeField, Min(0f)] private float sentenceDelay = 0.20f;
    [SerializeField, Min(0f)] private float ellipsisDelay = 0.35f;

    [Header("Optional dialogue SFX")]
    [SerializeField] private AudioClip dialogueSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;
    [SerializeField, Min(1)] private int charactersPerSound = 3;
    [SerializeField, Min(0f)] private float minimumSoundInterval = 0.08f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private AudioMixerGroup dialogueSfxMixerGroup;

    private string[] activeDialogueLines = Array.Empty<string>();
    private int currentLineIndex = -1;
    private bool completionRaised;
    private Coroutine typingRoutine;
    private int characterCount;
    private TMP_CharacterInfo[] lineCharacters = Array.Empty<TMP_CharacterInfo>();
    private int soundCharacterCount;
    private float nextSoundTime;
    private int lastNextFrame = -1;
    private AudioSource dialogueAudioSource;

    public event Action DialogueCompleted;

    public bool IsRunning { get; private set; }
    public bool IsTyping { get; private set; }
    public int CurrentLineIndex => currentLineIndex;
    public string CurrentLine => IsValidCurrentLine()
        ? activeDialogueLines[currentLineIndex]
        : string.Empty;

    private void Start()
    {
        if (dialoguePanel != null && !IsRunning)
        {
            dialoguePanel.SetActive(false);
        }
    }

    public void StartDialogue()
    {
        StartDialogue(dialogueLines);
    }

    public void StartDialogue(string[] lines)
    {
        StopTyping();
        lastNextFrame = -1;
        nextSoundTime = float.NegativeInfinity;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        activeDialogueLines = lines ?? Array.Empty<string>();
        currentLineIndex = -1;
        completionRaised = false;
        IsRunning = false;

        if (activeDialogueLines.Length == 0)
        {
            CompleteDialogue();
            return;
        }

        currentLineIndex = 0;
        IsRunning = true;
        ShowCurrentLine();
    }

    public void Next()
    {
        if (!IsRunning || (Application.isPlaying && lastNextFrame == Time.frameCount))
        {
            return;
        }

        lastNextFrame = Time.frameCount;
        if (IsTyping)
        {
            StopTyping();
            if (dialogueText != null) dialogueText.maxVisibleCharacters = characterCount;
            return;
        }

        if (currentLineIndex < activeDialogueLines.Length - 1)
        {
            currentLineIndex++;
            ShowCurrentLine();
            return;
        }

        CompleteDialogue();
    }

    private void ShowCurrentLine()
    {
        StopTyping();
        if (dialogueText == null)
        {
            return;
        }

        dialogueText.useMaxVisibleDescender = false;
        dialogueText.text = FormatLine(CurrentLine);
        dialogueText.maxVisibleCharacters = int.MaxValue;
        dialogueText.ForceMeshUpdate(true);
        characterCount = dialogueText.textInfo.characterCount;
        lineCharacters = new TMP_CharacterInfo[characterCount];
        Array.Copy(dialogueText.textInfo.characterInfo, lineCharacters, characterCount);
        dialogueText.maxVisibleCharacters = 0;

        if (characterCount == 0) return;
        if (characterInterval <= 0f || !isActiveAndEnabled)
        {
            dialogueText.maxVisibleCharacters = characterCount;
            return;
        }

        IsTyping = true;
        typingRoutine = StartCoroutine(TypeCurrentLine());
    }

    private IEnumerator TypeCurrentLine()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, characterInterval));
        for (int i = 0; i < characterCount; i++)
        {
            if (dialogueText == null) break;
            dialogueText.maxVisibleCharacters = i + 1;
            PlayCharacterSound(lineCharacters[i]);

            float delay = GetCharacterDelay(i);
            if (delay <= 0f)
            {
                dialogueText.maxVisibleCharacters = characterCount;
                break;
            }
            yield return new WaitForSecondsRealtime(delay);
        }
        IsTyping = false;
        typingRoutine = null;
    }

    private float GetCharacterDelay(int index)
    {
        TMP_CharacterInfo[] characters = lineCharacters;
        bool hasRemainingText = false;
        for (int i = index + 1; i < characterCount; i++)
        {
            if (!char.IsWhiteSpace(characters[i].character)) { hasRemainingText = true; break; }
        }
        if (!hasRemainingText) return 0f;

        char character = characters[index].character;
        float extra = 0f;
        if (character == '.' || character == '…')
        {
            int start = index;
            int end = index;
            while (start > 0 && IsEllipsisCharacter(characters[start - 1].character)) start--;
            while (end + 1 < characterCount && IsEllipsisCharacter(characters[end + 1].character)) end++;
            if (index == end)
            {
                bool isEllipsis = end - start + 1 >= 3;
                for (int i = start; i <= end; i++) isEllipsis |= characters[i].character == '…';
                extra = isEllipsis ? ellipsisDelay : sentenceDelay;
            }
        }
        else if (character == ',') extra = commaDelay;
        else if (character == '?' || character == '!') extra = sentenceDelay;
        return Mathf.Max(0f, characterInterval) + Mathf.Max(0f, extra);
    }

    private static bool IsEllipsisCharacter(char character) => character == '.' || character == '…';

    private void PlayCharacterSound(TMP_CharacterInfo character)
    {
        if (dialogueSound == null || !character.isVisible || char.IsWhiteSpace(character.character) ||
            char.IsPunctuation(character.character) || char.IsControl(character.character)) return;

        soundCharacterCount++;
        if (soundCharacterCount < Mathf.Max(1, charactersPerSound) || Time.unscaledTime < nextSoundTime) return;
        if (dialogueAudioSource == null) dialogueAudioSource = gameObject.AddComponent<AudioSource>();
        dialogueAudioSource.playOnAwake = false;
        dialogueAudioSource.loop = false;
        dialogueAudioSource.spatialBlend = 0f;
        if (dialogueSfxMixerGroup != null) dialogueAudioSource.outputAudioMixerGroup = dialogueSfxMixerGroup;
        float minPitch = Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.01f, 3f);
        float maxPitch = Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), minPitch, 3f);
        dialogueAudioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        dialogueAudioSource.PlayOneShot(dialogueSound, Mathf.Clamp01(soundVolume));
        soundCharacterCount = 0;
        nextSoundTime = Time.unscaledTime + Mathf.Max(0.01f, minimumSoundInterval);
    }

    private void StopTyping()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
        IsTyping = false;
        soundCharacterCount = 0;
        if (dialogueAudioSource != null) dialogueAudioSource.Stop();
    }

    private void OnDisable()
    {
        StopTyping();
        IsRunning = false;
    }

    private void OnDestroy() => StopTyping();

    private static string FormatLine(string line)
    {
        return line ?? string.Empty;
    }

    private void CompleteDialogue()
    {
        StopTyping();
        IsRunning = false;

        if (completionRaised)
        {
            return;
        }

        completionRaised = true;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        DialogueCompleted?.Invoke();
    }

    private bool IsValidCurrentLine()
    {
        return currentLineIndex >= 0 &&
               currentLineIndex < activeDialogueLines.Length;
    }
}
