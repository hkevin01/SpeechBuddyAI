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

    public double AccuracyScore { get; set; }
    public double PhonemeScore { get; set; }
    public double FluencyScore { get; set; }
    public double ConsistencyScore { get; set; }
    public double OverallScore { get; set; }

    public string Transcript { get; set; } = string.Empty;
    public int TrialCount { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
    public string ScoringProvider { get; set; } = "unknown";
}
