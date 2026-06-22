using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class TrendAnalysisService
{
    public string DescribePerformanceChange(double delta)
    {
        if (delta >= 0.15)
        {
            return $"Strong upward improvement ({delta:+0%;-0%;0%})";
        }

        if (delta >= 0.05)
        {
            return $"Moderate improvement ({delta:+0%;-0%;0%})";
        }

        if (delta > -0.05)
        {
            return $"Stable performance ({delta:+0%;-0%;0%})";
        }

        return $"Downward movement ({delta:+0%;-0%;0%})";
    }

    public IReadOnlyList<TrendPoint> BuildTrendPoints(IReadOnlyList<ProgressEntry> entries, int maxPoints = 12)
    {
        var sourceEntries = entries ?? Array.Empty<ProgressEntry>();
        var requestedPointCount = Math.Max(2, maxPoints);

        try
        {
            var points = sourceEntries
                .OrderBy(e => e.Timestamp)
                .TakeLast(requestedPointCount)
                .ToArray();

            var result = new List<TrendPoint>(points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                var score = Math.Max(0.0, Math.Min(1.0, points[i].OverallScore));
                var confidence = Math.Max(0.0, Math.Min(1.0, points[i].ConfidenceScore));
                result.Add(new TrendPoint
                {
                    AttemptIndex = i + 1,
                    Score = score,
                    BarWidth = 40 + (220 * score),
                    ConfidenceScore = confidence,
                    ConfidenceBarWidth = 40 + (220 * confidence)
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build trend points.", ex);
        }
    }

    public string InterpretTrajectory(IReadOnlyList<ProgressEntry> entries)
    {
        var sourceEntries = entries ?? Array.Empty<ProgressEntry>();

        try
        {
            var ordered = sourceEntries.OrderBy(e => e.Timestamp).ToArray();
            if (ordered.Length < 2)
            {
                return "Trajectory needs more than one attempt before interpretation is reliable.";
            }

            var first = ordered.First().OverallScore;
            var last = ordered.Last().OverallScore;
            var delta = last - first;

            if (delta >= 0.15)
            {
                return $"{DescribePerformanceChange(delta)} across the observed window. Keep current drill cadence and consider target expansion.";
            }

            if (delta >= 0.05)
            {
                return $"{DescribePerformanceChange(delta)}. Continue current plan and reinforce consistency through short daily repetitions.";
            }

            if (delta > -0.05)
            {
                return $"{DescribePerformanceChange(delta)}. Consider varying prompts to test generalization and identify hidden bottlenecks.";
            }

            return $"{DescribePerformanceChange(delta)}. Revisit target complexity and increase guided practice with slower pacing and immediate feedback.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to interpret trajectory.", ex);
        }
    }
}
