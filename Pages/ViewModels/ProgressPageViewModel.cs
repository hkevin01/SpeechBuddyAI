using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Pages.ViewModels;

public sealed class ProgressComparisonLegendItem
{
    public string Title { get; init; } = string.Empty;
    public string BackgroundColor { get; init; } = "#F1F3F5";
    public string BorderColor { get; init; } = "#ADB5BD";
}

public sealed class ProgressTimelineRow
{
    public string SessionDateText { get; init; } = string.Empty;
    public string AttemptsText { get; init; } = string.Empty;
    public string OverallText { get; init; } = string.Empty;
    public string ConfidenceText { get; init; } = string.Empty;
    public string DeltaText { get; init; } = string.Empty;
}

public sealed class ProgressSummaryBadge
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string BackgroundColor { get; init; } = "#F8F9FA";
    public string BorderColor { get; init; } = "#DEE2E6";
}

public sealed class ProgressTrendViewState
{
    public string TrajectoryText { get; init; } = "Trajectory not available yet.";
    public string ModerateThresholdText { get; init; } = "Moderate threshold: -";
    public string HighThresholdText { get; init; } = "High threshold: -";
    public IReadOnlyList<TrendPoint> TrendPoints { get; init; } = Array.Empty<TrendPoint>();
}

public sealed class ProgressFilterState
{
    public DateTime StartDateLocal { get; init; }
    public DateTime EndDateLocal { get; init; }
    public string DateRangeSummaryText { get; init; } = "Date range: -";
}

public sealed class ProgressComparisonViewState
{
    public string CurrentSessionDateText { get; init; } = "Date: -";
    public string CurrentSessionAttemptsText { get; init; } = "Attempts: -";
    public string CurrentSessionOverallText { get; init; } = "Overall Avg: -";
    public string CurrentSessionConfidenceText { get; init; } = "Confidence Avg: -";
    public string PreviousSessionDateText { get; init; } = "Date: -";
    public string PreviousSessionAttemptsText { get; init; } = "Attempts: -";
    public string PreviousSessionOverallText { get; init; } = "Overall Avg: -";
    public string PreviousSessionConfidenceText { get; init; } = "Confidence Avg: -";
    public string OverallDeltaText { get; init; } = "Overall delta: -";
    public string ConfidenceDeltaText { get; init; } = "Confidence delta: -";
    public string ConfidenceMovementText { get; init; } = "Band movement: -";
    public string ComparisonNarrativeText { get; init; } = "Comparison narrative: -";
    public string NormalizationModeText { get; init; } = "Normalization: -";
    public IReadOnlyList<TargetComparisonItem> TargetComparisons { get; init; } = Array.Empty<TargetComparisonItem>();
    public IReadOnlyList<ProgressSummaryBadge> SummaryBadges { get; init; } = Array.Empty<ProgressSummaryBadge>();
    public IReadOnlyList<ProgressComparisonLegendItem> ConfidenceLegendItems { get; init; } = Array.Empty<ProgressComparisonLegendItem>();
    public IReadOnlyList<ProgressTimelineRow> TimelineRows { get; init; } = Array.Empty<ProgressTimelineRow>();
}

