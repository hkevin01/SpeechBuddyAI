using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Services.Reports;

public sealed class ReportExportSettingsService
{
    private const string ShareBehaviorKey = "reports.defaultShareBehavior";

    private readonly IKeyValueStore _store;

    public ReportExportSettingsService(IKeyValueStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ReportShareBehavior GetDefaultShareBehavior()
    {
        var storedValue = _store.Get(ShareBehaviorKey, (double)ReportShareBehavior.ExportAndShare);
        var parsed = (int)Math.Round(storedValue);

        return parsed switch
        {
            (int)ReportShareBehavior.ExportOnly => ReportShareBehavior.ExportOnly,
            (int)ReportShareBehavior.ExportAndShare => ReportShareBehavior.ExportAndShare,
            _ => ReportShareBehavior.ExportAndShare
        };
    }

    public void SaveDefaultShareBehavior(ReportShareBehavior behavior)
    {
        _store.Set(ShareBehaviorKey, (double)behavior);
    }

    public void ResetDefaults()
    {
        SaveDefaultShareBehavior(ReportShareBehavior.ExportAndShare);
    }
}
