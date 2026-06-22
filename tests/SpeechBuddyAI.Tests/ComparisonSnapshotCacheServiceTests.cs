using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class ComparisonSnapshotCacheServiceTests
{
    [Fact]
    public void GetOrBuild_ReusesSnapshotForIdenticalInput()
    {
        var builder = new ComparisonExportBuilderService(
            new SessionComparisonService(),
            new ComparisonNarrativeGenerator(new TrendAnalysisService()));
        var cache = new ComparisonSnapshotCacheService(builder);

        var entries = new[]
        {
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.72,
                ConfidenceScore = 0.68,
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 6, 21, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.82,
                ConfidenceScore = 0.80,
                ConfidenceBand = "High"
            }
        };

        var first = cache.GetOrBuild(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Balanced);
        var second = cache.GetOrBuild(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Balanced);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrBuild_DifferentSmoothingStrengthProducesDifferentSnapshot()
    {
        var builder = new ComparisonExportBuilderService(
            new SessionComparisonService(),
            new ComparisonNarrativeGenerator(new TrendAnalysisService()));
        var cache = new ComparisonSnapshotCacheService(builder);

        var entries = new[]
        {
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.72,
                ConfidenceScore = 0.68,
                ConfidenceBand = "Moderate"
            },
            new ProgressEntry
            {
                Timestamp = new DateTime(2026, 6, 21, 9, 0, 0, DateTimeKind.Utc),
                TargetSound = "r",
                OverallScore = 0.82,
                ConfidenceScore = 0.80,
                ConfidenceBand = "High"
            }
        };

        var conservative = cache.GetOrBuild(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Conservative);
        var responsive = cache.GetOrBuild(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Responsive);

        Assert.NotSame(conservative, responsive);
    }
}
