using UnityEngine;

[CreateAssetMenu(menuName = "Bluff/UI/Result Flow Text")]
public sealed class ResultFlowText : ScriptableObject
{
    [Header("Round results")]
    public string roundPlayerWin;
    public string roundDealerWin;
    public string roundDraw;
    public string matchComplete;
    public string playerFold;
    public string dealerFold;
    [Tooltip("{0}: fold text, {1}: penalty")]
    public string foldPenaltyFormat;
    [Tooltip("{0}: carried pot")]
    public string carriedPotFormat;
    public string numberHand;
    public string doubleHand;
    public string straightHand;
    public string tripleHand;
    public string unknownHand;

    [Header("Match results")]
    public string matchPlayerWin;
    public string matchDealerWin;
    public string matchDraw;
    [TextArea, Tooltip("{0}: round detail, {1}: match winner, {2}: player chips, {3}: dealer chips")]
    public string finalSummaryFormat;

    [Header("Progress buttons")]
    public string nextRoundLabel;
    public string viewEndingLabel;
}
