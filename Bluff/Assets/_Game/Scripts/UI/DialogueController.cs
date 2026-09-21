using System;
using TMPro;
using UnityEngine;

public sealed class DialogueController : MonoBehaviour
{
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private string[] dialogueLines = Array.Empty<string>();

    private int currentLineIndex = -1;
    private bool completionRaised;

    public event Action DialogueCompleted;

    public bool IsRunning { get; private set; }
    public int CurrentLineIndex => currentLineIndex;
    public string CurrentLine => IsValidCurrentLine()
        ? dialogueLines[currentLineIndex]
        : string.Empty;

    public void StartDialogue()
    {
        currentLineIndex = -1;
        completionRaised = false;
        IsRunning = false;

        if (dialogueLines == null || dialogueLines.Length == 0)
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

        if (currentLineIndex < dialogueLines.Length - 1)
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
        return dialogueLines != null &&
               currentLineIndex >= 0 &&
               currentLineIndex < dialogueLines.Length;
    }
}
