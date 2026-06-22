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

    private sealed class InMemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

        public double Get(string key, double defaultValue)
        {
            return _values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, double value)
        {
            _values[key] = value;
        }
    }
}
