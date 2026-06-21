using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.SpeechScoring;

public sealed class OfflineSpeechScoringAdapter : ISpeechScoringAdapter
{
    public string Name => "offline-heuristic";
    public int Priority => 10;

    public Task<AdapterScoreResult> ScoreAsync(
        string targetSound,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries,
        CancellationToken cancellationToken = default)
    {
        var target = targetSound.Trim().ToLowerInvariant();
        var recognized = transcript.Trim().ToLowerInvariant();

        // Allows manual failover testing by entering a known trigger token.
        if (recognized.Contains("[force-fallback]", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Offline adapter fallback requested.");
        }

        var phonemeScore = recognized.Contains(target, StringComparison.Ordinal)
            ? 0.9
            : 0.35;

        var lengthRatio = Math.Min(Math.Max(1, target.Length), Math.Max(1, recognized.Length))
            / (double)Math.Max(Math.Max(1, target.Length), Math.Max(1, recognized.Length));

        var fluencyScore = Clamp(0.45 + 0.5 * lengthRatio);

        return Task.FromResult(new AdapterScoreResult
        {
            Provider = Name,
            PhonemeScore = phonemeScore,
            FluencyScore = fluencyScore
        });
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
