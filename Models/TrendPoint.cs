namespace SpeechBuddyAI.Models;

public sealed class TrendPoint
{
    public int AttemptIndex { get; init; }
    public double Score { get; init; }
    public string ScoreLabel => $"{Score:P0}";
    public double BarWidth { get; init; }
}
