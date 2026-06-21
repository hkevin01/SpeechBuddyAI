using Microsoft.Maui.Storage;
using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public void BuildExportText_RoutesFormatThroughServiceApi()
    {
        var service = new ReportService();
        var note = MakeNote();
        var entries = MakeEntries();

        var markdown = service.BuildExportText(note, entries, ReportExportFormat.Markdown);
        var csv = service.BuildExportText(note, entries, ReportExportFormat.CsvSummary);
        var text = service.BuildExportText(note, entries, ReportExportFormat.PlainText);

        Assert.Contains("# SpeechBuddyAI Session Report", markdown);
        Assert.Contains("Metric,Value", csv);
        Assert.Contains("SpeechBuddyAI Session Report", text);
    }

    [Fact]
    public async Task ExportReportAsync_UsesSameFileNameAsBuildExportFileName()
    {
        var service = new ReportService();
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
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.76,
                ScoringProvider = "offline-heuristic",
                ConfidenceBand = "High"
            }
        ];
    }
}
