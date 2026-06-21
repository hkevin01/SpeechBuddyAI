using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

// ID: SVC-DASH-001
// Purpose: Computes aggregate stats for the home dashboard from persisted progress entries.
public sealed class DashboardStatsService
{
    // ID: SVC-DASH-002
    // Purpose: Computes total attempts, average score, most-practiced target,
    //          most-improved target, and current daily-practice streak.
    public DashboardStats Compute(IReadOnlyList<ProgressEntry> entries)
    {
        var source = entries ?? Array.Empty<ProgressEntry>();

        if (source.Count == 0)
        {
            return new DashboardStats();
        }

        try
        {
            var totalAttempts = source.Count;
            var averageScore = source.Average(e => e.OverallScore);

            var mostPracticed = source
                .GroupBy(e => e.TargetSound, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var mostImproved = FindMostImprovedTarget(source);
            var streak = ComputePracticeStreak(source);

            return new DashboardStats
            {
                TotalAttempts = totalAttempts,
                AverageScore = averageScore,
                MostPracticedTarget = mostPracticed,
                MostImprovedTarget = mostImproved,
                CurrentPracticeStreakDays = streak
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to compute dashboard stats.", ex);
        }
    }

    private static string FindMostImprovedTarget(IReadOnlyList<ProgressEntry> entries)
    {
        var bestDelta = double.NegativeInfinity;
        var bestTarget = string.Empty;

        var byTarget = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(e => e.TargetSound, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byTarget)
        {
            var ordered = group.OrderBy(e => e.Timestamp).ToArray();
            if (ordered.Length < 2) continue;

            var delta = ordered.Last().OverallScore - ordered.First().OverallScore;
            if (delta > bestDelta)
            {
                bestDelta = delta;
                bestTarget = group.Key;
            }
        }

        return string.IsNullOrWhiteSpace(bestTarget) ? "n/a" : bestTarget;
    }

    private static int ComputePracticeStreak(IReadOnlyList<ProgressEntry> entries)
    {
        var today = DateTime.UtcNow.Date;
        var practiceDays = entries
            .Select(e => e.Timestamp.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToArray();

        if (practiceDays.Length == 0) return 0;

        // Streak starts only if practice happened today or yesterday
        var streakStart = practiceDays[0];
        if ((today - streakStart).TotalDays > 1) return 0;

        var streak = 1;
        for (var i = 1; i < practiceDays.Length; i++)
        {
            if ((practiceDays[i - 1] - practiceDays[i]).TotalDays == 1)
                streak++;
            else
                break;
        }

        return streak;
    }
}

public sealed class DashboardStats
{
    public int TotalAttempts { get; init; }
    public double AverageScore { get; init; }
    public string MostPracticedTarget { get; init; } = string.Empty;
    public string MostImprovedTarget { get; init; } = string.Empty;
    public int CurrentPracticeStreakDays { get; init; }
}
