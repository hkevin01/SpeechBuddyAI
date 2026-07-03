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
    public double EvidenceStrength { get; init; }
    public double ConfidenceVariance { get; init; }
    public bool AssignmentChangeSuppressed { get; init; }
    public double OverallScoreMean { get; init; }
    public double OverallScoreCiLower { get; init; }
    public double OverallScoreCiUpper { get; init; }
    public bool ConfidenceIntervalSuppressed { get; init; }
    public int ConfidenceIntervalMinSamples { get; init; }
    public int InstabilityWindowSize { get; init; }
    public int DeclineWindowSize { get; init; }
    public string ScoringFormulaVersion { get; init; } = string.Empty;
    public double InitialAverageScore { get; init; }
    public double MedialAverageScore { get; init; }
    public double FinalAverageScore { get; init; }
    public bool PositionSequenceSampleGateMet { get; init; }
    public IReadOnlyList<double> InitialAttemptScores { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> MedialAttemptScores { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> FinalAttemptScores { get; init; } = Array.Empty<double>();
    public string PositionSequence { get; init; } = "initial -> medial -> final";
    public string PositionDeltaSummary { get; init; } = "initial +0.00 | medial +0.00 | final +0.00";
    public double PositionWeightedDeclineScore { get; init; }
    public double FrequencyNormalizationFactor { get; init; }
    public double ReliabilityScore { get; init; }
    public double ReliabilitySampleDepthScore { get; init; }
    public double ReliabilityVarianceScore { get; init; }
    public double ReliabilityTrendStabilityScore { get; init; }
    public double CalibrationQualityScore { get; init; }
    public double CalibrationConfidenceAdjustment { get; init; }
}
