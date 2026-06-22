namespace SpeechBuddyAI.Services.Confidence;

public sealed class ConfidenceSettingsService : IConfidenceThresholdProvider
{
    private const string ModerateThresholdKey = "confidence.moderateThreshold";
    private const string HighThresholdKey = "confidence.highThreshold";
    private const string SessionComparisonNormalizationKey = "comparison.sessionNormalizationMode";

    public const double DefaultModerateThreshold = 0.60;
    public const double DefaultHighThreshold = 0.80;
    public const SessionComparisonNormalizationMode DefaultNormalizationMode = SessionComparisonNormalizationMode.AttemptWeighted;

    private readonly IKeyValueStore _store;

    public ConfidenceSettingsService(IKeyValueStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ConfidenceThresholds GetThresholds()
    {
        var moderate = Clamp(_store.Get(ModerateThresholdKey, DefaultModerateThreshold));
        var high = Clamp(_store.Get(HighThresholdKey, DefaultHighThreshold));

        if (high <= moderate)
        {
            high = Math.Min(1.0, moderate + 0.1);
        }

        return new ConfidenceThresholds(moderate, high);
    }

    public void SaveThresholds(double moderateThreshold, double highThreshold)
    {
        var moderate = Clamp(moderateThreshold);
        var high = Clamp(highThreshold);

        if (high <= moderate)
        {
            throw new ArgumentException("High threshold must be greater than moderate threshold.");
        }

        _store.Set(ModerateThresholdKey, moderate);
        _store.Set(HighThresholdKey, high);
    }

    public void ResetDefaults()
    {
        _store.Set(ModerateThresholdKey, DefaultModerateThreshold);
        _store.Set(HighThresholdKey, DefaultHighThreshold);
        _store.Set(SessionComparisonNormalizationKey, (double)DefaultNormalizationMode);
    }

    public SessionComparisonNormalizationMode GetSessionComparisonNormalizationMode()
    {
        var stored = _store.Get(SessionComparisonNormalizationKey, (double)DefaultNormalizationMode);
        var mode = (SessionComparisonNormalizationMode)(int)stored;

        return Enum.IsDefined(typeof(SessionComparisonNormalizationMode), mode)
            ? mode
            : DefaultNormalizationMode;
    }

    public void SaveSessionComparisonNormalizationMode(SessionComparisonNormalizationMode mode)
    {
        if (!Enum.IsDefined(typeof(SessionComparisonNormalizationMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        _store.Set(SessionComparisonNormalizationKey, (double)mode);
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
