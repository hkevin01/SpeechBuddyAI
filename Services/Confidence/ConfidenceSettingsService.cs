namespace SpeechBuddyAI.Services.Confidence;

public sealed class ConfidenceSettingsService : IConfidenceThresholdProvider
{
    private const string ModerateThresholdKey = "confidence.moderateThreshold";
    private const string HighThresholdKey = "confidence.highThreshold";
    private const string SessionComparisonNormalizationKey = "comparison.sessionNormalizationMode";
    private const string SessionComparisonSmoothingStrengthKey = "comparison.smoothingStrength";
    private const string ProgressDateRangeStartKey = "progress.dateRangeStart";
    private const string ProgressDateRangeEndKey = "progress.dateRangeEnd";
    private const string ProgressTargetFilterKey = "progress.targetFilter";

    public const double DefaultModerateThreshold = 0.60;
    public const double DefaultHighThreshold = 0.80;
    public const SessionComparisonNormalizationMode DefaultNormalizationMode = SessionComparisonNormalizationMode.AttemptWeighted;
    public const SessionComparisonSmoothingStrength DefaultSmoothingStrength = SessionComparisonSmoothingStrength.Balanced;
    public const int DefaultProgressDateRangeDays = 30;

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
        _store.Set(SessionComparisonSmoothingStrengthKey, (double)DefaultSmoothingStrength);
        _store.Set(ProgressDateRangeStartKey, double.NaN);
        _store.Set(ProgressDateRangeEndKey, double.NaN);
        _store.Set(ProgressTargetFilterKey, string.Empty);
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

    public SessionComparisonSmoothingStrength GetSessionComparisonSmoothingStrength()
    {
        var stored = _store.Get(SessionComparisonSmoothingStrengthKey, (double)DefaultSmoothingStrength);
        var strength = (SessionComparisonSmoothingStrength)(int)stored;

        return Enum.IsDefined(typeof(SessionComparisonSmoothingStrength), strength)
            ? strength
            : DefaultSmoothingStrength;
    }

    public void SaveSessionComparisonSmoothingStrength(SessionComparisonSmoothingStrength strength)
    {
        if (!Enum.IsDefined(typeof(SessionComparisonSmoothingStrength), strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength));
        }

        _store.Set(SessionComparisonSmoothingStrengthKey, (double)strength);
    }

    public (DateTime StartDateLocal, DateTime EndDateLocal) GetProgressDateRange(DateTime referenceDateLocal)
    {
        var reference = referenceDateLocal.Date;
        var fallbackStart = reference.AddDays(-DefaultProgressDateRangeDays);

        var storedStart = _store.Get(ProgressDateRangeStartKey, double.NaN);
        var storedEnd = _store.Get(ProgressDateRangeEndKey, double.NaN);

        var start = double.IsNaN(storedStart) ? fallbackStart : DateTime.FromOADate(storedStart).Date;
        var end = double.IsNaN(storedEnd) ? reference : DateTime.FromOADate(storedEnd).Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    public void SaveProgressDateRange(DateTime startDateLocal, DateTime endDateLocal)
    {
        var start = startDateLocal.Date;
        var end = endDateLocal.Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        _store.Set(ProgressDateRangeStartKey, start.ToOADate());
        _store.Set(ProgressDateRangeEndKey, end.ToOADate());
    }

    public string GetDefaultProgressTargetFilter()
    {
        return (_store.Get(ProgressTargetFilterKey, string.Empty) ?? string.Empty).Trim();
    }

    public void SaveDefaultProgressTargetFilter(string? targetFilter)
    {
        _store.Set(ProgressTargetFilterKey, (targetFilter ?? string.Empty).Trim());
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
