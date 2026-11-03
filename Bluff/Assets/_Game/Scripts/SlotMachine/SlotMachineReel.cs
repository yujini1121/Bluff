using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SlotMachineReel : MonoBehaviour
{
    [SerializeField] private Image[] cells;
    [SerializeField] private TMP_Text[] fallbackLabels;
    [SerializeField] private float cellHeight = 220;
    [SerializeField] private int initialSymbol;
    private SlotMachineSprites sprites;
    private float travel;
    private Sequence spin;
    private Tween highlight;

    public int CurrentSymbol { get; private set; }
    public bool IsMoving => spin != null && spin.IsActive();
    public float CenterError => Mathf.Abs(travel - Mathf.Round(travel)) * cellHeight;
    public bool HasReferences => cells != null && cells.Length == 5 && fallbackLabels != null && fallbackLabels.Length == 5 &&
        Array.TrueForAll(cells, cell => cell != null) && Array.TrueForAll(fallbackLabels, label => label != null);

    public void Configure(Image[] images, TMP_Text[] labels, float height, int initial)
    {
        cells = images; fallbackLabels = labels; cellHeight = height; initialSymbol = initial;
    }

    public void Initialize(SlotMachineSprites bank)
    {
        Cancel(); sprites = bank;
        CurrentSymbol = Mod(initialSymbol, bank.Count);
        travel = CurrentSymbol;
        Draw();
    }

    public void RollTo(int symbol, float duration, int loops, bool unscaled, Action stopped)
    {
        Cancel();
        int start = Mathf.CeilToInt(travel);
        int destination = start + sprites.Count * Mathf.Max(2, loops) + Mod(symbol - start, sprites.Count);
        spin = DOTween.Sequence().SetUpdate(unscaled);
        spin.Append(DOTween.To(() => travel, value => { travel = value; Draw(); }, destination + 0.075f,
            Mathf.Max(0.3f, duration)).SetEase(Ease.InOutSine));
        spin.Append(DOTween.To(() => travel, value => { travel = value; Draw(); }, destination, 0.16f).SetEase(Ease.OutSine));
        spin.OnComplete(() => {
            spin = null;
            travel = Mod(destination, sprites.Count);
            CurrentSymbol = symbol;
            Draw();
            stopped?.Invoke();
        });
    }

    private void Draw()
    {
        if (!HasReferences || sprites == null || sprites.Count == 0) return;
        int whole = Mathf.FloorToInt(travel);
        float fraction = travel - whole;
        for (int i = 0; i < cells.Length; i++) {
            int offset = i - 2;
            var symbol = sprites.symbols[Mod(whole + offset, sprites.Count)];
            var image = cells[i];
            image.rectTransform.anchoredPosition = new Vector2(0, (offset - fraction) * cellHeight);
            image.sprite = symbol.sprite;
            image.color = symbol.sprite ? Color.white : new Color(symbol.color.r, symbol.color.g, symbol.color.b, 0.12f);
            var label = fallbackLabels[i];
            label.text = symbol.fallbackLabel;
            label.color = symbol.color;
            label.enabled = !symbol.sprite;
        }
    }

    public void Emphasize(bool unscaled)
    {
        highlight?.Kill(false);
        cells[2].rectTransform.localScale = Vector3.one;
        highlight = cells[2].rectTransform.DOScale(1.13f, 0.19f).SetLoops(4, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(unscaled);
    }

    public void Cancel()
    {
        spin?.Kill(false); spin = null;
        highlight?.Kill(false); highlight = null;
        if (cells != null) foreach (var cell in cells) if (cell) cell.rectTransform.localScale = Vector3.one;
    }

    private static int Mod(int value, int count) => (value % count + count) % count;
    private void OnDisable() => Cancel();
    private void OnDestroy() => Cancel();
}
