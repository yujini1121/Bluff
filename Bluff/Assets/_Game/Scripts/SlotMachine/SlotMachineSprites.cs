using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Bluff/Slot Machine/Sprites")]
public sealed class SlotMachineSprites : ScriptableObject
{
    [Serializable]
    public sealed class Symbol
    {
        public string id;
        public Sprite sprite;
        public string fallbackLabel;
        public Color color = Color.white;
    }

    public Sprite logo, idle, win, lose, decoration;
    public Symbol[] symbols = Array.Empty<Symbol>();
    public int Count => symbols == null ? 0 : symbols.Length;
}
