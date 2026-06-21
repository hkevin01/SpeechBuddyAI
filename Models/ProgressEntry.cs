namespace SpeechBuddyAI.Models;

public class ProgressEntry
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string TargetSound { get; set; } = string.Empty;
    public double AccuracyScore { get; set; }
    public int TrialCount { get; set; }
    public string ErrorPattern { get; set; } = string.Empty;
}
