namespace SpeechBuddyAI.Models;

public sealed class ScoreComponents
{
    public double PhonemeScore { get; set; }
    public double FluencyScore { get; set; }
    public double ConsistencyScore { get; set; }
    public double OverallScore { get; set; }
}
