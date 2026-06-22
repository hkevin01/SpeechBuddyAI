using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public sealed class ComparisonNarrativeGenerator
{
    private readonly TrendAnalysisService _trendAnalysisService;

    public ComparisonNarrativeGenerator(TrendAnalysisService trendAnalysisService)
    {
        _trendAnalysisService = trendAnalysisService ?? throw new ArgumentNullException(nameof(trendAnalysisService));
    }

    public string Generate(SessionComparisonSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (!snapshot.HasCurrentSession)
        {
            return "No session comparison is available yet.";
        }

        if (!snapshot.HasPreviousSession)
        {
            return "The current session establishes a baseline. Add one more session to generate a comparison narrative.";
        }

        var overallPhrase = _trendAnalysisService.DescribePerformanceChange(snapshot.OverallDelta);
        var confidencePhrase = _trendAnalysisService.DescribePerformanceChange(snapshot.ConfidenceDelta);
        var strongestTarget = snapshot.TargetComparisons
            .OrderByDescending(item => Math.Abs(item.OverallDelta))
            .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(item => item.CurrentAttemptCount > 0 || item.PreviousAttemptCount > 0);

        var targetSentence = strongestTarget is null
            ? "Target-level changes were limited across the comparison window."
            : $"Largest target movement was {strongestTarget.TargetSound} ({strongestTarget.OverallDelta:+0%;-0%;0%}) with {strongestTarget.PreviousConfidenceBand.ToDisplayName()} to {strongestTarget.CurrentConfidenceBand.ToDisplayName()} confidence movement.";

        var timelineSentence = BuildTimelineSummarySentence(snapshot.RollingTimeline);
        var consistencySentence = BuildConsistencySentence(snapshot.TargetComparisons);

        return $"{overallPhrase} overall, with {confidencePhrase.ToLowerInvariant()} in confidence. {targetSentence} {timelineSentence} {consistencySentence}";
    }

    private static string BuildTimelineSummarySentence(IReadOnlyList<SessionTimelineItem> rollingTimeline)
    {
        if (rollingTimeline.Count < 2)
        {
            return "The current view contains a baseline timeline only.";
        }

        var latest = rollingTimeline[0];
        var earliest = rollingTimeline[^1];
        var delta = latest.SmoothedOverall - earliest.SmoothedOverall;

        return $"Across the last {rollingTimeline.Count} sessions in view, smoothed overall moved {delta:+0%;-0%;0%} from {earliest.SmoothedOverall:P0} to {latest.SmoothedOverall:P0}.";
    }

    private static string BuildConsistencySentence(IReadOnlyList<TargetComparisonItem> targetComparisons)
    {
        if (targetComparisons.Count == 0)
        {
            return "Consistency change is limited because there are no comparable targets yet.";
        }

        var stabilizing = targetComparisons
            .OrderByDescending(item => item.PreviousSessionVariance - item.CurrentSessionVariance)
            .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
            .First();

        var regressionRisk = targetComparisons
            .OrderByDescending(item => item.ConsistencyDecay)
            .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
            .First();

        if ((stabilizing.PreviousSessionVariance - stabilizing.CurrentSessionVariance) <= 0 && regressionRisk.ConsistencyDecay <= 0)
        {
            return "Consistency stayed fairly steady across the reviewed sessions.";
        }

        return $"Most stabilization appeared on {stabilizing.TargetSound}, while {regressionRisk.TargetSound} showed the strongest consistency drift.";
    }
}
