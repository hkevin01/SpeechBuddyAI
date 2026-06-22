using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

/// <summary>
/// Tests to validate that session comparison metrics in Progress page
/// always match the active target filter, not the full dataset.
/// </summary>
public sealed class ProgressPageFilteredComparisonTests
{
    [Fact]
    public void FilteredComparison_NoFilter_IncludesAllTargets()
    {
        var service = new SessionComparisonService();
        var allEntries = new[]
        {
            Entry("r", 0.50, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.70, 0.68, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.60, 0.55, new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.75, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.72, 0.70, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.78, 0.76, new DateTime(2026, 6, 20, 11, 0, 0, DateTimeKind.Utc))
        };

        var snapshot = service.Build(allEntries);

        Assert.Equal(3, snapshot.TargetComparisons.Count);
        var targetSounds = snapshot.TargetComparisons.Select(c => c.TargetSound).ToArray();
        Assert.Contains("r", targetSounds);
        Assert.Contains("s", targetSounds);
        Assert.Contains("l", targetSounds);
    }

    [Fact]
    public void FilteredComparison_FilterByR_OnlyRTargetInComparison()
    {
        var service = new SessionComparisonService();
        var allEntries = new[]
        {
            Entry("r", 0.50, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.70, 0.68, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.60, 0.55, new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.75, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.72, 0.70, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc)),
            Entry("l", 0.78, 0.76, new DateTime(2026, 6, 20, 11, 0, 0, DateTimeKind.Utc))
        };

        var filteredByR = allEntries.Where(e => e.TargetSound.Contains("r", StringComparison.OrdinalIgnoreCase)).ToArray();
        var snapshot = service.Build(filteredByR);

        Assert.Single(snapshot.TargetComparisons);
        Assert.Equal("r", snapshot.TargetComparisons[0].TargetSound);
        Assert.Equal(1, snapshot.PreviousAttemptCount);
        Assert.Equal(1, snapshot.CurrentAttemptCount);
    }

    [Fact]
    public void FilteredComparison_FilterByS_MetricsExcludeOtherTargets()
    {
        var service = new SessionComparisonService();
        var allEntries = new[]
        {
            Entry("r", 0.50, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.60, 0.50, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.75, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.85, 0.75, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var filteredByS = allEntries.Where(e => e.TargetSound.Contains("s", StringComparison.OrdinalIgnoreCase)).ToArray();
        var snapshot = service.Build(filteredByS);

        Assert.Single(snapshot.TargetComparisons);
        var sComparison = snapshot.TargetComparisons[0];
        Assert.Equal("s", sComparison.TargetSound);
        Assert.Equal(0.60, sComparison.PreviousAverageOverall, 3);
        Assert.Equal(0.85, sComparison.CurrentAverageOverall, 3);
        Assert.Equal(0.25, sComparison.OverallDelta, 3);
        Assert.Equal(0.50, snapshot.CurrentAverageOverall, 3);
        Assert.Equal(0.60, snapshot.PreviousAverageOverall, 3);
    }

    [Fact]
    public void FilteredComparison_MultipleSessionsFilteredByTarget_ComputesCorrectAverages()
    {
        var service = new SessionComparisonService();
        var allEntries = new[]
        {
            Entry("r", 0.40, 0.35, new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.50, 0.45, new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.60, 0.50, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.65, 0.55, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.70, 0.65, new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc)),
            Entry("r", 0.80, 0.70, new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.85, 0.75, new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc))
        };

        var filteredByR = allEntries.Where(e => e.TargetSound.Contains("r", StringComparison.OrdinalIgnoreCase)).ToArray();
        var snapshot = service.Build(filteredByR);

        Assert.True(snapshot.HasCurrentSession);
        Assert.True(snapshot.HasPreviousSession);
        Assert.Equal(new DateTime(2026, 6, 20), snapshot.CurrentSessionDate);
        Assert.Equal(new DateTime(2026, 6, 19), snapshot.PreviousSessionDate);
        Assert.Equal(1, snapshot.CurrentAttemptCount);
        Assert.Equal(2, snapshot.PreviousAttemptCount);
        Assert.Equal(0.80, snapshot.CurrentAverageOverall, 3);
        Assert.Equal(0.625, snapshot.PreviousAverageOverall, 3);
    }

    [Fact]
    public void FilteredComparison_EmptyFilterResult_ReturnsEmptySnapshot()
    {
        var service = new SessionComparisonService();
        var allEntries = new[]
        {
            Entry("r", 0.50, 0.45, new DateTime(2026, 6, 19, 9, 0, 0, DateTimeKind.Utc)),
            Entry("s", 0.70, 0.68, new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc))
        };

        var filteredByNonexistent = allEntries.Where(e => e.TargetSound.Contains("z", StringComparison.OrdinalIgnoreCase)).ToArray();
        var snapshot = service.Build(filteredByNonexistent);

        Assert.False(snapshot.HasCurrentSession);
        Assert.False(snapshot.HasPreviousSession);
        Assert.Empty(snapshot.TargetComparisons);
    }

    private static ProgressEntry Entry(string target, double overall, double confidence, DateTime timestamp)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            OverallScore = overall,
            ConfidenceScore = confidence,
            Timestamp = timestamp
        };
    }
}
