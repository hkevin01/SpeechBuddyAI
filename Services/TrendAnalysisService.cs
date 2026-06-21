using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class TrendAnalysisService
{
    public IReadOnlyList<TrendPoint> BuildTrendPoints(IReadOnlyList<ProgressEntry> entries, int maxPoints = 12)
    {
        var points = entries
            .OrderBy(e => e.Timestamp)
            .TakeLast(Math.Max(2, maxPoints))
            .ToArray();

        var result = new List<TrendPoint>(points.Length);
        for (var i = 0; i < points.Length; i++)
        {
            var score = Math.Max(0.0, Math.Min(1.0, points[i].OverallScore));
            result.Add(new TrendPoint
            {
                AttemptIndex = i + 1,
                Score = score,
                BarWidth = 40 + (220 * score)
            });
        }

        return result;
    }

    public string InterpretTrajectory(IReadOnlyList<ProgressEntry> entries)
    {
        var ordered = entries.OrderBy(e => e.Timestamp).ToArray();
        if (ordered.Length < 2)
        {
            return "Trajectory needs more than one attempt before interpretation is reliable.";
        }

        var first = ordered.First().OverallScore;
        var last = ordered.Last().OverallScore;
        var delta = last - first;

        if (delta >= 0.15)
        {
            return $"Strong upward trajectory (+{delta:P0}) across the observed window. Keep current drill cadence and consider target expansion.";
        }

        if (delta >= 0.05)
        {
            return $"Moderate improvement (+{delta:P0}). Continue current plan and reinforce consistency through short daily repetitions.";
        }

        if (delta > -0.05)
        {
            return $"Stable trajectory ({delta:P0} change). Consider varying prompts to test generalization and identify hidden bottlenecks.";
        }

        return $"Downward trajectory ({delta:P0}). Revisit target complexity and increase guided practice with slower pacing and immediate feedback.";
    }
}
