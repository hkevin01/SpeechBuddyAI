using SpeechBuddyAI.Models;
using SpeechBuddyAI.Pages.ViewModels;

namespace SpeechBuddyAI.Tests;

public sealed class ProgressPageViewModelTests
{
    [Fact]
    public void FilterEntries_MatchesTargetSoundCaseInsensitive()
    {
        var viewModel = new ProgressPageViewModel();
        var entries = new[]
        {
            new ProgressEntry { TargetSound = "R" },
            new ProgressEntry { TargetSound = "s" }
        };

        var filtered = viewModel.FilterEntries(entries, "r");

        Assert.Single(filtered);
        Assert.Equal("R", filtered[0].TargetSound);
    }

    [Fact]
    public void BuildComparisonViewState_ProvidesLegendAndPlaceholdersForEmptySnapshot()
    {
        var viewModel = new ProgressPageViewModel();

        var state = viewModel.BuildComparisonViewState(new SessionComparisonSnapshot(), "No session comparison is available yet.");

        Assert.Equal("Date: -", state.CurrentSessionDateText);
        Assert.Equal("Normalization: -", state.NormalizationModeText);
        Assert.Equal(3, state.ConfidenceLegendItems.Count);
        Assert.Equal("High confidence", state.ConfidenceLegendItems[0].Title);
        Assert.Empty(state.SummaryBadges);
        Assert.Empty(state.TimelineRows);
    }

    [Fact]
    public void BuildComparisonViewState_IncludesNormalizationAndTimelineRows()
    {
        var viewModel = new ProgressPageViewModel();
        var snapshot = new SessionComparisonSnapshot
        {
            HasCurrentSession = true,
            HasPreviousSession = true,
            CurrentSessionDate = new DateTime(2026, 6, 21),
            CurrentAttemptCount = 4,
            CurrentAverageOverall = 0.82,
            CurrentAverageConfidence = 0.76,
            PreviousSessionDate = new DateTime(2026, 6, 20),
            PreviousAttemptCount = 3,
            PreviousAverageOverall = 0.70,
            PreviousAverageConfidence = 0.65,
            NormalizationMode = SessionComparisonNormalizationMode.DayWeighted,
            TargetComparisons =
            [
                new TargetComparisonItem
                {
                    TargetSound = "r",
                    CurrentAverageOverall = 0.82,
                    PreviousAverageOverall = 0.70,
                    CurrentConfidenceBand = ConfidenceBand.High,
                    PreviousConfidenceBand = ConfidenceBand.Moderate
                }
            ],
            RollingTimeline =
            [
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 21),
                    AttemptCount = 4,
                    AverageOverall = 0.82,
                    AverageConfidence = 0.76,
                    HasComparisonBaseline = true,
                    OverallDeltaFromPreviousSession = 0.12,
                    ConfidenceDeltaFromPreviousSession = 0.11
                },
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 20),
                    AttemptCount = 3,
                    AverageOverall = 0.70,
                    AverageConfidence = 0.65,
                    HasComparisonBaseline = false
                }
            ]
        };

        var state = viewModel.BuildComparisonViewState(snapshot, "Strong upward improvement overall.");

        Assert.Equal("Normalization: Day-weighted averages", state.NormalizationModeText);
        Assert.Equal("Band movement: No confidence band movement detected.", state.ConfidenceMovementText);
        Assert.Equal(4, state.SummaryBadges.Count);
        Assert.Equal("Strongest Improved", state.SummaryBadges[0].Title);
        Assert.Equal("Most Variable", state.SummaryBadges[1].Title);
        Assert.Equal("Stabilizing", state.SummaryBadges[2].Title);
        Assert.Equal("Regression Risk", state.SummaryBadges[3].Title);
        Assert.Equal(2, state.TimelineRows.Count);
        Assert.Contains("+12%", state.TimelineRows[0].DeltaText);
    }

    [Fact]
    public void BuildComparisonViewState_BaselineOnlyTimelineRowShowsBaselineText()
    {
        var viewModel = new ProgressPageViewModel();
        var snapshot = new SessionComparisonSnapshot
        {
            HasCurrentSession = true,
            CurrentSessionDate = new DateTime(2026, 6, 21),
            CurrentAttemptCount = 2,
            CurrentAverageOverall = 0.75,
            CurrentAverageConfidence = 0.70,
            NormalizationMode = SessionComparisonNormalizationMode.AttemptWeighted,
            RollingTimeline =
            [
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 21),
                    AttemptCount = 2,
                    AverageOverall = 0.75,
                    AverageConfidence = 0.70,
                    HasComparisonBaseline = false
                }
            ]
        };

        var state = viewModel.BuildComparisonViewState(snapshot, "Baseline narrative.");

        Assert.Single(state.TimelineRows);
        Assert.Equal("Delta vs prior: baseline", state.TimelineRows[0].DeltaText);
        Assert.Equal("Overall delta: n/a", state.OverallDeltaText);
    }

    [Fact]
    public void BuildFilterState_SwapsInvertedDatesAndBuildsSummary()
    {
        var viewModel = new ProgressPageViewModel();

        var filterState = viewModel.BuildFilterState(new DateTime(2026, 6, 20), new DateTime(2026, 6, 5), new DateTime(2026, 6, 22));

        Assert.Equal(new DateTime(2026, 6, 5), filterState.StartDateLocal);
        Assert.Equal(new DateTime(2026, 6, 20), filterState.EndDateLocal);
        Assert.Equal("Date range: 2026-06-05 to 2026-06-20", filterState.DateRangeSummaryText);
    }

    [Fact]
    public void FilterEntriesByDateRange_KeepsOnlyEntriesInsideUtcRange()
    {
        var viewModel = new ProgressPageViewModel();
        var entries = new[]
        {
            new ProgressEntry { TargetSound = "r", Timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc) },
            new ProgressEntry { TargetSound = "s", Timestamp = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc) },
            new ProgressEntry { TargetSound = "l", Timestamp = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc) }
        };

        var filtered = viewModel.FilterEntriesByDateRange(entries, new DateTime(2026, 6, 10), new DateTime(2026, 6, 20));

        Assert.Single(filtered);
        Assert.Equal("s", filtered[0].TargetSound);
    }

    [Fact]
    public void BuildTrendViewState_AppliesThresholdTextAndGuides()
    {
        var viewModel = new ProgressPageViewModel();
        var trendService = new Services.TrendAnalysisService();
        var entries = new[]
        {
            new ProgressEntry { OverallScore = 0.40, ConfidenceScore = 0.50, Timestamp = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc) },
            new ProgressEntry { OverallScore = 0.70, ConfidenceScore = 0.78, Timestamp = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc) }
        };

        var state = viewModel.BuildTrendViewState(entries, trendService, new Services.Confidence.ConfidenceThresholds(0.60, 0.80));

        Assert.Equal("Moderate threshold: 60%", state.ModerateThresholdText);
        Assert.Equal("High threshold: 80%", state.HighThresholdText);
        Assert.Equal(2, state.TrendPoints.Count);
        Assert.All(state.TrendPoints, point => Assert.True(point.ModerateGuideOffset > 0));
    }
}
