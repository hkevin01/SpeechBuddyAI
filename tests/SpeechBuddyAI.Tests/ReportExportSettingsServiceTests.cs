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

    [Fact]
    public void PreferredExportFormat_DefaultsToPlainText()
    {
        var service = new ReportExportSettingsService(new InMemoryStore());

        var format = service.GetPreferredExportFormat();

        Assert.Equal(ReportExportFormat.PlainText, format);
    }

    [Fact]
    public void SavePreferredExportFormat_PersistsValue()
    {
        var service = new ReportExportSettingsService(new InMemoryStore());

        service.SavePreferredExportFormat(ReportExportFormat.CsvSummary);

        Assert.Equal(ReportExportFormat.CsvSummary, service.GetPreferredExportFormat());
    }

    [Fact]
    public void PreferredExportFormat_InvalidStoredValue_FallsBackToPlainText()
    {
        var store = new InMemoryStore();
        store.Set("reports.preferredExportFormat", 77);
        var service = new ReportExportSettingsService(store);

        Assert.Equal(ReportExportFormat.PlainText, service.GetPreferredExportFormat());
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
