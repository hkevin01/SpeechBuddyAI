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

    [Fact]
    public void ComputeAdaptiveThresholds_HigherVarianceAndLowerSupport_RaiseThresholds()
    {
        var baseline = new ConfidenceThresholds(0.60, 0.80);
        var calculator = new ConfidenceCalculator(new StubProvider(baseline));

        var stableHistory = Enumerable.Range(0, 12)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-12 + index),
                ConfidenceScore = 0.80 + ((index % 2 == 0) ? 0.01 : -0.01)
            })
            .ToArray();
        var noisySparseHistory = new[]
        {
            new ProgressEntry { Timestamp = DateTime.UtcNow.AddDays(-2), ConfidenceScore = 0.20 },
            new ProgressEntry { Timestamp = DateTime.UtcNow.AddDays(-1), ConfidenceScore = 0.92 },
            new ProgressEntry { Timestamp = DateTime.UtcNow, ConfidenceScore = 0.25 }
        };

        var stableThresholds = calculator.ComputeAdaptiveThresholds(stableHistory, consistencyUncertainty: 0.15);
        var noisyThresholds = calculator.ComputeAdaptiveThresholds(noisySparseHistory, consistencyUncertainty: 0.85);

        Assert.True(noisyThresholds.ModerateThreshold > stableThresholds.ModerateThreshold);
        Assert.True(noisyThresholds.HighThreshold > stableThresholds.HighThreshold);
    }

    [Fact]
    public void CalibrationMonitoring_SyntheticHistories_ProduceExpectedBandDistributions()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var stableHistory = Enumerable.Range(0, 12)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-12 + index),
                ConfidenceScore = 0.78 + ((index % 2 == 0) ? 0.01 : -0.01)
            })
            .ToArray();
        var noisyHistory = new[]
        {
            new ProgressEntry { Timestamp = DateTime.UtcNow.AddDays(-2), ConfidenceScore = 0.18 },
            new ProgressEntry { Timestamp = DateTime.UtcNow.AddDays(-1), ConfidenceScore = 0.91 },
            new ProgressEntry { Timestamp = DateTime.UtcNow, ConfidenceScore = 0.24 }
        };

        var scoreSamples = new[] { 0.66, 0.70, 0.74, 0.78, 0.82, 0.86 };
        var stableThresholds = calculator.ComputeAdaptiveThresholds(stableHistory, consistencyUncertainty: 0.20);
        var noisyThresholds = calculator.ComputeAdaptiveThresholds(noisyHistory, consistencyUncertainty: 0.90);

        var stableBands = scoreSamples.Select(score => calculator.ComputeBand(score, stableThresholds)).ToArray();
        var noisyBands = scoreSamples.Select(score => calculator.ComputeBand(score, noisyThresholds)).ToArray();

        var stableHighCount = stableBands.Count(band => band == "High");
        var noisyHighCount = noisyBands.Count(band => band == "High");
        var stableLowCount = stableBands.Count(band => band == "Low");
        var noisyLowCount = noisyBands.Count(band => band == "Low");

        Assert.True(stableHighCount >= noisyHighCount);
        Assert.True(noisyLowCount >= stableLowCount);
    }

    [Fact]
    public void ComputeScore_LowEmpiricalOutcome_WithHighSupport_CalibratesDown()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.88,
            FluencyScore = 0.84,
            ConsistencyScore = 0.86,
            OverallScore = 0.87
        };

        var uncalibrated = calculator.ComputeScore(scores, "rain rabbit", 10, "offline-heuristic", 0.20, "HighSupport");
        var calibrated = calculator.ComputeScore(scores, "rain rabbit", 10, "offline-heuristic", 0.20, "HighSupport", empiricalOutcomeMean: 0.45, empiricalOutcomeSupport: 1.0);

        Assert.True(calibrated < uncalibrated);
    }

    [Fact]
    public void ComputeScore_HighEmpiricalOutcome_WithHighSupport_CalibratesUp()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.70,
            FluencyScore = 0.68,
            ConsistencyScore = 0.69,
            OverallScore = 0.69
        };

        var uncalibrated = calculator.ComputeScore(scores, "rain rabbit", 10, "offline-heuristic", 0.20, "HighSupport");
        var calibrated = calculator.ComputeScore(scores, "rain rabbit", 10, "offline-heuristic", 0.20, "HighSupport", empiricalOutcomeMean: 0.90, empiricalOutcomeSupport: 1.0);

        Assert.True(calibrated > uncalibrated);
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
