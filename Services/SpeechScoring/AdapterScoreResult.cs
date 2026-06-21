namespace SpeechBuddyAI.Services.SpeechScoring;

public sealed class AdapterScoreResult
{
    public required string Provider { get; init; }
    public double PhonemeScore { get; init; }
    public double FluencyScore { get; init; }
}
