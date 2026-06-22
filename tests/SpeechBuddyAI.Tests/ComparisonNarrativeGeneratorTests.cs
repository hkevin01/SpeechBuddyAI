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
