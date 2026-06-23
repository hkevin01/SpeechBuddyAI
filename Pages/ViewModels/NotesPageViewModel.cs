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

    public AssignmentAnalyticsState BuildAssignmentAnalyticsState(
        IReadOnlyList<AssignmentSnapshot> snapshots,
        string? selectedTarget = null,
        int? selectedSnapshotId = null)
    {
        var source = snapshots ?? Array.Empty<AssignmentSnapshot>();
        if (source.Count == 0)
        {
            return new AssignmentAnalyticsState
            {
                SummaryText = "No assignment snapshots yet.",
                SnapshotRows = Array.Empty<AssignmentSnapshotRow>(),
                TargetOptions = Array.Empty<string>(),
                SeverityPoints = Array.Empty<SparklinePoint>(),
                InstabilityPoints = Array.Empty<SparklinePoint>(),
                DeclinePoints = Array.Empty<SparklinePoint>(),
                FrequencyPoints = Array.Empty<SparklinePoint>(),
                ConfidencePoints = Array.Empty<SparklinePoint>()
            };
        }

        var orderedSnapshots = source.OrderBy(snapshot => snapshot.SnapshotDateTicks).ToArray();
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

        var allTargets = orderedSnapshots
            .SelectMany(snapshot => AssignmentSnapshotService.ParseReasons(snapshot.TargetReasonsJson).Select(reason => reason.TargetSound))
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeTarget = ResolveTargetSelection(selectedTarget, allTargets);
        var selectedSnapshot = selectedSnapshotId.HasValue
            ? source.FirstOrDefault(snapshot => snapshot.Id == selectedSnapshotId.Value)
            : source.OrderByDescending(snapshot => snapshot.SnapshotDateTicks).FirstOrDefault();

        var severity = BuildComponentSeries(orderedSnapshots, activeTarget, reason => reason.SeverityScore);
        var instability = BuildComponentSeries(orderedSnapshots, activeTarget, reason => reason.InstabilityScore);
        var decline = BuildComponentSeries(orderedSnapshots, activeTarget, reason => reason.DeclineScore);
        var frequency = BuildComponentSeries(orderedSnapshots, activeTarget, reason => reason.FrequencyScore);
        var confidence = BuildComponentSeries(orderedSnapshots, activeTarget, reason => reason.ConfidenceFactor);

        var summaryPrefix = selectedSnapshot is null
            ? $"Showing {rows.Length} snapshots."
            : $"Showing {rows.Length} snapshots. Selected snapshot {selectedSnapshot.SnapshotDate:yyyy-MM-dd HH:mm 'UTC'}.";

        return new AssignmentAnalyticsState
        {
            SummaryText = $"{summaryPrefix} Drill-down target: {activeTarget}.",
            SnapshotRows = rows,
            TargetOptions = allTargets,
            SelectedTarget = activeTarget,
            SeverityTrendText = BuildSeriesLine("Severity", severity),
            InstabilityTrendText = BuildSeriesLine("Instability", instability),
            DeclineTrendText = BuildSeriesLine("Decline", decline),
            FrequencyTrendText = BuildSeriesLine("Frequency", frequency),
            ConfidenceTrendText = BuildSeriesLine("Confidence", confidence),
            SeverityPoints = BuildSparklinePoints(severity, "#2563EB"),
            InstabilityPoints = BuildSparklinePoints(instability, "#F59E0B"),
            DeclinePoints = BuildSparklinePoints(decline, "#DC2626"),
            FrequencyPoints = BuildSparklinePoints(frequency, "#7C3AED"),
            ConfidencePoints = BuildSparklinePoints(confidence, "#059669")
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

    private static IReadOnlyList<double> BuildComponentSeries(
        IReadOnlyList<AssignmentSnapshot> snapshots,
        string selectedTarget,
        Func<AssignmentFocusTargetReason, double> selector)
    {
        if (string.IsNullOrWhiteSpace(selectedTarget))
        {
            return Array.Empty<double>();
        }

        return snapshots
            .Select(snapshot => AssignmentSnapshotService.ParseReasons(snapshot.TargetReasonsJson)
                .FirstOrDefault(reason => string.Equals(reason.TargetSound, selectedTarget, StringComparison.OrdinalIgnoreCase)))
            .Where(reason => reason is not null)
            .Select(reason => Math.Clamp(selector(reason!), 0.0, 1.0))
            .ToArray();
    }

    private static IReadOnlyList<SparklinePoint> BuildSparklinePoints(IReadOnlyList<double> values, string colorHex)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<SparklinePoint>();
        }

        return values
            .Select(value => new SparklinePoint
            {
                Value = value,
                Height = 6 + (Math.Clamp(value, 0.0, 1.0) * 22),
                ColorHex = colorHex
            })
            .ToArray();
    }

    private static string ResolveTargetSelection(string? selectedTarget, IReadOnlyList<string> options)
    {
        if (options is null || options.Count == 0)
        {
            return "-";
        }

        if (!string.IsNullOrWhiteSpace(selectedTarget) &&
            options.Contains(selectedTarget, StringComparer.OrdinalIgnoreCase))
        {
            return selectedTarget;
        }

        return options[0];
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
    public IReadOnlyList<string> TargetOptions { get; init; } = Array.Empty<string>();
    public string SelectedTarget { get; init; } = "-";
    public IReadOnlyList<SparklinePoint> SeverityPoints { get; init; } = Array.Empty<SparklinePoint>();
    public IReadOnlyList<SparklinePoint> InstabilityPoints { get; init; } = Array.Empty<SparklinePoint>();
    public IReadOnlyList<SparklinePoint> DeclinePoints { get; init; } = Array.Empty<SparklinePoint>();
    public IReadOnlyList<SparklinePoint> FrequencyPoints { get; init; } = Array.Empty<SparklinePoint>();
    public IReadOnlyList<SparklinePoint> ConfidencePoints { get; init; } = Array.Empty<SparklinePoint>();
}

public sealed class AssignmentSnapshotRow
{
    public int SnapshotId { get; init; }
    public string SnapshotDateText { get; init; } = string.Empty;
    public string FocusTargetsText { get; init; } = string.Empty;
    public string DriftSummaryText { get; init; } = string.Empty;
}

public sealed class SparklinePoint
{
    public double Value { get; init; }
    public double Height { get; init; }
    public string ColorHex { get; init; } = "#2563EB";
}
