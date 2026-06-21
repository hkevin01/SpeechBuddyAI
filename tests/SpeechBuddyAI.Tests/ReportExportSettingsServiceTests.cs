using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Tests;

public sealed class ReportExportSettingsServiceTests
{
    [Fact]
    public void GetDefaultShareBehavior_ReturnsExportAndShareByDefault()
    {
        var store = new InMemoryStore();
        var service = new ReportExportSettingsService(store);

        var behavior = service.GetDefaultShareBehavior();

        Assert.Equal(ReportShareBehavior.ExportAndShare, behavior);
    }

    [Fact]
    public void SaveDefaultShareBehavior_PersistsValue()
    {
        var store = new InMemoryStore();
        var service = new ReportExportSettingsService(store);

        service.SaveDefaultShareBehavior(ReportShareBehavior.ExportOnly);

        Assert.Equal(ReportShareBehavior.ExportOnly, service.GetDefaultShareBehavior());
    }

    [Fact]
    public void GetDefaultShareBehavior_InvalidStoredValue_FallsBackToExportAndShare()
    {
        var store = new InMemoryStore();
        store.Set("reports.defaultShareBehavior", 99.0);
        var service = new ReportExportSettingsService(store);

        var behavior = service.GetDefaultShareBehavior();

        Assert.Equal(ReportShareBehavior.ExportAndShare, behavior);
    }

    [Fact]
    public void ResetDefaults_SetsExportAndShare()
    {
        var store = new InMemoryStore();
        var service = new ReportExportSettingsService(store);
        service.SaveDefaultShareBehavior(ReportShareBehavior.ExportOnly);

        service.ResetDefaults();

        Assert.Equal(ReportShareBehavior.ExportAndShare, service.GetDefaultShareBehavior());
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
