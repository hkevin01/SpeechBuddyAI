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

    [Fact]
    public void BuildCalibrationTable_ImprovementScenario_MapsHigherBinsToHigherOutcomes()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var history = Enumerable.Range(0, 14)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-14 + index),
                RawConfidenceScore = 0.35 + (index * 0.04),
                OverallScore = 0.30 + (index * 0.045)
            })
            .ToArray();

        var table = calculator.BuildCalibrationTable(history, minSamples: 8, binCount: 6);

        Assert.True(table.IsActive);
        Assert.Equal(6, table.Bins.Count);
        Assert.True(table.Bins[0].CalibratedOutcome <= table.Bins[^1].CalibratedOutcome);
    }

    [Fact]
    public void BuildCalibrationTable_DeteriorationScenario_FlattensWithIsotonicConstraint()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var history = Enumerable.Range(0, 14)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-14 + index),
                RawConfidenceScore = 0.35 + (index * 0.04),
                OverallScore = 0.82 - (index * 0.04)
            })
            .ToArray();

        var table = calculator.BuildCalibrationTable(history, minSamples: 8, binCount: 6);

        Assert.NotNull(table.Quality);
        Assert.False(table.Quality!.MeetsActivationGate);
        Assert.False(table.IsActive);
        for (var i = 1; i < table.Bins.Count; i++)
        {
            Assert.True(table.Bins[i].CalibratedOutcome >= table.Bins[i - 1].CalibratedOutcome);
        }
    }

    [Fact]
    public void ComputeUncertaintyDecomposition_OscillationScenario_IncreasesVarianceComponent()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var stable = Enumerable.Range(0, 12)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-12 + index),
                OverallScore = 0.70 + ((index % 2 == 0) ? 0.01 : -0.01)
            })
            .ToArray();
        var oscillating = Enumerable.Range(0, 12)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-12 + index),
                OverallScore = index % 2 == 0 ? 0.90 : 0.35
            })
            .ToArray();

        var stableDecomposition = calculator.ComputeUncertaintyDecomposition(stable, consistencyUncertainty: 0.35);
        var oscillatingDecomposition = calculator.ComputeUncertaintyDecomposition(oscillating, consistencyUncertainty: 0.35);

        Assert.True(oscillatingDecomposition.VarianceDriven > stableDecomposition.VarianceDriven);
        Assert.True(oscillatingDecomposition.VarianceDriven >= oscillatingDecomposition.SparsityDriven - 0.15);
    }

    [Fact]
    public void ComputeScore_WithActiveCalibrationTable_AppliesNonLinearCorrection()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.79,
            FluencyScore = 0.76,
            ConsistencyScore = 0.74,
            OverallScore = 0.78
        };
        var table = new ConfidenceCalibrationTable(
            new[]
            {
                new ConfidenceCalibrationBin(0.0, 0.5, 0.40, 4),
                new ConfidenceCalibrationBin(0.5, 1.0, 0.86, 8)
            },
            Support: 1.0,
            IsActive: true,
            Quality: new ConfidenceCalibrationQuality(1.0, 4, 0.02, 1.0, true));

        var withoutTable = calculator.ComputeScore(scores, "rocket", 6, "offline-heuristic", 0.25, "HighSupport", 0.75, 1.0);
        var withTable = calculator.ComputeScore(scores, "rocket", 6, "offline-heuristic", 0.25, "HighSupport", 0.75, 1.0, table);

        Assert.True(withTable > withoutTable);
    }

    [Fact]
    public void BuildCalibrationTable_SparseBinCoverage_RemainsInactive()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var history = Enumerable.Range(0, 10)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-10 + index),
                RawConfidenceScore = 0.88 + (index * 0.005),
                OverallScore = 0.45 + ((index % 2 == 0) ? 0.03 : -0.02)
            })
            .ToArray();

        var table = calculator.BuildCalibrationTable(history, minSamples: 8, binCount: 6);

        Assert.False(table.IsActive);
        Assert.NotNull(table.Quality);
        Assert.True(table.Quality!.Coverage < 0.5);
        Assert.False(table.Quality.MeetsActivationGate);
    }

    [Fact]
    public void BuildCalibrationTable_WellDistributedSamples_ActivatesQualityGate()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var history = Enumerable.Range(0, 30)
            .Select(index => new ProgressEntry
            {
                Timestamp = DateTime.UtcNow.AddDays(-30 + index),
                RawConfidenceScore = 0.05 + (index * 0.03),
                OverallScore = 0.10 + (index * 0.025)
            })
            .ToArray();

        var table = calculator.BuildCalibrationTable(history, minSamples: 8, binCount: 6);

        Assert.True(table.IsActive);
        Assert.NotNull(table.Quality);
        Assert.True(table.Quality!.Coverage >= 0.5);
        Assert.True(table.Quality.QualityScore > 0.55);
        Assert.True(table.Support > 0.0);
    }

    [Fact]
    public void ComputeScore_LowQualityCalibrationTable_HasSmallerImpactThanHighQualityTable()
    {
        var calculator = new ConfidenceCalculator(new StubProvider(new ConfidenceThresholds(0.60, 0.80)));
        var scores = new ScoreComponents
        {
            PhonemeScore = 0.76,
            FluencyScore = 0.72,
            ConsistencyScore = 0.74,
            OverallScore = 0.74
        };

        var highQuality = new ConfidenceCalibrationTable(
            new[]
            {
                new ConfidenceCalibrationBin(0.0, 0.5, 0.42, 5),
                new ConfidenceCalibrationBin(0.5, 1.0, 0.88, 9)
            },
            Support: 1.0,
            IsActive: true,
            Quality: new ConfidenceCalibrationQuality(1.0, 3, 0.02, 1.0, true));

        var lowQuality = new ConfidenceCalibrationTable(
            highQuality.Bins,
            Support: 1.0,
            IsActive: true,
            Quality: new ConfidenceCalibrationQuality(0.55, 2, 0.16, 0.56, true));

        var baseline = calculator.ComputeScore(scores, "rocket", 6, "offline-heuristic", 0.30, "ModerateSupport", 0.72, 0.9);
        var withHighQuality = calculator.ComputeScore(scores, "rocket", 6, "offline-heuristic", 0.30, "ModerateSupport", 0.72, 0.9, highQuality);
        var withLowQuality = calculator.ComputeScore(scores, "rocket", 6, "offline-heuristic", 0.30, "ModerateSupport", 0.72, 0.9, lowQuality);

        var highDelta = Math.Abs(withHighQuality - baseline);
        var lowDelta = Math.Abs(withLowQuality - baseline);

        Assert.True(highDelta > lowDelta);
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
