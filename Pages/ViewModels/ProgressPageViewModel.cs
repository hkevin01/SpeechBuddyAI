using SpeechBuddyAI.Models;

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
    public IReadOnlyList<ProgressComparisonLegendItem> ConfidenceLegendItems { get; init; } = Array.Empty<ProgressComparisonLegendItem>();
    public IReadOnlyList<ProgressTimelineRow> TimelineRows { get; init; } = Array.Empty<ProgressTimelineRow>();
}

public sealed class ProgressPageViewModel
{
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

    public ProgressComparisonViewState BuildComparisonViewState(SessionComparisonSnapshot snapshot, string narrative)
    {
        var safeSnapshot = snapshot ?? new SessionComparisonSnapshot();
        var safeNarrative = string.IsNullOrWhiteSpace(narrative) ? "No comparison narrative is available yet." : narrative.Trim();
        var legendItems = BuildConfidenceLegendItems();

        if (!safeSnapshot.HasCurrentSession)
        {
            return new ProgressComparisonViewState
            {
                ComparisonNarrativeText = $"Comparison narrative: {safeNarrative}",
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
            ConfidenceLegendItems = state.ConfidenceLegendItems,
            TimelineRows = state.TimelineRows
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
