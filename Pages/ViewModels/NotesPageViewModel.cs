using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Pages.ViewModels;

public sealed class NotesPageViewModel
{
    public SessionNote? PendingNote { get; private set; }
    public SessionNote? SelectedHistoryNote { get; private set; }

    public void SetGeneratedNote(SessionNote note)
    {
        PendingNote = note ?? throw new ArgumentNullException(nameof(note));
        SelectedHistoryNote = null;
    }

    public void SetSelectedHistoryNote(SessionNote note)
    {
        SelectedHistoryNote = note ?? throw new ArgumentNullException(nameof(note));
        PendingNote = null;
    }

    public void MarkSaved()
    {
        PendingNote = null;
    }

    public SessionNote? ResolveExportCandidate(SessionNote? mostRecentSaved)
    {
        return PendingNote ?? SelectedHistoryNote ?? mostRecentSaved;
    }

    public static ReportExportFormat ExportFormatFromPickerIndex(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => ReportExportFormat.Markdown,
            2 => ReportExportFormat.CsvSummary,
            _ => ReportExportFormat.PlainText
        };
    }

    public static int PickerIndexFromExportFormat(ReportExportFormat format)
    {
        return format switch
        {
            ReportExportFormat.Markdown => 1,
            ReportExportFormat.CsvSummary => 2,
            _ => 0
        };
    }

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

        var startUtc = localStart.ToUniversalTime();
        var endUtc = localEndExclusive.ToUniversalTime().AddTicks(-1);

        return (startUtc, endUtc);
    }
}
