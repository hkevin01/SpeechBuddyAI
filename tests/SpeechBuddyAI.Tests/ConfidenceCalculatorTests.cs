using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Tests;

public sealed class ConfidenceCalculatorTests
{
    [Fact]
    public void ComputeBand_RespectsConfiguredThresholds()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.55, 0.78)));

        Assert.Equal("Low", calculator.ComputeBand(0.50));
        Assert.Equal("Moderate", calculator.ComputeBand(0.60));
        Assert.Equal("High", calculator.ComputeBand(0.85));
    }

    [Fact]
    public void ComputeScore_OfflineProviderProducesHigherConfidenceThanCloud()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.9,
            FluencyScore = 0.8,
            ConsistencyScore = 0.85,
            OverallScore = 0.86
        };

        var offline = calculator.ComputeScore(scores, "rain rabbit rocket", 4, "offline-heuristic");
        var cloud = calculator.ComputeScore(scores, "rain rabbit rocket", 4, "fallback-cloud-sim");

        Assert.True(offline > cloud);
    }

    [Fact]
    public void ComputeScore_ClampsValueToZeroToOneRange()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 5.0,
            FluencyScore = -2.0,
            ConsistencyScore = 5.0,
            OverallScore = 5.0
        };

        var value = calculator.ComputeScore(scores, "a", 999, "offline-heuristic");
        Assert.InRange(value, 0.0, 1.0);
    }

    [Fact]
    public void ComputeScore_LowSupportConsistencyBand_IsDownWeighted()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.82,
            FluencyScore = 0.80,
            ConsistencyScore = 0.81,
            OverallScore = 0.81
        };

        var highSupport = calculator.ComputeScore(scores, "rabbit rain", 10, "offline-heuristic", 0.15, "HighSupport");
        var lowSupport = calculator.ComputeScore(scores, "rabbit rain", 1, "offline-heuristic", 0.85, "LowSupport");

        Assert.True(highSupport > lowSupport);
    }

    private sealed class StubProvider : IConfidenceThresholdProvider
    {
        private readonly ConfidenceThresholds _thresholds;

        public StubProvider(ConfidenceThresholds thresholds)
        {
            _thresholds = thresholds;
        }

        public ConfidenceThresholds GetThresholds() => _thresholds;
    }
}
