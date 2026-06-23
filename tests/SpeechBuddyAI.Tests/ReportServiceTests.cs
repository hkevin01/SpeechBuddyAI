using Microsoft.Maui.Storage;
using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task BuildExportText_RoutesFormatThroughServiceApi()
    {
        var (service, snapshots) = CreateService();
        await snapshots.SaveSnapshotAsync(new HomeAssignment
        {
            Title = "Home Practice Plan",
            Rationale = "Focus on r and s using weighted priority.",
            FocusTargets = ["r", "s"],
            SuggestedWords = ["rabbit", "sun"],
            FocusTargetReasons =
            [
                new AssignmentFocusTargetReason
                {
                    TargetSound = "r",
                    PriorityScore = 0.72,
                    SeverityScore = 0.61,
                    InstabilityScore = 0.32,
                    DeclineScore = 0.22,
                    FrequencyScore = 0.40,
                    ConfidenceFactor = 0.84,
                    PositionSequence = "final -> medial -> initial",
                    PositionDeltaSummary = "initial +0.02 | medial -0.03 | final -0.06"
                }
            ]
        }, 12);

        var note = MakeNote();
        var entries = MakeEntries();

        var markdown = service.BuildExportText(note, entries, ReportExportFormat.Markdown);
        var csv = service.BuildExportText(note, entries, ReportExportFormat.CsvSummary);
        var text = service.BuildExportText(note, entries, ReportExportFormat.PlainText);

        Assert.Contains("# SpeechBuddyAI Session Report", markdown);
        Assert.Contains("Metric,Value", csv);
        Assert.Contains("SpeechBuddyAI Session Report", text);
        Assert.Contains("Comparison Narrative", text);
        Assert.Contains("Assignment Selection Rationale", text);
    }

    [Fact]
    public async Task ExportReportAsync_UsesSameFileNameAsBuildExportFileName()
    {
        var (service, _) = CreateService();
        var note = MakeNote();
        var entries = MakeEntries();

        var tempRoot = Path.Combine(Path.GetTempPath(), "speechbuddy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        FileSystem.AppDataDirectory = tempRoot;

        var expectedName = service.BuildExportFileName(note, ReportExportFormat.Markdown);
        var exportedPath = await service.ExportReportAsync(note, entries, ReportExportFormat.Markdown);

        Assert.Equal(expectedName, Path.GetFileName(exportedPath));
        Assert.EndsWith(".md", exportedPath);

        var content = await File.ReadAllTextAsync(exportedPath);
        Assert.Contains("# SpeechBuddyAI Session Report", content);
    }

    [Fact]
    public async Task GenerateReportsAsync_ParentSummaryReflectsComparisonTrendLanguage()
    {
        var (service, _) = CreateService();
        var entries = MakeEntries();

        var report = await service.GenerateReportsAsync("Parent communication check", entries);

        Assert.Contains("Compared with the earlier session in this review window", report.ParentSummary);
        Assert.Contains("steady repetition", report.ParentSummary);
    }

    [Fact]
    public async Task GenerateReportsAsync_IncludesRationaleDriftAndPreviousCurrentContrast()
    {
        var (service, snapshots) = CreateService();
        await snapshots.SaveSnapshotAsync(new HomeAssignment
        {
            Title = "Home Practice Plan",
            Rationale = "Focus on r blends with slower pacing and steady repetition.",
            FocusTargets = ["r"],
            SuggestedWords = ["rabbit"],
            FocusTargetReasons =
            [
                new AssignmentFocusTargetReason
                {
                    TargetSound = "r",
                    PriorityScore = 0.71,
                    SeverityScore = 0.60,
                    InstabilityScore = 0.32,
                    DeclineScore = 0.18,
                    FrequencyScore = 0.44,
                    ConfidenceFactor = 0.78,
                    ConfidenceVariance = 0.012,
                    PositionSequence = "final -> medial -> initial",
                    PositionDeltaSummary = "initial +0.01 | medial -0.02 | final -0.07"
                }
            ]
        }, 8);
        await snapshots.SaveSnapshotAsync(new HomeAssignment
        {
            Title = "Home Practice Plan",
            Rationale = "Focus on r and s carryover with mixed phrase drills.",
            FocusTargets = ["r", "s"],
            SuggestedWords = ["rabbit", "sun"],
            FocusTargetReasons =
            [
                new AssignmentFocusTargetReason
                {
                    TargetSound = "r",
                    PriorityScore = 0.68,
                    SeverityScore = 0.57,
                    InstabilityScore = 0.30,
                    DeclineScore = 0.16,
                    FrequencyScore = 0.42,
                    ConfidenceFactor = 0.80,
                    ConfidenceVariance = 0.016,
                    PositionSequence = "medial -> final -> initial",
                    PositionDeltaSummary = "initial +0.02 | medial -0.04 | final -0.03"
                }
            ]
        }, 12);

        var report = await service.GenerateReportsAsync("Drift validation", MakeEntries());

        Assert.Contains("Rationale overlap", report.AssignmentRationaleDriftSummary);
        Assert.Contains("Previous rationale:", report.AssignmentSelectionDetails);
        Assert.Contains("Current rationale:", report.AssignmentSelectionDetails);
    }

    private static (ReportService Service, AssignmentSnapshotService Snapshots) CreateService()
    {
        var settings = new ConfidenceSettingsService(new InMemoryStore());
        var builder = new ComparisonExportBuilderService(
            new SessionComparisonService(),
            new ComparisonNarrativeGenerator(new TrendAnalysisService()));
        var cache = new ComparisonSnapshotCacheService(builder);
        var assignmentSnapshots = new AssignmentSnapshotService();

        return (new ReportService(settings, cache, assignmentSnapshots), assignmentSnapshots);
    }

    private static SessionNote MakeNote()
    {
        return new SessionNote
        {
            SessionDate = new DateTimeOffset(2026, 2, 5, 9, 45, 0, TimeSpan.Zero),
            RawNote = "Observed carryover in connected speech.",
            SoapSummary = "S: improved confidence. O: improved consistency. A: trend improving. P: continue scaffolded phrases.",
            ParentSummary = "Nice progress today with sentence-level speech."
        };
    }

    private static IReadOnlyList<ProgressEntry> MakeEntries()
    {
        return
        [
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.55,
                ConfidenceScore = 0.62,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.76,
                ConfidenceScore = 0.82,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "High"
            }
        ];
    }

    private sealed class InMemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

        public double Get(string key, double defaultValue)
        {
            return _values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, double value)
        {
            _values[key] = value;
        }

        public string Get(string key, string defaultValue)
        {
            return _strings.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, string value)
        {
            _strings[key] = value;
        }
    }
}
