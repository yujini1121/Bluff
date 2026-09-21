using System;
using TMPro;
using UnityEngine;

public sealed class DialogueController : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private string[] dialogueLines = Array.Empty<string>();
    [SerializeField] private GameObject dialoguePanel;

    private string[] activeDialogueLines = Array.Empty<string>();
    private int currentLineIndex = -1;
    private bool completionRaised;

    public event Action DialogueCompleted;

    public bool IsRunning { get; private set; }
    public int CurrentLineIndex => currentLineIndex;
    public string CurrentLine => IsValidCurrentLine()
        ? activeDialogueLines[currentLineIndex]
        : string.Empty;

    private void Start()
    {
        if (dialoguePanel != null)
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
        if (!IsRunning)
        {
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
        if (dialogueText == null)
        {
            return;
        }

        dialogueText.text = FormatLine(CurrentLine);
    }

    private static string FormatLine(string line)
    {
        return line ?? string.Empty;
    }

    private void CompleteDialogue()
    {
        IsRunning = false;

        if (completionRaised)
        {
            return;
        }

        completionRaised = true;
        DialogueCompleted?.Invoke();
    }

    private bool IsValidCurrentLine()
    {
        return currentLineIndex >= 0 &&
               currentLineIndex < activeDialogueLines.Length;
    }
}
