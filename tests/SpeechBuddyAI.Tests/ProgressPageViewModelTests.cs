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
        Assert.Equal(2, state.TimelineRows.Count);
        Assert.Contains("+12%", state.TimelineRows[0].DeltaText);
    }
}
