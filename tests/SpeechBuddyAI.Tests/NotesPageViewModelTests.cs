using SpeechBuddyAI.Models;
using SpeechBuddyAI.Pages.ViewModels;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Tests;

public sealed class NotesPageViewModelTests
{
    [Fact]
    public void ResolveExportCandidate_PrefersPendingThenSelectedThenRecent()
    {
        var vm = new NotesPageViewModel();
        var recent = new SessionNote { SoapSummary = "recent" };
        var selected = new SessionNote { SoapSummary = "selected" };
        var pending = new SessionNote { SoapSummary = "pending" };

        vm.SetSelectedHistoryNote(selected);
        Assert.Equal("selected", vm.ResolveExportCandidate(recent)?.SoapSummary);

        vm.SetGeneratedNote(pending);
        Assert.Equal("pending", vm.ResolveExportCandidate(recent)?.SoapSummary);
    }

    [Theory]
    [InlineData(0, ReportExportFormat.PlainText)]
    [InlineData(1, ReportExportFormat.Markdown)]
    [InlineData(2, ReportExportFormat.CsvSummary)]
    [InlineData(99, ReportExportFormat.PlainText)]
    public void ExportFormatFromPickerIndex_MapsExpectedValues(int pickerIndex, ReportExportFormat expected)
    {
        Assert.Equal(expected, NotesPageViewModel.ExportFormatFromPickerIndex(pickerIndex));
    }

    [Fact]
    public void BuildUtcDateRange_SwapsInvertedDatesAndReturnsInclusiveEnd()
    {
        var start = new DateTime(2026, 6, 20);
        var end = new DateTime(2026, 6, 1);

        var (startUtc, endUtc) = NotesPageViewModel.BuildUtcDateRange(start, end);

        Assert.True(startUtc <= endUtc);
        Assert.True((endUtc - startUtc) >= TimeSpan.FromDays(18));
    }

    [Fact]
    public void BuildComparisonPreviewState_UsesComparisonNarrativeAndTimeline()
    {
        var vm = new NotesPageViewModel();
        var snapshot = new SessionComparisonSnapshot
        {
            HasCurrentSession = true,
            HasPreviousSession = true,
            CurrentSessionDate = new DateTime(2026, 6, 21),
            CurrentAttemptCount = 3,
            CurrentAverageOverall = 0.82,
            CurrentAverageConfidence = 0.76,
            PreviousSessionDate = new DateTime(2026, 6, 20),
            PreviousAttemptCount = 2,
            PreviousAverageOverall = 0.70,
            PreviousAverageConfidence = 0.66,
            NormalizationMode = SessionComparisonNormalizationMode.DayWeighted,
            ComparisonNarrative = "Strong upward improvement overall. Across the last 2 sessions in view, smoothed overall moved +12%.",
            TargetComparisons =
            [
                new TargetComparisonItem
                {
                    TargetSound = "r",
                    CurrentAverageOverall = 0.82,
                    PreviousAverageOverall = 0.70,
                    CurrentConfidenceBand = ConfidenceBand.High,
                    PreviousConfidenceBand = ConfidenceBand.Moderate,
                    VariabilityIndex = 0.18
                }
            ],
            RollingTimeline =
            [
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 21),
                    AttemptCount = 3,
                    AverageOverall = 0.82,
                    AverageConfidence = 0.76,
                    HasComparisonBaseline = true,
                    OverallDeltaFromPreviousSession = 0.12,
                    ConfidenceDeltaFromPreviousSession = 0.10
                }
            ]
        };

        var state = vm.BuildComparisonPreviewState(snapshot);

        Assert.Equal("Comparison narrative: Strong upward improvement overall. Across the last 2 sessions in view, smoothed overall moved +12%.", state.ComparisonNarrativeText);
        Assert.Equal("Normalization: Day-weighted averages", state.NormalizationModeText);
        Assert.Equal(4, state.SummaryBadges.Count);
        Assert.Single(state.TimelineRows);
    }

    [Fact]
    public void BuildAssignmentAnalyticsState_ProducesSnapshotRowsAndTrendSeries()
    {
        var vm = new NotesPageViewModel();
        var snapshots = new[]
        {
            new AssignmentSnapshot
            {
                Id = 11,
                SnapshotDate = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero),
                FocusTargetsCsv = "r,s",
                TargetReasonsJson = "[{\"TargetSound\":\"r\",\"SeverityScore\":0.62,\"InstabilityScore\":0.34,\"DeclineScore\":0.18,\"FrequencyScore\":0.44,\"ConfidenceFactor\":0.79}]",
                RationaleDriftSummary = "Rationale overlap 78%; focus target changes: 1."
            }
        };

        var state = vm.BuildAssignmentAnalyticsState(snapshots);

        Assert.Single(state.SnapshotRows);
        Assert.Contains("Showing 1 snapshots", state.SummaryText);
        Assert.Contains("r", state.TargetOptions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Severity trend:", state.SeverityTrendText);
        Assert.Contains("Confidence trend:", state.ConfidenceTrendText);
        Assert.NotEmpty(state.SeverityPoints);
        Assert.NotEmpty(state.ConfidencePoints);
    }

    [Fact]
    public void BuildAssignmentAnalyticsState_RespectsSelectedTargetDrillDown()
    {
        var vm = new NotesPageViewModel();
        var snapshots = new[]
        {
            new AssignmentSnapshot
            {
                Id = 4,
                SnapshotDate = new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
                FocusTargetsCsv = "r,s",
                TargetReasonsJson = "[{\"TargetSound\":\"r\",\"SeverityScore\":0.55,\"InstabilityScore\":0.29,\"DeclineScore\":0.12,\"FrequencyScore\":0.36,\"ConfidenceFactor\":0.81},{\"TargetSound\":\"s\",\"SeverityScore\":0.63,\"InstabilityScore\":0.35,\"DeclineScore\":0.20,\"FrequencyScore\":0.42,\"ConfidenceFactor\":0.74}]"
            }
        };

        var state = vm.BuildAssignmentAnalyticsState(snapshots, selectedTarget: "s", selectedSnapshotId: 4);

        Assert.Equal("s", state.SelectedTarget, ignoreCase: true);
        Assert.Contains("0.63", state.SeverityTrendText);
        Assert.Contains("0.74", state.ConfidenceTrendText);
    }
}