public sealed class ProgressPageViewModel
{
    public static (DateTime StartUtc, DateTime EndUtc) BuildUtcDateRange(DateTime startLocalDate, DateTime endLocalDate)
    {
        var normalizedStart = startLocalDate.Date;
        var normalizedEnd = endLocalDate.Date;

        if (normalizedEnd < normalizedStart)
        {
            (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        }

        var localStart = DateTime.SpecifyKind(normalizedStart, DateTimeKind.Local);
        var localEndExclusive = DateTime.SpecifyKind(normalizedEnd.AddDays(1), DateTimeKind.Local);

        return (localStart.ToUniversalTime(), localEndExclusive.ToUniversalTime().AddTicks(-1));
    }

    public ProgressFilterState BuildFilterState(DateTime? requestedStartDate, DateTime? requestedEndDate, DateTime referenceDateLocal)
    {
        var reference = referenceDateLocal.Date;
        var fallbackStart = reference.AddDays(-30);
        var start = (requestedStartDate ?? fallbackStart).Date;
        var end = (requestedEndDate ?? reference).Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        return new ProgressFilterState
        {
            StartDateLocal = start,
            EndDateLocal = end,
            DateRangeSummaryText = $"Date range: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}"
        };
    }

    public IReadOnlyList<ProgressEntry> FilterEntries(IReadOnlyList<ProgressEntry> allEntries, string filterText)
    {
        var source = allEntries ?? Array.Empty<ProgressEntry>();
        var normalizedFilter = (filterText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedFilter))
        {
            return source;
        }

        return source
            .Where(entry => entry.TargetSound.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<ProgressEntry> FilterEntriesByDateRange(
        IReadOnlyList<ProgressEntry> entries,
        DateTime startLocalDate,
        DateTime endLocalDate)
    {
        var source = entries ?? Array.Empty<ProgressEntry>();
        var (startUtc, endUtc) = BuildUtcDateRange(startLocalDate, endLocalDate);

        return source
            .Where(entry => entry.Timestamp >= startUtc && entry.Timestamp <= endUtc)
            .ToArray();
    }

    public ProgressTrendViewState BuildTrendViewState(
        IReadOnlyList<ProgressEntry> filteredEntries,
        TrendAnalysisService trendAnalysisService,
        ConfidenceThresholds thresholds,
        int maxPoints = 12)
    {
        if (trendAnalysisService is null)
        {
            throw new ArgumentNullException(nameof(trendAnalysisService));
        }

        var points = trendAnalysisService.BuildTrendPoints(filteredEntries, maxPoints);
        return new ProgressTrendViewState
        {
            TrajectoryText = trendAnalysisService.InterpretTrajectory(filteredEntries),
            ModerateThresholdText = $"Moderate threshold: {thresholds.ModerateThreshold:P0}",
            HighThresholdText = $"High threshold: {thresholds.HighThreshold:P0}",
            TrendPoints = ApplyThresholdGuides(points, thresholds)
        };
    }

    public ProgressComparisonViewState BuildComparisonViewState(SessionComparisonSnapshot snapshot, string narrative)
    {
        var safeSnapshot = snapshot ?? new SessionComparisonSnapshot();
        var safeNarrative = string.IsNullOrWhiteSpace(narrative) ? "No comparison narrative is available yet." : narrative.Trim();
        var legendItems = BuildConfidenceLegendItems();
        var badges = BuildSummaryBadges(safeSnapshot.TargetComparisons);

        if (!safeSnapshot.HasCurrentSession)
        {
            return new ProgressComparisonViewState
            {
                ComparisonNarrativeText = $"Comparison narrative: {safeNarrative}",
                SummaryBadges = badges,
                ConfidenceLegendItems = legendItems
            };
        }

        var state = new ProgressComparisonViewState
        {
            CurrentSessionDateText = $"Date: {safeSnapshot.CurrentSessionDate:yyyy-MM-dd}",
            CurrentSessionAttemptsText = $"Attempts: {safeSnapshot.CurrentAttemptCount}",
            CurrentSessionOverallText = $"Overall Avg: {safeSnapshot.CurrentAverageOverall:P0}",
            CurrentSessionConfidenceText = $"Confidence Avg: {safeSnapshot.CurrentAverageConfidence:P0}",
            ComparisonNarrativeText = $"Comparison narrative: {safeNarrative}",
            NormalizationModeText = $"Normalization: {safeSnapshot.NormalizationMode.ToDisplayLabel()}",
            TargetComparisons = safeSnapshot.TargetComparisons,
            SummaryBadges = badges,
            ConfidenceLegendItems = legendItems,
            TimelineRows = safeSnapshot.RollingTimeline.Select(BuildTimelineRow).ToArray()
        };

        if (!safeSnapshot.HasPreviousSession)
        {
            return new ProgressComparisonViewState
            {
                CurrentSessionDateText = state.CurrentSessionDateText,
                CurrentSessionAttemptsText = state.CurrentSessionAttemptsText,
                CurrentSessionOverallText = state.CurrentSessionOverallText,
                CurrentSessionConfidenceText = state.CurrentSessionConfidenceText,
                PreviousSessionDateText = "Date: -",
                PreviousSessionAttemptsText = "Attempts: -",
                PreviousSessionOverallText = "Overall Avg: -",
                PreviousSessionConfidenceText = "Confidence Avg: -",
                OverallDeltaText = "Overall delta: n/a",
                ConfidenceDeltaText = "Confidence delta: n/a",
                ConfidenceMovementText = "Band movement: baseline only",
                ComparisonNarrativeText = state.ComparisonNarrativeText,
                NormalizationModeText = state.NormalizationModeText,
                TargetComparisons = state.TargetComparisons,
                SummaryBadges = state.SummaryBadges,
                ConfidenceLegendItems = state.ConfidenceLegendItems,
                TimelineRows = state.TimelineRows
            };
        }

        return new ProgressComparisonViewState
        {
            CurrentSessionDateText = state.CurrentSessionDateText,
            CurrentSessionAttemptsText = state.CurrentSessionAttemptsText,
            CurrentSessionOverallText = state.CurrentSessionOverallText,
            CurrentSessionConfidenceText = state.CurrentSessionConfidenceText,
            PreviousSessionDateText = $"Date: {safeSnapshot.PreviousSessionDate:yyyy-MM-dd}",
            PreviousSessionAttemptsText = $"Attempts: {safeSnapshot.PreviousAttemptCount}",
            PreviousSessionOverallText = $"Overall Avg: {safeSnapshot.PreviousAverageOverall:P0}",
            PreviousSessionConfidenceText = $"Confidence Avg: {safeSnapshot.PreviousAverageConfidence:P0}",
            OverallDeltaText = $"Overall delta: {safeSnapshot.OverallDelta:+0%;-0%;0%}",
            ConfidenceDeltaText = $"Confidence delta: {safeSnapshot.ConfidenceDelta:+0%;-0%;0%}",
            ConfidenceMovementText = $"Band movement: {safeSnapshot.ConfidenceBandMovementSummary}",
            ComparisonNarrativeText = state.ComparisonNarrativeText,
            NormalizationModeText = state.NormalizationModeText,
            TargetComparisons = state.TargetComparisons,
            SummaryBadges = state.SummaryBadges,
            ConfidenceLegendItems = state.ConfidenceLegendItems,
            TimelineRows = state.TimelineRows
        };
    }

    private static IReadOnlyList<ProgressSummaryBadge> BuildSummaryBadges(IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        if (targetComparisons.Count == 0)
        {
            return Array.Empty<ProgressSummaryBadge>();
        }

        var strongestImproved = targetComparisons
            .OrderByDescending(item => item.OverallDelta)
            .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
            .First();

        var mostVariable = targetComparisons
            .OrderByDescending(item => item.VariabilityIndex)
            .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
            .First();

        return new[]
        {
            new ProgressSummaryBadge
            {
                Title = "Strongest Improved",
                Value = $"{strongestImproved.TargetSound} ({strongestImproved.OverallDelta:+0%;-0%;0%})",
                BackgroundColor = strongestImproved.CurrentConfidenceBand.ToChipBackgroundColor(),
                BorderColor = strongestImproved.CurrentConfidenceBand.ToChipBorderColor()
            },
            new ProgressSummaryBadge
            {
                Title = "Most Variable",
                Value = $"{mostVariable.TargetSound} ({mostVariable.VariabilityIndex:0.00} variability)",
                BackgroundColor = mostVariable.CurrentConfidenceBand.ToChipBackgroundColor(),
                BorderColor = mostVariable.CurrentConfidenceBand.ToChipBorderColor()
            }
        };
    }

    private static IReadOnlyList<ProgressComparisonLegendItem> BuildConfidenceLegendItems()
    {
        return new[]
        {
            new ProgressComparisonLegendItem
            {
                Title = "High confidence",
                BackgroundColor = ConfidenceBand.High.ToChipBackgroundColor(),
                BorderColor = ConfidenceBand.High.ToChipBorderColor()
            },
            new ProgressComparisonLegendItem
            {
                Title = "Moderate confidence",
                BackgroundColor = ConfidenceBand.Moderate.ToChipBackgroundColor(),
                BorderColor = ConfidenceBand.Moderate.ToChipBorderColor()
            },
            new ProgressComparisonLegendItem
            {
                Title = "Low confidence",
                BackgroundColor = ConfidenceBand.Low.ToChipBackgroundColor(),
                BorderColor = ConfidenceBand.Low.ToChipBorderColor()
            }
        };
    }

    private static IReadOnlyList<TrendPoint> ApplyThresholdGuides(
        IReadOnlyList<TrendPoint> trendPoints,
        ConfidenceThresholds thresholds)
    {
        var moderateOffset = 40 + (220 * thresholds.ModerateThreshold);
        var highOffset = 40 + (220 * thresholds.HighThreshold);

        return trendPoints
            .Select(point => new TrendPoint
            {
                AttemptIndex = point.AttemptIndex,
                Score = point.Score,
                BarWidth = point.BarWidth,
                ConfidenceScore = point.ConfidenceScore,
                ConfidenceBarWidth = point.ConfidenceBarWidth,
                ModerateGuideOffset = moderateOffset,
                HighGuideOffset = highOffset
            })
            .ToArray();
    }

    private static ProgressTimelineRow BuildTimelineRow(SessionTimelineItem item)
    {
        return new ProgressTimelineRow
        {
            SessionDateText = item.SessionDate.ToString("yyyy-MM-dd"),
            AttemptsText = $"Attempts: {item.AttemptCount}",
            OverallText = $"Overall: {item.AverageOverall:P0}",
            ConfidenceText = $"Confidence: {item.AverageConfidence:P0}",
            DeltaText = item.HasComparisonBaseline
                ? $"Delta vs prior: {item.OverallDeltaFromPreviousSession:+0%;-0%;0%} overall, {item.ConfidenceDeltaFromPreviousSession:+0%;-0%;0%} confidence"
                : "Delta vs prior: baseline"
        };
    }
}
