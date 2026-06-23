using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Tests;

public sealed class ConfidenceSettingsServiceTests
{
    [Fact]
    public void GetSessionComparisonNormalizationMode_DefaultsToAttemptWeighted()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        Assert.Equal(SessionComparisonNormalizationMode.AttemptWeighted, service.GetSessionComparisonNormalizationMode());
    }

    [Fact]
    public void SaveSessionComparisonNormalizationMode_PersistsValue()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveSessionComparisonNormalizationMode(SessionComparisonNormalizationMode.DayWeighted);

        Assert.Equal(SessionComparisonNormalizationMode.DayWeighted, service.GetSessionComparisonNormalizationMode());
    }

    [Fact]
    public void SessionComparisonSmoothingStrength_DefaultsToBalanced()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        Assert.Equal(SessionComparisonSmoothingStrength.Balanced, service.GetSessionComparisonSmoothingStrength());
    }

    [Fact]
    public void SaveSessionComparisonSmoothingStrength_PersistsValue()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveSessionComparisonSmoothingStrength(SessionComparisonSmoothingStrength.Responsive);

        Assert.Equal(SessionComparisonSmoothingStrength.Responsive, service.GetSessionComparisonSmoothingStrength());
    }

    [Fact]
    public void ResetDefaults_RestoresAttemptWeightedNormalization()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());
        service.SaveSessionComparisonNormalizationMode(SessionComparisonNormalizationMode.DayWeighted);
        service.SaveSessionComparisonSmoothingStrength(SessionComparisonSmoothingStrength.Responsive);

        service.ResetDefaults();

        Assert.Equal(SessionComparisonNormalizationMode.AttemptWeighted, service.GetSessionComparisonNormalizationMode());
        Assert.Equal(SessionComparisonSmoothingStrength.Balanced, service.GetSessionComparisonSmoothingStrength());
    }

    [Fact]
    public void SaveProgressDateRange_PersistsAndOrdersDates()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveProgressDateRange(new DateTime(2026, 6, 20), new DateTime(2026, 6, 5));

        var (start, end) = service.GetProgressDateRange(new DateTime(2026, 6, 22));

        Assert.Equal(new DateTime(2026, 6, 5), start);
        Assert.Equal(new DateTime(2026, 6, 20), end);
    }

    [Fact]
    public void GetProgressDateRange_UsesDefaultWindowWhenUnset()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        var (start, end) = service.GetProgressDateRange(new DateTime(2026, 6, 22));

        Assert.Equal(new DateTime(2026, 5, 23), start);
        Assert.Equal(new DateTime(2026, 6, 22), end);
    }

    [Fact]
    public void SaveDefaultProgressTargetFilter_PersistsTrimmedValue()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveDefaultProgressTargetFilter("  r-blends  ");

        Assert.Equal("r-blends", service.GetDefaultProgressTargetFilter());
    }

    [Fact]
    public void AssignmentPrioritySettings_DefaultsToConfiguredWeights()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        var settings = service.GetAssignmentPrioritySettings();

        Assert.Equal(AssignmentPrioritySettings.DefaultSeverityWeight, settings.SeverityWeight, 3);
        Assert.Equal(AssignmentPrioritySettings.DefaultInstabilityWeight, settings.InstabilityWeight, 3);
        Assert.Equal(AssignmentPrioritySettings.DefaultDeclineWeight, settings.DeclineWeight, 3);
        Assert.Equal(AssignmentPrioritySettings.DefaultFrequencyWeight, settings.FrequencyWeight, 3);
        Assert.Equal(AssignmentPrioritySettings.DefaultConfidencePenaltyStrength, settings.ConfidencePenaltyStrength, 3);
    }

    [Fact]
    public void SaveAssignmentPrioritySettings_NormalizesWeights()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveAssignmentPrioritySettings(new AssignmentPrioritySettings
        {
            SeverityWeight = 1.0,
            InstabilityWeight = 1.0,
            DeclineWeight = 1.0,
            FrequencyWeight = 1.0,
            ConfidencePenaltyStrength = 0.8
        });

        var settings = service.GetAssignmentPrioritySettings();
        var sum = settings.SeverityWeight + settings.InstabilityWeight + settings.DeclineWeight + settings.FrequencyWeight;

        Assert.Equal(1.0, sum, 3);
        Assert.Equal(0.8, settings.ConfidencePenaltyStrength, 3);
    }

    [Fact]
    public void SaveAssignmentConfidenceVarianceGate_PersistsValue()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());

        service.SaveAssignmentConfidenceVarianceGate(0.055);

        Assert.Equal(0.055, service.GetAssignmentConfidenceVarianceGate(), 3);
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
