using SQLite;

namespace SpeechBuddyAI.Models;

public sealed class AssignmentSnapshot
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long SnapshotDateTicks { get; set; } = DateTimeOffset.UtcNow.Ticks;

    public string Rationale { get; set; } = string.Empty;
    public string FocusTargetsCsv { get; set; } = string.Empty;
    public string SuggestedWordsCsv { get; set; } = string.Empty;
    public string TargetReasonsJson { get; set; } = "[]";
    public string PreviousRationale { get; set; } = string.Empty;
    public string RationaleDriftSummary { get; set; } = string.Empty;
    public string PreviousFocusTargetsCsv { get; set; } = string.Empty;
    public int FocusChangeCount { get; set; }
    public bool AssignmentChangeSuppressed { get; set; }
    public int SourceEntryCount { get; set; }
    public string ComponentTracesJson { get; set; } = "[]";
    public string CalibrationMetricsJson { get; set; } = "{}";
    public string CalibrationSummary { get; set; } = string.Empty;
    public string ScoringFormulaVersion { get; set; } = string.Empty;
    public string AdvisoryWeightSuggestionJson { get; set; } = "{}";
    public bool ReviewRequired { get; set; }
    public double UncertaintyBudgetScore { get; set; }
    public double UncertaintyBudgetCap { get; set; }
    public string UncertaintyBudgetSummary { get; set; } = string.Empty;

    [Ignore]
    public DateTimeOffset SnapshotDate
    {
        get => new DateTimeOffset(SnapshotDateTicks, TimeSpan.Zero);
        set => SnapshotDateTicks = value.UtcTicks;
    }
}
