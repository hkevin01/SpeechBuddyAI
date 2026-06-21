using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.Reports;

public static class ReportExportFormatter
{
    public static string BuildContent(SessionNote note, IReadOnlyList<ProgressEntry> metadataEntries, ReportExportFormat format)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }

        var entries = metadataEntries ?? Array.Empty<ProgressEntry>();

        return format switch
        {
            ReportExportFormat.Markdown => BuildMarkdown(note, entries),
            ReportExportFormat.CsvSummary => BuildCsvSummary(note, entries),
            _ => BuildPlainText(note, entries)
        };
    }

    public static string BuildFileName(SessionNote note, ReportExportFormat format)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }

        var safeDate = note.SessionDateTicks == 0 ? DateTimeOffset.UtcNow : note.SessionDate;
        var extension = format switch
        {
            ReportExportFormat.Markdown => "md",
            ReportExportFormat.CsvSummary => "csv",
            _ => "txt"
        };

        return $"speechbuddy-report-{safeDate:yyyyMMdd-HHmmss}.{extension}";
    }

    private static string BuildPlainText(SessionNote note, IReadOnlyList<ProgressEntry> entries)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return
            "SpeechBuddyAI Session Report" + Environment.NewLine +
            "===========================" + Environment.NewLine +
            $"Session Date: {sessionDate}" + Environment.NewLine +
            Environment.NewLine +
            "Metadata" + Environment.NewLine +
            "--------" + Environment.NewLine +
            $"Scoring Providers: {BuildProviderSummary(entries)}" + Environment.NewLine +
            $"Confidence Bands: {BuildConfidenceBandSummary(entries)}" + Environment.NewLine +
            $"Target-Level Deltas: {BuildTargetDeltaSummary(entries)}" + Environment.NewLine +
            Environment.NewLine +
            "Raw Note" + Environment.NewLine +
            "--------" + Environment.NewLine +
            (string.IsNullOrWhiteSpace(note.RawNote) ? "(empty)" : note.RawNote.Trim()) + Environment.NewLine +
            Environment.NewLine +
            "Clinician SOAP Summary" + Environment.NewLine +
            "----------------------" + Environment.NewLine +
            SafeValue(note.SoapSummary) + Environment.NewLine +
            Environment.NewLine +
            "Parent-Friendly Summary" + Environment.NewLine +
            "-----------------------" + Environment.NewLine +
            SafeValue(note.ParentSummary) + Environment.NewLine;
    }

    private static string BuildMarkdown(SessionNote note, IReadOnlyList<ProgressEntry> entries)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return
            "# SpeechBuddyAI Session Report" + Environment.NewLine +
            Environment.NewLine +
            $"- Session Date: {sessionDate}" + Environment.NewLine +
            $"- Scoring Providers: {BuildProviderSummary(entries)}" + Environment.NewLine +
            $"- Confidence Bands: {BuildConfidenceBandSummary(entries)}" + Environment.NewLine +
            $"- Target-Level Deltas: {BuildTargetDeltaSummary(entries)}" + Environment.NewLine +
            Environment.NewLine +
            "## Raw Note" + Environment.NewLine +
            Environment.NewLine +
            (string.IsNullOrWhiteSpace(note.RawNote) ? "(empty)" : note.RawNote.Trim()) + Environment.NewLine +
            Environment.NewLine +
            "## Clinician SOAP Summary" + Environment.NewLine +
            Environment.NewLine +
            SafeValue(note.SoapSummary) + Environment.NewLine +
            Environment.NewLine +
            "## Parent-Friendly Summary" + Environment.NewLine +
            Environment.NewLine +
            SafeValue(note.ParentSummary) + Environment.NewLine;
    }

    private static string BuildCsvSummary(SessionNote note, IReadOnlyList<ProgressEntry> entries)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var lines = new List<string>
        {
            "Metric,Value",
            CsvLine("SessionDate", sessionDate),
            CsvLine("ScoringProviders", BuildProviderSummary(entries)),
            CsvLine("ConfidenceBands", BuildConfidenceBandSummary(entries)),
            CsvLine("TargetLevelDeltas", BuildTargetDeltaSummary(entries)),
            CsvLine("RawNote", string.IsNullOrWhiteSpace(note.RawNote) ? "(empty)" : note.RawNote.Trim()),
            CsvLine("SoapSummary", SafeValue(note.SoapSummary)),
            CsvLine("ParentSummary", SafeValue(note.ParentSummary))
        };

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string CsvLine(string metric, string value)
    {
        return CsvEscape(metric) + "," + CsvEscape(value);
    }

    private static string CsvEscape(string value)
    {
        var safe = value ?? string.Empty;
        if (safe.Contains('"') || safe.Contains(',') || safe.Contains('\n') || safe.Contains('\r'))
        {
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        return safe;
    }

    private static string BuildProviderSummary(IReadOnlyList<ProgressEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "n/a";
        }

        var grouped = entries
            .GroupBy(e => Normalize(e.ScoringProvider, "unknown"))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}");

        return string.Join(" | ", grouped);
    }

    private static string BuildConfidenceBandSummary(IReadOnlyList<ProgressEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "n/a";
        }

        var grouped = entries
            .GroupBy(e => Normalize(e.ConfidenceBand, "Unknown"))
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}");

        return string.Join(" | ", grouped);
    }

    private static string BuildTargetDeltaSummary(IReadOnlyList<ProgressEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "n/a";
        }

        var deltas = new List<string>();
        foreach (var group in entries.GroupBy(e => Normalize(e.TargetSound, "unknown")).OrderBy(g => g.Key))
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToArray();
            if (ordered.Length < 2)
            {
                continue;
            }

            var delta = ordered[^1].OverallScore - ordered[0].OverallScore;
            deltas.Add($"{group.Key}: {delta:+0.00;-0.00;0.00}");
        }

        return deltas.Count == 0 ? "n/a" : string.Join(" | ", deltas);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string SafeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();
    }
}
