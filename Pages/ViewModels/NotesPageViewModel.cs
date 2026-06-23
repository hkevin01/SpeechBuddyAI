using SpeechBuddyAI.Models;
using SpeechBuddyAI.Pages.ViewModels;
using SpeechBuddyAI.Services.Reports;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages.ViewModels;

public sealed class NotesPageViewModel
{
    private readonly ProgressPageViewModel _comparisonPreviewViewModel = new();

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

    public ProgressComparisonViewState BuildComparisonPreviewState(SessionComparisonSnapshot snapshot)
    {
        return _comparisonPreviewViewModel.BuildComparisonViewState(snapshot, snapshot?.ComparisonNarrative ?? string.Empty);
    }

    public AssignmentAnalyticsState BuildAssignmentAnalyticsState(IReadOnlyList<AssignmentSnapshot> snapshots)
    {
        var source = snapshots ?? Array.Empty<AssignmentSnapshot>();
        if (source.Count == 0)
        {
            return new AssignmentAnalyticsState
            {
                SummaryText = "No assignment snapshots yet.",
                SnapshotRows = Array.Empty<AssignmentSnapshotRow>()
            };
        }

        var rows = source
            .OrderByDescending(snapshot => snapshot.SnapshotDateTicks)
            .Select(snapshot => new AssignmentSnapshotRow
            {
                SnapshotId = snapshot.Id,
                SnapshotDateText = snapshot.SnapshotDate.ToString("yyyy-MM-dd HH:mm 'UTC'"),
                FocusTargetsText = "Focus: " + string.Join(", ", ParseCsv(snapshot.FocusTargetsCsv)),
                DriftSummaryText = string.IsNullOrWhiteSpace(snapshot.RationaleDriftSummary)
                    ? "No drift summary available."
                    : snapshot.RationaleDriftSummary
            })
            .ToArray();

        var latest = source.OrderByDescending(snapshot => snapshot.SnapshotDateTicks).First();
        var latestReasons = AssignmentSnapshotService.ParseReasons(latest.TargetReasonsJson);

        return new AssignmentAnalyticsState
        {
            SummaryText = $"Showing {rows.Length} snapshots. Latest focus: {string.Join(", ", ParseCsv(latest.FocusTargetsCsv))}.",
            SnapshotRows = rows,
            SeverityTrendText = BuildSeriesLine("Severity", latestReasons.Select(reason => reason.SeverityScore)),
            InstabilityTrendText = BuildSeriesLine("Instability", latestReasons.Select(reason => reason.InstabilityScore)),
            DeclineTrendText = BuildSeriesLine("Decline", latestReasons.Select(reason => reason.DeclineScore)),
            FrequencyTrendText = BuildSeriesLine("Frequency", latestReasons.Select(reason => reason.FrequencyScore)),
            ConfidenceTrendText = BuildSeriesLine("Confidence", latestReasons.Select(reason => reason.ConfidenceFactor))
        };
    }

    public AssignmentAnalyticsState BuildAssignmentAnalyticsStateForSnapshot(
        AssignmentSnapshot selectedSnapshot,
        IReadOnlyList<AssignmentSnapshot> allSnapshots)
    {
        var baseState = BuildAssignmentAnalyticsState(allSnapshots);
        var reasons = AssignmentSnapshotService.ParseReasons(selectedSnapshot?.TargetReasonsJson);

        return new AssignmentAnalyticsState
        {
            SummaryText = $"Selected snapshot from {selectedSnapshot?.SnapshotDate:yyyy-MM-dd HH:mm 'UTC'}.",
            SnapshotRows = baseState.SnapshotRows,
            SeverityTrendText = BuildSeriesLine("Severity", reasons.Select(reason => reason.SeverityScore)),
            InstabilityTrendText = BuildSeriesLine("Instability", reasons.Select(reason => reason.InstabilityScore)),
            DeclineTrendText = BuildSeriesLine("Decline", reasons.Select(reason => reason.DeclineScore)),
            FrequencyTrendText = BuildSeriesLine("Frequency", reasons.Select(reason => reason.FrequencyScore)),
            ConfidenceTrendText = BuildSeriesLine("Confidence", reasons.Select(reason => reason.ConfidenceFactor))
        };
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

    private static string BuildSeriesLine(string label, IEnumerable<double> points)
    {
        var values = (points ?? Array.Empty<double>())
            .Select(value => Math.Clamp(value, 0.0, 1.0))
            .ToArray();

        if (values.Length == 0)
        {
            return $"{label} trend: -";
        }

        var series = string.Join(" -> ", values.Select(value => value.ToString("0.00")));
        return $"{label} trend: {series}";
    }

    private static IReadOnlyList<string> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class AssignmentAnalyticsState
{
    public string SummaryText { get; init; } = "No assignment snapshots yet.";
    public IReadOnlyList<AssignmentSnapshotRow> SnapshotRows { get; init; } = Array.Empty<AssignmentSnapshotRow>();
    public string SeverityTrendText { get; init; } = "Severity trend: -";
    public string InstabilityTrendText { get; init; } = "Instability trend: -";
    public string DeclineTrendText { get; init; } = "Decline trend: -";
    public string FrequencyTrendText { get; init; } = "Frequency trend: -";
    public string ConfidenceTrendText { get; init; } = "Confidence trend: -";
}

public sealed class AssignmentSnapshotRow
{
    public int SnapshotId { get; init; }
    public string SnapshotDateText { get; init; } = string.Empty;
    public string FocusTargetsText { get; init; } = string.Empty;
    public string DriftSummaryText { get; init; } = string.Empty;
}
