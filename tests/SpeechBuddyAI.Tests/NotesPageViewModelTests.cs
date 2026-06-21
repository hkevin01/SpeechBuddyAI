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
}
