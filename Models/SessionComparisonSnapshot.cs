namespace SpeechBuddyAI.Models;

public sealed class TargetComparisonItem
{
    public string TargetSound { get; init; } = string.Empty;
    public double CurrentAverageOverall { get; init; }
    public double PreviousAverageOverall { get; init; }
    public double CurrentAverageConfidence { get; init; }
    public double PreviousAverageConfidence { get; init; }
    public ConfidenceBand CurrentConfidenceBand { get; init; }
    public ConfidenceBand PreviousConfidenceBand { get; init; }
    public int CurrentAttemptCount { get; init; }
    public int PreviousAttemptCount { get; init; }
    public double CurrentSessionVariance { get; init; }
    public double PreviousSessionVariance { get; init; }
    public double RecentSessionVariance { get; init; }
    public double ConsistencyDecay { get; init; }
    public double VariabilityIndex { get; init; }

    public double OverallDelta => CurrentAverageOverall - PreviousAverageOverall;
    public double ConfidenceDelta => CurrentAverageConfidence - PreviousAverageConfidence;
    public string ChipBackgroundColor => CurrentConfidenceBand.ToChipBackgroundColor();
    public string ChipBorderColor => CurrentConfidenceBand.ToChipBorderColor();
    public string ConfidenceMovementLabel => $"{PreviousConfidenceBand.ToDisplayName()} to {CurrentConfidenceBand.ToDisplayName()}";
}

public sealed class ConfidenceBandTransitionCount
{
    public ConfidenceBand FromBand { get; init; }
    public ConfidenceBand ToBand { get; init; }
    public int Count { get; init; }

    public string SummaryLabel => $"{Count} {FromBand.ToDisplayName()} to {ToBand.ToDisplayName()}";
}

public sealed class SessionTimelineItem
{
    public DateTime SessionDate { get; init; }
    public int AttemptCount { get; init; }
    public double AverageOverall { get; init; }
    public double AverageConfidence { get; init; }
    public double ConfidenceWeightedOverall { get; init; }
    public double SmoothedOverall { get; init; }
    public double SmoothedConfidence { get; init; }
    public bool HasComparisonBaseline { get; init; }
    public double OverallDeltaFromPreviousSession { get; init; }
    public double ConfidenceDeltaFromPreviousSession { get; init; }
}

public sealed class SessionComparisonSnapshot
{
    public bool HasCurrentSession { get; init; }
    public bool HasPreviousSession { get; init; }

    public DateTime CurrentSessionDate { get; init; }
    public int CurrentAttemptCount { get; init; }
    public double CurrentAverageOverall { get; init; }
    public double CurrentAverageConfidence { get; init; }

    public DateTime PreviousSessionDate { get; init; }
    public int PreviousAttemptCount { get; init; }
    public double PreviousAverageOverall { get; init; }
    public double PreviousAverageConfidence { get; init; }

    public SessionComparisonNormalizationMode NormalizationMode { get; init; } = SessionComparisonNormalizationMode.AttemptWeighted;
    public SessionComparisonSmoothingStrength SmoothingStrength { get; init; } = SessionComparisonSmoothingStrength.Balanced;
    public IReadOnlyList<TargetComparisonItem> TargetComparisons { get; init; } = Array.Empty<TargetComparisonItem>();
    public IReadOnlyList<ConfidenceBandTransitionCount> ConfidenceBandTransitions { get; init; } = Array.Empty<ConfidenceBandTransitionCount>();
    public IReadOnlyList<SessionTimelineItem> RollingTimeline { get; init; } = Array.Empty<SessionTimelineItem>();
    public string ComparisonNarrative { get; init; } = string.Empty;

    public double OverallDelta => CurrentAverageOverall - PreviousAverageOverall;
    public double ConfidenceDelta => CurrentAverageConfidence - PreviousAverageConfidence;

    public string ConfidenceBandMovementSummary => ConfidenceBandTransitions.Count == 0
        ? "No confidence band movement detected."
        : string.Join(", ", ConfidenceBandTransitions.Select(item => item.SummaryLabel));
}
