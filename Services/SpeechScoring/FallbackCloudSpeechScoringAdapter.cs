using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.SpeechScoring;

public sealed class FallbackCloudSpeechScoringAdapter : ISpeechScoringAdapter
{
    public string Name => "fallback-cloud-sim";
    public int Priority => 20;

    public async Task<AdapterScoreResult> ScoreAsync(
        string targetSound,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries,
        CancellationToken cancellationToken = default)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedTranscript = (transcript ?? string.Empty).Trim().ToLowerInvariant();
        var historySize = Math.Max(0, priorEntries?.Count ?? 0);

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Target sound and transcript are required for fallback scoring.");
        }

        try
        {
            await Task.Delay(50, cancellationToken);

            var tokenCount = normalizedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var phonemeScore = normalizedTranscript.Contains(normalizedTarget, StringComparison.Ordinal)
                ? 0.86
                : 0.45;

            var fluencyBase = 0.5 + (Math.Min(tokenCount, 8) / 20.0);
            var historyStabilityBoost = Math.Min(historySize, 10) * 0.005;
            var fluencyScore = Clamp(fluencyBase + historyStabilityBoost);

            return new AdapterScoreResult
            {
                Provider = Name,
                PhonemeScore = phonemeScore,
                FluencyScore = fluencyScore
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Fallback scoring failed.", ex);
        }
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
