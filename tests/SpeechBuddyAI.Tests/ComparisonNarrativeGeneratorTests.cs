using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class ComparisonNarrativeGeneratorTests
{
    [Fact]
    public void Generate_WithPreviousSession_UsesTrendLanguageAndTargetMovement()
    {
        var generator = new ComparisonNarrativeGenerator(new TrendAnalysisService());
        var snapshot = new SessionComparisonSnapshot
        {
            HasCurrentSession = true,
            HasPreviousSession = true,
            CurrentAverageOverall = 0.82,
            PreviousAverageOverall = 0.60,
            CurrentAverageConfidence = 0.78,
            PreviousAverageConfidence = 0.62,
            RollingTimeline =
            [
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 21),
                    AverageOverall = 0.82,
                    AverageConfidence = 0.78,
                    AttemptCount = 3,
                    HasComparisonBaseline = true,
                    OverallDeltaFromPreviousSession = 0.10,
                    ConfidenceDeltaFromPreviousSession = 0.09
                },
                new SessionTimelineItem
                {
                    SessionDate = new DateTime(2026, 6, 20),
                    AverageOverall = 0.72,
                    AverageConfidence = 0.69,
                    AttemptCount = 2,
                    HasComparisonBaseline = false
                }
            ],
            TargetComparisons =
            [
                new TargetComparisonItem
                {
                    TargetSound = "r",
                    CurrentAverageOverall = 0.82,
                    PreviousAverageOverall = 0.60,
                    CurrentConfidenceBand = ConfidenceBand.High,
                    PreviousConfidenceBand = ConfidenceBand.Moderate,
                    CurrentAttemptCount = 2,
                    PreviousAttemptCount = 2
                }
            ]
        };

        var narrative = generator.Generate(snapshot);

        Assert.Contains("Strong upward improvement", narrative);
        Assert.Contains("r", narrative);
        Assert.Contains("Moderate to High", narrative);
        Assert.Contains("Across the last 2 sessions in view", narrative);
    }

    [Fact]
    public void Generate_WithoutPreviousSession_ReturnsBaselineNarrative()
    {
        var generator = new ComparisonNarrativeGenerator(new TrendAnalysisService());
        var snapshot = new SessionComparisonSnapshot
        {
            HasCurrentSession = true,
            HasPreviousSession = false
        };

        var narrative = generator.Generate(snapshot);

        Assert.Contains("baseline", narrative, StringComparison.OrdinalIgnoreCase);
    }
}
