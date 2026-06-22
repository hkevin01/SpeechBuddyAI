using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class SessionComparisonServiceTests
{
    [Fact]
    public void Build_NoEntries_ReturnsEmptySnapshot()
    {
        var service = new SessionComparisonService();

        var snapshot = service.Build(Array.Empty<ProgressEntry>());

        Assert.False(snapshot.HasCurrentSession);
        Assert.False(snapshot.HasPreviousSession);
    }

    [Fact]
    public void Build_OneSession_ReturnsCurrentOnly()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.70, 0.65, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.75, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);

        Assert.True(snapshot.HasCurrentSession);
        Assert.False(snapshot.HasPreviousSession);
        Assert.Equal(new DateTime(2026, 6, 20), snapshot.CurrentSessionDate);
        Assert.Equal(2, snapshot.CurrentAttemptCount);
        Assert.Equal(0.75, snapshot.CurrentAverageOverall, 3);
        Assert.Equal(0.70, snapshot.CurrentAverageConfidence, 3);
    }

    [Fact]
    public void Build_TwoSessions_ComputesDeltas()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.60, 0.55, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.65, 0.58, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.72, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.84, 0.78, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);

        Assert.True(snapshot.HasCurrentSession);
        Assert.True(snapshot.HasPreviousSession);
        Assert.Equal(new DateTime(2026, 6, 20), snapshot.CurrentSessionDate);
        Assert.Equal(new DateTime(2026, 6, 19), snapshot.PreviousSessionDate);
        Assert.Equal(2, snapshot.CurrentAttemptCount);
        Assert.Equal(2, snapshot.PreviousAttemptCount);
        Assert.True(snapshot.OverallDelta > 0);
        Assert.True(snapshot.ConfidenceDelta > 0);
    }

    [Fact]
    public void Build_TwoSessions_ComputesConfidenceBandTransitions()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.55, 0.40, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc), "Low"),
            Entry("s", 0.68, 0.65, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("r", 0.78, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("s", 0.82, 0.85, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc), "High")
        };

        var snapshot = service.Build(entries);

        Assert.Equal(2, snapshot.ConfidenceBandTransitions.Count);
        Assert.Contains(snapshot.ConfidenceBandTransitions, item =>
            item.FromBand == ConfidenceBand.Low && item.ToBand == ConfidenceBand.Moderate && item.Count == 1);
        Assert.Contains(snapshot.ConfidenceBandTransitions, item =>
            item.FromBand == ConfidenceBand.Moderate && item.ToBand == ConfidenceBand.High && item.Count == 1);
    }

    [Fact]
    public void Build_DayWeightedNormalization_BalancesTargetsWithinSession()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.90, 0.88, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.90, 0.87, new DateTime(2026, 6, 19, 9, 15, 0, DateTimeKind.Utc)),
            Entry("r", 0.90, 0.86, new DateTime(2026, 6, 19, 9, 30, 0, DateTimeKind.Utc)),
            Entry("s", 0.30, 0.35, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.75, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.74, new DateTime(2026, 6, 20, 9, 15, 0, DateTimeKind.Utc)),
            Entry("s", 0.60, 0.55, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var attemptWeighted = service.Build(entries, SessionComparisonNormalizationMode.AttemptWeighted);
        var dayWeighted = service.Build(entries, SessionComparisonNormalizationMode.DayWeighted);

        Assert.Equal(0.7333333333333333, attemptWeighted.CurrentAverageOverall, 6);
        Assert.Equal(0.70, dayWeighted.CurrentAverageOverall, 6);
        Assert.Equal(SessionComparisonNormalizationMode.DayWeighted, dayWeighted.NormalizationMode);
    }

    [Fact]
    public void Build_RollingTimeline_IncludesRecentSessionsWithDeltas()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.30, 0.35, new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc), "Low"),
            Entry("r", 0.45, 0.50, new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc), "Low"),
            Entry("r", 0.60, 0.62, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("r", 0.75, 0.78, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc), "Moderate"),
            Entry("r", 0.85, 0.88, new DateTime(2026, 6, 21, 9, 0, 0, DateTimeKind.Utc), "High")
        };

        var snapshot = service.Build(entries);

        Assert.Equal(4, snapshot.RollingTimeline.Count);
        Assert.Equal(new DateTime(2026, 6, 21), snapshot.RollingTimeline[0].SessionDate);
        Assert.True(snapshot.RollingTimeline[0].HasComparisonBaseline);
        Assert.True(snapshot.RollingTimeline[0].SmoothedOverall > 0);
        Assert.True(snapshot.RollingTimeline[0].ConfidenceWeightedOverall > 0);
        Assert.True(snapshot.RollingTimeline[0].OverallDeltaFromPreviousSession > 0);
        Assert.False(snapshot.RollingTimeline[^1].HasComparisonBaseline);
    }

    [Fact]
    public void Build_PerTargetComparisons_ComputesVariabilityMetrics()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.40, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.65, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.50, 0.55, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.90, 0.75, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);
        var target = Assert.Single(snapshot.TargetComparisons);

        Assert.True(target.CurrentSessionVariance > 0);
        Assert.True(target.RecentSessionVariance > 0);
        Assert.True(target.VariabilityIndex > 0);
    }

    [Fact]
    public void Build_WithDifferentSmoothingStrengths_ChangesTimelineResponsiveness()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.35, 0.40, new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.55, 0.55, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.85, 0.75, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc))
        };

        var conservative = service.Build(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Conservative);
        var responsive = service.Build(entries, SessionComparisonNormalizationMode.AttemptWeighted, SessionComparisonSmoothingStrength.Responsive);

        Assert.Equal(SessionComparisonSmoothingStrength.Conservative, conservative.SmoothingStrength);
        Assert.Equal(SessionComparisonSmoothingStrength.Responsive, responsive.SmoothingStrength);
        Assert.True(responsive.RollingTimeline[0].SmoothedOverall > conservative.RollingTimeline[0].SmoothedOverall);
    }

    [Fact]
    public void Build_PerTargetComparisons_SortedByDeltaMagnitude()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.50, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.70, 0.68, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.80, 0.75, new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.75, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.72, 0.70, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.78, 0.76, new DateTime(2026, 6, 20, 11, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);

        Assert.NotEmpty(snapshot.TargetComparisons);
        var rComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "r");
        var sComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "s");
        var lComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "l");

        Assert.NotNull(rComparison);
        Assert.NotNull(sComparison);
        Assert.NotNull(lComparison);

        Assert.Equal(0.25, rComparison.OverallDelta, 3);
        Assert.Equal(0.02, sComparison.OverallDelta, 3);
        Assert.Equal(-0.02, lComparison.OverallDelta, 3);

        Assert.Equal("r", snapshot.TargetComparisons[0].TargetSound);
    }

    [Fact]
    public void Build_PerTargetComparisons_TrackAttemptCountPerSession()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.60, 0.55, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.65, 0.58, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.72, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.84, 0.78, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.82, 0.75, new DateTime(2026, 6, 20, 11, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);

        var rComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "r");
        Assert.NotNull(rComparison);
        Assert.Equal(2, rComparison.PreviousAttemptCount);
        Assert.Equal(3, rComparison.CurrentAttemptCount);
    }

    [Fact]
    public void Build_OneSessionOnly_TargetComparisonsHaveZeroPreviousScores()
    {
        var service = new SessionComparisonService();
        var entries = new[]
        {
            Entry("r", 0.80, 0.72, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.75, 0.70, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(entries);

        var rComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "r");
        var sComparison = snapshot.TargetComparisons.FirstOrDefault(c => c.TargetSound == "s");

        Assert.NotNull(rComparison);
        Assert.NotNull(sComparison);
        Assert.Equal(0.80, rComparison.CurrentAverageOverall, 3);
        Assert.Equal(0.0, rComparison.PreviousAverageOverall);
        Assert.Equal(0.80, rComparison.OverallDelta, 3);
    }

    private static ProgressEntry Entry(
        string target,
        double overall,
        double confidence,
        DateTime timestamp,
        string confidenceBand = "Moderate")
    {
        return new ProgressEntry
        {
            TargetSound = target,
            OverallScore = overall,
            ConfidenceScore = confidence,
            ConfidenceBand = confidenceBand,
            Timestamp = timestamp
        };
    }
}
