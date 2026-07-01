namespace SpeechBuddyAI.Models;

public sealed class PracticeAttemptResult
{
    public required ScoreComponents Scores { get; init; }
    public required ProgressEntry Entry { get; init; }
    public required string Provider { get; init; }
    public required double ConfidenceScore { get; init; }
    public required string ConfidenceBand { get; init; }
    public required string ScoringFormulaVersion { get; init; }
    public required bool HistoricalDriftDetected { get; init; }
    public required double HistoricalDriftZScore { get; init; }
    public required string HistoricalDriftSummary { get; init; }
}
