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
    public void ResetDefaults_RestoresAttemptWeightedNormalization()
    {
        var service = new ConfidenceSettingsService(new InMemoryStore());
        service.SaveSessionComparisonNormalizationMode(SessionComparisonNormalizationMode.DayWeighted);

        service.ResetDefaults();

        Assert.Equal(SessionComparisonNormalizationMode.AttemptWeighted, service.GetSessionComparisonNormalizationMode());
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
