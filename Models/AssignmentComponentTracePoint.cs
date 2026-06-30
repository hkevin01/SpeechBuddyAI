namespace SpeechBuddyAI.Models;

public sealed class AssignmentComponentTracePoint
{
    public string TargetSound { get; init; } = string.Empty;
    public double PriorityScore { get; init; }
    public double SeverityScore { get; init; }
    public double InstabilityScore { get; init; }
    public double DeclineScore { get; init; }
    public double FrequencyScore { get; init; }
    public double ConfidenceFactor { get; init; }
    public double EvidenceStrength { get; init; }
    public double OverallScoreMean { get; init; }
    public double OverallScoreCiLower { get; init; }
    public double OverallScoreCiUpper { get; init; }
    public bool ConfidenceIntervalSuppressed { get; init; }
    public int ConfidenceIntervalMinSamples { get; init; }
    public int InstabilityWindowSize { get; init; }
    public int DeclineWindowSize { get; init; }
    public int AttemptCount { get; init; }
    public string ScoringFormulaVersion { get; init; } = string.Empty;
}
