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
            ComparisonNarrative = "Strong upward improvement overall. Across the last 2 sessions in view, overall moved +12%.",
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

        Assert.Equal("Comparison narrative: Strong upward improvement overall. Across the last 2 sessions in view, overall moved +12%.", state.ComparisonNarrativeText);
        Assert.Equal("Normalization: Day-weighted averages", state.NormalizationModeText);
        Assert.Single(state.SummaryBadges);
        Assert.Single(state.TimelineRows);
    }
}
