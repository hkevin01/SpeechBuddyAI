using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.Confidence;

public sealed class ConfidenceCalculator
{
    private readonly IConfidenceThresholdProvider _thresholdProvider;

    public ConfidenceCalculator(IConfidenceThresholdProvider thresholdProvider)
    {
        _thresholdProvider = thresholdProvider ?? throw new ArgumentNullException(nameof(thresholdProvider));
    }

    public double ComputeScore(ScoreComponents scores, string transcript, int priorEntryCount, string provider)
    {
        if (scores is null)
        {
            throw new ArgumentNullException(nameof(scores));
        }

        var normalizedTranscript = (transcript ?? string.Empty).Trim();
        var normalizedProvider = (provider ?? string.Empty).Trim();
        var historyCount = Math.Max(0, priorEntryCount);

        var tokenCount = normalizedTranscript
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        var transcriptSignal = Math.Min(tokenCount, 8) / 8.0;
        var providerBonus = normalizedProvider.Contains("offline", StringComparison.OrdinalIgnoreCase) ? 0.08 : 0.03;
        var historySignal = Math.Min(historyCount, 10) / 10.0;
        var scoreSpread = Math.Abs(scores.PhonemeScore - scores.FluencyScore);
        var spreadPenalty = Math.Min(scoreSpread, 0.45);

        var rawScore =
            (0.45 * Clamp(scores.OverallScore)) +
            (0.2 * Clamp(scores.ConsistencyScore)) +
            (0.2 * transcriptSignal) +
            (0.1 * historySignal) +
            providerBonus -
            (0.15 * spreadPenalty);

        return Clamp(rawScore);
    }

    public string ComputeBand(double confidenceScore)
    {
        var thresholds = _thresholdProvider.GetThresholds();
        var score = Clamp(confidenceScore);

        if (score >= thresholds.HighThreshold)
        {
            return "High";
        }

        if (score >= thresholds.ModerateThreshold)
        {
            return "Moderate";
        }

        return "Low";
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
