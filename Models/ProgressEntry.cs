using SQLite;

namespace SpeechBuddyAI.Models;

public class ProgressEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Indexed]
    public string TargetSound { get; set; } = string.Empty;

    [Indexed]
    public string BaseTargetSound { get; set; } = string.Empty;

    public string PositionTag { get; set; } = string.Empty;

    public double AccuracyScore { get; set; }
    public double PhonemeScore { get; set; }
    public double FluencyScore { get; set; }
    public double ConsistencyScore { get; set; }
    public double OverallScore { get; set; }

    public string Transcript { get; set; } = string.Empty;
    public int TrialCount { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
    public string ScoringProvider { get; set; } = "unknown";
    public double ConfidenceScore { get; set; }
    public string ConfidenceBand { get; set; } = "Low";
    public string ScoringFormulaVersion { get; set; } = string.Empty;
    public bool HistoricalDriftDetected { get; set; }
    public double HistoricalDriftZScore { get; set; }
    public string HistoricalDriftSummary { get; set; } = string.Empty;
    public double AdaptiveModerateThreshold { get; set; }
    public double AdaptiveHighThreshold { get; set; }
    public double RawConfidenceScore { get; set; }
    public double EmpiricalOutcomeMean { get; set; }
    public double EmpiricalOutcomeSupport { get; set; }
    public double CalibrationResidual { get; set; }
    public string CalibrationMethod { get; set; } = string.Empty;
    public string CalibrationTableJson { get; set; } = string.Empty;
    public double VarianceUncertaintyComponent { get; set; }
    public double SparsityUncertaintyComponent { get; set; }

    [Ignore]
    public ConfidenceBand ConfidenceBandValue => ConfidenceBandExtensions.Parse(ConfidenceBand);
}
