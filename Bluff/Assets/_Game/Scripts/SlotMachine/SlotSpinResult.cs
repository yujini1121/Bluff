using System;

public enum SlotSpinMode { Normal, ForceWin, ForceLoss }
public enum SlotOutcome { Loss, Win, Jackpot }

[Serializable]
public sealed class SlotSpinResult
{
    public int Left { get; }
    public int Middle { get; }
    public int Right { get; }
    public string LeftId { get; }
    public string MiddleId { get; }
    public string RightId { get; }
    public bool IsWin => Left == Middle && Middle == Right;
    public SlotOutcome Outcome { get; }

    public int GetSymbolIndex(int reel) => reel == 0 ? Left : reel == 1 ? Middle : Right;

    public SlotSpinResult(int left, int middle, int right, SlotMachineSprites sprites)
    {
        Left = left; Middle = middle; Right = right;
        LeftId = sprites.symbols[left].id;
        MiddleId = sprites.symbols[middle].id;
        RightId = sprites.symbols[right].id;
        Outcome = !IsWin ? SlotOutcome.Loss :
            LeftId.IndexOf("seven", StringComparison.OrdinalIgnoreCase) >= 0 ? SlotOutcome.Jackpot : SlotOutcome.Win;
    }

    public static SlotSpinResult Choose(SlotMachineSprites sprites, float winChance, SlotSpinMode mode, Random random)
    {
        if (sprites == null || sprites.Count < 2) throw new ArgumentException("At least two symbols are required, including placeholders.");
        int count = sprites.Count;
        bool win = mode == SlotSpinMode.ForceWin ||
            (mode == SlotSpinMode.Normal && random.NextDouble() < Math.Max(0, Math.Min(1, winChance)));
        int a = random.Next(count);
        int b = win ? a : random.Next(count);
        int c = win ? a : random.Next(count);
        if (!win && a == b && b == c) {
            c = random.Next(count - 1);
            if (c >= a) c++;
        }
        return new SlotSpinResult(a, b, c, sprites);
    }
}
