using System;
using UnityEngine;
using UnityEngine.Events;

public sealed class SlotMachineController : MonoBehaviour
{
    [Serializable] public sealed class ResultEvent : UnityEvent<SlotSpinResult> { }
    [Header("Slot Machine References")]
    [SerializeField] private SlotMachineSprites sprites;
    [SerializeField] private SlotMachineReel[] reels;
    [SerializeField] private SlotMachineDisplay display;
    [Header("Spin")]
    [SerializeField, Range(0, 1)] private float winChance = 0.25f;
    [SerializeField, Min(0.5f)] private float spinDuration = 2.8f;
    [SerializeField, Min(0.2f)] private float stopInterval = 0.5f;
    [SerializeField, Min(2)] private int stripLoops = 3;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField] private ResultEvent onSpinCompleted = new ResultEvent();

    private readonly System.Random random = new System.Random();
    private Action<SlotSpinResult> completion;
    private SlotSpinResult pending;
    private int remaining;
    private bool initialized;
    public bool IsSpinning { get; private set; }
    public SlotSpinResult LastResult { get; private set; }
    public float WinChance { get => winChance; set => winChance = Mathf.Clamp01(value); }
    public SlotMachineReel[] Reels => reels == null ? Array.Empty<SlotMachineReel>() : (SlotMachineReel[])reels.Clone();
    public event Action<SlotSpinResult> SpinCompleted;
    public event Action SpinCancelled;
    public bool IsConfigured => sprites && sprites.Count >= 2 && display && display.HasReferences &&
        reels != null && reels.Length == 3 && Array.TrueForAll(reels, reel => reel && reel.HasReferences);

    public void Configure(SlotMachineSprites bank, SlotMachineReel[] strips, SlotMachineDisplay screens)
    {
        sprites = bank; reels = strips; display = screens;
        Initialize();
    }

    private void OnEnable() { if (IsConfigured) Initialize(); }
    private void Initialize()
    {
        foreach (var reel in reels) reel.Initialize(sprites);
        display.Initialize(sprites); initialized = true;
    }

    public bool TrySpin(Action<SlotSpinResult> completed = null) => TrySpin(SlotSpinMode.Normal, completed);

    public bool TrySpin(SlotSpinMode mode, Action<SlotSpinResult> completed = null)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsSpinning || !IsConfigured) return false;
        if (!initialized) Initialize();
        pending = SlotSpinResult.Choose(sprites, winChance, mode, random);
        completion = completed; remaining = 3; IsSpinning = true;
        display.ShowSpinning(useUnscaledTime);
        for (int i = 0; i < reels.Length; i++) {
            reels[i].RollTo(pending.GetSymbolIndex(i), Mathf.Max(0.5f, spinDuration) + i * Mathf.Max(0.2f, stopInterval),
                Mathf.Max(2, stripLoops) + i, useUnscaledTime, OnReelStopped);
        }
        return true;
    }

    private void OnReelStopped()
    {
        if (!IsSpinning || --remaining != 0) return;
        var result = pending;
        LastResult = result; pending = null; IsSpinning = false;
        var callback = completion; completion = null;
        display.ShowResult(result, useUnscaledTime);
        if (result.IsWin) foreach (var reel in reels) reel.Emphasize(useUnscaledTime);
        SpinCompleted?.Invoke(result);
        onSpinCompleted.Invoke(result);
        callback?.Invoke(result);
    }

    [ContextMenu("Spin (Play Mode)")] public void Spin() => TrySpin();
    [ContextMenu("Force Win (Play Mode)")] public void ForceWin() => TrySpin(SlotSpinMode.ForceWin);
    [ContextMenu("Force Loss (Play Mode)")] public void ForceLoss() => TrySpin(SlotSpinMode.ForceLoss);

    private void OnDisable()
    {
        bool cancelled = IsSpinning;
        if (reels != null) foreach (var reel in reels) if (reel) reel.Cancel();
        if (display) display.StopEffects();
        initialized = false; IsSpinning = false; pending = null; completion = null;
        if (cancelled) SpinCancelled?.Invoke();
    }
}
