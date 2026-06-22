using SpeechBuddyAI.Models;
using SpeechBuddyAI.Pages.ViewModels;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class ProgressComparisonEndToEndTests
{
    [Fact]
    public void DateRangeFilter_WithNoEntries_ProducesEmptyComparisonState()
    {
        var viewModel = new ProgressPageViewModel();
        var builder = CreateBuilder();
        var entries = new[]
        {
            new ProgressEntry { TargetSound = "r", Timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc), OverallScore = 0.6, ConfidenceScore = 0.65, ConfidenceBand = "Moderate" }
        };

        var dateFiltered = viewModel.FilterEntriesByDateRange(entries, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
        var snapshot = builder.Build(dateFiltered);
        var state = viewModel.BuildComparisonViewState(snapshot, snapshot.ComparisonNarrative);

        Assert.Empty(dateFiltered);
        Assert.Equal("Date: -", state.CurrentSessionDateText);
        Assert.Empty(state.TimelineRows);
        Assert.Empty(state.SummaryBadges);
    }

    [Fact]
    public void FilteredMultiSessionComparison_PreservesScopedTimelineAndBadges()
    {
        var viewModel = new ProgressPageViewModel();
        var builder = CreateBuilder();
        var entries = new[]
        {
            Entry("r", 0.45, 0.40, new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc), "Low"),
            Entry("s", 0.55, 0.58, new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("r", 0.65, 0.60, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("s", 0.50, 0.52, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc), "Low"),
            Entry("r", 0.80, 0.78, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), "High"),
            Entry("s", 0.48, 0.50, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc), "Low")
        };

        var dateFiltered = viewModel.FilterEntriesByDateRange(entries, new DateTime(2026, 6, 19), new DateTime(2026, 6, 20));
        var targetFiltered = viewModel.FilterEntries(dateFiltered, "r");
        var snapshot = builder.Build(targetFiltered, SessionComparisonNormalizationMode.DayWeighted);
        var state = viewModel.BuildComparisonViewState(snapshot, snapshot.ComparisonNarrative);

        Assert.Equal("Date: 2026-06-20", state.CurrentSessionDateText);
        Assert.Equal("Normalization: Day-weighted averages", state.NormalizationModeText);
        Assert.Equal(2, state.TimelineRows.Count);
        Assert.Equal(1, state.TargetComparisons.Count);
        Assert.Equal("Strongest Improved", state.SummaryBadges[0].Title);
        Assert.Equal("Most Variable", state.SummaryBadges[1].Title);
        Assert.Contains("Across the last 2 sessions in view", state.ComparisonNarrativeText);
    }

    private static ComparisonExportBuilderService CreateBuilder()
    {
        return new ComparisonExportBuilderService(
            new SessionComparisonService(),
            new ComparisonNarrativeGenerator(new TrendAnalysisService()));
    }

    private static ProgressEntry Entry(string target, double overall, double confidence, DateTime timestamp, string confidenceBand)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            OverallScore = overall,
            ConfidenceScore = confidence,
            ConfidenceBand = confidenceBand,
            Timestamp = timestamp
        };
    }
}
