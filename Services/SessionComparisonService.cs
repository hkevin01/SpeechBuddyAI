using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public sealed class SessionComparisonService
{
    private const int RollingTimelineLength = 4;
    private const double MinimumWeight = 0.15;

    public SessionComparisonSnapshot Build(
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode = SessionComparisonNormalizationMode.AttemptWeighted)
    {
        var source = entries ?? Array.Empty<ProgressEntry>();

        try
        {
            var sessions = source
                .GroupBy(e => e.Timestamp.Date)
                .OrderByDescending(g => g.Key)
                .ToArray();

            if (sessions.Length == 0)
            {
                return new SessionComparisonSnapshot();
            }

            var rollingTimeline = BuildRollingTimeline(sessions, normalizationMode);

            var current = sessions[0].ToArray();
            var currentSnapshot = new SessionComparisonSnapshot
            {
                HasCurrentSession = true,
                NormalizationMode = normalizationMode,
                CurrentSessionDate = sessions[0].Key,
                CurrentAttemptCount = current.Length,
                CurrentAverageOverall = ComputeNormalizedAverage(current, normalizationMode, entry => entry.OverallScore),
                CurrentAverageConfidence = ComputeNormalizedAverage(current, normalizationMode, entry => entry.ConfidenceScore)
            };

            if (sessions.Length == 1)
            {
                var targetComparisons = BuildTargetComparisons(current, Array.Empty<ProgressEntry>());
                return new SessionComparisonSnapshot
                {
                    HasCurrentSession = true,
                    NormalizationMode = normalizationMode,
                    CurrentSessionDate = currentSnapshot.CurrentSessionDate,
                    CurrentAttemptCount = currentSnapshot.CurrentAttemptCount,
                    CurrentAverageOverall = currentSnapshot.CurrentAverageOverall,
                    CurrentAverageConfidence = currentSnapshot.CurrentAverageConfidence,
                    TargetComparisons = targetComparisons,
                    RollingTimeline = rollingTimeline
                };
            }

            var previous = sessions[1].ToArray();
            var targetComparisons = BuildTargetComparisons(current, previous);
            var transitions = BuildConfidenceBandTransitions(targetComparisons);

            return new SessionComparisonSnapshot
            {
                HasCurrentSession = true,
                HasPreviousSession = true,
                NormalizationMode = normalizationMode,
                CurrentSessionDate = currentSnapshot.CurrentSessionDate,
                CurrentAttemptCount = currentSnapshot.CurrentAttemptCount,
                CurrentAverageOverall = currentSnapshot.CurrentAverageOverall,
                CurrentAverageConfidence = currentSnapshot.CurrentAverageConfidence,
                PreviousSessionDate = sessions[1].Key,
                PreviousAttemptCount = previous.Length,
                PreviousAverageOverall = ComputeNormalizedAverage(previous, normalizationMode, entry => entry.OverallScore),
                PreviousAverageConfidence = ComputeNormalizedAverage(previous, normalizationMode, entry => entry.ConfidenceScore),
                TargetComparisons = targetComparisons,
                ConfidenceBandTransitions = transitions,
                RollingTimeline = rollingTimeline
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build session comparison snapshot.", ex);
        }
    }

    private static IReadOnlyList<TargetComparisonItem> BuildTargetComparisons(
        IReadOnlyList<ProgressEntry> currentSession,
        IReadOnlyList<ProgressEntry> previousSession)
    {
        var currentByTarget = currentSession
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(e => e.TargetSound, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var previousByTarget = previousSession
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(e => e.TargetSound, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var allTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        allTargets.UnionWith(currentByTarget.Keys);
        allTargets.UnionWith(previousByTarget.Keys);

        var comparisons = new List<TargetComparisonItem>();
        foreach (var target in allTargets.OrderBy(t => t))
        {
            var hasCurrentData = currentByTarget.TryGetValue(target, out var currentData);
            var hasPreviousData = previousByTarget.TryGetValue(target, out var previousData);

            var currentAvg = hasCurrentData ? currentData.Average(e => e.OverallScore) : 0.0;
            var previousAvg = hasPreviousData ? previousData.Average(e => e.OverallScore) : 0.0;
            var currentConfidenceAvg = hasCurrentData ? currentData.Average(e => e.ConfidenceScore) : 0.0;
            var previousConfidenceAvg = hasPreviousData ? previousData.Average(e => e.ConfidenceScore) : 0.0;
            var currentVariance = ComputeVariance(currentData, entry => entry.OverallScore);
            var previousVariance = ComputeVariance(previousData, entry => entry.OverallScore);
            var recentVariance = ComputeVariance(ConcatEntries(currentData, previousData), entry => entry.OverallScore);
            var consistencyDecay = Math.Max(0.0, currentVariance - previousVariance);
            var variabilityIndex = Math.Sqrt(Math.Max(0.0, recentVariance)) + (0.5 * consistencyDecay);

            comparisons.Add(new TargetComparisonItem
            {
                TargetSound = target,
                CurrentAverageOverall = currentAvg,
                PreviousAverageOverall = previousAvg,
                CurrentAverageConfidence = currentConfidenceAvg,
                PreviousAverageConfidence = previousConfidenceAvg,
                CurrentConfidenceBand = ResolveDominantBand(currentData),
                PreviousConfidenceBand = ResolveDominantBand(previousData),
                CurrentAttemptCount = hasCurrentData ? currentData.Length : 0,
                PreviousAttemptCount = hasPreviousData ? previousData.Length : 0,
                CurrentSessionVariance = currentVariance,
                PreviousSessionVariance = previousVariance,
                RecentSessionVariance = recentVariance,
                ConsistencyDecay = consistencyDecay,
                VariabilityIndex = variabilityIndex
            });
        }

        return comparisons
            .OrderByDescending(c => Math.Abs(c.OverallDelta))
            .ThenByDescending(c => c.VariabilityIndex)
            .ToArray();
    }

    private static IReadOnlyList<ConfidenceBandTransitionCount> BuildConfidenceBandTransitions(
        IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        return targetComparisons
            .Where(item => item.PreviousAttemptCount > 0 && item.CurrentAttemptCount > 0)
            .Where(item => item.PreviousConfidenceBand != item.CurrentConfidenceBand)
            .GroupBy(item => new { item.PreviousConfidenceBand, item.CurrentConfidenceBand })
            .OrderBy(group => group.Key.PreviousConfidenceBand)
            .ThenBy(group => group.Key.CurrentConfidenceBand)
            .Select(group => new ConfidenceBandTransitionCount
            {
                FromBand = group.Key.PreviousConfidenceBand,
                ToBand = group.Key.CurrentConfidenceBand,
                Count = group.Count()
            })
            .ToArray();
    }

    private static double ComputeNormalizedAverage(
        IReadOnlyList<ProgressEntry> sessionEntries,
        SessionComparisonNormalizationMode normalizationMode,
        Func<ProgressEntry, double> selector)
    {
        if (sessionEntries.Count == 0)
        {
            return 0.0;
        }

        if (normalizationMode == SessionComparisonNormalizationMode.AttemptWeighted)
        {
            return sessionEntries.Average(selector);
        }

        return sessionEntries
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.TargetSound) ? "unknown" : entry.TargetSound.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Average(selector))
            .Average();
    }

    private static ConfidenceBand ResolveDominantBand(IReadOnlyList<ProgressEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return ConfidenceBand.Low;
        }

        return entries
            .GroupBy(entry => entry.ConfidenceBandValue)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .Select(group => group.Key)
            .First();
    }

    private static IReadOnlyList<SessionTimelineItem> BuildRollingTimeline(
        IReadOnlyList<IGrouping<DateTime, ProgressEntry>> sessions,
        SessionComparisonNormalizationMode normalizationMode)
    {
        var aggregates = sessions
            .Take(RollingTimelineLength)
            .Select(group =>
            {
                var sessionEntries = group.ToArray();
                return new
                {
                    SessionDate = group.Key,
                    AttemptCount = sessionEntries.Length,
                    AverageOverall = ComputeNormalizedAverage(sessionEntries, normalizationMode, entry => entry.OverallScore),
                    AverageConfidence = ComputeNormalizedAverage(sessionEntries, normalizationMode, entry => entry.ConfidenceScore),
                    ConfidenceWeightedOverall = ComputeConfidenceWeightedAverage(sessionEntries)
                };
            })
            .ToArray();

        var chronological = aggregates.OrderBy(item => item.SessionDate).ToArray();
        var smoothed = new Dictionary<DateTime, (double Overall, double Confidence)>();
        for (var i = 0; i < chronological.Length; i++)
        {
            var current = chronological[i];
            if (i == 0)
            {
                smoothed[current.SessionDate] = (current.ConfidenceWeightedOverall, current.AverageConfidence);
                continue;
            }

            var previous = smoothed[chronological[i - 1].SessionDate];
            var alpha = Math.Clamp(0.35 + (0.4 * current.AverageConfidence), 0.35, 0.75);
            var smoothedOverall = (alpha * current.ConfidenceWeightedOverall) + ((1.0 - alpha) * previous.Overall);
            var smoothedConfidence = (alpha * current.AverageConfidence) + ((1.0 - alpha) * previous.Confidence);
            smoothed[current.SessionDate] = (smoothedOverall, smoothedConfidence);
        }

        var timeline = new List<SessionTimelineItem>(aggregates.Length);
        for (var i = 0; i < aggregates.Length; i++)
        {
            var current = aggregates[i];
            var hasBaseline = i + 1 < aggregates.Length;
            var baseline = hasBaseline ? aggregates[i + 1] : null;
            var currentSmoothed = smoothed[current.SessionDate];
            var baselineSmoothed = hasBaseline ? smoothed[baseline!.SessionDate] : (0.0, 0.0);

            timeline.Add(new SessionTimelineItem
            {
                SessionDate = current.SessionDate,
                AttemptCount = current.AttemptCount,
                AverageOverall = current.AverageOverall,
                AverageConfidence = current.AverageConfidence,
                ConfidenceWeightedOverall = current.ConfidenceWeightedOverall,
                SmoothedOverall = currentSmoothed.Overall,
                SmoothedConfidence = currentSmoothed.Confidence,
                HasComparisonBaseline = hasBaseline,
                OverallDeltaFromPreviousSession = hasBaseline ? currentSmoothed.Overall - baselineSmoothed.Overall : 0.0,
                ConfidenceDeltaFromPreviousSession = hasBaseline ? currentSmoothed.Confidence - baselineSmoothed.Confidence : 0.0
            });
        }

        return timeline;
    }

    private static IReadOnlyList<ProgressEntry> ConcatEntries(
        IReadOnlyList<ProgressEntry>? currentData,
        IReadOnlyList<ProgressEntry>? previousData)
    {
        if ((currentData is null || currentData.Count == 0) && (previousData is null || previousData.Count == 0))
        {
            return Array.Empty<ProgressEntry>();
        }

        return (currentData ?? Array.Empty<ProgressEntry>())
            .Concat(previousData ?? Array.Empty<ProgressEntry>())
            .ToArray();
    }

    private static double ComputeVariance(IReadOnlyList<ProgressEntry>? entries, Func<ProgressEntry, double> selector)
    {
        if (entries is null || entries.Count < 2)
        {
            return 0.0;
        }

        var values = entries.Select(selector).ToArray();
        var mean = values.Average();
        return values.Average(value => Math.Pow(value - mean, 2));
    }

    private static double ComputeConfidenceWeightedAverage(IReadOnlyList<ProgressEntry> entries)
    {
        if (entries.Count == 0)
        {
            return 0.0;
        }

        var weighted = entries
            .Select(entry => new
            {
                Weight = Math.Max(MinimumWeight, entry.ConfidenceScore),
                entry.OverallScore
            })
            .ToArray();

        var denominator = weighted.Sum(item => item.Weight);
        if (denominator <= 0)
        {
            return entries.Average(entry => entry.OverallScore);
        }

        return weighted.Sum(item => item.OverallScore * item.Weight) / denominator;
    }
}
