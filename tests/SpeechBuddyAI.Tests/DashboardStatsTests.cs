using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class DashboardStatsTests
{
    [Fact]
    public void Compute_EmptyList_ReturnsZeroStats()
    {
        var svc = new DashboardStatsService();
        var stats = svc.Compute(Array.Empty<ProgressEntry>());

        Assert.Equal(0, stats.TotalAttempts);
        Assert.Equal(0.0, stats.AverageScore);
        Assert.Equal(string.Empty, stats.MostPracticedTarget);
        Assert.Equal(0, stats.CurrentPracticeStreakDays);
    }

    [Fact]
    public void Compute_SingleEntry_ReturnsTotalAndMostPracticed()
    {
        var entry = MakeEntry("r", 0.8, DateTime.UtcNow);
        var svc = new DashboardStatsService();
        var stats = svc.Compute([entry]);

        Assert.Equal(1, stats.TotalAttempts);
        Assert.Equal("r", stats.MostPracticedTarget);
    }

    [Fact]
    public void Compute_MostImprovedReflectsDelta()
    {
        var entries = new[]
        {
            MakeEntry("r", 0.4, DateTime.UtcNow.AddDays(-2)),
            MakeEntry("r", 0.9, DateTime.UtcNow.AddDays(-1)),
            MakeEntry("l", 0.5, DateTime.UtcNow.AddDays(-2)),
            MakeEntry("l", 0.55, DateTime.UtcNow.AddDays(-1)),
        };

        var stats = new DashboardStatsService().Compute(entries);
        Assert.Equal("r", stats.MostImprovedTarget);
    }

    [Fact]
    public void Compute_StreakIsZeroWhenLastPracticeWasTwoDaysAgo()
    {
        var entry = MakeEntry("r", 0.8, DateTime.UtcNow.AddDays(-2));
        var stats = new DashboardStatsService().Compute([entry]);
        Assert.Equal(0, stats.CurrentPracticeStreakDays);
    }

    private static ProgressEntry MakeEntry(string target, double score, DateTime when)
        => new() { TargetSound = target, OverallScore = score, Timestamp = when };
}
