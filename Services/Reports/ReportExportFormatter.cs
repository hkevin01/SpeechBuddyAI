using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.Reports;

public static class ReportExportFormatter
{
    public static string BuildContent(
        SessionNote note,
        IReadOnlyList<ProgressEntry> metadataEntries,
        ReportExportFormat format,
        SessionComparisonNormalizationMode normalizationMode = SessionComparisonNormalizationMode.AttemptWeighted)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }

        var entries = metadataEntries ?? Array.Empty<ProgressEntry>();

        return format switch
        {
            ReportExportFormat.Markdown => BuildMarkdown(note, entries, normalizationMode),
            ReportExportFormat.CsvSummary => BuildCsvSummary(note, entries, normalizationMode),
            _ => BuildPlainText(note, entries, normalizationMode)
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

    private static string BuildPlainText(
        SessionNote note,
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var snapshot = BuildComparisonSnapshot(entries, normalizationMode);
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
            $"Session Comparison: {BuildSessionComparisonOverview(snapshot)}" + Environment.NewLine +
            $"Confidence Movement: {snapshot.ConfidenceBandMovementSummary}" + Environment.NewLine +
            $"Comparison Normalization: {normalizationMode.ToDisplayLabel()}" + Environment.NewLine +
            Environment.NewLine +
            "Comparison Narrative" + Environment.NewLine +
            "--------------------" + Environment.NewLine +
            SafeValue(snapshot.ComparisonNarrative) + Environment.NewLine +
            Environment.NewLine +
            "Rolling Session History" + Environment.NewLine +
            "-----------------------" + Environment.NewLine +
            BuildPlainTextTimeline(snapshot.RollingTimeline) + Environment.NewLine +
            Environment.NewLine +
            "Per-Target Comparison" + Environment.NewLine +
            "---------------------" + Environment.NewLine +
            BuildPlainTextTargetComparison(snapshot.TargetComparisons) + Environment.NewLine +
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

    private static string BuildMarkdown(
        SessionNote note,
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var snapshot = BuildComparisonSnapshot(entries, normalizationMode);
        return
            "# SpeechBuddyAI Session Report" + Environment.NewLine +
            Environment.NewLine +
            $"- Session Date: {sessionDate}" + Environment.NewLine +
            $"- Scoring Providers: {BuildProviderSummary(entries)}" + Environment.NewLine +
            $"- Confidence Bands: {BuildConfidenceBandSummary(entries)}" + Environment.NewLine +
            $"- Target-Level Deltas: {BuildTargetDeltaSummary(entries)}" + Environment.NewLine +
            $"- Session Comparison: {BuildSessionComparisonOverview(snapshot)}" + Environment.NewLine +
            $"- Confidence Movement: {snapshot.ConfidenceBandMovementSummary}" + Environment.NewLine +
            $"- Comparison Normalization: {normalizationMode.ToDisplayLabel()}" + Environment.NewLine +
            Environment.NewLine +
            "## Comparison Narrative" + Environment.NewLine +
            Environment.NewLine +
            SafeValue(snapshot.ComparisonNarrative) + Environment.NewLine +
            Environment.NewLine +
            "## Rolling Session History" + Environment.NewLine +
            Environment.NewLine +
            BuildMarkdownTimeline(snapshot.RollingTimeline) + Environment.NewLine +
            Environment.NewLine +
            "## Per-Target Comparison" + Environment.NewLine +
            Environment.NewLine +
            BuildMarkdownTargetComparison(snapshot.TargetComparisons) + Environment.NewLine +
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

    private static string BuildCsvSummary(
        SessionNote note,
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode)
    {
        var sessionDate = note.SessionDate.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var snapshot = BuildComparisonSnapshot(entries, normalizationMode);
        var lines = new List<string>
        {
            "Metric,Value",
            CsvLine("SessionDate", sessionDate),
            CsvLine("ScoringProviders", BuildProviderSummary(entries)),
            CsvLine("ConfidenceBands", BuildConfidenceBandSummary(entries)),
            CsvLine("TargetLevelDeltas", BuildTargetDeltaSummary(entries)),
            CsvLine("SessionComparison", BuildSessionComparisonOverview(snapshot)),
            CsvLine("ConfidenceMovement", snapshot.ConfidenceBandMovementSummary),
            CsvLine("ComparisonNormalization", normalizationMode.ToDisplayLabel()),
            CsvLine("ComparisonNarrative", SafeValue(snapshot.ComparisonNarrative)),
            CsvLine("RollingSessionHistory", BuildCsvTimeline(snapshot.RollingTimeline)),
            CsvLine("TargetComparisonTable", BuildCsvTargetComparison(snapshot.TargetComparisons)),
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

    private static SessionComparisonSnapshot BuildComparisonSnapshot(
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode)
    {
        var snapshot = new SessionComparisonService().Build(entries, normalizationMode);
        var narrative = new ComparisonNarrativeGenerator(new TrendAnalysisService()).Generate(snapshot);

        return new SessionComparisonSnapshot
        {
            HasCurrentSession = snapshot.HasCurrentSession,
            HasPreviousSession = snapshot.HasPreviousSession,
            CurrentSessionDate = snapshot.CurrentSessionDate,
            CurrentAttemptCount = snapshot.CurrentAttemptCount,
            CurrentAverageOverall = snapshot.CurrentAverageOverall,
            CurrentAverageConfidence = snapshot.CurrentAverageConfidence,
            PreviousSessionDate = snapshot.PreviousSessionDate,
            PreviousAttemptCount = snapshot.PreviousAttemptCount,
            PreviousAverageOverall = snapshot.PreviousAverageOverall,
            PreviousAverageConfidence = snapshot.PreviousAverageConfidence,
            NormalizationMode = snapshot.NormalizationMode,
            TargetComparisons = snapshot.TargetComparisons,
            ConfidenceBandTransitions = snapshot.ConfidenceBandTransitions,
            RollingTimeline = snapshot.RollingTimeline,
            ComparisonNarrative = narrative
        };
    }

    private static string BuildSessionComparisonOverview(SessionComparisonSnapshot snapshot)
    {
        if (!snapshot.HasCurrentSession)
        {
            return "n/a";
        }

        if (!snapshot.HasPreviousSession)
        {
            return "Baseline only - no previous session available.";
        }

        return $"Overall {snapshot.OverallDelta:+0%;-0%;0%} | Confidence {snapshot.ConfidenceDelta:+0%;-0%;0%}";
    }

    private static string BuildPlainTextTargetComparison(IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        if (targetComparisons.Count == 0)
        {
            return "n/a";
        }

        return string.Join(
            Environment.NewLine,
            targetComparisons.Select(item =>
                $"- {item.TargetSound}: {item.OverallDelta:+0%;-0%;0%} overall, {item.ConfidenceMovementLabel}, current {item.CurrentAverageOverall:P0}, previous {item.PreviousAverageOverall:P0}"));
    }

    private static string BuildMarkdownTargetComparison(IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        if (targetComparisons.Count == 0)
        {
            return "n/a";
        }

        var lines = new List<string>
        {
            "| Target | Delta | Confidence Move | Current Avg | Previous Avg |",
            "| --- | --- | --- | --- | --- |"
        };

        lines.AddRange(targetComparisons.Select(item =>
            $"| {item.TargetSound} | {item.OverallDelta:+0%;-0%;0%} | {item.ConfidenceMovementLabel} | {item.CurrentAverageOverall:P0} | {item.PreviousAverageOverall:P0} |"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildCsvTargetComparison(IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        if (targetComparisons.Count == 0)
        {
            return "n/a";
        }

        return string.Join(
            " | ",
            targetComparisons.Select(item =>
                $"{item.TargetSound}:{item.OverallDelta:+0%;-0%;0%} ({item.ConfidenceMovementLabel})"));
    }

    private static string BuildPlainTextTimeline(IReadOnlyList<SessionTimelineItem> rollingTimeline)
    {
        if (rollingTimeline.Count == 0)
        {
            return "n/a";
        }

        return string.Join(
            Environment.NewLine,
            rollingTimeline.Select(item =>
                $"- {item.SessionDate:yyyy-MM-dd}: attempts {item.AttemptCount}, overall {item.AverageOverall:P0}, confidence {item.AverageConfidence:P0}, {(item.HasComparisonBaseline ? $"delta {item.OverallDeltaFromPreviousSession:+0%;-0%;0%} overall / {item.ConfidenceDeltaFromPreviousSession:+0%;-0%;0%} confidence" : "baseline")}"));
    }

    private static string BuildMarkdownTimeline(IReadOnlyList<SessionTimelineItem> rollingTimeline)
    {
        if (rollingTimeline.Count == 0)
        {
            return "n/a";
        }

        var lines = new List<string>
        {
            "| Session | Attempts | Overall | Confidence | Delta vs Prior |",
            "| --- | --- | --- | --- | --- |"
        };

        lines.AddRange(rollingTimeline.Select(item =>
            $"| {item.SessionDate:yyyy-MM-dd} | {item.AttemptCount} | {item.AverageOverall:P0} | {item.AverageConfidence:P0} | {(item.HasComparisonBaseline ? $"{item.OverallDeltaFromPreviousSession:+0%;-0%;0%} overall / {item.ConfidenceDeltaFromPreviousSession:+0%;-0%;0%} confidence" : "baseline")} |"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildCsvTimeline(IReadOnlyList<SessionTimelineItem> rollingTimeline)
    {
        if (rollingTimeline.Count == 0)
        {
            return "n/a";
        }

        return string.Join(
            " | ",
            rollingTimeline.Select(item =>
                $"{item.SessionDate:yyyy-MM-dd}:{item.AttemptCount} attempts,{item.AverageOverall:P0} overall,{item.AverageConfidence:P0} confidence,{(item.HasComparisonBaseline ? $"{item.OverallDeltaFromPreviousSession:+0%;-0%;0%} overall/{item.ConfidenceDeltaFromPreviousSession:+0%;-0%;0%} confidence" : "baseline")}"));
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
