namespace SpeechBuddyAI.Models;

public sealed class PracticeAttemptResult
{
    public required ScoreComponents Scores { get; init; }
    public required ProgressEntry Entry { get; init; }
}
