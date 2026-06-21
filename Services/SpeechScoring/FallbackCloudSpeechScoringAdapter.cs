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
        // Simulates a network-based scorer in this milestone.
        await Task.Delay(50, cancellationToken);

        var target = targetSound.Trim().ToLowerInvariant();
        var recognized = transcript.Trim().ToLowerInvariant();
        var tokenCount = recognized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        var phonemeScore = recognized.Contains(target, StringComparison.Ordinal)
            ? 0.86
            : 0.45;

        var fluencyScore = Clamp(0.5 + (Math.Min(tokenCount, 8) / 20.0));

        return new AdapterScoreResult
        {
            Provider = Name,
            PhonemeScore = phonemeScore,
            FluencyScore = fluencyScore
        };
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
