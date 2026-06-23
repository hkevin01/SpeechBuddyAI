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
        var snapshot = BuildSnapshot(entries);

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.PlainText, snapshot);

        Assert.Contains("SpeechBuddyAI Session Report", content);
        Assert.Contains("Metadata", content);
        Assert.Contains("Scoring Providers:", content);
        Assert.Contains("Confidence Bands:", content);
        Assert.Contains("Target-Level Deltas:", content);
        Assert.Contains("Session Comparison:", content);
        Assert.Contains("Timeline Smoothing:", content);
        Assert.Contains("Comparison Narrative", content);
        Assert.Contains("Rolling Session History", content);
        Assert.Contains("Per-Target Comparison", content);
        Assert.Contains("Assignment Selection Rationale", content);
        Assert.Contains("variability", content);
        Assert.Contains("drift", content);
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
        var snapshot = BuildSnapshot(entries);

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.Markdown, snapshot);

        Assert.Contains("# SpeechBuddyAI Session Report", content);
        Assert.Contains("- Scoring Providers:", content);
        Assert.Contains("## Comparison Narrative", content);
        Assert.Contains("## Rolling Session History", content);
        Assert.Contains("## Per-Target Comparison", content);
        Assert.Contains("## Assignment Selection Rationale", content);
        Assert.Contains("| Target | Delta | Confidence Move | Current Avg | Previous Avg | Variability | Drift |", content);
        Assert.Contains("## Clinician SOAP Summary", content);
    }

    [Fact]
    public void BuildContent_Markdown_MultiTargetComparisonTableIncludesAllTargetsAndBandMovement()
    {
        var note = MakeNote();
        var entries = MakeMultiTargetEntries();
        var snapshot = BuildSnapshot(entries);

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.Markdown, snapshot);

        Assert.Contains("- Confidence Movement: 1 Low to Moderate, 1 Moderate to High", content);
        Assert.Contains("| Session | Attempts | Overall | Smoothed Overall | Confidence | Delta vs Prior |", content);
        Assert.Contains("| 2026-01-24 | 2 | 68 % |", content);
        Assert.Contains("| r | +20% | Moderate to High | 80 % | 60 % |", content);
        Assert.Contains("| s | +20% | Low to Moderate | 55 % | 35 % |", content);
        Assert.Contains("| Target | Delta | Confidence Move | Current Avg | Previous Avg | Variability | Drift |", content);
    }

    [Fact]
    public void BuildContent_CsvSummary_ContainsExpectedMetrics()
    {
        var note = MakeNote();
        var entries = MakeEntries();
        var snapshot = BuildSnapshot(entries);

        var content = ReportExportFormatter.BuildContent(note, entries, ReportExportFormat.CsvSummary, snapshot);

        Assert.Contains("Metric,Value", content);
        Assert.Contains("ScoringProviders", content);
        Assert.Contains("ConfidenceBands", content);
        Assert.Contains("TargetLevelDeltas", content);
        Assert.Contains("SessionComparison", content);
        Assert.Contains("TimelineSmoothing", content);
        Assert.Contains("RollingSessionHistory", content);
        Assert.Contains("TargetComparisonTable", content);
        Assert.Contains("AssignmentSelectionSummary", content);
        Assert.Contains("AssignmentSelectionDetails", content);
        Assert.Contains("var", content);
        Assert.Contains("drift", content);
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
        Assert.Contains("Session Comparison: n/a", content);
    }

    [Fact]
    public void BuildContent_CsvSummary_EscapesCommaAndQuotes()
    {
        var note = MakeNote();
        note.RawNote = "line one, with comma and \"quotes\"";
        var snapshot = BuildSnapshot(MakeEntries());

        var content = ReportExportFormatter.BuildContent(note, MakeEntries(), ReportExportFormat.CsvSummary, snapshot);

        Assert.Contains("\"line one, with comma and \"\"quotes\"\"\"", content);
    }

    private static SessionComparisonSnapshot BuildSnapshot(IReadOnlyList<ProgressEntry> entries)
    {
        var builder = new ComparisonExportBuilderService(
            new SessionComparisonService(),
            new ComparisonNarrativeGenerator(new TrendAnalysisService()));

        return builder.Build(entries);
    }

    private static SessionNote MakeNote()
    {
        return new SessionNote
        {
            SessionDate = new DateTimeOffset(2026, 1, 25, 14, 30, 0, TimeSpan.Zero),
            RawNote = "Child demonstrated better /r/ in short phrases.",
            SoapSummary = "S: reports home practice. O: accuracy improved. A: gains are emerging. P: continue drills.",
            ParentSummary = "Great effort today. Keep daily short practice sessions.",
            AssignmentSelectionSummary = "Focus on r and s based on weighted priority.",
            AssignmentSelectionDetails = "- r: priority 0.72\n- s: priority 0.61"
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
                ConfidenceScore = 0.65,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 24, 10, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.80,
                ConfidenceScore = 0.84,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "High"
            }
        ];
    }

    private static IReadOnlyList<ProgressEntry> MakeMultiTargetEntries()
    {
        return
        [
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.60,
                ConfidenceScore = 0.65,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 20, 10, 0, 0, DateTimeKind.Utc),
                TargetSound = "s",
                OverallScore = 0.35,
                ConfidenceScore = 0.40,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Low"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 24, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.80,
                ConfidenceScore = 0.84,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "High"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 1, 24, 10, 0, 0, DateTimeKind.Utc),
                TargetSound = "s",
                OverallScore = 0.55,
                ConfidenceScore = 0.67,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            }
        ];
    }
}
