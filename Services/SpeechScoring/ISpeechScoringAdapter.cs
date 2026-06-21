using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.SpeechScoring;

public interface ISpeechScoringAdapter
{
    string Name { get; }
    int Priority { get; }

    Task<AdapterScoreResult> ScoreAsync(
        string targetSound,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries,
        CancellationToken cancellationToken = default);
}
