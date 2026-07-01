using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.SpeechScoring;

namespace SpeechBuddyAI.Tests;

public sealed class ConsistencyEstimatorTests
{
    [Fact]
    public void Estimate_ReturnsNeutral_WhenHistoryIsSparse()
    {
        var estimator = new ConsistencyEstimator();
        var entries = new[]
        {
            Entry("r", "initial", 0.70, DateTime.UtcNow.AddDays(-1))
        };

        var consistency = estimator.Estimate(entries, "initial");

        Assert.Equal(0.5, consistency, 3);
    }

    [Fact]
    public void Estimate_PenalizesHighVarianceSeries()
    {
        var estimator = new ConsistencyEstimator();
        var now = DateTime.UtcNow;
        var stable = new[]
        {
            Entry("r", "initial", 0.70, now.AddDays(-6)),
            Entry("r", "initial", 0.69, now.AddDays(-5)),
            Entry("r", "initial", 0.71, now.AddDays(-4)),
            Entry("r", "initial", 0.70, now.AddDays(-3)),
            Entry("r", "initial", 0.72, now.AddDays(-2)),
            Entry("r", "initial", 0.71, now.AddDays(-1))
        };

        var volatileSet = new[]
        {
            Entry("r", "initial", 0.20, now.AddDays(-6)),
            Entry("r", "initial", 0.86, now.AddDays(-5)),
            Entry("r", "initial", 0.31, now.AddDays(-4)),
            Entry("r", "initial", 0.88, now.AddDays(-3)),
            Entry("r", "initial", 0.24, now.AddDays(-2)),
            Entry("r", "initial", 0.82, now.AddDays(-1))
        };

        var stableConsistency = estimator.Estimate(stable, "initial");
        var volatileConsistency = estimator.Estimate(volatileSet, "initial");

        Assert.True(stableConsistency > volatileConsistency);
    }

    [Fact]
    public void Estimate_UsesPositionScopedHistory_WhenSufficientSamplesExist()
    {
        var estimator = new ConsistencyEstimator();
        var now = DateTime.UtcNow;
        var entries = new[]
        {
            Entry("r", "initial", 0.72, now.AddDays(-8)),
            Entry("r", "initial", 0.70, now.AddDays(-7)),
            Entry("r", "initial", 0.71, now.AddDays(-6)),
            Entry("r", "medial", 0.20, now.AddDays(-5)),
            Entry("r", "medial", 0.88, now.AddDays(-4)),
            Entry("r", "medial", 0.26, now.AddDays(-3)),
            Entry("r", "medial", 0.86, now.AddDays(-2)),
            Entry("r", "medial", 0.24, now.AddDays(-1))
        };

        var initialConsistency = estimator.Estimate(entries, "initial");
        var medialConsistency = estimator.Estimate(entries, "medial");

        Assert.True(initialConsistency > medialConsistency);
    }

    private static ProgressEntry Entry(string target, string position, double overall, DateTime at)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            BaseTargetSound = target,
            PositionTag = position,
            OverallScore = overall,
            PhonemeScore = overall,
            FluencyScore = overall,
            ConsistencyScore = 0.5,
            Transcript = "sample",
            Timestamp = at,
            ConfidenceScore = 0.7,
            ConfidenceBand = "Moderate"
        };
    }
}
