using Microsoft.Maui.Storage;
using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;
using SpeechBuddyAI.Services.SpeechScoring;

namespace SpeechBuddyAI.Tests;

public sealed class ScoringPersistenceReportEndToEndTests
{
    [Fact]
    public async Task AttemptScoring_ToPersistence_ToReportExportText_FlowsEndToEnd()
    {
        var previousAppData = FileSystem.AppDataDirectory;
        var tempRoot = Path.Combine(Path.GetTempPath(), "speechbuddy-e2e-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        FileSystem.AppDataDirectory = tempRoot;

        try
        {
            var progressService = new ProgressTrackingService();
            await SeedHistoryAsync(progressService);

            var settings = new ConfidenceSettingsService(new InMemoryStore());
            var confidenceCalculator = new ConfidenceCalculator(settings);
            var speechService = new AiSpeechService(
                progressService,
                new ISpeechScoringAdapter[] { new FixedScoringAdapter() },
                confidenceCalculator,
                new ConsistencyEstimator());

            var attempt = await speechService.EvaluateAndPersistAttemptAsync("r:initial", "rain rabbit rocket");
            var persistedEntries = await progressService.GetEntriesAsync();

            var reportService = new ReportService(
                settings,
                new ComparisonSnapshotCacheService(
                    new ComparisonExportBuilderService(
                        new SessionComparisonService(),
                        new ComparisonNarrativeGenerator(new TrendAnalysisService()))),
                new AssignmentSnapshotService());

            var note = await reportService.GenerateReportsAsync("Integration check note.", persistedEntries);
            var exportText = reportService.BuildExportText(note, persistedEntries, ReportExportFormat.PlainText);

            var flowVerified =
                attempt.Entry.TargetSound == "r" &&
                persistedEntries.Any(entry =>
                    entry.TargetSound == "r" &&
                    entry.Transcript == "rain rabbit rocket" &&
                    entry.ScoringProvider == "test-adapter" &&
                    !string.IsNullOrWhiteSpace(entry.ScoringFormulaVersion)) &&
                exportText.Contains("Scoring Providers:", StringComparison.OrdinalIgnoreCase) &&
                exportText.Contains("test-adapter", StringComparison.OrdinalIgnoreCase) &&
                exportText.Contains("Scoring Formula Versions:", StringComparison.OrdinalIgnoreCase) &&
                exportText.Contains(AiSpeechService.ScoringFormulaVersion, StringComparison.Ordinal) &&
                exportText.Contains("Historical Drift Analytics:", StringComparison.OrdinalIgnoreCase) &&
                exportText.Contains("Adaptive Thresholds:", StringComparison.OrdinalIgnoreCase);

            Assert.True(flowVerified, exportText);
        }
        finally
        {
            FileSystem.AppDataDirectory = previousAppData;
        }
    }

    private static async Task SeedHistoryAsync(ProgressTrackingService progressService)
    {
        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new ProgressEntry
            {
                Timestamp = now.AddDays(-6),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "red",
                AccuracyScore = 0.58,
                PhonemeScore = 0.56,
                FluencyScore = 0.62,
                ConsistencyScore = 0.54,
                OverallScore = 0.57,
                TrialCount = 1,
                ErrorPattern = "phoneme_mismatch",
                ScoringProvider = "seed",
                ConfidenceScore = 0.55,
                ConfidenceBand = "Low"
            },
            new ProgressEntry
            {
                Timestamp = now.AddDays(-5),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "run",
                AccuracyScore = 0.60,
                PhonemeScore = 0.59,
                FluencyScore = 0.63,
                ConsistencyScore = 0.56,
                OverallScore = 0.60,
                TrialCount = 2,
                ErrorPattern = "phoneme_mismatch",
                ScoringProvider = "seed",
                ConfidenceScore = 0.58,
                ConfidenceBand = "Low"
            },
            new ProgressEntry
            {
                Timestamp = now.AddDays(-4),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "rope",
                AccuracyScore = 0.62,
                PhonemeScore = 0.61,
                FluencyScore = 0.65,
                ConsistencyScore = 0.58,
                OverallScore = 0.62,
                TrialCount = 3,
                ErrorPattern = "fluency_instability",
                ScoringProvider = "seed",
                ConfidenceScore = 0.61,
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = now.AddDays(-3),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "ring",
                AccuracyScore = 0.64,
                PhonemeScore = 0.63,
                FluencyScore = 0.66,
                ConsistencyScore = 0.60,
                OverallScore = 0.64,
                TrialCount = 4,
                ErrorPattern = "fluency_instability",
                ScoringProvider = "seed",
                ConfidenceScore = 0.63,
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = now.AddDays(-2),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "road",
                AccuracyScore = 0.66,
                PhonemeScore = 0.65,
                FluencyScore = 0.68,
                ConsistencyScore = 0.62,
                OverallScore = 0.66,
                TrialCount = 5,
                ErrorPattern = "none",
                ScoringProvider = "seed",
                ConfidenceScore = 0.66,
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = now.AddDays(-1),
                TargetSound = "r",
                BaseTargetSound = "r",
                PositionTag = "initial",
                Transcript = "rock",
                AccuracyScore = 0.68,
                PhonemeScore = 0.67,
                FluencyScore = 0.69,
                ConsistencyScore = 0.64,
                OverallScore = 0.68,
                TrialCount = 6,
                ErrorPattern = "none",
                ScoringProvider = "seed",
                ConfidenceScore = 0.68,
                ConfidenceBand = "Moderate"
            }
        };

        foreach (var item in seed)
        {
            await progressService.AddEntryAsync(item);
        }
    }

    private sealed class FixedScoringAdapter : ISpeechScoringAdapter
    {
        public string Name => "test-adapter";
        public int Priority => 0;

        public Task<AdapterScoreResult> ScoreAsync(
            string targetSound,
            string transcript,
            IReadOnlyList<ProgressEntry> priorEntries,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AdapterScoreResult
            {
                Provider = "test-adapter",
                PhonemeScore = 0.84,
                FluencyScore = 0.79
            });
        }
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
