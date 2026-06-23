namespace SpeechBuddyAI.Models;

public sealed class AssignmentFocusTargetReason
{
    public string TargetSound { get; init; } = string.Empty;
    public double PriorityScore { get; init; }
    public double SeverityScore { get; init; }
    public double InstabilityScore { get; init; }
    public double DeclineScore { get; init; }
    public double FrequencyScore { get; init; }
    public double ConfidenceFactor { get; init; }
    public double ConfidenceVariance { get; init; }
    public bool AssignmentChangeSuppressed { get; init; }
    public double InitialAverageScore { get; init; }
    public double MedialAverageScore { get; init; }
    public double FinalAverageScore { get; init; }
    public bool PositionSequenceSampleGateMet { get; init; }
    public IReadOnlyList<double> InitialAttemptScores { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> MedialAttemptScores { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> FinalAttemptScores { get; init; } = Array.Empty<double>();
    public string PositionSequence { get; init; } = "initial -> medial -> final";
    public string PositionDeltaSummary { get; init; } = "initial +0.00 | medial +0.00 | final +0.00";
}
