using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class AiTextServiceTests
{
    [Fact]
    public async Task GenerateHomeAssignmentAsync_NoHistory_ReturnsDefaultPlan()
    {
        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);

        var assignment = await service.GenerateHomeAssignmentAsync(Array.Empty<ProgressEntry>());

        Assert.Equal("Home Practice Plan", assignment.Title);
        Assert.Empty(assignment.FocusTargets);
        Assert.NotEmpty(assignment.SuggestedWords);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_PrioritizesDecliningAndUnstableTargets()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            // /r/ declines and becomes unstable.
            Entry("r:initial", 0.88, now.AddDays(-7), "phoneme_mismatch", 0.9),
            Entry("r:medial", 0.84, now.AddDays(-6), "phoneme_mismatch", 0.8),
            Entry("r:initial", 0.61, now.AddDays(-3), "phoneme_mismatch", 0.6),
            Entry("r:final", 0.47, now.AddDays(-2), "phoneme_mismatch", 0.55),
            Entry("r:final", 0.56, now.AddDays(-1), "phoneme_mismatch", 0.52),

            // /l/ is weaker but stable and not declining.
            Entry("l:initial", 0.58, now.AddDays(-7), "fluency_instability", 0.75),
            Entry("l:medial", 0.57, now.AddDays(-5), "fluency_instability", 0.73),
            Entry("l:final", 0.56, now.AddDays(-3), "fluency_instability", 0.72),
            Entry("l:initial", 0.56, now.AddDays(-1), "fluency_instability", 0.71),

            // /s/ is improving.
            Entry("s:initial", 0.40, now.AddDays(-8), "inconsistent_attempts", 0.45),
            Entry("s:medial", 0.50, now.AddDays(-4), "inconsistent_attempts", 0.42),
            Entry("s:final", 0.64, now.AddDays(-1), "none", 0.40)
        };

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("r", assignment.FocusTargets[0]);
        Assert.Contains("priority", assignment.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(assignment.FocusTargetReasons);
        Assert.Contains("->", assignment.FocusTargetReasons[0].PositionSequence);
        Assert.Contains("initial", assignment.FocusTargetReasons[0].PositionDeltaSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_WhenPositionSamplesBelowGate_UsesDefaultSequence()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            Entry("r:initial", 0.72, now.AddDays(-4), "none", 0.7),
            Entry("r:medial", 0.66, now.AddDays(-3), "none", 0.7),
            Entry("r:final", 0.60, now.AddDays(-2), "none", 0.7),
            Entry("r:final", 0.58, now.AddDays(-1), "none", 0.7)
        };

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargetReasons);
        var reason = assignment.FocusTargetReasons[0];
        Assert.False(reason.PositionSequenceSampleGateMet);
        Assert.Equal("initial -> medial -> final", reason.PositionSequence);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_EvidenceWeighting_PreventsSingleAttemptOutlierFromDominating()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            Entry("r:initial", 0.62, now.AddDays(-8), "phoneme_mismatch", 0.8),
            Entry("r:medial", 0.60, now.AddDays(-7), "phoneme_mismatch", 0.8),
            Entry("r:final", 0.58, now.AddDays(-6), "phoneme_mismatch", 0.8),
            Entry("r:initial", 0.55, now.AddDays(-5), "phoneme_mismatch", 0.8),
            Entry("r:medial", 0.53, now.AddDays(-4), "phoneme_mismatch", 0.8),
            Entry("r:final", 0.50, now.AddDays(-3), "phoneme_mismatch", 0.8),

            // One severe but sparse outlier that should be tempered by evidence weighting.
            Entry("s:initial", 0.10, now.AddDays(-1), "inconsistent_attempts", 0.9)
        };

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("r", assignment.FocusTargets[0]);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_HardFreeze_HoldsPreviousTargetsDuringHighVariance()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        var settings = new ConfidenceSettingsService(store);
        settings.SaveAssignmentSuppressionBehavior(AssignmentSuppressionBehavior.HardFreeze);
        settings.SaveAssignmentConfidenceVarianceGate(0.001);

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), settings, snapshotService);

        var baselineHistory = new[]
        {
            Entry("s:initial", 0.40, now.AddDays(-10), "pattern_s", 0.65),
            Entry("s:medial", 0.38, now.AddDays(-9), "pattern_s", 0.62),
            Entry("s:final", 0.35, now.AddDays(-8), "pattern_s", 0.68),
            Entry("r:initial", 0.78, now.AddDays(-7), "pattern_r", 0.72),
            Entry("r:medial", 0.80, now.AddDays(-6), "pattern_r", 0.74)
        };
        var baselineAssignment = await service.GenerateHomeAssignmentAsync(baselineHistory);
        await snapshotService.SaveSnapshotAsync(baselineAssignment, baselineHistory.Length);

        var conflictingHistory = new[]
        {
            Entry("r:initial", 0.34, now.AddDays(-5), "pattern_r", 0.95),
            Entry("r:medial", 0.31, now.AddDays(-4), "pattern_r", 0.15),
            Entry("r:final", 0.29, now.AddDays(-3), "pattern_r", 0.92),
            Entry("r:initial", 0.33, now.AddDays(-2), "pattern_r", 0.18),
            Entry("s:initial", 0.22, now.AddDays(-1), "pattern_s", 0.90)
        };

        var assignment = await service.GenerateHomeAssignmentAsync(conflictingHistory);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.NotEmpty(baselineAssignment.FocusTargets);
        Assert.Equal(baselineAssignment.FocusTargets[0], assignment.FocusTargets[0]);
        Assert.Contains(assignment.FocusTargetReasons, reason => reason.AssignmentChangeSuppressed);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_WarningOnly_AllowsReprioritizationDuringHighVariance()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        var settings = new ConfidenceSettingsService(store);
        settings.SaveAssignmentSuppressionBehavior(AssignmentSuppressionBehavior.WarningOnly);
        settings.SaveAssignmentConfidenceVarianceGate(0.001);

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), settings, snapshotService);

        var baselineHistory = new[]
        {
            Entry("s:initial", 0.40, now.AddDays(-10), "pattern_s", 0.65),
            Entry("s:medial", 0.38, now.AddDays(-9), "pattern_s", 0.62),
            Entry("s:final", 0.35, now.AddDays(-8), "pattern_s", 0.68),
            Entry("r:initial", 0.78, now.AddDays(-7), "pattern_r", 0.72),
            Entry("r:medial", 0.80, now.AddDays(-6), "pattern_r", 0.74)
        };
        var baselineAssignment = await service.GenerateHomeAssignmentAsync(baselineHistory);
        await snapshotService.SaveSnapshotAsync(baselineAssignment, baselineHistory.Length);

        var conflictingHistory = new[]
        {
            Entry("r:initial", 0.34, now.AddDays(-5), "pattern_r", 0.95),
            Entry("r:medial", 0.31, now.AddDays(-4), "pattern_r", 0.15),
            Entry("r:final", 0.29, now.AddDays(-3), "pattern_r", 0.92),
            Entry("r:initial", 0.33, now.AddDays(-2), "pattern_r", 0.18),
            Entry("s:initial", 0.22, now.AddDays(-1), "pattern_s", 0.90)
        };

        var assignment = await service.GenerateHomeAssignmentAsync(conflictingHistory);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("r", assignment.FocusTargets[0]);
        Assert.DoesNotContain(assignment.FocusTargetReasons, reason => reason.AssignmentChangeSuppressed);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_ProvidesConfidenceIntervalsAndAdaptiveWindows()
    {
        var now = DateTime.UtcNow;
        var history = Enumerable.Range(0, 11)
            .Select(index => Entry("r:initial", 0.72 - (index * 0.02), now.AddDays(-11 + index), "pattern_r", 0.65 + ((index % 2 == 0) ? 0.20 : -0.20)))
            .ToArray();

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        var reason = Assert.Single(assignment.FocusTargetReasons);
        Assert.True(reason.InstabilityWindowSize >= 3);
        Assert.True(reason.DeclineWindowSize >= reason.InstabilityWindowSize);
        Assert.True(reason.OverallScoreCiLower <= reason.OverallScoreMean);
        Assert.True(reason.OverallScoreCiUpper >= reason.OverallScoreMean);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_SuppressesConfidenceInterval_WhenSamplesBelowClinicianThreshold()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            Entry("r:initial", 0.61, now.AddDays(-3), "pattern_r", 0.75),
            Entry("r:medial", 0.58, now.AddDays(-2), "pattern_r", 0.72),
            Entry("r:final", 0.55, now.AddDays(-1), "pattern_r", 0.70)
        };

        var store = new InMemoryStore();
        var settings = new ConfidenceSettingsService(store);
        settings.SaveAssignmentConfidenceIntervalMinSamples(6);

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), settings, snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        var reason = Assert.Single(assignment.FocusTargetReasons);
        Assert.True(reason.ConfidenceIntervalSuppressed);
        Assert.Equal(6, reason.ConfidenceIntervalMinSamples);
        Assert.Equal(0.0, reason.OverallScoreCiLower, 3);
        Assert.Equal(0.0, reason.OverallScoreCiUpper, 3);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_CrossTargetNormalization_PrioritizesSevereLowerFrequencyTarget()
    {
        var now = DateTime.UtcNow;
        var highFrequencyModerate = Enumerable.Range(0, 18)
            .Select(index => Entry("r:initial", 0.58 + ((index % 3) * 0.02), now.AddDays(-25 + index), "pattern_r", 0.78))
            .ToArray();
        var lowFrequencySevere = new[]
        {
            Entry("th:initial", 0.26, now.AddDays(-5), "pattern_th", 0.70),
            Entry("th:medial", 0.24, now.AddDays(-3), "pattern_th", 0.72),
            Entry("th:final", 0.22, now.AddDays(-1), "pattern_th", 0.74)
        };

        var history = highFrequencyModerate.Concat(lowFrequencySevere).ToArray();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), new AssignmentSnapshotService());
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("th", assignment.FocusTargets[0]);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_ReliabilityProfile_CanTriggerSuppressionFlag()
    {
        var now = DateTime.UtcNow;
        var settingsStore = new InMemoryStore();
        var settings = new ConfidenceSettingsService(settingsStore);
        settings.SaveAssignmentSuppressionBehavior(AssignmentSuppressionBehavior.HardFreeze);
        settings.SaveAssignmentConfidenceVarianceGate(0.90);

        var volatileSparseHistory = new[]
        {
            Entry("r:initial", 0.18, now.AddDays(-2), "pattern_r", 0.95),
            Entry("r:initial", 0.89, now.AddDays(-1), "pattern_r", 0.22),
            Entry("r:initial", 0.20, now, "pattern_r", 0.91)
        };

        var service = new AiTextService(new PhonemeWordBankService(), settings, new AssignmentSnapshotService());
        var assignment = await service.GenerateHomeAssignmentAsync(volatileSparseHistory);

        Assert.NotEmpty(assignment.FocusTargetReasons);
        Assert.Contains(assignment.FocusTargetReasons, reason => reason.ReliabilityScore < 0.5);
        Assert.Contains("review", assignment.UncertaintyBudgetSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_UncertaintyBudgetCap_CanMarkReviewRequired()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        var settings = new ConfidenceSettingsService(store);
        settings.SaveAssignmentUncertaintyBudgetCap(0.10);

        var history = new[]
        {
            Entry("r:initial", 0.18, now.AddDays(-2), "pattern_r", 0.25),
            Entry("r:initial", 0.88, now.AddDays(-1), "pattern_r", 0.20),
            Entry("s:initial", 0.26, now, "pattern_s", 0.28)
        };

        var service = new AiTextService(new PhonemeWordBankService(), settings, new AssignmentSnapshotService());
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.True(assignment.ReviewRequired);
        Assert.True(assignment.UncertaintyBudgetScore > assignment.UncertaintyBudgetCap);
        Assert.Contains("review", assignment.UncertaintyBudgetSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_PositionWeightTuning_ShiftsPositionWeightedDecline()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            Entry("r:initial", 0.92, now.AddDays(-9), "pattern_r", 0.86),
            Entry("r:initial", 0.86, now.AddDays(-8), "pattern_r", 0.84),
            Entry("r:initial", 0.80, now.AddDays(-7), "pattern_r", 0.82),
            Entry("r:initial", 0.74, now.AddDays(-6), "pattern_r", 0.80),
            Entry("r:medial", 0.70, now.AddDays(-5), "pattern_r", 0.80),
            Entry("r:medial", 0.69, now.AddDays(-4), "pattern_r", 0.80),
            Entry("r:medial", 0.68, now.AddDays(-3), "pattern_r", 0.80),
            Entry("r:final", 0.66, now.AddDays(-2), "pattern_r", 0.80),
            Entry("r:final", 0.65, now.AddDays(-1), "pattern_r", 0.80),
            Entry("r:final", 0.64, now, "pattern_r", 0.80)
        };

        var defaultService = new AiTextService(
            new PhonemeWordBankService(),
            new ConfidenceSettingsService(new InMemoryStore()),
            new AssignmentSnapshotService());
        var defaultAssignment = await defaultService.GenerateHomeAssignmentAsync(history);

        var weightedStore = new InMemoryStore();
        var weightedSettings = new ConfidenceSettingsService(weightedStore);
        weightedSettings.SaveAssignmentPrioritySettings(new AssignmentPrioritySettings
        {
            SeverityWeight = 0.45,
            InstabilityWeight = 0.20,
            DeclineWeight = 0.20,
            FrequencyWeight = 0.15,
            ConfidencePenaltyStrength = 0.60,
            PositionInitialWeight = 0.90,
            PositionMedialWeight = 0.05,
            PositionFinalWeight = 0.05
        });

        var weightedService = new AiTextService(
            new PhonemeWordBankService(),
            weightedSettings,
            new AssignmentSnapshotService());
        var weightedAssignment = await weightedService.GenerateHomeAssignmentAsync(history);

        var baselineDecline = Assert.Single(defaultAssignment.FocusTargetReasons).PositionWeightedDeclineScore;
        var tunedDecline = Assert.Single(weightedAssignment.FocusTargetReasons).PositionWeightedDeclineScore;

        Assert.True(tunedDecline > baselineDecline);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_LowCalibrationQuality_ReducesAutomaticRecommendationConfidence()
    {
        var now = DateTime.UtcNow;
        var rInitial = Entry("r:initial", 0.42, now.AddDays(-4), "pattern_r", 0.82);
        rInitial.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        rInitial.EmpiricalOutcomeSupport = 0.85;
        rInitial.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.38}}";

        var rMedial = Entry("r:medial", 0.40, now.AddDays(-3), "pattern_r", 0.80);
        rMedial.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        rMedial.EmpiricalOutcomeSupport = 0.80;
        rMedial.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.36}}";

        var rFinal = Entry("r:final", 0.39, now.AddDays(-2), "pattern_r", 0.81);
        rFinal.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        rFinal.EmpiricalOutcomeSupport = 0.78;
        rFinal.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.35}}";

        var sInitial = Entry("s:initial", 0.43, now.AddDays(-4), "pattern_s", 0.82);
        sInitial.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        sInitial.EmpiricalOutcomeSupport = 0.85;
        sInitial.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.82}}";

        var sMedial = Entry("s:medial", 0.41, now.AddDays(-3), "pattern_s", 0.80);
        sMedial.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        sMedial.EmpiricalOutcomeSupport = 0.80;
        sMedial.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.84}}";

        var sFinal = Entry("s:final", 0.40, now.AddDays(-2), "pattern_s", 0.81);
        sFinal.CalibrationMethod = "isotonic-binned+empirical-shrinkage";
        sFinal.EmpiricalOutcomeSupport = 0.78;
        sFinal.CalibrationTableJson = "{\"quality\":{\"qualityScore\":0.85}}";

        var history = new[] { rInitial, rMedial, rFinal, sInitial, sMedial, sFinal };

        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), new AssignmentSnapshotService());
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        var rReason = assignment.FocusTargetReasons.First(reason => string.Equals(reason.TargetSound, "r", StringComparison.OrdinalIgnoreCase));
        var sReason = assignment.FocusTargetReasons.First(reason => string.Equals(reason.TargetSound, "s", StringComparison.OrdinalIgnoreCase));

        Assert.True(rReason.CalibrationQualityScore < sReason.CalibrationQualityScore);
        Assert.True(rReason.CalibrationConfidenceAdjustment < sReason.CalibrationConfidenceAdjustment);
        Assert.True(rReason.PriorityScore < sReason.PriorityScore);
    }

    private static ProgressEntry Entry(string target, double overall, DateTime timestamp, string pattern, double confidence)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            BaseTargetSound = target.Split(':')[0],
            PositionTag = target.Contains(':') ? target.Split(':')[1] : string.Empty,
            OverallScore = overall,
            Timestamp = timestamp,
            ErrorPattern = pattern,
            Transcript = "sample",
            ConfidenceScore = confidence,
            ConfidenceBand = "Moderate"
        };
    }

    private sealed class InMemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _stringValues = new(StringComparer.Ordinal);

        public double Get(string key, double defaultValue)
        {
            return _values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, double value)
        {
            _values[key] = value;
        }

        public string Get(string key, string defaultValue)
        {
            return _stringValues.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, string value)
        {
            _stringValues[key] = value;
        }
    }
}
