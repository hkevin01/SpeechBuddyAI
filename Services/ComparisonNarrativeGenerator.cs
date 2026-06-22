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

        return $"{overallPhrase} overall, with {confidencePhrase.ToLowerInvariant()} in confidence. {targetSentence} {timelineSentence}";
    }

    private static string BuildTimelineSummarySentence(IReadOnlyList<SessionTimelineItem> rollingTimeline)
    {
        if (rollingTimeline.Count < 2)
        {
            return "The current view contains a baseline timeline only.";
        }

        var latest = rollingTimeline[0];
        var earliest = rollingTimeline[^1];
        var delta = latest.AverageOverall - earliest.AverageOverall;

        return $"Across the last {rollingTimeline.Count} sessions in view, overall moved {delta:+0%;-0%;0%} from {earliest.AverageOverall:P0} to {latest.AverageOverall:P0}.";
    }
}
