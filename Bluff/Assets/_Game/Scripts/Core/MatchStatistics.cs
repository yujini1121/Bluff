public sealed class MatchStatistics
{
    public int PlayerItemsUsed { get; internal set; }
    public int LargestPotRound { get; internal set; }
    public int LargestPotAmount { get; internal set; }
    public double PlaySeconds { get; internal set; }
    public int LongestRound { get; internal set; }
    public double LongestRoundSeconds { get; internal set; }
    public int RoundsWon { get; internal set; }
    public int RoundsLost { get; internal set; }
    public int RoundsDrawn { get; internal set; }
    public long PlayerChipsBet { get; internal set; }
    public int RoundsPlayed => RoundsWon + RoundsLost + RoundsDrawn;
}
