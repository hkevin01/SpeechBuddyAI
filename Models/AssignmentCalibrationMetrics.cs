namespace SpeechBuddyAI.Models;

public sealed class AssignmentCalibrationMetrics
{
    public int MatchedTargetCountNext1 { get; init; }
    public double MeanAbsoluteErrorNext1 { get; init; }
    public double MeanSquaredErrorNext1 { get; init; }
    public double RankAgreementNext1 { get; init; }
    public double TopTargetHitRateNext1 { get; init; }

    public int MatchedTargetCountNext3 { get; init; }
    public double MeanAbsoluteErrorNext3 { get; init; }
    public double MeanSquaredErrorNext3 { get; init; }
    public double RankAgreementNext3 { get; init; }
    public double TopTargetHitRateNext3 { get; init; }

    public int MatchedTargetCount { get; init; }
    public double MeanAbsoluteError { get; init; }
    public double MeanSquaredError { get; init; }
    public double RankAgreement { get; init; }
    public double TopTargetHitRate { get; init; }
    public string Summary { get; init; } = string.Empty;
}
