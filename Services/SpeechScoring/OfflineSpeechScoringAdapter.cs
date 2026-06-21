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
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedTranscript = (transcript ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Target sound and transcript are required for offline scoring.");
        }

        try
        {
            if (normalizedTranscript.Contains("[force-fallback]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Offline adapter fallback requested.");
            }

            var phonemeScore = normalizedTranscript.Contains(normalizedTarget, StringComparison.Ordinal)
                ? 0.9
                : 0.35;

            var lengthRatio = Math.Min(Math.Max(1, normalizedTarget.Length), Math.Max(1, normalizedTranscript.Length))
                / (double)Math.Max(Math.Max(1, normalizedTarget.Length), Math.Max(1, normalizedTranscript.Length));

            var fluencyScore = Clamp(0.45 + 0.5 * lengthRatio);

            return Task.FromResult(new AdapterScoreResult
            {
                Provider = Name,
                PhonemeScore = phonemeScore,
                FluencyScore = fluencyScore
            });
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Offline scoring failed.", ex);
        }
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
