using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Tests;

public sealed class ReportExportFormatterTests
{
    [Fact]
    public void BuildContent_PlainText_IncludesMetadataHeadersAndSections()
    {
        var note = MakeNote();
        var entries = MakeEntries();

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.PlainText);

        Assert.Contains("SpeechBuddyAI Session Report", content);
        Assert.Contains("Metadata", content);
        Assert.Contains("Scoring Providers:", content);
        Assert.Contains("Confidence Bands:", content);
        Assert.Contains("Target-Level Deltas:", content);
        Assert.Contains("offline-heuristic: 2", content);
        Assert.Contains("High: 1", content);
        Assert.Contains("Moderate: 1", content);
        Assert.Contains("r: +0.20", content);
    }

    [Fact]
    public void BuildContent_Markdown_UsesMarkdownHeadingsAndBullets()
    {
        var note = MakeNote();
        var entries = MakeEntries();

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.Markdown);

        Assert.Contains("# SpeechBuddyAI Session Report", content);
        Assert.Contains("- Scoring Providers:", content);
        Assert.Contains("## Clinician SOAP Summary", content);
    }

    [Fact]
    public void BuildContent_CsvSummary_ContainsExpectedMetrics()
    {
        var note = MakeNote();
        var entries = MakeEntries();

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.CsvSummary);

        Assert.Contains("Metric,Value", content);
        Assert.Contains("ScoringProviders", content);
        Assert.Contains("ConfidenceBands", content);
        Assert.Contains("TargetLevelDeltas", content);
    }

    [Fact]
    public void BuildFileName_UsesExpectedExtensionPerFormat()
    {
        var note = MakeNote();

        var txt = ReportExportFormatter.BuildFileName(note, ReportExportFormat.PlainText);
        var md = ReportExportFormatter.BuildFileName(note, ReportExportFormat.Markdown);
        var csv = ReportExportFormatter.BuildFileName(note, ReportExportFormat.CsvSummary);

        Assert.EndsWith(".txt", txt);
        Assert.EndsWith(".md", md);
        Assert.EndsWith(".csv", csv);
        Assert.StartsWith("speechbuddy-report-", txt);
    }

    [Fact]
    public void BuildContent_EmptyMetadata_UsesNaFallbacks()
    {
        var note = MakeNote();

        var content = ReportExportFormatter.BuildContent(note, Array.Empty<ProgressEntry>(), ReportExportFormat.PlainText);

        Assert.Contains("Scoring Providers: n/a", content);
        Assert.Contains("Confidence Bands: n/a", content);
        Assert.Contains("Target-Level Deltas: n/a", content);
    }

    [Fact]
    public void BuildContent_CsvSummary_EscapesCommaAndQuotes()
    {
        var note = MakeNote();
        note.RawNote = "line one, with comma and \"quotes\"";

        var content = ReportExportFormatter.BuildContent(note, MakeEntries(), ReportExportFormat.CsvSummary);

        Assert.Contains("\"line one, with comma and \"\"quotes\"\"\"", content);
    }

    private static SessionNote MakeNote()
    {
        return new SessionNote
        {
            SessionDate = new DateTimeOffset(2026, 1, 25, 14, 30, 0, TimeSpan.Zero),
            RawNote = "Child demonstrated better /r/ in short phrases.",
            SoapSummary = "S: reports home practice. O: accuracy improved. A: gains are emerging. P: continue drills.",
            ParentSummary = "Great effort today. Keep daily short practice sessions."
        };
    }

    private static IReadOnlyList<ProgressEntry> MakeEntries()
    {
        return
        [
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.60,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 24, 10, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.80,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "High"
            }
        ];
    }
}
