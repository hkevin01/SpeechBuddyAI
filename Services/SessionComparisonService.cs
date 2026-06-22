using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public sealed class SessionComparisonService
{
    private const int RollingTimelineLength = 4;

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
                PreviousAttemptCount = hasPreviousData ? previousData.Length : 0
            });
        }

        return comparisons
            .OrderByDescending(c => Math.Abs(c.OverallDelta))
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
                    AverageConfidence = ComputeNormalizedAverage(sessionEntries, normalizationMode, entry => entry.ConfidenceScore)
                };
            })
            .ToArray();

        var timeline = new List<SessionTimelineItem>(aggregates.Length);
        for (var i = 0; i < aggregates.Length; i++)
        {
            var current = aggregates[i];
            var hasBaseline = i + 1 < aggregates.Length;
            var baseline = hasBaseline ? aggregates[i + 1] : null;

            timeline.Add(new SessionTimelineItem
            {
                SessionDate = current.SessionDate,
                AttemptCount = current.AttemptCount,
                AverageOverall = current.AverageOverall,
                AverageConfidence = current.AverageConfidence,
                HasComparisonBaseline = hasBaseline,
                OverallDeltaFromPreviousSession = hasBaseline ? current.AverageOverall - baseline!.AverageOverall : 0.0,
                ConfidenceDeltaFromPreviousSession = hasBaseline ? current.AverageConfidence - baseline!.AverageConfidence : 0.0
            });
        }

        return timeline;
    }
}
